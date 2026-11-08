using UnityEngine;

/// Mũi tên: bay theo đường parabol, trúng người chơi / mob thì gây sát thương, trúng block thì cắm lại.
/// Mũi tên do người chơi bắn có thể nhặt lại.
public class Arrow : MonoBehaviour
{
    const float Gravity = 20f;
    const float Lifetime = 30f;

    static Material shaftMat, tipMat, featherMat;

    World world;
    Vector3 velocity;
    float damage;
    Object owner;           // PlayerStats hoặc Mob
    bool fromPlayer;
    bool stuck;
    float age;

    public static Arrow Spawn(World world, Vector3 pos, Vector3 velocity, float damage, Object owner, bool fromPlayer)
    {
        var go = new GameObject("Arrow");
        go.transform.position = pos;
        var a = go.AddComponent<Arrow>();
        a.world = world;
        a.velocity = velocity;
        a.damage = damage;
        a.owner = owner;
        a.fromPlayer = fromPlayer;
        a.BuildVisual();
        go.transform.rotation = Quaternion.LookRotation(velocity);
        return a;
    }

    void BuildVisual()
    {
        EnsureMaterials();
        Part(new Vector3(0f, 0f, -0.05f), new Vector3(0.05f, 0.05f, 0.6f), shaftMat);
        Part(new Vector3(0f, 0f, 0.27f), new Vector3(0.09f, 0.09f, 0.1f), tipMat);
        Part(new Vector3(0f, 0f, -0.32f), new Vector3(0.14f, 0.02f, 0.14f), featherMat);
        Part(new Vector3(0f, 0f, -0.32f), new Vector3(0.02f, 0.14f, 0.14f), featherMat);
    }

    void Part(Vector3 localPos, Vector3 size, Material mat)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(transform, false);
        p.transform.localPosition = localPos;
        p.transform.localScale = size;
        var r = p.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void EnsureMaterials()
    {
        if (shaftMat != null) return;
        var template = GameAssets.Instance != null ? GameAssets.Instance.mobOpaqueMaterial : null;
        if (template == null) return;
        shaftMat = Colored(template, new Color(0.45f, 0.3f, 0.15f));
        tipMat = Colored(template, new Color(0.55f, 0.55f, 0.55f));
        featherMat = Colored(template, new Color(0.95f, 0.95f, 0.95f));
    }

    static Material Colored(Material template, Color c)
    {
        var m = new Material(template);
        m.SetTexture("_BaseMap", Texture2D.whiteTexture);
        m.SetColor("_BaseColor", c);
        return m;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > Lifetime || world == null)
        {
            Destroy(gameObject);
            return;
        }

        if (stuck)
        {
            // Nhặt lại mũi tên của mình
            var player = world.Player;
            var inv = PlayerInventory.Instance;
            if (fromPlayer && player != null && inv != null && age > 0.5f &&
                (player.position + Vector3.up - transform.position).sqrMagnitude < 2f && inv.CanFit(new ItemStack(ItemId.Arrow)))
            {
                inv.Inventory.Add(new ItemStack(ItemId.Arrow));
                Destroy(gameObject);
            }
            return;
        }

        velocity.y -= Gravity * dt;
        velocity *= Mathf.Pow(0.99f, dt * 20f);
        Vector3 start = transform.position;
        Vector3 step = velocity * dt;
        float len = step.magnitude;
        if (len < 1e-5f) return;
        Vector3 dir = step / len;

        float best = len;
        Mob hitMob = null;
        bool hitPlayer = false;

        // Người chơi
        var stats = PlayerStats.Instance;
        if (!fromPlayer && stats != null && !PlayerStats.IsDead)
        {
            var cc = stats.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                float t = VoxelPhysics.RayBox(start, dir, cc.bounds, len);
                if (t >= 0f && t < best) { best = t; hitPlayer = true; }
            }
        }

        // Mob
        foreach (var m in Mob.All)
        {
            if (m.IsDying || (Object)m == owner) continue;
            if ((m.Position - start).sqrMagnitude > (len + 4f) * (len + 4f)) continue;
            float t = VoxelPhysics.RayBox(start, dir, m.Bounds, len);
            if (t >= 0f && t < best) { best = t; hitMob = m; hitPlayer = false; }
        }

        // Block
        float blockT = -1f;
        for (float t = 0f; t <= best; t += 0.08f)
        {
            Vector3 p = start + dir * t;
            if (world.IsSolidAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)))
            {
                blockT = t;
                break;
            }
        }

        if (blockT >= 0f)
        {
            transform.position = start + dir * Mathf.Max(0f, blockT - 0.05f);
            stuck = true;
            age = Mathf.Max(age, Lifetime - 20f);   // cắm 20 giây rồi biến mất
            return;
        }

        if (hitPlayer)
        {
            stats.Damage(damage, start - dir, owner as Mob);
            Destroy(gameObject);
            return;
        }

        if (hitMob != null)
        {
            if (hitMob.Damage(damage, start - dir * 2f, owner) && fromPlayer) Wolf.OnOwnerAttacked(hitMob);
            Destroy(gameObject);
            return;
        }

        transform.position = start + step;
        transform.rotation = Quaternion.LookRotation(velocity);
        if (transform.position.y < -20f) Destroy(gameObject);
    }
}
