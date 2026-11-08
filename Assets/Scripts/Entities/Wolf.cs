using UnityEngine;

/// Sói: hoang dã thì đi lang thang (đánh nó sẽ nổi giận). Cho ăn xương để thuần hoá -> thú cưng:
/// đi theo chủ, chuột phải để ngồi / đứng, cho ăn thịt để hồi máu, tự đánh mob mà chủ đánh hoặc mob đánh chủ.
public class Wolf : Mob
{
    const float TameChance = 0.34f;

    public bool IsTamed { get; private set; }
    public bool IsSitting { get; private set; }
    public bool IsAngry => angryTimer > 0f;

    public override string DisplayName => IsTamed ? "Sói (của bạn)" : "Sói";

    float angryTimer;
    float attackCooldown;
    Mob target;

    protected override void Configure()
    {
        MaxHealth = 8;
        HalfWidth = 0.3f;
        Height = 0.85f;
    }

    protected override ModelDef BuildModel() => MobModels.Wolf();

    protected override Texture2D CurrentTexture()
    {
        var a = GameAssets.Instance;
        if (a == null) return null;
        if (IsTamed) return a.wolfTame != null ? a.wolfTame : a.wolf;
        if (IsAngry) return a.wolfAngry;
        return a.wolf;
    }

    protected override bool OpaqueTexture => IsTamed;   // wolf_tame.tga: alpha là mặt nạ vòng cổ

    // ---------------------------------------------------------------- AI

    protected override void Think(float dt)
    {
        attackCooldown -= dt;
        Vector3 eye = transform.position + Vector3.up * 0.7f;

        if (angryTimer > 0f)
        {
            angryTimer -= dt;
            if (angryTimer <= 0f) RefreshMaterial();
        }

        if (IsSitting)
        {
            headPitch = 0f;
            return;
        }

        // Mục tiêu tấn công (do chủ chỉ định)
        if (target != null && (!target.IsAlive || HorizontalDistance(target.Position) > 20f)) target = null;
        if (target != null)
        {
            Chase(target.Position, eye, () => target.Damage(IsTamed ? 4f : 2f, transform.position, this));
            return;
        }

        var player = world.Player;
        var stats = PlayerStats.Instance;

        // Sói hoang nổi giận cắn người chơi
        if (!IsTamed && IsAngry && player != null && stats != null && !PlayerStats.IsDead)
        {
            Chase(player.position, eye, () => stats.Damage(2f, transform.position, this));
            return;
        }

        if (IsTamed && player != null)
        {
            float dist = HorizontalDistance(player.position);
            if (dist > 14f && !PlayerStats.IsDead) TeleportNear(player.position);
            else if (dist > 3.5f)
            {
                MoveTowards(player.position, 4.5f);
                faceTarget = true;
                lookTarget = player.position;
            }
            else
            {
                // Đứng gần chủ, ngước nhìn chủ
                faceTarget = true;
                lookTarget = player.position;
                headPitch = PitchTowards(eye, player.position + Vector3.up * 1.6f);
            }
            return;
        }

        Wander(dt, 1.4f);
        headPitch = Mathf.Lerp(headPitch, 0f, dt * 3f);
    }

    void Chase(Vector3 pos, Vector3 eye, System.Action attack)
    {
        IsSitting = false;
        MoveTowards(pos, 4.2f);
        faceTarget = true;
        lookTarget = pos;
        headPitch = PitchTowards(eye, pos + Vector3.up * 0.5f);

        if (HorizontalDistance(pos) < 1.2f && Mathf.Abs(pos.y - transform.position.y) < 1.6f)
        {
            moveDir = Vector3.zero;
            if (attackCooldown <= 0f)
            {
                attackCooldown = 1f;
                wantJump = true;
                attack();
            }
        }
    }

    void TeleportNear(Vector3 p)
    {
        for (int i = 0; i < 10; i++)
        {
            int x = Mathf.FloorToInt(p.x) + Random.Range(-2, 3);
            int z = Mathf.FloorToInt(p.z) + Random.Range(-2, 3);
            int y = world.GetTopSolidY(x, z) + 1;
            var pos = new Vector3(x + 0.5f, y, z + 0.5f);
            if (Mathf.Abs(y - p.y) > 4f || VoxelPhysics.Overlaps(world, pos, HalfWidth, Height)) continue;
            transform.position = pos;
            velocity = Vector3.zero;
            return;
        }
    }

    protected override void OnOutsideLoadedArea()
    {
        // Sói đã thuần bị bỏ lại ở vùng chưa load -> dịch chuyển về cạnh chủ
        var player = world.Player;
        if (IsTamed && !IsSitting && player != null && !PlayerStats.IsDead && world.IsChunkReadyAt(player.position))
            TeleportNear(player.position);
    }

    // ---------------------------------------------------------------- interact / combat

    public override bool Interact(PlayerInventory player)
    {
        var held = player.Held;
        var def = held.Def;

        if (!IsTamed)
        {
            if (IsAngry || held.id != ItemId.Bone) return false;
            player.ConsumeHeld(1);
            if (Random.value < TameChance)
            {
                IsTamed = true;
                Persistent = true;
                IsSitting = true;
                MaxHealth = 20;
                Health = 20;
                angryTimer = 0f;
                target = null;
                RefreshMaterial();
                GameHUD.Toast("Đã thuần hoá sói! Chuột phải để cho nó ngồi / đứng dậy.");
            }
            else GameHUD.Toast("Sói chưa chịu... thử cho thêm xương.");
            return true;
        }

        // Cho ăn thịt để hồi máu
        if (def != null && def.IsFood && held.id != ItemId.RottenFlesh && Health < MaxHealth)
        {
            player.ConsumeHeld(1);
            Heal(def.food * 2f);
            GameHUD.Toast("Sói đã được cho ăn.");
            return true;
        }

        IsSitting = !IsSitting;
        target = null;
        GameHUD.Toast(IsSitting ? "Sói đang ngồi đợi." : "Sói đi theo bạn.");
        return true;
    }

    protected override void OnDamaged(float amount, Object attacker)
    {
        IsSitting = false;
        if (attacker is PlayerStats || attacker is PlayerInventory)
        {
            if (!IsTamed)
            {
                bool wasAngry = IsAngry;
                angryTimer = 30f;
                if (!wasAngry) RefreshMaterial();
                // Cả bầy nổi giận
                foreach (var m in All)
                    if (m is Wolf w && w != this && !w.IsTamed && (w.Position - Position).sqrMagnitude < 16f * 16f)
                    {
                        bool a = w.IsAngry;
                        w.angryTimer = 30f;
                        if (!a) w.RefreshMaterial();
                    }
            }
        }
        else if (attacker is Mob mob && mob != this)
        {
            target = mob;
        }
    }

    /// Người chơi đánh mob nào thì sói đã thuần lao vào mob đó.
    public static void OnOwnerAttacked(Mob victim)
    {
        if (victim == null || victim is Wolf w0 && w0.IsTamed) return;
        foreach (var m in All)
            if (m is Wolf w && w.IsTamed && !w.IsSitting) w.target = victim;
    }

    /// Mob nào đánh người chơi thì sói đã thuần bảo vệ chủ.
    public static void OnOwnerHurtBy(Mob attacker)
    {
        if (attacker == null || attacker is Wolf w0 && w0.IsTamed) return;
        foreach (var m in All)
            if (m is Wolf w && w.IsTamed && !w.IsSitting) w.target = attacker;
    }

    // ---------------------------------------------------------------- animation

    protected override void Animate(float dt)
    {
        model.ResetPose();

        float tail = IsTamed ? (0.55f - (MaxHealth - Health) * 0.02f) * Mathf.PI
                   : IsAngry ? 1.5393804f : Mathf.PI / 5f;
        float wag = (IsTamed && !IsSitting) ? Mathf.Sin(Time.time * 12f) * 0.4f * Mathf.Max(0.3f, limbAmount) : 0f;

        if (IsSitting)
        {
            // Tư thế ngồi lấy từ Minecraft Java
            model.SetPivot("upper_body", -1f, 16f, -3f);
            model.SetRotation("upper_body", Mathf.PI * 2f / 5f);
            model.SetPivot("body", 0f, 18f, 0f);
            model.SetRotation("body", Mathf.PI / 4f);
            model.SetPivot("tail", -1f, 21f, 6f);
            model.SetRotation("tail", tail);
            model.SetPivot("right_hind_leg", -2.5f, 22.7f, 2f);
            model.SetRotation("right_hind_leg", Mathf.PI * 1.5f);
            model.SetPivot("left_hind_leg", 0.5f, 22.7f, 2f);
            model.SetRotation("left_hind_leg", Mathf.PI * 1.5f);
            model.SetPivot("right_front_leg", -2.49f, 17f, -4f);
            model.SetRotation("right_front_leg", 5.811947f);
            model.SetPivot("left_front_leg", 0.51f, 17f, -4f);
            model.SetRotation("left_front_leg", 5.811947f);
        }
        else
        {
            AnimateQuadrupedLegs();
            model.SetRotation("tail", tail, wag);
        }

        model.SetRotation("head", headPitch);
    }

    protected override void SpawnDrops() { }
}
