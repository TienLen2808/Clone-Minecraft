using UnityEngine;

/// Bộ xương: ban đêm xuất hiện, giữ khoảng cách và bắn tên vào người chơi, cháy dưới nắng.
/// Chết rơi xương (thuần hoá sói) và mũi tên.
public class Skeleton : Mob
{
    float shootCooldown = 1.5f;
    float strafeTimer;
    int strafeSide = 1;
    float aimTimer;

    public override bool IsHostile => true;
    public override string DisplayName => "Bộ xương";

    protected override void Configure()
    {
        MaxHealth = 20;
        HalfWidth = 0.3f;
        Height = 1.95f;
    }

    protected override ModelDef BuildModel() => MobModels.Skeleton();
    protected override Texture2D CurrentTexture() => GameAssets.Instance != null ? GameAssets.Instance.skeleton : null;

    protected override void Think(float dt)
    {
        shootCooldown -= dt;
        BurnInDaylight(dt);

        Vector3 eye = transform.position + Vector3.up * 1.7f;
        if (PlayerInRange(18f, out var target))
        {
            Vector3 targetChest = target + Vector3.up * 1.2f;
            bool see = HasLineOfSight(eye, targetChest);
            float dist = HorizontalDistance(target);

            faceTarget = true;
            lookTarget = target;
            headPitch = PitchTowards(eye, targetChest);

            // Giữ khoảng cách 6-12 block, đi vòng sang ngang
            Vector3 to = target - transform.position;
            to.y = 0f;
            to.Normalize();
            strafeTimer -= dt;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(1f, 2.5f);
                strafeSide = Random.value < 0.5f ? -1 : 1;
            }
            Vector3 side = Vector3.Cross(Vector3.up, to) * strafeSide;

            if (!see || dist > 12f) { moveDir = to; moveSpeed = 2.2f; }
            else if (dist < 6f) { moveDir = (-to + side * 0.5f).normalized; moveSpeed = 2f; }
            else { moveDir = side; moveSpeed = 1.2f; }
            if (!SafeAhead(moveDir)) moveDir = Vector3.zero;

            if (see && dist < 16f)
            {
                aimTimer += dt;
                if (shootCooldown <= 0f && aimTimer > 0.6f)
                {
                    shootCooldown = Random.Range(1.6f, 2.4f);
                    aimTimer = 0f;
                    Shoot(eye, targetChest, dist);
                }
            }
            else aimTimer = 0f;
            return;
        }

        aimTimer = 0f;
        Wander(dt, 1.2f);
        headPitch = Mathf.Lerp(headPitch, 0f, dt * 3f);
    }

    void Shoot(Vector3 from, Vector3 target, float dist)
    {
        Vector3 start = from + transform.forward * 0.6f;
        Vector3 dir = target - start;
        dir.y += dist * 0.07f;          // nhắm cao hơn bù trọng lực
        dir.Normalize();
        // Độ chính xác vừa phải
        dir = Quaternion.Euler(Random.Range(-2.5f, 2.5f), Random.Range(-2.5f, 2.5f), 0f) * dir;
        Arrow.Spawn(world, start, dir * 26f, Random.Range(2f, 4f), this, false);
    }

    protected override void Animate(float dt)
    {
        float a = Mathf.Cos(limbSwing * 0.6662f) * 1.4f * limbAmount;
        model.SetRotation("right_leg", a);
        model.SetRotation("left_leg", -a);

        // Giương cung: tay phải giơ thẳng, tay trái kéo dây
        bool aiming = aimTimer > 0f;
        if (aiming)
        {
            model.SetRotation("right_arm", -Mathf.PI / 2f + headPitch, -0.1f);
            model.SetRotation("left_arm", -Mathf.PI / 2f + headPitch, 0.5f);
        }
        else
        {
            model.SetRotation("right_arm", -a * 0.8f);
            model.SetRotation("left_arm", a * 0.8f);
        }
        model.SetRotation("head", headPitch);
    }

    protected override void SpawnDrops()
    {
        Drop(ItemId.Bone, 0, 2);
        Drop(ItemId.Arrow, 0, 2);
    }
}
