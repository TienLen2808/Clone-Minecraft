using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Một chunk 16 x 128 x 16: giữ dữ liệu block và tự dựng mesh (ẩn mặt bị che + ambient occlusion).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    const int S = VoxelData.ChunkSize;
    const int H = VoxelData.ChunkHeight;

    public Vector2Int Coord { get; private set; }
    public byte[] Blocks { get; private set; }
    public bool HasMesh { get; private set; }
    public bool IsDirty { get; set; } = true;
    public bool IsModified { get; set; }   // người chơi đã sửa -> cần giữ lại khi unload

    World world;
    Mesh mesh;
    MeshFilter meshFilter;
    MeshCollider meshCollider;
    int worldX, worldZ;

    // Buffer dùng chung cho mọi chunk (chỉ dựng mesh trên main thread)
    static readonly List<Vector3> verts = new List<Vector3>(16384);
    static readonly List<Vector3> normals = new List<Vector3>(16384);
    static readonly List<Vector3> uvs = new List<Vector3>(16384);
    static readonly List<Color32> colors = new List<Color32>(16384);
    static readonly List<int> tris = new List<int>(24576);
    static readonly int[] ao = new int[4];

    public void Init(World owner, Vector2Int coord, byte[] blocks, Material material)
    {
        world = owner;
        Coord = coord;
        Blocks = blocks;
        worldX = coord.x * S;
        worldZ = coord.y * S;

        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;

        mesh = new Mesh { name = $"Chunk {coord.x},{coord.y}", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    // ---------------------------------------------------------------- data

    public BlockId GetLocal(int x, int y, int z) => (BlockId)Blocks[VoxelData.Index(x, y, z)];

    public void SetLocal(int x, int y, int z, BlockId id)
    {
        Blocks[VoxelData.Index(x, y, z)] = (byte)id;
        IsDirty = true;
        IsModified = true;
    }

    /// Lấy block theo toạ độ local, cho phép tràn sang chunk bên cạnh.
    BlockId GetRelative(int x, int y, int z)
    {
        if (y < 0) return BlockId.Bedrock;      // coi đáy thế giới là đặc -> không vẽ mặt dưới
        if (y >= H) return BlockId.Air;
        if (x >= 0 && x < S && z >= 0 && z < S) return (BlockId)Blocks[VoxelData.Index(x, y, z)];
        return world.GetBlock(worldX + x, y, worldZ + z);
    }

    bool IsOpaqueAt(int x, int y, int z) => BlockDatabase.IsOpaque(GetRelative(x, y, z));

    static bool ShouldDrawFace(BlockId self, BlockId neighbor)
    {
        if (neighbor == BlockId.Air) return true;
        var n = BlockDatabase.Get(neighbor);
        if (n.isOpaque) return false;
        if (neighbor == self && n.cullSameType) return false;
        return true;
    }

    // ---------------------------------------------------------------- mesh

    public void BuildMesh()
    {
        verts.Clear(); normals.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();

        for (int y = 0; y < H; y++)
        for (int z = 0; z < S; z++)
        for (int x = 0; x < S; x++)
        {
            var id = (BlockId)Blocks[VoxelData.Index(x, y, z)];
            if (id == BlockId.Air) continue;
            var def = BlockDatabase.Get(id);

            for (int f = 0; f < 6; f++)
            {
                Vector3Int d = VoxelData.FaceDirections[f];
                if (!ShouldDrawFace(id, GetRelative(x + d.x, y + d.y, z + d.z))) continue;
                AddFace(x, y, z, f, d, def.faceTextures[f]);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        meshCollider.sharedMesh = null;
        if (verts.Count > 0) meshCollider.sharedMesh = mesh;

        HasMesh = true;
        IsDirty = false;
    }

    void AddFace(int x, int y, int z, int face, Vector3Int normal, int texture)
    {
        int start = verts.Count;
        Vector3Int[] corners = VoxelData.FaceCorners[face];

        for (int v = 0; v < 4; v++)
        {
            Vector3Int c = corners[v];
            verts.Add(new Vector3(x + c.x, y + c.y, z + c.z));
            normals.Add(normal);
            Vector2 uv = VoxelData.FaceUVs[v];
            uvs.Add(new Vector3(uv.x, uv.y, texture));
            ao[v] = VertexAO(x, y, z, normal, c);
            colors.Add(VoxelData.AOColors[ao[v]]);
        }

        // Lật đường chéo của quad để AO không bị lệch hướng
        if (ao[0] + ao[2] < ao[1] + ao[3])
        {
            tris.Add(start + 1); tris.Add(start + 2); tris.Add(start + 3);
            tris.Add(start + 1); tris.Add(start + 3); tris.Add(start + 0);
        }
        else
        {
            tris.Add(start + 0); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start + 0); tris.Add(start + 2); tris.Add(start + 3);
        }
    }

    /// AO kiểu Minecraft: xét 2 khối cạnh + 1 khối góc nằm trước mặt, quanh đỉnh đang xét.
    int VertexAO(int x, int y, int z, Vector3Int n, Vector3Int corner)
    {
        int bx = x + n.x, by = y + n.y, bz = z + n.z;

        Vector3Int t1, t2;
        if (n.x != 0)
        {
            t1 = new Vector3Int(0, corner.y == 1 ? 1 : -1, 0);
            t2 = new Vector3Int(0, 0, corner.z == 1 ? 1 : -1);
        }
        else if (n.y != 0)
        {
            t1 = new Vector3Int(corner.x == 1 ? 1 : -1, 0, 0);
            t2 = new Vector3Int(0, 0, corner.z == 1 ? 1 : -1);
        }
        else
        {
            t1 = new Vector3Int(corner.x == 1 ? 1 : -1, 0, 0);
            t2 = new Vector3Int(0, corner.y == 1 ? 1 : -1, 0);
        }

        bool s1 = IsOpaqueAt(bx + t1.x, by + t1.y, bz + t1.z);
        bool s2 = IsOpaqueAt(bx + t2.x, by + t2.y, bz + t2.z);
        if (s1 && s2) return 0;
        bool c = IsOpaqueAt(bx + t1.x + t2.x, by + t1.y + t2.y, bz + t1.z + t2.z);
        return 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (c ? 1 : 0));
    }
}
