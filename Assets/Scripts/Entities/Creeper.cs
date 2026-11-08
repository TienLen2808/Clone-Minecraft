using UnityEngine;

/// Creeper: lặng lẽ tiến lại gần, châm ngòi 1.5 giây (phình to + nháy trắng) rồi phát nổ phá block.
/// Chạy ra xa trên 7 block thì ngòi tắt.
public class Creeper : Mob
{
    const float FuseTime = 1.5f;
    const float ExplosionRadius = 3f;

    float fuse;
    bool exploded;

    public override bool IsHostile => true;
    public override string DisplayName => "Creeper";

    protected override void Configure()
    {
        MaxHealth = 20;
        HalfWidth = 0.3f;
        Height = 1.7f;
    }

    protected override ModelDef BuildModel() => MobModels.Creeper();
    protected override Texture2D CurrentTexture() => GameAssets.Instance != null ? GameAssets.Instance.creeper : null;

    protected override void Think(float dt)
    {
        if (exploded) return;

        if (PlayerInRange(16f, out var target) && HasLineOfSight(transform.position + Vector3.up * 1.4f, target + Vector3.up * 1.5f))
        {
            float dist = Vector3.Distance(target, transform.position);
            faceTarget = true;
            lookTarget = target;

            if (dist < 3f || (fuse > 0f && dist < 7f))
            {
                fuse += dt;   // đứng lại châm ngòi
                if (fuse >= FuseTime) Explode();
            }
            else
            {
                fuse = Mathf.Max(0f, fuse - dt);
                MoveTowards(target, 2.2f);
            }
            return;
        }

        fuse = Mathf.Max(0f, fuse - dt);
        Wander(dt, 1.2f);
    }

    void Explode()
    {
        exploded = true;
        world.Explode(transform.position + Vector3.up * 0.8f, ExplosionRadius, this);
        Destroy(gameObject);
    }

    protected override void Animate(float dt)
    {
        AnimateQuadrupedLegs();

        // Phình to + nháy trắng khi sắp nổ
        float f = fuse / FuseTime;
        float swell = 1f + Mathf.Sin(f * 60f) * f * 0.03f + f * 0.25f;
        model.transform.localScale = new Vector3(swell, 1f + f * 0.12f, swell);
    }

    protected override Color TintColor()
    {
        float f = fuse / FuseTime;
        if (f > 0f && hurtTimer <= 0f && (int)(f * 12f) % 2 == 0) return new Color(2.2f, 2.2f, 2.2f);
        return base.TintColor();
    }

    protected override void SpawnDrops()
    {
        if (!exploded) Drop(ItemId.Gunpowder, 0, 2);
    }
}
