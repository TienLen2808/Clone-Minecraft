using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Menu Minecraft:
///  - Setup World Scene: dựng texture, material, GameAssets và tạo scene World.unity đầy đủ (World, Player, mặt trời).
///  - Rebuild Block Textures: dựng lại Texture2DArray + GameAssets (sau khi thêm block / item mới).
[InitializeOnLoad]
public static class MinecraftSetup
{
    /// Tăng số này khi GameAssets cần dữ liệu mới -> Editor tự dựng lại sau khi compile.
    const int DataVersion = 3;

    static MinecraftSetup()
    {
        EditorApplication.delayCall += AutoRefresh;
    }

    static void AutoRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var existing = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
        if (existing == null || existing.dataVersion >= DataVersion) return;
        if (BuildGameAssets() != null)
            Debug.Log("[Minecraft] Đã tự cập nhật GameAssets (texture, icon, mob mới).");
    }

    const string GeneratedFolder = "Assets/Generated";
    const string ArrayPath = GeneratedFolder + "/BlockTextures.asset";
    const string AssetsPath = GeneratedFolder + "/GameAssets.asset";
    const string ScenePath = "Assets/Scenes/World.unity";
    const string Root = "Assets/Textures/Minecraft";
    const int TileSize = 16;

    static readonly Color SkyColor = new Color(0.53f, 0.72f, 1f);

    [MenuItem("Minecraft/Rebuild Block Textures")]
    public static void RebuildBlockTextures()
    {
        var assets = BuildGameAssets();
        if (assets != null) Debug.Log($"[Minecraft] Đã dựng {assets.blockTextures.depth} texture block và {AssetsPath}.");
    }

    [MenuItem("Minecraft/Setup World Scene")]
    public static void SetupWorldScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var assets = BuildGameAssets();
        if (assets == null) return;
        var skinMat = MobMaterial(GeneratedFolder + "/PlayerSkin.mat", true, assets.steveSkin);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Môi trường
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.75f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = SkyColor;
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 95f;

        // ---- Mặt trời
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.6f;
        sunGO.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        RenderSettings.sun = sun;

        // ---- World
        var worldGO = new GameObject("World");
        var world = worldGO.AddComponent<World>();
        var cycle = worldGO.AddComponent<DayNightCycle>();
        var spawner = worldGO.AddComponent<MobSpawner>();

        // ---- Player
        var playerGO = new GameObject("Player");
        playerGO.transform.position = new Vector3(0.5f, 120f, 0.5f);
        var cc = playerGO.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.5f;
        cc.skinWidth = 0.03f;
        cc.slopeLimit = 50f;
        cc.minMoveDistance = 0f;

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(playerGO.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 400f;
        camGO.AddComponent<AudioListener>();

        var modelGO = new GameObject("Model");
        modelGO.transform.SetParent(playerGO.transform, false);
        var model = modelGO.AddComponent<PlayerModel>();

        playerGO.AddComponent<PlayerStats>();
        playerGO.AddComponent<PlayerInventory>();
        var controller = playerGO.AddComponent<PlayerController>();
        var interaction = playerGO.AddComponent<BlockInteraction>();
        playerGO.AddComponent<GameHUD>();
        playerGO.AddComponent<InventoryUI>();
        playerGO.AddComponent<MobileControls>();

        // ---- Gán reference (field private [SerializeField])
        Set(world, "assets", assets);
        Set(world, "blockMaterial", assets.voxelMaterial);
        Set(world, "player", playerGO.transform);
        Set(cycle, "sun", sun);
        Set(spawner, "world", world);
        Set(model, "skin", assets.steveSkin);
        Set(model, "material", skinMat);
        Set(controller, "world", world);
        Set(controller, "cameraTransform", camGO.transform);
        Set(interaction, "world", world);
        Set(interaction, "cameraTransform", camGO.transform);
        Set(interaction, "outlineMaterial", assets.outlineMaterial);

        // ---- Lưu scene + thêm vào Build Profiles
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild(ScenePath);

        Selection.activeGameObject = playerGO;
        Debug.Log("[Minecraft] Đã tạo scene " + ScenePath + ". Bấm Play để vào game!");
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning($"[Minecraft] Không thấy field '{field}' trên {target.GetType().Name}");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- GameAssets

    static GameAssets BuildGameAssets()
    {
        EnsureFolder(GeneratedFolder);

        var array = BuildTextureArray();
        if (array == null) return null;

        var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<GameAssets>();
            AssetDatabase.CreateAsset(assets, AssetsPath);
        }

        assets.blockTextures = array;
        assets.voxelMaterial = VoxelMaterial(array);
        assets.outlineMaterial = UnlitMaterial(GeneratedFolder + "/BlockOutline.mat", Color.black, null, false, false);
        assets.crackMaterial = UnlitMaterial(GeneratedFolder + "/BlockCrack.mat", new Color(0.4f, 0.4f, 0.4f, 0.9f), null, true, false);
        assets.itemSpriteMaterial = UnlitMaterial(GeneratedFolder + "/ItemSprite.mat", Color.white, null, false, true);
        assets.mobCutoutMaterial = MobMaterial(GeneratedFolder + "/MobCutout.mat", true, null);
        assets.mobOpaqueMaterial = MobMaterial(GeneratedFolder + "/MobOpaque.mat", false, null);
        assets.rainMaterial = UnlitMaterial(GeneratedFolder + "/Rain.mat", Color.white, null, true, false,
                                            "Universal Render Pipeline/Particles/Unlit");

        assets.destroyStages = new Texture2D[10];
        for (int i = 0; i < 10; i++) assets.destroyStages[i] = Tex($"environment/destroy_stage_{i}");

        // Icon item: index = (int)ItemId
        assets.itemIcons = new Texture2D[ItemDatabase.MaxId + 1];
        foreach (var def in ItemDatabase.All)
        {
            var icon = Tex(def.iconPath);
            if (icon == null) Debug.LogWarning($"[Minecraft] Thiếu icon {def.iconPath} cho {def.displayName}");
            assets.itemIcons[(int)def.id] = icon;
        }

        assets.steveSkin = Tex("entity/steve");
        assets.pig = Tex("entity/pig/pig");
        assets.cow = Tex("entity/cow/cow");
        assets.sheep = Tex("entity/sheep/sheep");
        assets.chicken = Tex("entity/chicken/chicken");
        assets.zombie = Tex("entity/zombie/zombie");
        assets.wolf = Tex("entity/wolf/wolf");
        assets.wolfTame = Tex("entity/wolf/wolf_tame");
        assets.wolfAngry = Tex("entity/wolf/wolf_angry");
        assets.spider = Tex("entity/spider/spider");
        assets.creeper = Tex("entity/creeper/creeper");
        assets.skeleton = Tex("entity/skeleton/skeleton");
        assets.polarBear = Tex("entity/polarbear");

        assets.heartFull = Tex("ui/heart");
        assets.heartHalf = Tex("ui/heart_half");
        assets.heartEmpty = Tex("ui/heart_background");
        assets.hungerFull = Tex("ui/hunger_full");
        assets.hungerHalf = Tex("ui/hunger_half");
        assets.hungerEmpty = Tex("ui/hunger_background");

        assets.dataVersion = DataVersion;
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        return assets;
    }

    /// Load texture theo đường dẫn tương đối trong Assets/Textures/Minecraft (không đuôi).
    static Texture2D Tex(string relative)
    {
        foreach (var ext in new[] { ".png", ".tga" })
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{relative}{ext}");
            if (t != null) return t;
        }
        return null;
    }

    // ---------------------------------------------------------------- texture array

    static Texture2DArray BuildTextureArray()
    {
        string[] names = BlockDatabase.TextureNames;
        var textures = new List<Texture2D>();

        foreach (var name in names)
        {
            string path = FindBlockTexturePath(name);
            if (path == null)
            {
                EditorUtility.DisplayDialog("Minecraft Setup",
                    $"Không tìm thấy texture '{name}' trong {BlockDatabase.TextureFolder}.", "OK");
                return null;
            }

            // Đảm bảo đọc được pixel
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        var array = new Texture2DArray(TileSize, TileSize, textures.Count, TextureFormat.RGBA32, true, false)
        {
            name = "BlockTextures",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            anisoLevel = 0,
        };

        for (int i = 0; i < textures.Count; i++)
            array.SetPixels(ReadTile(textures[i]), i, 0);
        array.Apply(true, false);

        if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath) != null)
            AssetDatabase.DeleteAsset(ArrayPath);
        AssetDatabase.CreateAsset(array, ArrayPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath);
    }

    /// Lấy ô 16x16: nếu là texture động (dải dọc) thì lấy frame đầu tiên; kích thước khác thì resample nearest.
    static Color[] ReadTile(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        int size = Mathf.Min(w, h);
        Color[] src = tex.GetPixels(0, h - size, size, size);   // frame trên cùng
        if (size == TileSize) return src;

        var dst = new Color[TileSize * TileSize];
        for (int y = 0; y < TileSize; y++)
        for (int x = 0; x < TileSize; x++)
            dst[y * TileSize + x] = src[(y * size / TileSize) * size + (x * size / TileSize)];
        return dst;
    }

    static string FindBlockTexturePath(string name)
    {
        foreach (var ext in new[] { ".png", ".tga" })
        {
            string p = $"{BlockDatabase.TextureFolder}/{name}{ext}";
            if (File.Exists(p)) return p;
        }
        return null;
    }

    // ---------------------------------------------------------------- materials

    static Material LoadOrCreate(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        return mat;
    }

    static Material VoxelMaterial(Texture2DArray array)
    {
        var shader = Shader.Find("Minecraft/VoxelLit");
        if (shader == null)
        {
            Debug.LogError("[Minecraft] Không tìm thấy shader Minecraft/VoxelLit.");
            return null;
        }
        var mat = LoadOrCreate(GeneratedFolder + "/VoxelMaterial.mat", shader);
        mat.SetTexture("_BlockTextures", array);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// URP Unlit: transparent = alpha blend (vết nứt), cutout = alpha clip (sprite item).
    static Material UnlitMaterial(string path, Color color, Texture2D tex, bool transparent, bool cutout,
                                  string shaderName = "Universal Render Pipeline/Unlit")
    {
        var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = LoadOrCreate(path, shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);

        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            mat.SetFloat("_Surface", 0f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (cutout)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// URP Lit cho mob / người chơi.
    static Material MobMaterial(string path, bool cutout, Texture2D tex)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = LoadOrCreate(path, shader);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (tex != null) mat.SetTexture("_BaseMap", tex);

        if (cutout)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.Geometry;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- utils

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void AddSceneToBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
