using UnityEngine;

/// Sinh địa hình theo seed: đồi, núi, bãi cát, quặng, cây sồi.
/// Hoàn toàn tất định (deterministic) theo toạ độ nên chunk nào sinh trước/sau cũng khớp nhau.
public class TerrainGenerator
{
    public const int SeaLevel = 48;

    const int H = VoxelData.ChunkHeight;
    const int S = VoxelData.ChunkSize;

    readonly int seed;
    readonly float baseOffX, baseOffZ, hillOffX, hillOffZ, mountOffX, mountOffZ;

    public TerrainGenerator(int seed)
    {
        this.seed = seed;
        var rng = new System.Random(seed);
        baseOffX = rng.Next(-20000, 20000);
        baseOffZ = rng.Next(-20000, 20000);
        hillOffX = rng.Next(-20000, 20000);
        hillOffZ = rng.Next(-20000, 20000);
        mountOffX = rng.Next(-20000, 20000);
        mountOffZ = rng.Next(-20000, 20000);
    }

    // ---------------------------------------------------------------- height

    public int GetHeight(int x, int z)
    {
        float baseN = Fbm((x + baseOffX) * 0.008f, (z + baseOffZ) * 0.008f, 4);
        float hills = Fbm((x + hillOffX) * 0.025f, (z + hillOffZ) * 0.025f, 3);
        float mount = Mathf.PerlinNoise((x + mountOffX) * 0.003f, (z + mountOffZ) * 0.003f);
        float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, mount));

        float h = 52f + (baseN - 0.5f) * 20f + (hills - 0.5f) * 6f + m * baseN * 55f;
        return Mathf.Clamp((int)h, 5, H - 12);
    }

    static float Fbm(float x, float z, int octaves)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, z * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    // ---------------------------------------------------------------- chunk

    public byte[] Generate(Vector2Int coord)
    {
        var blocks = new byte[VoxelData.ChunkVolume];
        int ox = coord.x * S;
        int oz = coord.y * S;

        for (int lz = 0; lz < S; lz++)
        for (int lx = 0; lx < S; lx++)
        {
            int wx = ox + lx, wz = oz + lz;
            int h = GetHeight(wx, wz);
            bool beach = h <= SeaLevel + 1;
            bool peak = h > 100;

            for (int y = 0; y <= h; y++)
            {
                BlockId id;
                if (y == 0) id = BlockId.Bedrock;
                else if (y <= 3 && Hash01(wx, y, wz, seed) < (4 - y) * 0.25f) id = BlockId.Bedrock;
                else if (y < h - 3) id = Ore(wx, y, wz);
                else if (y < h) id = beach ? BlockId.Sand : (peak ? BlockId.Stone : BlockId.Dirt);
                else id = beach ? BlockId.Sand : (peak ? BlockId.Stone : BlockId.Grass);

                blocks[VoxelData.Index(lx, y, lz)] = (byte)id;
            }
        }

        // Cây: duyệt rộng thêm 2 ô để lá của cây ở chunk bên cạnh lấn sang vẫn được vẽ.
        for (int wz = oz - 2; wz < oz + S + 2; wz++)
        for (int wx = ox - 2; wx < ox + S + 2; wx++)
        {
            if (Hash01(wx, 0, wz, seed + 7) >= 0.012f) continue;
            int h = GetHeight(wx, wz);
            if (h <= SeaLevel + 1 || h > 100) continue;
            PlaceTree(blocks, ox, oz, wx, h + 1, wz);
        }

        return blocks;
    }

    BlockId Ore(int x, int y, int z)
    {
        float r = Hash01(x, y, z, seed + 3);
        if (y < 16 && r < 0.0015f) return BlockId.DiamondOre;
        if (y < 32 && r < 0.003f) return BlockId.GoldOre;
        if (y < 64 && r < 0.008f) return BlockId.IronOre;
        if (r < 0.014f) return BlockId.CoalOre;
        if (r > 0.985f) return BlockId.Gravel;
        return BlockId.Stone;
    }

    void PlaceTree(byte[] blocks, int ox, int oz, int wx, int y0, int wz)
    {
        int trunk = 4 + (int)(Hash01(wx, 1, wz, seed + 11) * 3f); // 4..6
        int top = y0 + trunk - 1;

        // Tán lá
        for (int ly = top - 2; ly <= top + 1; ly++)
        {
            int r = ly <= top - 1 ? 2 : 1;
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                bool corner = Mathf.Abs(dx) == r && Mathf.Abs(dz) == r;
                if (corner && r == 2 && Hash01(wx + dx, ly, wz + dz, seed + 13) < 0.5f) continue;
                if (corner && r == 1 && ly == top + 1) continue;
                SetIn(blocks, ox, oz, wx + dx, ly, wz + dz, BlockId.OakLeaves, onlyIfAir: true);
            }
        }

        // Thân
        for (int y = y0; y <= top; y++)
            SetIn(blocks, ox, oz, wx, y, wz, BlockId.OakLog, onlyIfAir: false);

        SetIn(blocks, ox, oz, wx, y0 - 1, wz, BlockId.Dirt, onlyIfAir: false);
    }

    static void SetIn(byte[] blocks, int ox, int oz, int wx, int y, int wz, BlockId id, bool onlyIfAir)
    {
        int lx = wx - ox, lz = wz - oz;
        if (lx < 0 || lx >= S || lz < 0 || lz >= S || y < 0 || y >= H) return;
        int i = VoxelData.Index(lx, y, lz);
        if (onlyIfAir && blocks[i] != (byte)BlockId.Air) return;
        blocks[i] = (byte)id;
    }

    // ---------------------------------------------------------------- hash

    public static uint Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B9u;
            h ^= (uint)x * 0x8DA6B343u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xD8163841u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)z * 0xCB1AB31Fu;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }

    public static float Hash01(int x, int y, int z, int seed) => (Hash(x, y, z, seed) >> 8) / 16777216f;
}
