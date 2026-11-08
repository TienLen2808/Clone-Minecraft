using UnityEngine;

/// Gấu Bắc Cực - thú dữ ban ngày: đi lang thang, người chơi lại gần (6 block) hoặc đánh nó thì đứng dậy lao tới tấn công.
/// Máu trâu, đánh đau. Chết rơi cá sống và da.
public class PolarBear : Mob
{
    float attackCooldown;
    float angerTimer;
    float standUp;      // 0..1 tư thế đứng 2 chân khi tấn công

    public override bool IsHostile => false;    // lưu theo chunk như động vật
    public override string DisplayName => "Gấu Bắc Cực";

    protected override void Configure()
    {
        MaxHealth = 30;
        HalfWidth = 0.7f;
        Height = 1.4f;
    }

    protected override ModelDef BuildModel() => MobModels.PolarBear();
    protected override Texture2D CurrentTexture() => GameAssets.Instance != null ? GameAssets.Instance.polarBear : null;

    protected override void Think(float dt)
    {
        attackCooldown -= dt;
        angerTimer -= dt;

        if (PlayerInRange(angerTimer > 0f ? 24f : 6f, out var target))
        {
            angerTimer = Mathf.Max(angerTimer, 8f);     // đã thấy người thì đuổi một lúc
            MoveTowards(target, 3.4f);
            faceTarget = true;
            lookTarget = target;

            float dist = HorizontalDistance(target);
            if (dist < 2.2f && Mathf.Abs(target.y - transform.position.y) < 1.8f)
            {
                moveDir = Vector3.zero;
                if (attackCooldown <= 0f)
                {
                    attackCooldown = 1.2f;
                    PlayerStats.Instance.Damage(6f, transform.position, this);
                }
            }
            return;
        }

        Wander(dt, 1.4f);
    }

    protected override void OnDamaged(float amount, Object attacker)
    {
        angerTimer = 20f;
    }

    protected override void Animate(float dt)
    {
        model.ResetPose();
        AnimateQuadrupedLegs();

        // Đứng lên 2 chân khi sắp vồ
        float target = attackCooldown > 0.7f ? 1f : 0f;
        standUp = Mathf.MoveTowards(standUp, target, dt * 5f);
        if (standUp > 0f)
        {
            float s = standUp;
            model.SetRotation("body", Mathf.PI / 2f - s * Mathf.PI / 2f * 0.4f);
            model.SetPivot("body", -2f, 9f - s * 4f, 12f + s * 2f);
            model.SetPivot("head", 0f, 10f - s * 9f, -16f + s * 8f);
            model.SetRotation("right_front_leg", -s * 1.2f);
            model.SetRotation("left_front_leg", -s * 1.2f);
            model.SetPivot("right_front_leg", -3.5f, 14f - s * 8f, -8f + s * 6f);
            model.SetPivot("left_front_leg", 3.5f, 14f - s * 8f, -8f + s * 6f);
        }
    }

    protected override void SpawnDrops()
    {
        Drop(ItemId.RawCod, 0, 2);
        Drop(ItemId.Leather, 0, 2);
    }
}
