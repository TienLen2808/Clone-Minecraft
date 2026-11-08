using System.Collections.Generic;
using UnityEngine;

/// Sinh mob kiểu Minecraft:
///  - Động vật / thú (heo, bò, cừu, gà, sói, gấu) sinh MỘT LẦN khi chunk được tạo lần đầu, theo đàn.
///    Loại con vật đổi theo từng vùng (~64x64 block) nên đi khắp bản đồ sẽ gặp các đàn khác nhau.
///    Khi chunk bị unload, các con vật trong đó được lưu lại và trả về khi quay lại.
///  - Quái (zombie, bộ xương, nhện, creeper) sinh rải rác vào ban đêm ở các chunk xung quanh, xa người chơi.
public class MobSpawner : MonoBehaviour
{
    [SerializeField] World world;

    [Header("Động vật (theo chunk)")]
    [Range(0f, 1f)] [SerializeField] float herdChancePerChunk = 0.08f;
    [Range(0f, 1f)] [SerializeField] float wolfChancePerChunk = 0.015f;
    [Range(0f, 1f)] [SerializeField] float bearChancePerChunk = 0.01f;

    [Header("Quái ban đêm")]
    [SerializeField] int maxMonsters = 12;
    [SerializeField] float monsterSpawnInterval = 2f;

    struct SavedMob
    {
        public MobType type;
        public Vector3 position;
        public float yaw;
    }

    readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    readonly Dictionary<Vector2Int, List<SavedMob>> saved = new Dictionary<Vector2Int, List<SavedMob>>();
    readonly List<Vector2Int> pending = new List<Vector2Int>();   // chunk mới tạo, chờ đủ hàng xóm để tìm chỗ đứng
    float monsterTimer;
    bool subscribed;

    void OnEnable() => Subscribe();

    void OnDisable()
    {
        if (world != null && subscribed)
        {
            world.ChunkCreated -= OnChunkCreated;
            world.ChunkUnloading -= OnChunkUnloading;
        }
        subscribed = false;
    }

    void Subscribe()
    {
        if (subscribed) return;
        if (world == null) world = World.Instance != null ? World.Instance : GetComponent<World>();
        if (world == null) return;
        world.ChunkCreated += OnChunkCreated;
        world.ChunkUnloading += OnChunkUnloading;
        subscribed = true;
    }

    void Update()
    {
        Subscribe();
        if (world == null) return;

        ProcessPending();

        if (world.Player == null) return;
        var controller = world.Player.GetComponent<PlayerController>();
        if (controller != null && !controller.IsSpawned) return;

        monsterTimer -= Time.deltaTime;
        if (monsterTimer <= 0f)
        {
            monsterTimer = monsterSpawnInterval;
            TrySpawnMonster();
        }
    }

    // ---------------------------------------------------------------- chunk events

    void OnChunkCreated(Vector2Int coord) => pending.Add(coord);

    void ProcessPending()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var coord = pending[i];
            var center = new Vector3(coord.x * 16 + 8, 0f, coord.y * 16 + 8);
            if (!world.IsChunkLoadedAt(center)) { pending.RemoveAt(i); continue; }
            if (!world.IsChunkReadyAt(center)) continue;     // đợi chunk có mesh (đủ hàng xóm)
            pending.RemoveAt(i);

            if (saved.TryGetValue(coord, out var list))
            {
                foreach (var m in list)
                {
                    var mob = Spawn(m.type, m.position);
                    mob.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                }
                saved.Remove(coord);
            }
            else if (visited.Add(coord))
            {
                PopulateNewChunk(coord);
            }
        }
    }

    void OnChunkUnloading(Vector2Int coord)
    {
        pending.Remove(coord);
        List<SavedMob> list = null;
        foreach (var m in Mob.All.ToArray())
        {
            if (m.IsDying || m.Persistent) continue;
            if (VoxelData.ChunkCoordOf(m.Position) != coord) continue;
            if (!m.IsHostile)
            {
                if (list == null) list = new List<SavedMob>();
                list.Add(new SavedMob { type = m.Type, position = m.Position, yaw = m.transform.eulerAngles.y });
            }
            Destroy(m.gameObject);
        }
        if (list != null) saved[coord] = list;
    }

    // ---------------------------------------------------------------- animals

    void PopulateNewChunk(Vector2Int coord)
    {
        float r = Random.value;

        if (r < bearChancePerChunk)
        {
            if (FindSpotInChunk(coord, false, out var pos)) Spawn(MobType.PolarBear, pos);
            return;
        }
        r -= bearChancePerChunk;

        if (r < wolfChancePerChunk)
        {
            if (FindSpotInChunk(coord, true, out var pos))
            {
                int n = Random.Range(2, 5);
                for (int i = 0; i < n; i++) SpawnNear(MobType.Wolf, pos);
            }
            return;
        }
        r -= wolfChancePerChunk;

        if (r < herdChancePerChunk)
        {
            if (!FindSpotInChunk(coord, true, out var pos)) return;
            MobType type = RegionAnimal(coord);
            int n = type == MobType.Chicken ? Random.Range(3, 6) : Random.Range(2, 5);
            for (int i = 0; i < n; i++) SpawnNear(type, pos);
        }
    }

    /// Mỗi vùng 4x4 chunk có loài "chính" (70%), còn lại ngẫu nhiên -> đàn rải khắp nơi, mỗi nơi một kiểu.
    MobType RegionAnimal(Vector2Int coord)
    {
        MobType[] kinds = { MobType.Pig, MobType.Cow, MobType.Sheep, MobType.Chicken };
        float h = TerrainGenerator.Hash01(coord.x >> 2, 77, coord.y >> 2, world.Seed);
        MobType main = kinds[Mathf.Min(3, (int)(h * 4f))];
        return Random.value < 0.7f ? main : kinds[Random.Range(0, 4)];
    }

    bool FindSpotInChunk(Vector2Int coord, bool needGrass, out Vector3 pos)
    {
        for (int i = 0; i < 8; i++)
        {
            int x = coord.x * 16 + Random.Range(1, 15);
            int z = coord.y * 16 + Random.Range(1, 15);
            if (TryGround(x, z, needGrass, out pos)) return true;
        }
        pos = default;
        return false;
    }

    void SpawnNear(MobType type, Vector3 around)
    {
        for (int i = 0; i < 8; i++)
        {
            int x = Mathf.FloorToInt(around.x) + Random.Range(-3, 4);
            int z = Mathf.FloorToInt(around.z) + Random.Range(-3, 4);
            if (!world.IsChunkReadyAt(new Vector3(x + 0.5f, 0f, z + 0.5f))) continue;
            if (TryGround(x, z, type != MobType.PolarBear, out var pos))
            {
                Spawn(type, pos);
                return;
            }
        }
    }

    bool TryGround(int x, int z, bool needGrass, out Vector3 pos)
    {
        pos = default;
        int y = world.GetTopSolidY(x, z);
        BlockId ground = world.GetBlock(x, y, z);
        if (needGrass && ground != BlockId.Grass) return false;
        if (ground == BlockId.OakLeaves || ground == BlockId.Glass) return false;
        if (world.GetBlock(x, y + 1, z) != BlockId.Air || world.GetBlock(x, y + 2, z) != BlockId.Air) return false;
        pos = new Vector3(x + 0.5f, y + 1, z + 0.5f);
        return true;
    }

    // ---------------------------------------------------------------- monsters

    void TrySpawnMonster()
    {
        var cycle = DayNightCycle.Instance;
        bool storm = WeatherSystem.Instance != null && WeatherSystem.Instance.IsThundering;   // giông: tối như đêm
        if (cycle == null || (!cycle.IsNight && !storm)) return;

        Vector3 pp = world.Player.position;
        int monsters = 0;
        foreach (var m in Mob.All)
            if (m.IsHostile && (m.Position - pp).sqrMagnitude < 96f * 96f) monsters++;
        if (monsters >= maxMonsters) return;

        // Chọn điểm ngẫu nhiên 24..56 block quanh người chơi
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float d = Random.Range(24f, 56f);
            int x = Mathf.FloorToInt(pp.x + Mathf.Cos(a) * d);
            int z = Mathf.FloorToInt(pp.z + Mathf.Sin(a) * d);
            if (!world.IsChunkReadyAt(new Vector3(x + 0.5f, 0f, z + 0.5f))) continue;
            if (!TryGround(x, z, false, out var pos)) continue;

            float r = Random.value;
            MobType type = r < 0.32f ? MobType.Zombie : r < 0.6f ? MobType.Skeleton : r < 0.82f ? MobType.Spider : MobType.Creeper;
            Spawn(type, pos);
            if (type == MobType.Zombie && Random.value < 0.4f) SpawnNear(MobType.Zombie, pos);
            return;
        }
    }

    // ---------------------------------------------------------------- spawn

    public Mob Spawn(MobType type, Vector3 pos)
    {
        var go = new GameObject(type.ToString());
        go.transform.position = pos;
        Mob mob;
        switch (type)
        {
            case MobType.Zombie: mob = go.AddComponent<Zombie>(); break;
            case MobType.Wolf: mob = go.AddComponent<Wolf>(); break;
            case MobType.Spider: mob = go.AddComponent<Spider>(); break;
            case MobType.Creeper: mob = go.AddComponent<Creeper>(); break;
            case MobType.Skeleton: mob = go.AddComponent<Skeleton>(); break;
            case MobType.PolarBear: mob = go.AddComponent<PolarBear>(); break;
            default: mob = go.AddComponent<Animal>(); break;
        }
        mob.Init(world, type);
        return mob;
    }
}
