using UnityEngine;

/// Zombie: xuất hiện ban đêm, đuổi theo và đánh người chơi, cháy dưới nắng.
public class Zombie : Mob
{
    const float SightRange = 24f;
    const float AttackRange = 1.25f;
    const float AttackDamage = 3f;

    float attackCooldown;

    public override bool IsHostile => true;
    public override string DisplayName => "Zombie";
    Mob targetMob;          // sói tấn công thì zombie đánh lại sói

    protected override void Configure()
    {
        MaxHealth = 20;
        HalfWidth = 0.3f;
        Height = 1.95f;
    }

    protected override ModelDef BuildModel() => MobModels.Zombie();
    protected override Texture2D CurrentTexture() => GameAssets.Instance != null ? GameAssets.Instance.zombie : null;

    protected override void Think(float dt)
    {
        attackCooldown -= dt;
        Vector3 eye = transform.position + Vector3.up * 1.7f;

        BurnInDaylight(dt);

        // Ưu tiên đánh sói đang tấn công mình
        if (targetMob != null && (!targetMob.IsAlive || HorizontalDistance(targetMob.Position) > 16f)) targetMob = null;
        if (targetMob != null)
        {
            ChaseAndAttack(targetMob.Position, eye, () => targetMob.Damage(AttackDamage, transform.position, this));
            return;
        }

        var player = world.Player;
        var stats = PlayerStats.Instance;
        if (player != null && stats != null && !PlayerStats.IsDead)
        {
            float dist = HorizontalDistance(player.position);
            float dy = Mathf.Abs(player.position.y - transform.position.y);
            if (dist < SightRange && dy < 10f)
            {
                ChaseAndAttack(player.position, eye, () => stats.Damage(AttackDamage, transform.position, this));
                return;
            }
        }

        Wander(dt, 1.2f);
        headPitch = Mathf.Lerp(headPitch, 0f, dt * 3f);
    }

    void ChaseAndAttack(Vector3 target, Vector3 eye, System.Action attack)
    {
        MoveTowards(target, 2.4f);
        faceTarget = true;
        lookTarget = target;
        headPitch = PitchTowards(eye, target + Vector3.up * 1.4f);

        float dist = HorizontalDistance(target);
        if (dist < AttackRange && Mathf.Abs(target.y - transform.position.y) < 1.6f)
        {
            moveDir = Vector3.zero;
            if (attackCooldown <= 0f)
            {
                attackCooldown = 1f;
                attack();
            }
        }
    }

    protected override void OnDamaged(float amount, Object attacker)
    {
        if (attacker is Wolf w) targetMob = w;
    }

    protected override void Animate(float dt)
    {
        float a = Mathf.Cos(limbSwing * 0.6662f) * 1.4f * limbAmount;
        model.SetRotation("right_leg", a);
        model.SetRotation("left_leg", -a);

        // Tay giơ về phía trước, đung đưa nhẹ
        float sway = Mathf.Sin(Time.time * 1.6f) * 0.05f;
        float swing = attackCooldown > 0.75f ? -0.6f : 0f;
        model.SetRotation("right_arm", -Mathf.PI / 2f + sway + swing, 0f, 0.05f);
        model.SetRotation("left_arm", -Mathf.PI / 2f - sway + swing, 0f, -0.05f);
        model.SetRotation("head", headPitch);
    }

    protected override void SpawnDrops()
    {
        Drop(ItemId.RottenFlesh, 0, 2);
        if (Random.value < 0.4f) Drop(ItemId.Bone, 1, 2);   // để có xương thuần hoá sói
    }
}
