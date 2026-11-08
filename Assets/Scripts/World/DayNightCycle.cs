using UnityEngine;

/// Chu kỳ ngày đêm: xoay mặt trời, đổi màu trời/sương mù/ánh sáng môi trường.
/// time: 0 = bình minh, 0.25 = trưa, 0.5 = hoàng hôn, 0.75 = nửa đêm.
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [SerializeField] Light sun;
    [SerializeField] float dayLengthSeconds = 600f;   // 10 phút một ngày
    [Range(0f, 1f)] [SerializeField] float time = 0.05f;
    [SerializeField] float sunIntensity = 1.25f;

    static readonly Color DaySky = new Color(0.53f, 0.72f, 1f);
    static readonly Color NightSky = new Color(0.02f, 0.03f, 0.07f);
    static readonly Color SunsetSky = new Color(0.95f, 0.55f, 0.3f);
    static readonly Color DayAmbient = new Color(0.6f, 0.65f, 0.75f);
    static readonly Color NightAmbient = new Color(0.12f, 0.14f, 0.24f);

    public float TimeOfDay => time;
    public int Day { get; private set; } = 1;
    public float SunElevation { get; private set; }
    public bool IsDay => SunElevation > 0.05f;
    public bool IsNight => SunElevation < -0.08f;

    /// Giờ trong game dạng "HH:mm" (bình minh = 06:00).
    public string Clock
    {
        get
        {
            float hours = (time * 24f + 6f) % 24f;
            int h = (int)hours;
            int m = (int)((hours - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }

    void Awake()
    {
        Instance = this;
        if (sun == null) sun = RenderSettings.sun;
    }

    void Update()
    {
        time += Time.deltaTime / Mathf.Max(1f, dayLengthSeconds);
        if (time >= 1f)
        {
            time -= 1f;
            Day++;
        }
        Apply();
    }

    void Apply()
    {
        float angle = time * 360f;
        SunElevation = Mathf.Sin(time * Mathf.PI * 2f);
        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.25f, SunElevation));

        // Thời tiết
        var w = WeatherSystem.Instance;
        float rain = w != null ? w.RainLevel : 0f;
        float thunder = w != null ? w.ThunderLevel : 0f;
        float flash = w != null ? w.LightningFlash : 0f;
        float gloom = 1f - rain * 0.55f - thunder * 0.2f;      // độ sáng còn lại khi mưa / giông

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(angle, -35f, 0f);
            float above = Mathf.Clamp01(SunElevation * 4f);
            sun.intensity = sunIntensity * above * gloom + flash * 1.5f;
            sun.shadows = above > 0.01f ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = 0.6f * (1f - rain * 0.7f);
        }

        Color sky = Color.Lerp(NightSky, DaySky, day);
        float sunset = Mathf.Clamp01(1f - Mathf.Abs(SunElevation) / 0.22f) * (time < 0.5f ? 0.55f : 0.75f);
        sky = Color.Lerp(sky, SunsetSky, sunset * day * (1f - rain));
        Color stormSky = new Color(0.42f, 0.46f, 0.52f) * Mathf.Lerp(0.12f, 1f, day) * (1f - thunder * 0.35f);
        sky = Color.Lerp(sky, stormSky, rain * 0.85f);
        sky = Color.Lerp(sky, new Color(0.85f, 0.88f, 1f), flash * 0.75f);

        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
        RenderSettings.ambientLight = Color.Lerp(NightAmbient, DayAmbient, day) * (1f - rain * 0.35f) + Color.white * flash * 0.6f;
        Shader.SetGlobalFloat("_MCDayLight", Mathf.Lerp(0.22f, 1f, day) * (1f - rain * 0.3f - thunder * 0.15f) + flash * 0.8f);

        // Sương mù dày hơn khi mưa
        var world = World.Instance;
        if (world != null)
        {
            RenderSettings.fogStartDistance = world.BaseFogStart * (1f - rain * 0.6f);
            RenderSettings.fogEndDistance = world.BaseFogEnd * (1f - rain * 0.35f);
        }
    }
}
