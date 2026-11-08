using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Mô tả model kiểu Minecraft Java (ModelPart / addBox): toạ độ pixel, trục Y hướng xuống, mặt trước là -Z,
/// gốc Y = 24 là mặt đất. EntityModel tự chuyển sang toạ độ Unity.
public class ModelDef
{
    public int texWidth = 64, texHeight = 32;
    public float scale = 1f / 16f;
    public readonly List<PartDef> parts = new List<PartDef>();

    public ModelDef(int texW, int texH, float scale = 1f / 16f)
    {
        texWidth = texW;
        texHeight = texH;
        this.scale = scale;
    }

    /// pivot: toạ độ Java (px). rotation: radian giống Java (xRot, yRot, zRot).
    public PartDef Part(string name, float px, float py, float pz, float xRot = 0, float yRot = 0, float zRot = 0)
    {
        var p = new PartDef
        {
            name = name,
            pivot = new Vector3(px, py, pz),
            rotation = new Vector3(xRot, yRot, zRot) * Mathf.Rad2Deg,
        };
        parts.Add(p);
        return p;
    }
}

public class PartDef
{
    public string name;
    public Vector3 pivot;
    public Vector3 rotation;   // độ
    public readonly List<CubeDef> cubes = new List<CubeDef>();

    public PartDef Box(float x, float y, float z, float w, float h, float d, int u, int v,
                       float inflate = 0f, bool mirror = false)
    {
        cubes.Add(new CubeDef
        {
            from = new Vector3(x, y, z),
            size = new Vector3(w, h, d),
            uv = new Vector2Int(u, v),
            inflate = inflate,
            mirror = mirror,
        });
        return this;
    }
}

public class CubeDef
{
    public Vector3 from, size;
    public Vector2Int uv;
    public float inflate;
    public bool mirror;
}

/// Model entity dựng từ các khối hộp, mỗi bộ phận là một Transform để animate.
public class EntityModel : MonoBehaviour
{
    readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3> restPositions = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Quaternion> restRotations = new Dictionary<string, Quaternion>();
    readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    MaterialPropertyBlock block;
    ModelDef def;
    Color currentTint = Color.white;

    public ModelDef Definition => def;

    public static EntityModel Create(Transform parent, ModelDef def, Material material, string name = "Model")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var model = go.AddComponent<EntityModel>();
        model.Build(def, material);
        return model;
    }

    public void Build(ModelDef modelDef, Material material)
    {
        def = modelDef;
        foreach (var p in def.parts)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = JavaPivotToLocal(p.pivot);
            go.transform.localRotation = JavaRotation(p.rotation.x, p.rotation.y, p.rotation.z);

            var mesh = BuildPartMesh(p);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.On;
            renderers.Add(mr);

            parts[p.name] = go.transform;
            restPositions[p.name] = go.transform.localPosition;
            restRotations[p.name] = go.transform.localRotation;
        }
    }

    public Vector3 JavaPivotToLocal(Vector3 javaPivot) =>
        new Vector3(-javaPivot.x, 24f - javaPivot.y, -javaPivot.z) * def.scale;

    // ---------------------------------------------------------------- pose

    public Transform Part(string name) => parts.TryGetValue(name, out var t) ? t : null;

    /// Đặt góc xoay tuyệt đối (radian, quy ước Java).
    public void SetRotation(string name, float xRot, float yRot = 0f, float zRot = 0f)
    {
        if (parts.TryGetValue(name, out var t))
            t.localRotation = JavaRotation(xRot * Mathf.Rad2Deg, yRot * Mathf.Rad2Deg, zRot * Mathf.Rad2Deg);
    }

    /// Minecraft xoay bộ phận theo thứ tự Z * Y * X (khác Quaternion.Euler của Unity). Đơn vị: độ.
    public static Quaternion JavaRotation(float x, float y, float z) =>
        Quaternion.AngleAxis(z, Vector3.forward) * Quaternion.AngleAxis(y, Vector3.up) * Quaternion.AngleAxis(x, Vector3.right);

    /// Đặt pivot (toạ độ Java).
    public void SetPivot(string name, float px, float py, float pz)
    {
        if (parts.TryGetValue(name, out var t)) t.localPosition = JavaPivotToLocal(new Vector3(px, py, pz));
    }

    public void ResetPose()
    {
        foreach (var kv in parts)
        {
            kv.Value.localPosition = restPositions[kv.Key];
            kv.Value.localRotation = restRotations[kv.Key];
        }
    }

    public void SetMaterial(Material material)
    {
        foreach (var r in renderers) r.sharedMaterial = material;
    }

    public void SetShadowOnly(bool shadowOnly)
    {
        var mode = shadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (var r in renderers) r.shadowCastingMode = mode;
    }

    /// Nhuộm màu (đỏ khi bị đánh).
    public void SetTint(Color c)
    {
        if (c == currentTint) return;
        currentTint = c;
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- mesh

    Mesh BuildPartMesh(PartDef p)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        foreach (var c in p.cubes) AddCube(c, verts, uvs, tris);

        var mesh = new Mesh { name = p.name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// Sao chép đúng ModelPart.Cube của Minecraft Java (vị trí đỉnh + UV từng mặt).
    void AddCube(CubeDef c, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        float g = c.inflate;
        float x0 = c.from.x - g, y0 = c.from.y - g, z0 = c.from.z - g;
        float x1 = c.from.x + c.size.x + g, y1 = c.from.y + c.size.y + g, z1 = c.from.z + c.size.z + g;
        if (c.mirror) { float t = x0; x0 = x1; x1 = t; }

        var v1 = new Vector3(x0, y0, z0);
        var v2 = new Vector3(x1, y0, z0);
        var v3 = new Vector3(x1, y1, z0);
        var v4 = new Vector3(x0, y1, z0);
        var v5 = new Vector3(x0, y0, z1);
        var v6 = new Vector3(x1, y0, z1);
        var v7 = new Vector3(x1, y1, z1);
        var v8 = new Vector3(x0, y1, z1);

        float w = c.size.x, h = c.size.y, d = c.size.z;
        float u = c.uv.x, v = c.uv.y;
        float l = u, m = u + d, n = u + d + w, o = u + d + w + w, pp = u + d + w + d, q = u + d + w + d + w;
        float r = v, s = v + d, t2 = v + d + h;

        Vector3 center = (new Vector3(x0, y0, z0) + new Vector3(x1, y1, z1)) * 0.5f;

        AddPolygon(verts, uvs, tris, center, new[] { v6, v5, v1, v2 }, m, r, n, s);   // trên (Java DOWN)
        AddPolygon(verts, uvs, tris, center, new[] { v3, v4, v8, v7 }, n, s, o, r);   // dưới (Java UP)
        AddPolygon(verts, uvs, tris, center, new[] { v1, v5, v8, v4 }, l, s, m, t2);  // WEST
        AddPolygon(verts, uvs, tris, center, new[] { v2, v1, v4, v3 }, m, s, n, t2);  // NORTH (mặt trước)
        AddPolygon(verts, uvs, tris, center, new[] { v6, v2, v3, v7 }, n, s, pp, t2); // EAST
        AddPolygon(verts, uvs, tris, center, new[] { v5, v6, v7, v8 }, pp, s, q, t2); // SOUTH (mặt sau)
    }

    void AddPolygon(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 javaCenter,
                    Vector3[] java, float u1, float v1, float u2, float v2)
    {
        float tw = def.texWidth, th = def.texHeight;
        var faceUV = new[]
        {
            new Vector2(u2 / tw, 1f - v1 / th),
            new Vector2(u1 / tw, 1f - v1 / th),
            new Vector2(u1 / tw, 1f - v2 / th),
            new Vector2(u2 / tw, 1f - v2 / th),
        };

        int start = verts.Count;
        Vector3 faceCenter = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            verts.Add(ToUnity(java[i]));
            uvs.Add(faceUV[i]);
            faceCenter += java[i];
        }
        faceCenter *= 0.25f;

        // Chọn chiều quấn tam giác sao cho mặt hướng ra ngoài (front face của Unity)
        Vector3 a = verts[start], b = verts[start + 1], c = verts[start + 2];
        Vector3 normal = Vector3.Cross(b - a, c - a);
        Vector3 outward = ToUnity(faceCenter) - ToUnity(javaCenter);
        bool flip = Vector3.Dot(normal, outward) < 0f;

        if (!flip)
        {
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }
        else
        {
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
            tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
        }
    }

    Vector3 ToUnity(Vector3 java) => new Vector3(-java.x, -java.y, -java.z) * def.scale;
}
