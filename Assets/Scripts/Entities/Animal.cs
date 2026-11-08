using UnityEngine;

/// Động vật hiền: heo, bò, cừu, gà. Đi lang thang, bị đánh thì bỏ chạy, chết rơi thịt/da/len/lông.
public class Animal : Mob
{
    float walkSpeed = 1.6f;
    float panicTimer;
    Vector3 panicFrom;
    float lookTimer, lookPitch;
    float panicSeed;

    public override string DisplayName =>
        Type == MobType.Pig ? "Heo" : Type == MobType.Cow ? "Bò" : Type == MobType.Sheep ? "Cừu" : "Gà";   // mỗi con chạy lệch hướng khác nhau

    protected override void Configure()
    {
        panicSeed = Random.Range(0f, 100f);
        switch (Type)
        {
            case MobType.Pig:
                MaxHealth = 10; HalfWidth = 0.45f; Height = 0.9f; walkSpeed = 1.7f;
                break;
            case MobType.Cow:
                MaxHealth = 10; HalfWidth = 0.45f; Height = 1.4f; walkSpeed = 1.6f;
                break;
            case MobType.Sheep:
                MaxHealth = 8; HalfWidth = 0.45f; Height = 1.3f; walkSpeed = 1.6f;
                break;
            case MobType.Chicken:
                MaxHealth = 4; HalfWidth = 0.2f; Height = 0.7f; walkSpeed = 1.5f; slowFall = 3f;
                break;
        }
    }

    protected override ModelDef BuildModel()
    {
        switch (Type)
        {
            case MobType.Cow: return MobModels.Cow();
            case MobType.Sheep: return MobModels.Sheep();
            case MobType.Chicken: return MobModels.Chicken();
            default: return MobModels.Pig();
        }
    }

    protected override Texture2D CurrentTexture()
    {
        var a = GameAssets.Instance;
        if (a == null) return null;
        switch (Type)
        {
            case MobType.Cow: return a.cow;
            case MobType.Sheep: return a.sheep;
            case MobType.Chicken: return a.chicken;
            default: return a.pig;
        }
    }

    // sheep.tga của Bedrock dùng alpha làm mặt nạ nhuộm màu len -> vẽ đặc
    protected override bool OpaqueTexture => Type == MobType.Sheep;

    protected override void Think(float dt)
    {
        if (panicTimer > 0f)
        {
            panicTimer -= dt;
            Vector3 away = transform.position - panicFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            // Lắc hướng một chút cho tự nhiên
            away = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f + panicSeed) * 35f, 0f) * away.normalized;
            moveDir = away;
            moveSpeed = walkSpeed * 2.1f;
            return;
        }

        Wander(dt, walkSpeed);

        // Thỉnh thoảng cúi/ngẩng đầu
        lookTimer -= dt;
        if (lookTimer <= 0f)
        {
            lookTimer = Random.Range(2f, 5f);
            lookPitch = Random.value < 0.3f ? Random.Range(0.2f, 0.6f) : Random.Range(-0.2f, 0.1f);
        }
        headPitch = Mathf.Lerp(headPitch, lookPitch, dt * 3f);
    }

    protected override void OnDamaged(float amount, Object attacker)
    {
        panicTimer = 5f;
        panicFrom = attacker is Component c ? c.transform.position : transform.position - transform.forward;
    }

    protected override void Animate(float dt)
    {
        if (Type == MobType.Chicken)
        {
            float a = Mathf.Cos(limbSwing * 0.6662f) * 1.4f * limbAmount;
            model.SetRotation("right_leg", a);
            model.SetRotation("left_leg", -a);
            float flap = grounded ? 0f : (Mathf.Sin(Time.time * 25f) + 1f) * 0.8f;
            model.SetRotation("right_wing", 0f, 0f, flap);
            model.SetRotation("left_wing", 0f, 0f, -flap);
            model.SetRotation("head", headPitch * 0.5f);
            return;
        }

        AnimateQuadrupedLegs();
        model.SetRotation("head", headPitch);
    }

    protected override void SpawnDrops()
    {
        switch (Type)
        {
            case MobType.Pig:
                Drop(ItemId.RawPorkchop, 1, 3);
                break;
            case MobType.Cow:
                Drop(ItemId.RawBeef, 1, 3);
                Drop(ItemId.Leather, 0, 2);
                break;
            case MobType.Sheep:
                Drop(ItemId.RawMutton, 1, 2);
                Drop(ItemId.WhiteWool, 1, 1);
                break;
            case MobType.Chicken:
                Drop(ItemId.RawChicken, 1, 1);
                Drop(ItemId.Feather, 0, 2);
                break;
        }
    }
}
