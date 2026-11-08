using UnityEngine;

/// Hiệu ứng nổ: quả cầu sáng phình ra rồi mờ dần.
public class ExplosionFlash : MonoBehaviour
{
    const float Duration = 0.4f;

    Material mat;
    float radius, t;

    public static void Spawn(Vector3 pos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "Explosion";
        go.transform.position = pos;
        var f = go.AddComponent<ExplosionFlash>();
        f.radius = radius;

        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var template = GameAssets.Instance != null ? GameAssets.Instance.crackMaterial : null;
        if (template != null)
        {
            f.mat = new Material(template);
            f.mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            f.mat.mainTexture = Texture2D.whiteTexture;
            r.sharedMaterial = f.mat;
        }
        f.Apply();
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= Duration)
        {
            if (mat != null) Destroy(mat);
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float k = t / Duration;
        transform.localScale = Vector3.one * Mathf.Lerp(0.6f, radius * 2.4f, Mathf.Sqrt(k));
        if (mat != null) mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.8f, (1f - k) * 0.85f));
    }
}
