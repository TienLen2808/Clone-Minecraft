using UnityEngine;

/// Nhện: ban đêm hung dữ, ban ngày chỉ tấn công khi bị đánh. Bò leo tường, vồ người từ xa.
public class Spider : Mob
{
    float attackCooldown, leapCooldown;
    bool provoked;

    public override bool IsHostile => true;
    public override string DisplayName => "Nhện";

    protected override void Configure()
    {
        MaxHealth = 16;
        HalfWidth = 0.65f;
        Height = 0.9f;
        canClimb = true;
    }

    protected override ModelDef BuildModel() => MobModels.Spider();
    protected override Texture2D CurrentTexture() => GameAssets.Instance != null ? GameAssets.Instance.spider : null;
    protected override bool OpaqueTexture => true;   // spider.tga: alpha là mặt nạ mắt phát sáng

    protected override void Think(float dt)
    {
        attackCooldown -= dt;
        leapCooldown -= dt;

        bool night = (DayNightCycle.Instance != null && !DayNightCycle.Instance.IsDay)
                     || (WeatherSystem.Instance != null && WeatherSystem.Instance.IsThundering);
        if ((night || provoked) && PlayerInRange(16f, out var target))
        {
            MoveTowards(target, 3.2f);
            faceTarget = true;
            lookTarget = target;
            float dist = HorizontalDistance(target);

            // Vồ tới
            if (grounded && dist > 1.5f && dist < 4f && leapCooldown <= 0f)
            {
                leapCooldown = 1.5f;
                Vector3 d = (target - transform.position);
                d.y = 0f;
                d.Normalize();
                velocity = d * 7f + Vector3.up * 6f;
            }

            if (dist < 1.4f && Mathf.Abs(target.y - transform.position.y) < 1.5f && attackCooldown <= 0f)
            {
                attackCooldown = 1f;
                PlayerStats.Instance.Damage(2f, transform.position, this);
            }
            return;
        }

        Wander(dt, 1.5f);
    }

    protected override void OnDamaged(float amount, Object attacker)
    {
        if (attacker is PlayerStats) provoked = true;
    }

    protected override void Animate(float dt)
    {
        float s = limbSwing * 0.6662f * 2f;
        float amt = limbAmount;
        float y1 = -(Mathf.Cos(s) * 0.4f) * amt;
        float y2 = -(Mathf.Cos(s + Mathf.PI) * 0.4f) * amt;
        float y3 = -(Mathf.Cos(s + Mathf.PI / 2f) * 0.4f) * amt;
        float y4 = -(Mathf.Cos(s + Mathf.PI * 1.5f) * 0.4f) * amt;
        float z1 = Mathf.Abs(Mathf.Sin(limbSwing * 0.6662f) * 0.4f) * amt;
        float z2 = Mathf.Abs(Mathf.Sin(limbSwing * 0.6662f + Mathf.PI) * 0.4f) * amt;
        float z3 = Mathf.Abs(Mathf.Sin(limbSwing * 0.6662f + Mathf.PI / 2f) * 0.4f) * amt;
        float z4 = Mathf.Abs(Mathf.Sin(limbSwing * 0.6662f + Mathf.PI * 1.5f) * 0.4f) * amt;
        float[] yAdd = { y1, -y1, y2, -y2, y3, -y3, y4, -y4 };
        float[] zAdd = { z1, -z1, z2, -z2, z3, -z3, z4, -z4 };

        var legs = MobModels.SpiderLegs;
        for (int i = 0; i < legs.Length; i++)
            model.SetRotation(legs[i].name, 0f, legs[i].yRot + yAdd[i], legs[i].zRot + zAdd[i]);
    }

    protected override void SpawnDrops()
    {
        Drop(ItemId.String, 0, 2);
        if (Random.value < 0.33f) Drop(ItemId.SpiderEye, 1, 1);
    }
}
