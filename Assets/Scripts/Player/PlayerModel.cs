using UnityEngine;
using UnityEngine.Rendering;

/// Nhân vật Steve (skin 64x64), dựng bằng EntityModel. Tay chân vung khi đi, vung tay khi đánh/đào, cúi người khi lén.
public class PlayerModel : MonoBehaviour
{
    [SerializeField] Texture2D skin;
    [SerializeField] Material material;   // để trống -> tự tạo URP/Lit có alpha clip

    const string DefaultSkinPath = "Assets/Textures/Minecraft/entity/steve.png";

    EntityModel model;
    float walkPhase, swing, swingTimer;
    bool built, firstPerson;

    void Awake() => Build();

    public void Build()
    {
        if (built) return;
        built = true;

        if (skin == null && GameAssets.Instance != null) skin = GameAssets.Instance.steveSkin;
#if UNITY_EDITOR
        if (skin == null && material == null)
            skin = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultSkinPath);
#endif
        if (material == null) material = CreateDefaultMaterial(skin);

        model = EntityModel.Create(transform, MobModels.Steve(), material, "Steve");
        model.SetShadowOnly(firstPerson);
    }

    static Material CreateDefaultMaterial(Texture2D tex)
    {
        var template = GameAssets.Instance != null ? GameAssets.Instance.mobCutoutMaterial : null;
        Material mat;
        if (template != null) mat = new Material(template);
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Smoothness", 0f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        mat.name = "PlayerSkin (runtime)";
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
        }
        return mat;
    }

    // ---------------------------------------------------------------- public

    /// Góc nhìn thứ nhất: ẩn model nhưng vẫn đổ bóng.
    public void SetFirstPerson(bool fp)
    {
        firstPerson = fp;
        if (model != null) model.SetShadowOnly(fp);
    }

    /// Vung tay phải (đánh / đào / đặt block).
    public void Swing()
    {
        if (swingTimer <= 0f) swingTimer = 0.3f;
    }

    public void SetHurt(bool hurt)
    {
        if (model != null) model.SetTint(hurt ? new Color(1f, 0.4f, 0.4f) : Color.white);
    }

    public void Animate(float horizontalSpeed, float pitchDeg, bool grounded, bool sneaking, float dt)
    {
        if (model == null) return;

        float target = grounded ? Mathf.Clamp01(horizontalSpeed / 4.3f) : swing * 0.9f;
        swing = Mathf.Lerp(swing, target, dt * 10f);
        walkPhase += horizontalSpeed * dt * 2.6f;

        float a = Mathf.Sin(walkPhase) * 0.9f * swing;
        float idle = Mathf.Sin(Time.time * 1.5f) * 0.05f;

        model.ResetPose();
        model.SetRotation("right_leg", a);
        model.SetRotation("left_leg", -a);
        model.SetRotation("left_arm", -a * 0.8f, 0f, -idle - 0.03f);

        float armX = a * 0.8f;
        if (swingTimer > 0f)
        {
            swingTimer -= dt;
            float t = 1f - swingTimer / 0.3f;
            armX = -Mathf.Sin(t * Mathf.PI) * 1.6f;
        }
        model.SetRotation("right_arm", armX, 0f, idle + 0.03f);

        model.SetRotation("head", Mathf.Clamp(pitchDeg, -89f, 89f) * Mathf.Deg2Rad);

        if (sneaking)
        {
            // Cúi người như Minecraft
            model.SetRotation("body", 0.5f);
            model.SetPivot("head", 0f, 4.2f, 0f);
            model.SetPivot("body", 0f, 3.2f, 0f);
            model.SetPivot("right_arm", -5f, 5.2f, 0f);
            model.SetPivot("left_arm", 5f, 5.2f, 0f);
            model.SetPivot("right_leg", -1.9f, 12.2f, 4f);
            model.SetPivot("left_leg", 1.9f, 12.2f, 4f);
        }

        SetHurt(PlayerStats.Instance != null && PlayerStats.Instance.DamageFlash > 0.6f);
    }
}
