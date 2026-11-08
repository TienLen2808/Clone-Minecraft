using System.Collections.Generic;
using UnityEngine;

public enum MobType
{
    Pig,
    Cow,
    Sheep,
    Chicken,
    Zombie,
    Wolf,
    Spider,
    Creeper,
    Skeleton,
    PolarBear,
}

/// Lớp cơ sở cho mọi sinh vật: máu, vật lý voxel, knockback, nháy đỏ khi bị đánh, chết rơi đồ, animation đi.
public abstract class Mob : MonoBehaviour
{
    public static readonly List<Mob> All = new List<Mob>();

    const float Gravity = 32f;
    const float JumpVelocity = 8.4f;

    public MobType Type { get; private set; }
    public float MaxHealth { get; protected set; } = 10f;
    public float Health { get; protected set; }
    public float HalfWidth { get; protected set; } = 0.45f;
    public float Height { get; protected set; } = 0.9f;
    public bool Persistent { get; protected set; }      // không tự biến mất (sói đã thuần)
    public virtual bool IsHostile => false;             // quái: biến mất khi ở xa, không lưu theo chunk
    public virtual string DisplayName => Type.ToString();
    public bool IsDying => dying;
    public bool IsAlive => !dying && this != null;
    public Vector3 Position => transform.position;
    public Bounds Bounds => new Bounds(transform.position + Vector3.up * Height * 0.5f,
                                       new Vector3(HalfWidth * 2f, Height, HalfWidth * 2f));

    protected World world;
    protected EntityModel model;
    protected Vector3 velocity;
    protected bool grounded, hitWall;

    // AI điền các giá trị này trong Think()
    protected Vector3 moveDir;
    protected float moveSpeed;
    protected bool wantJump;
    protected float headPitch;          // radian
    protected bool faceTarget;          // true = quay mặt theo lookTarget thay vì hướng đi
    protected Vector3 lookTarget;

    protected float bodyYaw;
    protected float limbSwing, limbAmount;
    protected float hurtTimer, invulnTimer, controlLockTimer;
    protected float slowFall;           // > 0: rơi chậm (gà)
    protected bool canClimb;            // leo tường (nhện)
    protected float jumpVelocity = JumpVelocity;

    bool dying;
    float deathTimer, despawnTimer;

    static readonly Dictionary<Texture2D, Material> materialCache = new Dictionary<Texture2D, Material>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Init(World w, MobType type)
    {
        world = w;
        Type = type;
        Configure();
        Health = MaxHealth;
        bodyYaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        model = EntityModel.Create(transform, BuildModel(), GetMaterial(CurrentTexture(), OpaqueTexture));
    }

    // ---------------------------------------------------------------- abstract

    protected abstract void Configure();
    protected abstract ModelDef BuildModel();
    protected abstract Texture2D CurrentTexture();
    protected virtual bool OpaqueTexture => false;
    protected abstract void Think(float dt);
    protected abstract void Animate(float dt);
    protected abstract void SpawnDrops();

    /// Người chơi chuột phải vào mob. Trả về true nếu đã xử lý.
    public virtual bool Interact(PlayerInventory player) => false;

    protected virtual void OnDamaged(float amount, Object attacker) { }

    /// Màu nhuộm model mỗi frame (mặc định: đỏ khi vừa bị đánh).
    protected virtual Color TintColor() => hurtTimer > 0f ? new Color(1f, 0.4f, 0.4f) : Color.white;

    /// Gọi khi mob đứng ở vùng chưa load (mob bị đóng băng).
    protected virtual void OnOutsideLoadedArea() { }

    // ---------------------------------------------------------------- material

    protected static Material GetMaterial(Texture2D tex, bool opaque)
    {
        if (tex == null) return null;
        if (materialCache.TryGetValue(tex, out var m) && m != null) return m;
        var assets = GameAssets.Instance;
        var template = assets != null ? (opaque ? assets.mobOpaqueMaterial : assets.mobCutoutMaterial) : null;
        if (template == null) return null;
        m = new Material(template) { name = "Mob " + tex.name };
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        materialCache[tex] = m;
        return m;
    }

    protected void RefreshMaterial()
    {
        var mat = GetMaterial(CurrentTexture(), OpaqueTexture);
        if (mat != null) model.SetMaterial(mat);
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.deltaTime;
        if (world == null) return;

        if (dying)
        {
            deathTimer += dt;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Min(deathTimer * 4f, 1f) * 90f);
            model.SetTint(new Color(1f, 0.35f, 0.35f));
            if (deathTimer > 1f)
            {
                SpawnDrops();
                Destroy(gameObject);
            }
            return;
        }

        // Tự biến mất khi quá xa người chơi
        despawnTimer -= dt;
        if (despawnTimer <= 0f)
        {
            despawnTimer = 2f;
            var p = world.Player;
            // Động vật được lưu theo chunk (MobSpawner), chỉ quái mới tự biến mất khi quá xa
            if (IsHostile && !Persistent && p != null && (p.position - transform.position).sqrMagnitude > 90f * 90f)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (!world.IsChunkReadyAt(transform.position))
        {
            OnOutsideLoadedArea();
            return;
        }

        hurtTimer -= dt;
        invulnTimer -= dt;
        controlLockTimer -= dt;

        moveDir = Vector3.zero;
        wantJump = false;
        faceTarget = false;
        Think(dt);

        // ---- Vật lý
        Vector3 desired = moveDir * moveSpeed;
        float control = controlLockTimer > 0f ? 0f : (grounded ? 12f : 3f);
        velocity.x = Mathf.Lerp(velocity.x, desired.x, Mathf.Clamp01(control * dt));
        velocity.z = Mathf.Lerp(velocity.z, desired.z, Mathf.Clamp01(control * dt));
        if (grounded && controlLockTimer > 0f)
        {
            velocity.x *= Mathf.Pow(0.1f, dt);
            velocity.z *= Mathf.Pow(0.1f, dt);
        }

        bool blocked = hitWall && moveDir.sqrMagnitude > 0.01f;
        if (canClimb && blocked) velocity.y = 3.5f;          // bò lên tường
        else if (grounded && (wantJump || blocked)) velocity.y = jumpVelocity;

        velocity.y -= Gravity * dt;
        if (slowFall > 0f && velocity.y < -slowFall) velocity.y = -slowFall;

        Vector3 pos = transform.position;
        var r = VoxelPhysics.Move(world, ref pos, ref velocity, HalfWidth, Height, dt);
        grounded = r.grounded;
        hitWall = r.hitWall;
        transform.position = pos;

        if (pos.y < -20f)
        {
            Destroy(gameObject);
            return;
        }

        // ---- Hướng quay người
        float targetYaw = bodyYaw;
        if (faceTarget)
        {
            Vector3 d = lookTarget - pos;
            if (d.x * d.x + d.z * d.z > 0.01f) targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
        else if (moveDir.sqrMagnitude > 0.01f)
        {
            targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
        }
        bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, 420f * dt);
        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

        // ---- Animation
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        limbAmount = Mathf.Lerp(limbAmount, Mathf.Clamp01(speed / 2.5f), dt * 10f);
        limbSwing += speed * dt * 6f;
        Animate(dt);

        model.SetTint(TintColor());
    }

    // ---------------------------------------------------------------- combat

    /// Trả về true nếu nhận sát thương.
    public virtual bool Damage(float amount, Vector3 sourcePos, Object attacker, bool knockback = true)
    {
        if (dying || invulnTimer > 0f) return false;

        Health -= amount;
        invulnTimer = 0.5f;
        hurtTimer = 0.35f;

        if (knockback)
        {
            Vector3 dir = transform.position - sourcePos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitSphere;
            dir.y = 0f;
            dir.Normalize();
            velocity.x = dir.x * 6f;
            velocity.z = dir.z * 6f;
            if (grounded) velocity.y = 6f;
            controlLockTimer = 0.35f;
        }

        OnDamaged(amount, attacker);
        if (Health <= 0f) Die();
        return true;
    }

    public void Heal(float amount) => Health = Mathf.Min(MaxHealth, Health + amount);

    /// Đẩy mob (vụ nổ...).
    public void Push(Vector3 v)
    {
        velocity += v;
        controlLockTimer = 0.4f;
    }

    // ---------------------------------------------------------------- hostile helpers

    float burnTimer;

    /// Cháy dưới nắng (zombie, bộ xương). Trả về true nếu đang cháy.
    protected bool BurnInDaylight(float dt)
    {
        var cycle = DayNightCycle.Instance;
        if (cycle == null || !cycle.IsDay || !world.CanSeeSky(transform.position)) return false;
        if (WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining) return false;   // mưa dập lửa
        burnTimer -= dt;
        if (burnTimer <= 0f)
        {
            burnTimer = 1f;
            Damage(1f, transform.position, null, false);
        }
        return true;
    }

    /// Người chơi trong tầm (và còn sống) thì trả về vị trí.
    protected bool PlayerInRange(float range, out Vector3 pos)
    {
        pos = default;
        var p = world.Player;
        if (p == null || PlayerStats.Instance == null || PlayerStats.IsDead) return false;
        if (HorizontalDistance(p.position) > range || Mathf.Abs(p.position.y - transform.position.y) > 12f) return false;
        pos = p.position;
        return true;
    }

    /// Có nhìn thấy điểm đích không (không bị block đặc chắn).
    protected bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        d /= len;
        for (float t = 0.5f; t < len; t += 0.4f)
        {
            Vector3 p = from + d * t;
            if (BlockDatabase.IsOpaque(world.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z))))
                return false;
        }
        return true;
    }

    protected virtual void Die()
    {
        dying = true;
        deathTimer = 0f;
    }

    protected void Drop(ItemId id, int min, int max)
    {
        int n = Random.Range(min, max + 1);
        if (n > 0) world.DropItem(transform.position + Vector3.up * 0.5f, new ItemStack(id, n));
    }

    // ---------------------------------------------------------------- AI helpers

    protected float wanderTimer;
    protected Vector3 wanderDir;

    /// Đi lang thang: lúc đứng, lúc đi hướng ngẫu nhiên; tránh lao xuống vực.
    protected void Wander(float dt, float speed)
    {
        wanderTimer -= dt;
        if (wanderTimer <= 0f)
        {
            if (Random.value < 0.45f)
            {
                wanderDir = Vector3.zero;
                wanderTimer = Random.Range(2f, 6f);
            }
            else
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                wanderDir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                wanderTimer = Random.Range(1.5f, 4f);
            }
        }

        if (wanderDir != Vector3.zero && !SafeAhead(wanderDir))
        {
            wanderDir = Vector3.zero;
            wanderTimer = Random.Range(0.5f, 1.5f);
        }

        moveDir = wanderDir;
        moveSpeed = speed;
    }

    /// Phía trước có đất trong vòng 3 block bên dưới không.
    protected bool SafeAhead(Vector3 dir)
    {
        Vector3 ahead = transform.position + dir * (HalfWidth + 0.6f);
        int x = Mathf.FloorToInt(ahead.x), z = Mathf.FloorToInt(ahead.z);
        int y = Mathf.FloorToInt(transform.position.y);
        for (int dy = 0; dy <= 3; dy++)
            if (world.IsSolidAt(x, y - 1 - dy, z) || world.IsSolidAt(x, y - dy, z)) return true;
        return false;
    }

    protected void MoveTowards(Vector3 target, float speed)
    {
        Vector3 d = target - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.04f) return;
        moveDir = d.normalized;
        moveSpeed = speed;
    }

    protected float HorizontalDistance(Vector3 a)
    {
        Vector3 d = a - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    /// Góc cúi đầu (radian) để nhìn vào một điểm.
    protected float PitchTowards(Vector3 eyePos, Vector3 target)
    {
        Vector3 d = target - eyePos;
        float flat = new Vector2(d.x, d.z).magnitude;
        return Mathf.Clamp(-Mathf.Atan2(d.y, flat), -0.8f, 0.8f);
    }

    // ---- Animation dùng chung
    protected void AnimateQuadrupedLegs()
    {
        float a = Mathf.Cos(limbSwing * 0.6662f) * 1.4f * limbAmount;
        model.SetRotation("right_hind_leg", a);
        model.SetRotation("left_hind_leg", -a);
        model.SetRotation("right_front_leg", -a);
        model.SetRotation("left_front_leg", a);
    }
}
