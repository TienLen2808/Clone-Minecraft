using UnityEngine;

/// Định nghĩa model các mob, lấy từ model Minecraft Java (toạ độ pixel, Y hướng xuống, mặt trước -Z).
public static class MobModels
{
    const float HalfPi = Mathf.PI / 2f;

    /// Người chơi (skin 64x64 có lớp ngoài).
    public static ModelDef Steve()
    {
        var m = new ModelDef(64, 64, 0.9375f / 16f);
        m.Part("head", 0, 0, 0)
            .Box(-4, -8, -4, 8, 8, 8, 0, 0)
            .Box(-4, -8, -4, 8, 8, 8, 32, 0, 0.5f);
        m.Part("body", 0, 0, 0)
            .Box(-4, 0, -2, 8, 12, 4, 16, 16)
            .Box(-4, 0, -2, 8, 12, 4, 16, 32, 0.25f);
        m.Part("right_arm", -5, 2, 0)
            .Box(-3, -2, -2, 4, 12, 4, 40, 16)
            .Box(-3, -2, -2, 4, 12, 4, 40, 32, 0.25f);
        m.Part("left_arm", 5, 2, 0)
            .Box(-1, -2, -2, 4, 12, 4, 32, 48)
            .Box(-1, -2, -2, 4, 12, 4, 48, 48, 0.25f);
        m.Part("right_leg", -1.9f, 12, 0)
            .Box(-2, 0, -2, 4, 12, 4, 0, 16)
            .Box(-2, 0, -2, 4, 12, 4, 0, 32, 0.25f);
        m.Part("left_leg", 1.9f, 12, 0)
            .Box(-2, 0, -2, 4, 12, 4, 16, 48)
            .Box(-2, 0, -2, 4, 12, 4, 0, 48, 0.25f);
        return m;
    }

    /// Zombie (texture cũ 64x32: tay/chân trái là bản lật của bên phải).
    public static ModelDef Zombie()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 0, 0)
            .Box(-4, -8, -4, 8, 8, 8, 0, 0)
            .Box(-4, -8, -4, 8, 8, 8, 32, 0, 0.5f);
        m.Part("body", 0, 0, 0).Box(-4, 0, -2, 8, 12, 4, 16, 16);
        m.Part("right_arm", -5, 2, 0, -HalfPi).Box(-3, -2, -2, 4, 12, 4, 40, 16);
        m.Part("left_arm", 5, 2, 0, -HalfPi).Box(-1, -2, -2, 4, 12, 4, 40, 16, 0f, true);
        m.Part("right_leg", -1.9f, 12, 0).Box(-2, 0, -2, 4, 12, 4, 0, 16);
        m.Part("left_leg", 1.9f, 12, 0).Box(-2, 0, -2, 4, 12, 4, 0, 16, 0f, true);
        return m;
    }

    public static ModelDef Pig()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 12, -6)
            .Box(-4, -4, -8, 8, 8, 8, 0, 0)
            .Box(-2, 0, -9, 4, 3, 1, 16, 16);
        m.Part("body", 0, 11, 2, HalfPi).Box(-5, -10, -7, 10, 16, 8, 28, 8);
        Legs4(m, 6, 3, 7, -5, 4);
        return m;
    }

    public static ModelDef Cow()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 4, -8)
            .Box(-4, -4, -6, 8, 8, 6, 0, 0)
            .Box(-5, -5, -4, 1, 3, 1, 22, 0)
            .Box(4, -5, -4, 1, 3, 1, 22, 0);
        m.Part("body", 0, 5, 2, HalfPi)
            .Box(-6, -10, -7, 12, 18, 10, 18, 4)
            .Box(-2, 2, -8, 4, 6, 1, 52, 0);
        Legs4(m, 12, 4, 7, -6, 4);
        return m;
    }

    /// Cừu: sheep.tga 64x64 của Bedrock — nửa trên là thân, nửa dưới (v + 32) là lớp len.
    public static ModelDef Sheep()
    {
        var m = new ModelDef(64, 64);
        m.Part("head", 0, 6, -8)
            .Box(-3, -4, -6, 6, 6, 8, 0, 0)
            .Box(-3, -4, -4, 6, 6, 6, 0, 32, 0.6f);
        m.Part("body", 0, 5, 2, HalfPi)
            .Box(-4, -10, -7, 8, 16, 6, 28, 8)
            .Box(-4, -10, -7, 8, 16, 6, 28, 40, 1.75f);
        string[] names = { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" };
        float[,] pivots = { { -3, 12, 7 }, { 3, 12, 7 }, { -3, 12, -5 }, { 3, 12, -5 } };
        for (int i = 0; i < 4; i++)
        {
            m.Part(names[i], pivots[i, 0], pivots[i, 1], pivots[i, 2])
                .Box(-2, 0, -2, 4, 12, 4, 0, 16)
                .Box(-2, 0, -2, 4, 6, 4, 0, 48, 0.5f);
        }
        return m;
    }

    public static ModelDef Chicken()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 15, -4)
            .Box(-2, -6, -2, 4, 6, 3, 0, 0)
            .Box(-2, -4, -4, 4, 2, 2, 14, 0)
            .Box(-1, -2, -3, 2, 2, 2, 14, 4);
        m.Part("body", 0, 16, 0, HalfPi).Box(-3, -4, -3, 6, 8, 6, 0, 9);
        m.Part("right_leg", -2, 19, 1).Box(-1, 0, -3, 3, 5, 3, 26, 0);
        m.Part("left_leg", 1, 19, 1).Box(-1, 0, -3, 3, 5, 3, 26, 0);
        m.Part("right_wing", -4, 13, 0).Box(0, 0, -3, 1, 4, 6, 24, 13);
        m.Part("left_wing", 4, 13, 0).Box(-1, 0, -3, 1, 4, 6, 24, 13);
        return m;
    }

    public static ModelDef Wolf()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", -1, 13.5f, -7)
            .Box(-2, -3, -2, 6, 6, 4, 0, 0)
            .Box(-2, -5, 0, 2, 2, 1, 16, 14)
            .Box(2, -5, 0, 2, 2, 1, 16, 14)
            .Box(-0.5f, -0.001f, -5, 3, 3, 4, 0, 10);
        m.Part("body", 0, 14, 2, HalfPi).Box(-3, -2, -3, 6, 9, 6, 18, 14);
        m.Part("upper_body", -1, 14, -3, HalfPi).Box(-3, -3, -3, 8, 6, 7, 21, 0);
        m.Part("right_hind_leg", -2.5f, 16, 7).Box(0, 0, -1, 2, 8, 2, 0, 18);
        m.Part("left_hind_leg", 0.5f, 16, 7).Box(0, 0, -1, 2, 8, 2, 0, 18);
        m.Part("right_front_leg", -2.5f, 16, -4).Box(0, 0, -1, 2, 8, 2, 0, 18);
        m.Part("left_front_leg", 0.5f, 16, -4).Box(0, 0, -1, 2, 8, 2, 0, 18);
        m.Part("tail", -1, 12, 8, Mathf.PI / 5f).Box(0, 0, -1, 2, 8, 2, 9, 18);
        return m;
    }

    public static ModelDef Creeper()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 6, 0).Box(-4, -8, -4, 8, 8, 8, 0, 0);
        m.Part("body", 0, 6, 0).Box(-4, 0, -2, 8, 12, 4, 16, 16);
        m.Part("right_hind_leg", -2, 18, 4).Box(-2, 0, -2, 4, 6, 4, 0, 16);
        m.Part("left_hind_leg", 2, 18, 4).Box(-2, 0, -2, 4, 6, 4, 0, 16);
        m.Part("right_front_leg", -2, 18, -4).Box(-2, 0, -2, 4, 6, 4, 0, 16);
        m.Part("left_front_leg", 2, 18, -4).Box(-2, 0, -2, 4, 6, 4, 0, 16);
        return m;
    }

    /// Bộ xương: giống người nhưng tay chân mảnh 2x2.
    public static ModelDef Skeleton()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 0, 0).Box(-4, -8, -4, 8, 8, 8, 0, 0);
        m.Part("body", 0, 0, 0).Box(-4, 0, -2, 8, 12, 4, 16, 16);
        m.Part("right_arm", -5, 2, 0).Box(-1, -2, -1, 2, 12, 2, 40, 16);
        m.Part("left_arm", 5, 2, 0).Box(-1, -2, -1, 2, 12, 2, 40, 16, 0f, true);
        m.Part("right_leg", -2, 12, 0).Box(-1, 0, -1, 2, 12, 2, 0, 16);
        m.Part("left_leg", 2, 12, 0).Box(-1, 0, -1, 2, 12, 2, 0, 16, 0f, true);
        return m;
    }

    /// Nhện: 8 chân, góc nghỉ lấy từ SpiderModel của Java.
    public static ModelDef Spider()
    {
        var m = new ModelDef(64, 32);
        m.Part("head", 0, 15, -3).Box(-4, -4, -8, 8, 8, 8, 32, 4);
        m.Part("body0", 0, 15, 0).Box(-3, -3, -3, 6, 6, 6, 0, 0);
        m.Part("body1", 0, 15, 9).Box(-5, -4, -6, 10, 8, 12, 0, 12);
        foreach (var leg in SpiderLegs)
        {
            bool right = leg.x < 0;
            m.Part(leg.name, leg.x, 15, leg.z, 0f, leg.yRot, leg.zRot)
                .Box(right ? -15 : -1, -1, -1, 16, 2, 2, 18, 0);
        }
        return m;
    }

    public struct SpiderLeg
    {
        public string name;
        public float x, z, yRot, zRot;
        public SpiderLeg(string n, float x, float z, float yRot, float zRot) { name = n; this.x = x; this.z = z; this.yRot = yRot; this.zRot = zRot; }
    }

    const float Q = Mathf.PI / 4f, F2 = 0.3926991f;
    public static readonly SpiderLeg[] SpiderLegs =
    {
        new SpiderLeg("right_hind_leg", -4, 2, F2 * 2f, -Q),
        new SpiderLeg("left_hind_leg", 4, 2, -F2 * 2f, Q),
        new SpiderLeg("right_middle_hind_leg", -4, 1, F2, -Q * 0.74f),
        new SpiderLeg("left_middle_hind_leg", 4, 1, -F2, Q * 0.74f),
        new SpiderLeg("right_middle_front_leg", -4, 0, -F2, -Q * 0.74f),
        new SpiderLeg("left_middle_front_leg", 4, 0, F2, Q * 0.74f),
        new SpiderLeg("right_front_leg", -4, -1, -F2 * 2f, -Q),
        new SpiderLeg("left_front_leg", 4, -1, F2 * 2f, Q),
    };

    /// Gấu Bắc Cực (texture 128x64).
    public static ModelDef PolarBear()
    {
        var m = new ModelDef(128, 64);
        m.Part("head", 0, 10, -16)
            .Box(-3.5f, -3, -3, 7, 7, 7, 0, 0)
            .Box(-2.5f, 1, -6, 5, 3, 3, 0, 44)
            .Box(-4.5f, -4, -1, 2, 2, 1, 26, 0)
            .Box(2.5f, -4, -1, 2, 2, 1, 26, 0, 0f, true);
        m.Part("body", -2, 9, 12, HalfPi)
            .Box(-5, -13, -7, 14, 14, 11, 0, 19)
            .Box(-4, -25, -7, 12, 12, 10, 39, 0);
        m.Part("right_hind_leg", -4.5f, 14, 6).Box(-2, 0, -2, 4, 10, 8, 50, 22);
        m.Part("left_hind_leg", 4.5f, 14, 6).Box(-2, 0, -2, 4, 10, 8, 50, 22);
        m.Part("right_front_leg", -3.5f, 14, -8).Box(-2, 0, -2, 4, 10, 6, 50, 40);
        m.Part("left_front_leg", 3.5f, 14, -8).Box(-2, 0, -2, 4, 10, 6, 50, 40);
        return m;
    }

    /// 4 chân thú: legHeight, x lệch tâm, z chân sau, z chân trước, bề rộng chân.
    static void Legs4(ModelDef m, float legHeight, float x, float zHind, float zFront, float size)
    {
        float py = 24 - legHeight;
        float half = size / 2f;
        m.Part("right_hind_leg", -x, py, zHind).Box(-half, 0, -half, size, legHeight, size, 0, 16);
        m.Part("left_hind_leg", x, py, zHind).Box(-half, 0, -half, size, legHeight, size, 0, 16);
        m.Part("right_front_leg", -x, py, zFront).Box(-half, 0, -half, size, legHeight, size, 0, 16);
        m.Part("left_front_leg", x, py, zFront).Box(-half, 0, -half, size, legHeight, size, 0, 16);
    }
}
