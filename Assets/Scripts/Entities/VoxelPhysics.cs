using UnityEngine;

/// Va chạm AABB với lưới voxel cho entity (mob, vật phẩm rơi). Không dùng PhysX.
/// pos = tâm đáy hộp (chân), hw = nửa bề rộng, h = chiều cao.
public static class VoxelPhysics
{
    const float Eps = 0.001f;

    public struct Result
    {
        public bool grounded;
        public bool hitWall;
        public bool hitCeiling;
    }

    public static Result Move(World world, ref Vector3 pos, ref Vector3 velocity, float hw, float h, float dt)
    {
        var result = new Result();
        Vector3 delta = velocity * dt;

        float maxStep = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
        int steps = Mathf.Clamp(Mathf.CeilToInt(maxStep / 0.45f), 1, 16);
        Vector3 step = delta / steps;

        for (int i = 0; i < steps; i++)
        {
            if (step.y != 0f && MoveAxis(world, ref pos, 1, step.y, hw, h))
            {
                if (step.y < 0f) result.grounded = true;
                else result.hitCeiling = true;
                velocity.y = 0f;
                step.y = 0f;
            }
            if (step.x != 0f && MoveAxis(world, ref pos, 0, step.x, hw, h))
            {
                result.hitWall = true;
                velocity.x = 0f;
                step.x = 0f;
            }
            if (step.z != 0f && MoveAxis(world, ref pos, 2, step.z, hw, h))
            {
                result.hitWall = true;
                velocity.z = 0f;
                step.z = 0f;
            }
        }

        if (!result.grounded) result.grounded = IsOnGround(world, pos, hw);
        return result;
    }

    public static bool IsOnGround(World world, Vector3 pos, float hw)
    {
        int y = Mathf.FloorToInt(pos.y - 0.02f);
        int x0 = Mathf.FloorToInt(pos.x - hw + Eps), x1 = Mathf.FloorToInt(pos.x + hw - Eps);
        int z0 = Mathf.FloorToInt(pos.z - hw + Eps), z1 = Mathf.FloorToInt(pos.z + hw - Eps);
        for (int x = x0; x <= x1; x++)
        for (int z = z0; z <= z1; z++)
            if (world.IsSolidAt(x, y, z)) return true;
        return false;
    }

    public static bool Overlaps(World world, Vector3 pos, float hw, float h)
    {
        int x0 = Mathf.FloorToInt(pos.x - hw + Eps), x1 = Mathf.FloorToInt(pos.x + hw - Eps);
        int y0 = Mathf.FloorToInt(pos.y + Eps), y1 = Mathf.FloorToInt(pos.y + h - Eps);
        int z0 = Mathf.FloorToInt(pos.z - hw + Eps), z1 = Mathf.FloorToInt(pos.z + hw - Eps);
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
            if (world.IsSolidAt(x, y, z)) return true;
        return false;
    }

    /// Di chuyển theo 1 trục; nếu đụng block thì dừng sát mặt block và trả về true.
    static bool MoveAxis(World world, ref Vector3 pos, int axis, float d, float hw, float h)
    {
        Vector3 min = new Vector3(pos.x - hw, pos.y, pos.z - hw);
        Vector3 max = new Vector3(pos.x + hw, pos.y + h, pos.z + hw);

        // Hai trục còn lại
        int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
        int a1From = Mathf.FloorToInt(min[a1] + Eps), a1To = Mathf.FloorToInt(max[a1] - Eps);
        int a2From = Mathf.FloorToInt(min[a2] + Eps), a2To = Mathf.FloorToInt(max[a2] - Eps);

        if (d > 0f)
        {
            int start = Mathf.FloorToInt(max[axis] - Eps) + 1;
            int end = Mathf.FloorToInt(max[axis] + d - Eps);
            for (int c = start; c <= end; c++)
            {
                if (LayerSolid(world, axis, c, a1, a1From, a1To, a2, a2From, a2To))
                {
                    pos[axis] = c - (max[axis] - pos[axis]) - Eps;
                    return true;
                }
            }
        }
        else
        {
            int start = Mathf.FloorToInt(min[axis] + Eps) - 1;
            int end = Mathf.FloorToInt(min[axis] + d + Eps);
            for (int c = start; c >= end; c--)
            {
                if (LayerSolid(world, axis, c, a1, a1From, a1To, a2, a2From, a2To))
                {
                    pos[axis] = c + 1 + (pos[axis] - min[axis]) + Eps;
                    return true;
                }
            }
        }

        pos[axis] += d;
        return false;
    }

    static bool LayerSolid(World world, int axis, int c, int a1, int a1From, int a1To, int a2, int a2From, int a2To)
    {
        var cell = new Vector3Int();
        for (int i = a1From; i <= a1To; i++)
        for (int j = a2From; j <= a2To; j++)
        {
            cell[axis] = c;
            cell[a1] = i;
            cell[a2] = j;
            if (world.IsSolidAt(cell.x, cell.y, cell.z)) return true;
        }
        return false;
    }

    /// Tia cắt hộp AABB (slab test). Trả về khoảng cách hoặc -1.
    public static float RayBox(Vector3 origin, Vector3 dir, Bounds b, float maxDist)
    {
        float tMin = 0f, tMax = maxDist;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(dir[i]) < 1e-6f)
            {
                if (origin[i] < b.min[i] || origin[i] > b.max[i]) return -1f;
                continue;
            }
            float inv = 1f / dir[i];
            float t1 = (b.min[i] - origin[i]) * inv;
            float t2 = (b.max[i] - origin[i]) * inv;
            if (t1 > t2) { float t = t1; t1 = t2; t2 = t; }
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            if (tMin > tMax) return -1f;
        }
        return tMin;
    }
}
