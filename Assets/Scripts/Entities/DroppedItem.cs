using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Vật phẩm rơi trên đất: block nhỏ xoay tròn hoặc sprite item. Người chơi đi qua thì nhặt.
public class DroppedItem : MonoBehaviour
{
    public static readonly List<DroppedItem> All = new List<DroppedItem>();

    const float HalfWidth = 0.125f;
    const float Height = 0.25f;
    const float Gravity = 20f;
    const float PickupRadius = 1.4f;
    const float MagnetRadius = 2.2f;
    const float Lifetime = 300f;

    static readonly Dictionary<ItemId, Material> spriteMaterials = new Dictionary<ItemId, Material>();
    static Mesh spriteMesh;

    public ItemStack Stack;

    World world;
    Vector3 velocity;
    float pickupTime, age, mergeTimer;
    Transform visual;
    float spinOffset;

    public static DroppedItem Spawn(World world, Vector3 position, ItemStack stack, Vector3 velocity, float pickupDelay)
    {
        var go = new GameObject("Item " + ItemDatabase.Name(stack.id));
        var item = go.AddComponent<DroppedItem>();
        item.world = world;
        item.Stack = stack;
        item.velocity = velocity;
        item.pickupTime = Time.time + pickupDelay;
        go.transform.position = position;
        item.BuildVisual();
        return item;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void BuildVisual()
    {
        spinOffset = Random.value * 360f;
        var v = new GameObject("Visual");
        v.transform.SetParent(transform, false);
        visual = v.transform;

        var mf = v.AddComponent<MeshFilter>();
        var mr = v.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.On;

        var def = Stack.Def;
        if (def != null && def.IsBlock && world.BlockMaterial != null)
        {
            mf.sharedMesh = BuildBlockMesh(def.placeBlock);
            mr.sharedMaterial = world.BlockMaterial;
            visual.localScale = Vector3.one * 0.25f;
            visual.localPosition = new Vector3(0f, 0.2f, 0f);
        }
        else
        {
            mf.sharedMesh = GetSpriteMesh();
            mr.sharedMaterial = GetSpriteMaterial(Stack.id);
            visual.localScale = Vector3.one * 0.45f;
            visual.localPosition = new Vector3(0f, 0.25f, 0f);
        }
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > Lifetime) { Destroy(gameObject); return; }
        if (!world.IsChunkLoadedAt(transform.position)) return;

        var player = world.Player;
        var inv = PlayerInventory.Instance;
        bool canPickup = Time.time >= pickupTime && player != null && inv != null && !PlayerStats.IsDead;

        Vector3 pos = transform.position;
        if (canPickup)
        {
            Vector3 target = player.position + Vector3.up * 0.8f;
            Vector3 toPlayer = target - pos;
            float dist = toPlayer.magnitude;

            if (dist < PickupRadius && inv.CanFit(Stack))
            {
                var rest = inv.Inventory.Add(Stack);
                if (rest.IsEmpty) { Destroy(gameObject); return; }
                Stack = rest;
            }
            else if (dist < MagnetRadius && inv.CanFit(Stack))
            {
                // Hút về phía người chơi
                velocity = Vector3.Lerp(velocity, toPlayer.normalized * 8f, dt * 8f);
            }
        }

        velocity.y -= Gravity * dt;
        var r = VoxelPhysics.Move(world, ref pos, ref velocity, HalfWidth, Height, dt);
        if (r.grounded)
        {
            velocity.x *= Mathf.Pow(0.02f, dt);
            velocity.z *= Mathf.Pow(0.02f, dt);
        }
        transform.position = pos;
        if (pos.y < -20f) Destroy(gameObject);

        // Xoay + nhấp nhô
        float bob = Mathf.Sin((age + spinOffset) * 2.5f) * 0.06f;
        var lp = visual.localPosition;
        lp.y = (Stack.Def != null && Stack.Def.IsBlock ? 0.2f : 0.25f) + bob;
        visual.localPosition = lp;
        visual.localRotation = Quaternion.Euler(0f, (age * 90f + spinOffset) % 360f, 0f);

        mergeTimer -= dt;
        if (mergeTimer <= 0f)
        {
            mergeTimer = 0.5f;
            TryMerge();
        }
    }

    void TryMerge()
    {
        if (Stack.count >= Stack.MaxStack) return;
        foreach (var other in All)
        {
            if (other == this || other.Stack.IsEmpty || !other.Stack.CanStackWith(Stack)) continue;
            if ((other.transform.position - transform.position).sqrMagnitude > 0.8f * 0.8f) continue;
            int move = Mathf.Min(Stack.MaxStack - Stack.count, other.Stack.count);
            Stack.count += move;
            other.Stack.count -= move;
            if (other.Stack.count <= 0)
            {
                other.Stack = ItemStack.Empty;
                Destroy(other.gameObject);
            }
            return;
        }
    }

    // ---------------------------------------------------------------- meshes

    static readonly Dictionary<BlockId, Mesh> blockMeshes = new Dictionary<BlockId, Mesh>();

    public static Mesh BuildBlockMesh(BlockId id)
    {
        if (blockMeshes.TryGetValue(id, out var cached) && cached != null) return cached;

        var def = BlockDatabase.Get(id);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector3>();
        var colors = new List<Color32>();
        var tris = new List<int>();

        for (int f = 0; f < 6; f++)
        {
            int start = verts.Count;
            for (int v = 0; v < 4; v++)
            {
                Vector3Int c = VoxelData.FaceCorners[f][v];
                verts.Add(new Vector3(c.x - 0.5f, c.y - 0.5f, c.z - 0.5f));
                normals.Add(VoxelData.FaceDirections[f]);
                Vector2 uv = VoxelData.FaceUVs[v];
                uvs.Add(new Vector3(uv.x, uv.y, def.faceTextures[f]));
                colors.Add(new Color32(255, 255, 255, 255));
            }
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        var mesh = new Mesh { name = "Item " + id };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        blockMeshes[id] = mesh;
        return mesh;
    }

    static Mesh GetSpriteMesh()
    {
        if (spriteMesh != null) return spriteMesh;
        // Quad 2 mặt, tâm ở giữa
        var v = new[]
        {
            new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(-0.5f, -0.5f, 0),
        };
        var uv = new[]
        {
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0),
        };
        var n = new[]
        {
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
        };
        spriteMesh = new Mesh { name = "Item Sprite" };
        spriteMesh.vertices = v;
        spriteMesh.uv = uv;
        spriteMesh.normals = n;
        spriteMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        spriteMesh.RecalculateBounds();
        return spriteMesh;
    }

    public static Material GetSpriteMaterial(ItemId id)
    {
        if (spriteMaterials.TryGetValue(id, out var m) && m != null) return m;
        var assets = GameAssets.Instance;
        if (assets == null || assets.itemSpriteMaterial == null) return null;
        m = new Material(assets.itemSpriteMaterial) { name = "Item " + id };
        var icon = assets.Icon(id);
        if (icon != null)
        {
            m.SetTexture("_BaseMap", icon);
            m.mainTexture = icon;
        }
        spriteMaterials[id] = m;
        return m;
    }
}
