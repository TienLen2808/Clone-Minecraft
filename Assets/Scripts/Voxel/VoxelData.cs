using UnityEngine;

/// Hằng số và bảng hình học dùng chung cho hệ thống voxel.
public static class VoxelData
{
    public const int ChunkSize = 16;      // X, Z
    public const int ChunkHeight = 128;   // Y
    public const int ChunkVolume = ChunkSize * ChunkSize * ChunkHeight;

    // Thứ tự mặt: 0 East(+X), 1 West(-X), 2 Top(+Y), 3 Bottom(-Y), 4 North(+Z), 5 South(-Z)
    public const int East = 0, West = 1, Top = 2, Bottom = 3, North = 4, South = 5;

    public static readonly Vector3Int[] FaceDirections =
    {
        new Vector3Int( 1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0),
        new Vector3Int( 0,-1, 0),
        new Vector3Int( 0, 0, 1),
        new Vector3Int( 0, 0,-1),
    };

    // 4 góc của mỗi mặt, xếp theo chiều kim đồng hồ khi nhìn từ ngoài vào (front face của Unity).
    // v0 = dưới-trái, v1 = trên-trái, v2 = trên-phải, v3 = dưới-phải.
    public static readonly Vector3Int[][] FaceCorners =
    {
        new[] { new Vector3Int(1,0,0), new Vector3Int(1,1,0), new Vector3Int(1,1,1), new Vector3Int(1,0,1) }, // East
        new[] { new Vector3Int(0,0,1), new Vector3Int(0,1,1), new Vector3Int(0,1,0), new Vector3Int(0,0,0) }, // West
        new[] { new Vector3Int(0,1,0), new Vector3Int(0,1,1), new Vector3Int(1,1,1), new Vector3Int(1,1,0) }, // Top
        new[] { new Vector3Int(0,0,0), new Vector3Int(1,0,0), new Vector3Int(1,0,1), new Vector3Int(0,0,1) }, // Bottom
        new[] { new Vector3Int(1,0,1), new Vector3Int(1,1,1), new Vector3Int(0,1,1), new Vector3Int(0,0,1) }, // North
        new[] { new Vector3Int(0,0,0), new Vector3Int(0,1,0), new Vector3Int(1,1,0), new Vector3Int(1,0,0) }, // South
    };

    public static readonly Vector2[] FaceUVs =
    {
        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)
    };

    // Độ sáng ambient occlusion theo số khối che (0 = tối nhất, 3 = không bị che)
    public static readonly Color32[] AOColors =
    {
        new Color32(115, 115, 115, 255),
        new Color32(158, 158, 158, 255),
        new Color32(204, 204, 204, 255),
        new Color32(255, 255, 255, 255),
    };

    public static int Index(int x, int y, int z) => x + z * ChunkSize + y * ChunkSize * ChunkSize;

    public static Vector2Int ChunkCoordOf(int worldX, int worldZ) => new Vector2Int(worldX >> 4, worldZ >> 4);

    public static Vector2Int ChunkCoordOf(Vector3 worldPos) =>
        ChunkCoordOf(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
}
