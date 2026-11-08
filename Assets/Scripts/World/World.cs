using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

/// Quản lý thế giới: load/unload chunk quanh người chơi, đọc/ghi block, rương/lò nung, rơi vật phẩm.
public class World : MonoBehaviour
{
    [SerializeField] GameAssets assets;
    [SerializeField] Material blockMaterial;
    [SerializeField] Transform player;

    [Header("Generation")]
    [SerializeField] int seed = 12345;
    [SerializeField] bool randomSeed = false;

    [Header("Streaming")]
    [Range(2, 16)] [SerializeField] int viewDistance = 6;   // tính theo chunk
    [SerializeField] int chunksGeneratedPerFrame = 8;
    [SerializeField] int chunksMeshedPerFrame = 4;

    const int S = VoxelData.ChunkSize;
    const int H = VoxelData.ChunkHeight;

    readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    readonly Dictionary<Vector2Int, byte[]> savedChunks = new Dictionary<Vector2Int, byte[]>(); // chunk đã sửa nhưng đã unload
    readonly Dictionary<Vector3Int, BlockEntity> blockEntities = new Dictionary<Vector3Int, BlockEntity>();
    readonly List<FurnaceEntity> furnaces = new List<FurnaceEntity>();

    TerrainGenerator generator;
    Vector2Int[] offsets;          // sắp theo khoảng cách, bán kính viewDistance + 1
    int offsetsRadius = -1;
    Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    public static World Instance { get; private set; }

    /// Chunk vừa được tạo (dữ liệu đã có) / sắp bị huỷ. MobSpawner dùng để sinh & lưu động vật theo vùng.
    public event Action<Vector2Int> ChunkCreated;
    public event Action<Vector2Int> ChunkUnloading;
    public int Seed => seed;
    public int ViewDistance => viewDistance;
    public int LoadedChunkCount => chunks.Count;
    public Transform Player => player;
    public Material BlockMaterial => blockMaterial;
    public TerrainGenerator Generator => generator;
    public float BaseFogStart { get; private set; } = 55f;
    public float BaseFogEnd { get; private set; } = 95f;

    void Awake()
    {
        Instance = this;
        if (assets != null) GameAssets.Instance = assets;
        if (GetComponent<DayNightCycle>() == null) gameObject.AddComponent<DayNightCycle>();
        if (GetComponent<MobSpawner>() == null) gameObject.AddComponent<MobSpawner>();
        if (GetComponent<WeatherSystem>() == null) gameObject.AddComponent<WeatherSystem>();
        if (blockMaterial == null && assets != null) blockMaterial = assets.voxelMaterial;

        // Điện thoại: giảm tầm nhìn cho mượt
        if (Application.isMobilePlatform) viewDistance = Mathf.Min(viewDistance, 4);

        if (randomSeed) seed = Random.Range(int.MinValue, int.MaxValue);
        generator = new TerrainGenerator(seed);
        Shader.SetGlobalFloat("_MCDayLight", 1f);

        if (blockMaterial == null || assets == null)
            Debug.LogError("[World] Thiếu GameAssets / blockMaterial. Chạy menu Minecraft > Setup World Scene.");
    }

    void Start()
    {
        ApplyFog();
    }

    void ApplyFog()
    {
        float range = viewDistance * S;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        BaseFogStart = range * 0.55f;
        BaseFogEnd = range * 0.95f;
        RenderSettings.fogStartDistance = BaseFogStart;
        RenderSettings.fogEndDistance = BaseFogEnd;
        var cam = Camera.main;
        if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, range + 32f);
    }

    void Update()
    {
        TickFurnaces(Time.deltaTime);

        if (player == null || blockMaterial == null) return;

        if (offsetsRadius != viewDistance + 1) BuildOffsets();

        Vector2Int pc = VoxelData.ChunkCoordOf(player.position);
        if (pc != lastPlayerChunk)
        {
            lastPlayerChunk = pc;
            UnloadFarChunks(pc);
        }

        int genBudget = chunksGeneratedPerFrame;
        int meshBudget = chunksMeshedPerFrame;

        foreach (var off in offsets)
        {
            var coord = pc + off;
            if (!chunks.TryGetValue(coord, out var chunk))
            {
                if (genBudget > 0)
                {
                    CreateChunk(coord);
                    genBudget--;
                }
                continue;
            }

            bool inView = Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.y)) <= viewDistance;
            if (inView && chunk.IsDirty && meshBudget > 0 && NeighborsLoaded(coord))
            {
                chunk.BuildMesh();
                meshBudget--;
            }

            if (genBudget <= 0 && meshBudget <= 0) break;
        }
    }

    void BuildOffsets()
    {
        offsetsRadius = viewDistance + 1;
        var list = new List<Vector2Int>();
        for (int z = -offsetsRadius; z <= offsetsRadius; z++)
        for (int x = -offsetsRadius; x <= offsetsRadius; x++)
            list.Add(new Vector2Int(x, z));
        list.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        offsets = list.ToArray();
        ApplyFog();
    }

    void CreateChunk(Vector2Int coord)
    {
        var go = new GameObject($"Chunk {coord.x},{coord.y}");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(coord.x * S, 0, coord.y * S);

        byte[] data;
        bool restored = savedChunks.TryGetValue(coord, out data);
        if (!restored) data = generator.Generate(coord);

        var chunk = go.AddComponent<Chunk>();
        chunk.Init(this, coord, data, blockMaterial);
        chunk.IsModified = restored;
        chunks.Add(coord, chunk);
        ChunkCreated?.Invoke(coord);
    }

    bool NeighborsLoaded(Vector2Int c)
    {
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            if (!chunks.ContainsKey(new Vector2Int(c.x + dx, c.y + dz))) return false;
        }
        return true;
    }

    readonly List<Vector2Int> toRemove = new List<Vector2Int>();

    void UnloadFarChunks(Vector2Int pc)
    {
        int limit = viewDistance + 2;
        toRemove.Clear();
        foreach (var kv in chunks)
        {
            var d = kv.Key - pc;
            if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) > limit) toRemove.Add(kv.Key);
        }
        foreach (var coord in toRemove)
        {
            ChunkUnloading?.Invoke(coord);
            var chunk = chunks[coord];
            if (chunk.IsModified) savedChunks[coord] = chunk.Blocks;
            Destroy(chunk.gameObject);
            chunks.Remove(coord);
        }
    }

    // ---------------------------------------------------------------- blocks

    public BlockId GetBlock(int x, int y, int z)
    {
        if (y < 0) return BlockId.Bedrock;
        if (y >= H) return BlockId.Air;
        if (!chunks.TryGetValue(new Vector2Int(x >> 4, z >> 4), out var chunk)) return BlockId.Air;
        return chunk.GetLocal(x & 15, y, z & 15);
    }

    public BlockId GetBlock(Vector3Int p) => GetBlock(p.x, p.y, p.z);

    public bool IsSolidAt(int x, int y, int z) => BlockDatabase.IsSolid(GetBlock(x, y, z));

    /// rebuildNow = false: chỉ đánh dấu chunk cần dựng lại (Update sẽ dựng) - dùng khi sửa nhiều block một lúc.
    public bool SetBlock(Vector3Int p, BlockId id, bool rebuildNow = true)
    {
        if (p.y < 0 || p.y >= H) return false;
        var coord = new Vector2Int(p.x >> 4, p.z >> 4);
        if (!chunks.TryGetValue(coord, out var chunk)) return false;

        int lx = p.x & 15, lz = p.z & 15;
        chunk.SetLocal(lx, p.y, lz, id);
        if (rebuildNow) chunk.BuildMesh();

        // Block ở rìa chunk -> dựng lại chunk bên cạnh (kể cả chéo, vì AO)
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            if (dx == -1 && lx != 0) continue;
            if (dx == 1 && lx != S - 1) continue;
            if (dz == -1 && lz != 0) continue;
            if (dz == 1 && lz != S - 1) continue;
            if (chunks.TryGetValue(new Vector2Int(coord.x + dx, coord.y + dz), out var n) && n.HasMesh)
            {
                if (rebuildNow) n.BuildMesh();
                else n.IsDirty = true;
            }
        }
        return true;
    }

    /// Phá block trong survival: rơi đồ (nếu đào đúng công cụ) + đồ trong rương/lò.
    public void BreakBlock(Vector3Int p, ItemDef tool, bool dropItems = true, bool rebuildNow = true)
    {
        BlockId id = GetBlock(p);
        if (id == BlockId.Air) return;

        if (blockEntities.TryGetValue(p, out var be))
        {
            foreach (var inv in be.Inventories)
                for (int i = 0; i < inv.Size; i++)
                    if (!inv[i].IsEmpty) DropItem(p + Vector3.one * 0.5f, inv[i]);
            blockEntities.Remove(p);
            if (be is FurnaceEntity f) furnaces.Remove(f);
        }

        SetBlock(p, BlockId.Air, rebuildNow);

        if (!dropItems || !BlockDatabase.CanHarvest(id, tool)) return;
        var def = BlockDatabase.Get(id);
        if (def.noDrop || Random.value > def.dropChance) return;

        ItemId item = def.drop != ItemId.None ? def.drop : ItemDatabase.FromBlock(id);
        if (def.altDrop != ItemId.None && Random.value < def.altDropChance) item = def.altDrop;
        int count = Random.Range(def.dropMin, def.dropMax + 1);
        if (count > 0) DropItem(p + Vector3.one * 0.5f, new ItemStack(item, count));
    }

    public DroppedItem DropItem(Vector3 position, ItemStack stack, Vector3? velocity = null, float pickupDelay = 0.5f)
    {
        if (stack.IsEmpty) return null;
        Vector3 v = velocity ?? new Vector3(Random.Range(-1.5f, 1.5f), 3f, Random.Range(-1.5f, 1.5f));
        return DroppedItem.Spawn(this, position, stack, v, pickupDelay);
    }

    /// Vụ nổ: phá block hình cầu (đá gốc không phá được), gây sát thương + hất văng người chơi và mob.
    public void Explode(Vector3 center, float radius, Object source)
    {
        int r = Mathf.CeilToInt(radius);
        var c = Vector3Int.FloorToInt(center);
        for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r; y++)
        for (int z = -r; z <= r; z++)
        {
            var p = new Vector3Int(c.x + x, c.y + y, c.z + z);
            float d = Vector3.Distance(p + Vector3.one * 0.5f, center);
            if (d > radius - Random.Range(0f, 0.8f)) continue;
            BlockId id = GetBlock(p);
            if (id == BlockId.Air || id == BlockId.Bedrock) continue;
            BreakBlock(p, null, Random.value < 0.3f, false);
        }

        float reach = radius * 2f;

        var stats = PlayerStats.Instance;
        if (stats != null && !PlayerStats.IsDead)
        {
            Vector3 pc = stats.transform.position + Vector3.up * 0.9f;
            float d = Vector3.Distance(pc, center);
            if (d < reach)
            {
                float impact = 1f - d / reach;
                float dmg = ((impact * impact + impact) / 2f * 7f * reach + 1f) * 0.6f;
                stats.Damage(dmg, center, source as Mob, false, "nổ tung");
                var pcl = stats.GetComponent<PlayerController>();
                if (pcl != null) pcl.AddKnockback((pc - center).normalized * impact * 14f + Vector3.up * impact * 8f);
            }
        }

        foreach (var m in Mob.All.ToArray())
        {
            if ((Object)m == source || m.IsDying) continue;
            Vector3 mc = m.Bounds.center;
            float d = Vector3.Distance(mc, center);
            if (d >= reach) continue;
            float impact = 1f - d / reach;
            m.Damage(((impact * impact + impact) / 2f * 7f * reach + 1f) * 0.6f, center, source, false);
            m.Push((mc - center).normalized * impact * 12f + Vector3.up * impact * 6f);
        }

        ExplosionFlash.Spawn(center, radius);
    }

    public bool IsChunkReadyAt(Vector3 worldPos) =>
        chunks.TryGetValue(VoxelData.ChunkCoordOf(worldPos), out var c) && c.HasMesh;

    public bool IsChunkLoadedAt(Vector3 worldPos) => chunks.ContainsKey(VoxelData.ChunkCoordOf(worldPos));

    /// Y của block đặc cao nhất tại cột (x, z). Chunk phải đã load.
    public int GetTopSolidY(int x, int z)
    {
        for (int y = H - 1; y >= 0; y--)
            if (BlockDatabase.IsSolid(GetBlock(x, y, z))) return y;
        return 0;
    }

    /// Có nhìn thấy bầu trời từ vị trí này không (dùng cho zombie cháy nắng).
    public bool CanSeeSky(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x), z = Mathf.FloorToInt(pos.z);
        for (int y = Mathf.FloorToInt(pos.y) + 2; y < H; y++)
            if (BlockDatabase.IsOpaque(GetBlock(x, y, z))) return false;
        return true;
    }

    // ---------------------------------------------------------------- block entities

    public ChestEntity GetChest(Vector3Int p)
    {
        if (blockEntities.TryGetValue(p, out var be) && be is ChestEntity c) return c;
        var chest = new ChestEntity { position = p };
        blockEntities[p] = chest;
        return chest;
    }

    public FurnaceEntity GetFurnace(Vector3Int p)
    {
        if (blockEntities.TryGetValue(p, out var be) && be is FurnaceEntity f) return f;
        var furnace = new FurnaceEntity { position = p };
        blockEntities[p] = furnace;
        furnaces.Add(furnace);
        return furnace;
    }

    void TickFurnaces(float dt)
    {
        for (int i = 0; i < furnaces.Count; i++)
        {
            var f = furnaces[i];
            if (!f.Tick(dt)) continue;
            BlockId current = GetBlock(f.position);
            if (current == BlockId.Furnace || current == BlockId.FurnaceLit)
                SetBlock(f.position, f.IsBurning ? BlockId.FurnaceLit : BlockId.Furnace);
        }
    }
}
