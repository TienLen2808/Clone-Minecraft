using UnityEngine;
using UnityEngine.InputSystem;

public enum WeatherType
{
    Clear,
    Rain,
    Thunder,
}

/// Thời tiết kiểu Minecraft: nắng -> mưa -> (đôi khi) giông bão, luân phiên ngẫu nhiên.
///  - Mưa: hạt mưa rơi quanh camera, dừng lại khi chạm mái/đất (không mưa trong nhà, trong hang).
///    Trên núi cao (y > 95) mưa thành tuyết.
///  - Trời tối lại, sương mù dày hơn, tiếng mưa (tổng hợp bằng code, nhỏ lại khi đứng dưới mái).
///  - Giông: sét đánh loé sáng cả bầu trời + tiếng sấm, quái có thể xuất hiện cả ban ngày.
///  - Zombie / bộ xương không bị cháy khi trời mưa.
/// F6 để đổi thời tiết (thử nghiệm).
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [SerializeField] Vector2 clearDuration = new Vector2(240f, 600f);   // giây
    [SerializeField] Vector2 rainDuration = new Vector2(90f, 240f);
    [Range(0f, 1f)] [SerializeField] float thunderChance = 0.3f;
    [SerializeField] float snowAltitude = 95f;

    public WeatherType Current { get; private set; } = WeatherType.Clear;
    public float RainLevel { get; private set; }         // 0..1 (mượt dần)
    public float ThunderLevel { get; private set; }      // 0..1
    public float LightningFlash { get; private set; }    // 0..1 loé sáng
    public bool IsRaining => RainLevel > 0.3f;
    public bool IsThundering => ThunderLevel > 0.5f;

    float timer;
    float nextLightning;
    ParticleSystem rain;
    ParticleSystemRenderer rainRenderer;
    bool snowMode;
    AudioSource rainAudio;
    AudioLowPassFilter rainFilter;
    AudioSource thunderAudio;
    AudioClip[] thunderClips;
    Material rainMat, boltMat;
    LineRenderer bolt;
    float boltTimer;
    float pendingThunderDelay = -1f, pendingThunderVolume;

    public string DisplayName
    {
        get
        {
            if (snowMode && RainLevel > 0.3f) return "Tuyết";
            switch (Current)
            {
                case WeatherType.Rain: return "Mưa";
                case WeatherType.Thunder: return "Giông bão";
                default: return "Nắng";
            }
        }
    }

    void Awake()
    {
        Instance = this;
        timer = Random.Range(clearDuration.x * 0.5f, clearDuration.y);
    }

    void Start()
    {
        CreateRain();
        CreateAudio();
        CreateBolt();
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.deltaTime;

        var kb = Keyboard.current;
        if (kb != null && kb.f6Key.wasPressedThisFrame)
        {
            SetWeather((WeatherType)(((int)Current + 1) % 3));
            GameHUD.Toast("Thời tiết: " + DisplayName);
        }

        timer -= dt;
        if (timer <= 0f)
        {
            if (Current == WeatherType.Clear)
                SetWeather(Random.value < thunderChance ? WeatherType.Thunder : WeatherType.Rain);
            else
                SetWeather(WeatherType.Clear);
        }

        float targetRain = Current == WeatherType.Clear ? 0f : 1f;
        float targetThunder = Current == WeatherType.Thunder ? 1f : 0f;
        RainLevel = Mathf.MoveTowards(RainLevel, targetRain, dt / 8f);        // chuyển dần trong ~8 giây
        ThunderLevel = Mathf.MoveTowards(ThunderLevel, targetThunder, dt / 8f);

        UpdateLightning(dt);
        UpdateRainVisual();
        UpdateAudio(dt);
    }

    public void SetWeather(WeatherType type)
    {
        Current = type;
        Vector2 d = type == WeatherType.Clear ? clearDuration : rainDuration;
        timer = Random.Range(d.x, d.y);
        nextLightning = Random.Range(3f, 8f);
    }

    // ---------------------------------------------------------------- rain particles

    void CreateRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        rain = go.AddComponent<ParticleSystem>();
        rainRenderer = go.GetComponent<ParticleSystemRenderer>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = rain.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = Application.isMobilePlatform ? 1500 : 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(44f, 44f, 1f);
        shape.rotation = new Vector3(90f, 0f, 0f);    // phát hạt hướng xuống dưới

        var col = rain.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.lifetimeLoss = 1f;        // chạm là biến mất
        col.bounce = 0f;
        col.radiusScale = 0.5f;
        col.quality = Application.isMobilePlatform ? ParticleSystemCollisionQuality.Low : ParticleSystemCollisionQuality.Medium;

        rainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rainRenderer.receiveShadows = false;

        var template = GameAssets.Instance != null ? GameAssets.Instance.rainMaterial : null;
        if (template != null)
        {
            rainMat = new Material(template);
            var tex = MakeStreakTexture();
            rainMat.SetTexture("_BaseMap", tex);
            rainMat.mainTexture = tex;
            rainRenderer.sharedMaterial = rainMat;
        }

        ApplyRainMode(false);
        rain.Play();
    }

    void ApplyRainMode(bool snow)
    {
        snowMode = snow;
        var main = rain.main;
        if (snow)
        {
            main.startLifetime = 7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            rainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        else
        {
            main.startLifetime = 1.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.06f);
            main.startColor = new Color(0.62f, 0.7f, 0.85f, 0.55f);
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.velocityScale = 0.05f;
            rainRenderer.lengthScale = 2f;
        }
    }

    void UpdateRainVisual()
    {
        var cam = Camera.main;
        if (rain == null || cam == null) return;

        Vector3 cp = cam.transform.position;
        bool snow = cp.y > snowAltitude;
        if (snow != snowMode) ApplyRainMode(snow);

        // Đi theo camera, hơi lệch về phía trước để phủ tầm nhìn
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        rain.transform.position = cp + Vector3.up * 14f + fwd.normalized * 6f;

        // Trong hang sâu thì tắt mưa (hạt mưa không được xuyên qua trần đá)
        bool deepUnderground = false;
        var world = World.Instance;
        if (world != null && !world.CanSeeSky(cp))
        {
            int top = world.GetTopSolidY(Mathf.FloorToInt(cp.x), Mathf.FloorToInt(cp.z));
            deepUnderground = top - cp.y > 4f;
        }

        float maxRate = (Application.isMobilePlatform ? 900f : 3000f) * (snow ? 0.35f : 1f);
        var em = rain.emission;
        em.rateOverTime = deepUnderground ? 0f : RainLevel * maxRate;
    }

    static Texture2D MakeStreakTexture()
    {
        var tex = new Texture2D(8, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 8; x++)
        {
            float fx = 1f - Mathf.Abs((x + 0.5f) / 8f * 2f - 1f);
            float fy = Mathf.Sin((y + 0.5f) / 32f * Mathf.PI);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(fx * fx * fy * 1.4f)));
        }
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- lightning

    void CreateBolt()
    {
        var go = new GameObject("Lightning");
        go.transform.SetParent(transform, false);
        bolt = go.AddComponent<LineRenderer>();
        bolt.positionCount = 14;
        bolt.useWorldSpace = true;
        bolt.widthMultiplier = 0.7f;
        bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.receiveShadows = false;

        var template = GameAssets.Instance != null ? GameAssets.Instance.crackMaterial : null;
        if (template != null)
        {
            boltMat = new Material(template);
            boltMat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            boltMat.mainTexture = Texture2D.whiteTexture;
            boltMat.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f, 0.95f));
            bolt.sharedMaterial = boltMat;
        }
        bolt.enabled = false;
    }

    void UpdateLightning(float dt)
    {
        LightningFlash = Mathf.Max(0f, LightningFlash - dt * 3.5f);

        boltTimer -= dt;
        if (bolt != null && bolt.enabled && boltTimer <= 0f) bolt.enabled = false;

        if (pendingThunderDelay >= 0f)
        {
            pendingThunderDelay -= dt;
            if (pendingThunderDelay < 0f && thunderAudio != null && thunderClips != null)
                thunderAudio.PlayOneShot(thunderClips[Random.Range(0, thunderClips.Length)], pendingThunderVolume);
        }

        if (ThunderLevel < 0.6f) return;
        nextLightning -= dt;
        if (nextLightning > 0f) return;
        nextLightning = Random.Range(5f, 16f);
        Strike();
    }

    void Strike()
    {
        var world = World.Instance;
        var cam = Camera.main;
        if (world == null || cam == null) return;

        Vector3 cp = cam.transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(20f, 90f);
        int x = Mathf.FloorToInt(cp.x + Mathf.Cos(angle) * dist);
        int z = Mathf.FloorToInt(cp.z + Mathf.Sin(angle) * dist);
        float groundY = world.IsChunkLoadedAt(new Vector3(x, 0f, z)) ? world.GetTopSolidY(x, z) + 1f : cp.y - 10f;

        LightningFlash = 1f;

        // Tia sét gấp khúc
        if (bolt != null)
        {
            int n = bolt.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 pt = new Vector3(x + 0.5f, Mathf.Lerp(groundY + 90f, groundY, t), z + 0.5f);
                if (i > 0 && i < n - 1) pt += new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
                bolt.SetPosition(i, pt);
            }
            bolt.enabled = true;
            boltTimer = 0.25f;
        }

        // Sấm đến sau theo khoảng cách
        pendingThunderDelay = dist / 120f;
        pendingThunderVolume = Mathf.Lerp(1f, 0.45f, dist / 90f);
    }

    // ---------------------------------------------------------------- audio (tổng hợp bằng code)

    void CreateAudio()
    {
        var cam = Camera.main;
        var host = cam != null ? cam.gameObject : gameObject;

        rainAudio = host.AddComponent<AudioSource>();
        rainAudio.clip = MakeRainClip();
        rainAudio.loop = true;
        rainAudio.spatialBlend = 0f;
        rainAudio.volume = 0f;
        rainAudio.playOnAwake = false;
        rainAudio.Play();
        rainFilter = host.AddComponent<AudioLowPassFilter>();
        rainFilter.cutoffFrequency = 22000f;

        thunderAudio = host.AddComponent<AudioSource>();
        thunderAudio.spatialBlend = 0f;
        thunderAudio.playOnAwake = false;
        thunderClips = new[] { MakeThunderClip(1), MakeThunderClip(2) };
    }

    void UpdateAudio(float dt)
    {
        if (rainAudio == null) return;
        var world = World.Instance;
        var cam = Camera.main;
        bool covered = world != null && cam != null && !world.CanSeeSky(cam.transform.position);

        float target = (snowMode ? 0f : RainLevel) * (covered ? 0.18f : 0.32f) * (1f + ThunderLevel * 0.3f);
        rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, target, dt * 0.2f);
        rainFilter.cutoffFrequency = Mathf.Lerp(rainFilter.cutoffFrequency, covered ? 900f : 9000f, dt * 3f);
    }

    /// Tiếng mưa: nhiễu trắng lọc mềm + tiếng hạt mưa lách tách, vòng lặp 4 giây liền mạch.
    static AudioClip MakeRainClip()
    {
        const int rate = 22050;
        int len = rate * 4;
        var data = new float[len];
        var rng = new System.Random(1234);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.35f;       // lọc thông thấp
            lp2 += (lp - lp2) * 0.6f;
            float v = lp2 * 0.6f;
            if (rng.NextDouble() < 0.0025) v += (float)(rng.NextDouble() * 0.6 - 0.3);   // giọt lớn
            data[i] = v;
        }
        // Nối mượt đầu - cuối
        int fade = rate / 4;
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            data[i] = data[i] * t + data[len - fade + i] * (1f - t);
        }
        var clip = AudioClip.Create("Rain", len - fade, 1, rate, false);
        var trimmed = new float[len - fade];
        System.Array.Copy(data, trimmed, len - fade);
        clip.SetData(trimmed, 0);
        return clip;
    }

    /// Tiếng sấm: tiếng nổ "rắc" ngắn rồi tiếng ì ầm trầm dần.
    static AudioClip MakeThunderClip(int seed)
    {
        const int rate = 22050;
        int len = rate * 6;
        var data = new float[len];
        var rng = new System.Random(seed * 999);
        float brown = 0f, lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            brown = Mathf.Clamp(brown + white * 0.08f, -1f, 1f) * 0.995f;
            lp += (brown - lp) * 0.08f;

            float crack = t < 0.25f ? white * Mathf.Exp(-t * 18f) * 0.8f : 0f;
            float rumbleEnv = Mathf.Clamp01(t * 6f) * Mathf.Exp(-t * 0.7f);
            float wobble = 0.7f + 0.3f * Mathf.Sin(t * 5.3f + seed) * Mathf.Sin(t * 2.1f);
            data[i] = Mathf.Clamp(crack + lp * 3.2f * rumbleEnv * wobble, -1f, 1f);
        }
        var clip = AudioClip.Create("Thunder" + seed, len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (rainMat != null) Destroy(rainMat);
        if (boltMat != null) Destroy(boltMat);
    }
}
