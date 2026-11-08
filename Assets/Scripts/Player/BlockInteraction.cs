using UnityEngine;

/// Tương tác survival:
///  - Giữ chuột trái: đào block (thời gian theo độ cứng + công cụ, có vết nứt); click vào mob để đánh.
///  - Chuột phải: dùng (mở bàn chế tạo / rương / lò nung, thuần hoá sói), ăn đồ ăn (giữ), đặt block.
///  - Chuột giữa: chọn ô hotbar có block đang nhìn.
public class BlockInteraction : MonoBehaviour
{
    [SerializeField] World world;
    [SerializeField] Transform cameraTransform;
    [SerializeField] Material outlineMaterial;
    [SerializeField] float blockReach = 4.5f;
    [SerializeField] float attackReach = 3.5f;
    [SerializeField] float placeRepeatDelay = 0.25f;
    [SerializeField] float eatDuration = 1.6f;

    PlayerController controller;
    PlayerInventory inventory;
    PlayerStats stats;
    CharacterController playerCC;

    GameObject outline, crack;
    MeshRenderer crackRenderer;
    Material crackMaterialInstance;
    int crackStage = -1;

    // Mục tiêu
    bool hasBlock;
    Vector3Int targetBlock, targetNormal;
    Mob targetMob;

    // Đào
    Vector3Int miningBlock;
    float miningProgress;
    bool mining;
    float mineCooldown, swingTimer;

    float nextPlaceTime, attackCooldown;
    bool eating;
    float eatTimer;
    int eatSlot = -1;
    bool autoEat;           // bấm R / nút Ăn: tự ăn hết, không cần giữ
    bool drawingBow;
    float drawTimer;

    public bool IsEating => eating;
    public string ContextHint { get; private set; } = "";
    public bool IsDrawingBow => drawingBow;
    public float DrawProgress => drawingBow ? Mathf.Clamp01(drawTimer) : 0f;
    public float EatProgress => eating ? eatTimer / eatDuration : 0f;
    public float MiningProgress => mining ? miningProgress : 0f;
    public Mob TargetMob => targetMob;

    void Start()
    {
        // Lấy ở Start (sau mọi Awake) vì PlayerController có thể vừa tự thêm các component này
        controller = GetComponent<PlayerController>();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        playerCC = GetComponent<CharacterController>();

        if (world == null) world = World.Instance;
        if (outlineMaterial == null && GameAssets.Instance != null) outlineMaterial = GameAssets.Instance.outlineMaterial;
        CreateOutline();
        CreateCrack();
    }

    void Update()
    {
        bool active = world != null && controller != null && controller.IsSpawned && !PlayerStats.IsDead
                      && !UIState.IsOpen && GameInput.PointerCaptured;
        if (!active)
        {
            outline.SetActive(false);
            StopMining();
            eating = false;
            drawingBow = false;
            ContextHint = "";
            return;
        }

        float dt = Time.deltaTime;
        attackCooldown -= dt;
        mineCooldown -= dt;

        FindTarget();

        outline.SetActive(hasBlock && targetMob == null);
        if (hasBlock) outline.transform.position = targetBlock;

        // ---- Ăn nhanh (phím R / nút Ăn): tự chọn đồ ăn tốt nhất trong túi
        if (GameInput.QuickEat && !eating) TryQuickEat();
        if (eating && autoEat) ContinueEating(dt);

        // ---- Điện thoại: chạm nhanh = đánh mob, nếu không thì dùng / đặt block
        if (GameInput.Tap)
        {
            if (targetMob != null) Attack(targetMob);
            else UseOnce();
        }

        // ---- Chuột trái / giữ màn hình
        if (GameInput.PrimaryPressed && targetMob != null)
        {
            Attack(targetMob);
        }
        else if (GameInput.PrimaryHeld && targetMob != null && GameInput.MobileMode)
        {
            Attack(targetMob);   // giữ tay vào mob trên điện thoại = đánh liên tục
        }
        else if (GameInput.PrimaryHeld && hasBlock && targetMob == null)
        {
            Mine(dt);
        }
        else
        {
            StopMining();
        }

        // ---- Chuột phải / giữ khi cầm đồ ăn
        if (GameInput.SecondaryPressed)
        {
            UseOnce();
        }
        else if (GameInput.SecondaryHeld)
        {
            if (!eating && !drawingBow && GameInput.MobileMode)
            {
                TryStartEating();     // giữ màn hình khi cầm đồ ăn
                TryStartDrawing();    // hoặc cầm cung
            }
            if (drawingBow) drawTimer += dt;
            if (eating) { if (!autoEat) ContinueEating(dt); }
            else if (!GameInput.MobileMode && Time.time >= nextPlaceTime && HeldIsBlock()) TryPlace();
        }
        else
        {
            if (!autoEat) eating = false;
            if (drawingBow) ReleaseBow();
        }

        ContextHint = BuildHint();

        // ---- Chuột giữa
        if (GameInput.MiddlePressed && hasBlock) PickBlock();
    }

    // ---------------------------------------------------------------- target

    void FindTarget()
    {
        Vector3 origin = controller.EyePosition;
        Vector3 dir = controller.LookDirection;

        hasBlock = VoxelRaycast(origin, dir, blockReach, out targetBlock, out targetNormal);
        float blockDist = hasBlock
            ? VoxelPhysics.RayBox(origin, dir, new Bounds(targetBlock + Vector3.one * 0.5f, Vector3.one), blockReach + 1f)
            : float.MaxValue;
        if (blockDist < 0f) blockDist = 0f;

        targetMob = null;
        float best = Mathf.Min(attackReach, blockDist);
        foreach (var m in Mob.All)
        {
            if (m.IsDying) continue;
            if ((m.Position - origin).sqrMagnitude > 64f) continue;
            float t = VoxelPhysics.RayBox(origin, dir, m.Bounds, attackReach);
            if (t >= 0f && t < best)
            {
                best = t;
                targetMob = m;
            }
        }
    }

    // ---------------------------------------------------------------- combat

    void Attack(Mob mob)
    {
        if (attackCooldown > 0f) return;
        attackCooldown = 0.25f;
        controller.Model?.Swing();

        var def = inventory.HeldDef;
        float damage = def != null ? def.attackDamage : 1f;
        // Đòn chí mạng khi đang rơi
        if (!playerCC.isGrounded && playerCC.velocity.y < -0.5f) damage *= 1.5f;

        if (mob.Damage(damage, transform.position, stats))
        {
            Wolf.OnOwnerAttacked(mob);
            stats.AddExhaustion(0.1f);
            if (def != null && def.IsTool) inventory.DamageHeldTool(def.tool == ToolType.Sword ? 1 : 2);
        }
    }

    // ---------------------------------------------------------------- mining

    void Mine(float dt)
    {
        if (mineCooldown > 0f) return;

        if (!mining || miningBlock != targetBlock)
        {
            mining = true;
            miningBlock = targetBlock;
            miningProgress = 0f;
        }

        BlockId id = world.GetBlock(miningBlock);
        var tool = inventory.HeldDef;
        float time = BlockDatabase.BreakTime(id, tool);
        if (float.IsInfinity(time)) return;

        swingTimer -= dt;
        if (swingTimer <= 0f)
        {
            swingTimer = 0.25f;
            controller.Model?.Swing();
        }

        miningProgress += time <= 0f ? 1f : dt / time;
        if (miningProgress >= 1f)
        {
            world.BreakBlock(miningBlock, tool);
            if (tool != null && tool.IsTool) inventory.DamageHeldTool(tool.tool == ToolType.Sword ? 2 : 1);
            stats.AddExhaustion(0.005f);
            StopMining();
            mineCooldown = 0.15f;
            return;
        }

        SetCrack(miningBlock, Mathf.Clamp((int)(miningProgress * 10f), 0, 9));
    }

    void StopMining()
    {
        mining = false;
        miningProgress = 0f;
        SetCrack(Vector3Int.zero, -1);
    }

    // ---------------------------------------------------------------- use / place / eat

    void UseOnce()
    {
        nextPlaceTime = Time.time + placeRepeatDelay;

        // 1. Mob (thuần hoá / cho ăn / ra lệnh ngồi)
        if (targetMob != null && targetMob.Interact(inventory))
        {
            controller.Model?.Swing();
            return;
        }

        // 2. Block có giao diện
        if (hasBlock && targetMob == null && !controller.IsSneaking)
        {
            BlockId id = world.GetBlock(targetBlock);
            if (BlockDatabase.Get(id).interactable)
            {
                OpenBlockUI(id, targetBlock);
                return;
            }
        }

        // 3. Kéo cung
        if (inventory.HeldDef != null && inventory.HeldDef.isBow)
        {
            TryStartDrawing();
            return;
        }

        // 4. Ăn
        var def = inventory.HeldDef;
        if (def != null && def.IsFood)
        {
            if (stats.CanEat(def)) StartEating(inventory.Selected, false);
            else GameHUD.Toast("Bạn đang no, chưa ăn được.");
            return;
        }

        // 4. Đặt block
        if (HeldIsBlock()) TryPlace();
    }

    void TryStartDrawing()
    {
        var def = inventory.HeldDef;
        if (def == null || !def.isBow) return;
        if (inventory.Inventory.Count(ItemId.Arrow) <= 0)
        {
            GameHUD.Toast("Không có mũi tên!");
            return;
        }
        drawingBow = true;
        drawTimer = 0f;
    }

    void ReleaseBow()
    {
        drawingBow = false;
        var def = inventory.HeldDef;
        if (def == null || !def.isBow) return;

        // Đường cong lực kéo giống Minecraft (kéo đủ 1 giây = lực tối đa)
        float t = Mathf.Clamp01(drawTimer);
        float power = Mathf.Clamp01((t * t + t * 2f) / 3f);
        if (power < 0.1f) return;

        int slot = inventory.Inventory.FindSlot(ItemId.Arrow);
        if (slot < 0) return;
        inventory.Inventory.Consume(slot);

        Vector3 dir = controller.LookDirection;
        float damage = 2f + 7f * power;
        if (power >= 1f) damage += Random.Range(0f, 3f);   // chí mạng
        Arrow.Spawn(world, controller.EyePosition + dir * 0.4f, dir * (45f * power), damage, stats, true);
        inventory.DamageHeldTool(1);
        controller.Model?.Swing();
    }

    void TryStartEating()
    {
        var def = inventory.HeldDef;
        if (def == null || !def.IsFood || !stats.CanEat(def)) return;
        StartEating(inventory.Selected, false);
    }

    void StartEating(int slot, bool auto)
    {
        eating = true;
        eatTimer = 0f;
        eatSlot = slot;
        autoEat = auto;
    }

    /// Phím R / nút Ăn: ăn món đang cầm, nếu không cầm đồ ăn thì tự lấy món tốt nhất trong túi.
    void TryQuickEat()
    {
        var held = inventory.HeldDef;
        int slot = held != null && held.IsFood ? inventory.Selected : inventory.BestFoodSlot();
        if (slot < 0)
        {
            GameHUD.Toast("Không có đồ ăn! Giết heo, bò, gà, cừu để lấy thịt.");
            return;
        }
        var def = inventory.Inventory[slot].Def;
        if (!stats.CanEat(def))
        {
            GameHUD.Toast("Bạn đang no, chưa ăn được.");
            return;
        }
        StartEating(slot, true);
    }

    void OpenBlockUI(BlockId id, Vector3Int pos)
    {
        var ui = InventoryUI.Instance;
        if (ui == null) return;
        switch (id)
        {
            case BlockId.CraftingTable: ui.OpenCraftingTable(); break;
            case BlockId.Chest: ui.OpenChest(world.GetChest(pos)); break;
            case BlockId.Furnace:
            case BlockId.FurnaceLit: ui.OpenFurnace(world.GetFurnace(pos)); break;
        }
    }

    void ContinueEating(float dt)
    {
        var stack = eatSlot >= 0 ? inventory.Inventory[eatSlot] : ItemStack.Empty;
        var def = stack.IsEmpty ? null : stack.Def;
        if (def == null || !def.IsFood || !stats.CanEat(def))
        {
            eating = false;
            return;
        }
        eatTimer += dt;
        if ((int)(eatTimer * 5f) != (int)((eatTimer - dt) * 5f)) controller.Model?.Swing();
        if (eatTimer >= eatDuration)
        {
            stats.Eat(def);
            inventory.Inventory.Consume(eatSlot);
            eating = false;
            GameHUD.Toast($"Đã ăn {def.displayName} (+{def.food / 2f:0.#} đùi gà)");
        }
    }

    // ---------------------------------------------------------------- gợi ý thao tác

    string BuildHint()
    {
        bool m = GameInput.MobileMode;
        string tap = m ? "Chạm" : "Chuột phải";
        string hit = m ? "Chạm" : "Chuột trái";

        if (eating) return "Đang ăn...";
        if (drawingBow) return m ? "Thả tay ra để bắn" : "Thả chuột phải để bắn";

        var held = inventory.HeldDef;

        if (targetMob != null)
        {
            if (targetMob is Wolf w)
            {
                if (w.IsTamed) return $"{tap}: bảo sói ngồi / đi theo   ·   cầm thịt rồi {tap.ToLower()} để cho ăn";
                if (!w.IsAngry && held != null && held.id == ItemId.Bone) return $"{tap}: cho sói ăn xương để thuần hoá";
                if (!w.IsAngry) return "Cầm XƯƠNG rồi " + tap.ToLower() + " vào sói để thuần hoá";
            }
            return $"{hit}: đánh {targetMob.DisplayName}";
        }

        if (hasBlock)
        {
            BlockId id = world.GetBlock(targetBlock);
            if (BlockDatabase.Get(id).interactable) return $"{tap}: mở {BlockDatabase.Get(id).displayName}";
        }

        if (held != null)
        {
            if (held.IsFood)
                return stats.CanEat(held) ? (m ? "Bấm nút ĂN để ăn" : "Bấm R (hoặc giữ chuột phải) để ăn") : "Bạn đang no";
            if (held.isBow) return m ? "Giữ màn hình để kéo cung, thả ra để bắn" : "Giữ chuột phải để kéo cung, thả ra để bắn";
            if (held.IsBlock && hasBlock) return $"{tap}: đặt {held.displayName}";
        }

        if (stats.Hunger < 18 && inventory.BestFoodSlot() >= 0)
            return m ? "Đói rồi! Bấm nút ĂN để ăn và hồi máu" : "Đói rồi! Bấm R để ăn và hồi máu";

        return "";
    }

    bool HeldIsBlock()
    {
        var def = inventory.HeldDef;
        return def != null && def.IsBlock;
    }

    void TryPlace()
    {
        nextPlaceTime = Time.time + placeRepeatDelay;
        if (!hasBlock || targetMob != null || targetNormal == Vector3Int.zero) return;

        var def = inventory.HeldDef;
        if (def == null || !def.IsBlock) return;

        Vector3Int p = targetBlock + targetNormal;
        if (world.GetBlock(p) != BlockId.Air) return;

        if (BlockDatabase.IsSolid(def.placeBlock))
        {
            var cell = new Bounds(p + Vector3.one * 0.5f, Vector3.one * 0.98f);
            if (playerCC.bounds.Intersects(cell)) return;
            foreach (var m in Mob.All)
                if (m.Bounds.Intersects(cell)) return;
        }

        if (world.SetBlock(p, def.placeBlock))
        {
            inventory.ConsumeHeld(1);
            controller.Model?.Swing();
        }
    }

    void PickBlock()
    {
        ItemId item = ItemDatabase.FromBlock(world.GetBlock(targetBlock));
        var inv = inventory.Inventory;
        int slot = inv.FindSlot(item, 0, PlayerInventory.HotbarSize);
        if (slot >= 0)
        {
            inventory.Select(slot);
            return;
        }
        slot = inv.FindSlot(item, PlayerInventory.HotbarSize, PlayerInventory.Size);
        if (slot >= 0)
        {
            // Đổi chỗ với ô hotbar đang chọn
            var a = inv[inventory.Selected];
            inv[inventory.Selected] = inv[slot];
            inv[slot] = a;
        }
    }

    // ---------------------------------------------------------------- raycast

    /// Amanatides & Woo: đi qua từng ô voxel dọc theo tia, trả về block đặc đầu tiên và pháp tuyến mặt bị trúng.
    bool VoxelRaycast(Vector3 origin, Vector3 dir, float maxDist, out Vector3Int hit, out Vector3Int normal)
    {
        hit = default;
        normal = Vector3Int.zero;
        dir.Normalize();

        var p = new Vector3Int(Mathf.FloorToInt(origin.x), Mathf.FloorToInt(origin.y), Mathf.FloorToInt(origin.z));
        var step = new Vector3Int(dir.x > 0 ? 1 : -1, dir.y > 0 ? 1 : -1, dir.z > 0 ? 1 : -1);

        float tMaxX = TMax(origin.x, dir.x, p.x), tMaxY = TMax(origin.y, dir.y, p.y), tMaxZ = TMax(origin.z, dir.z, p.z);
        float tDX = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
        float tDY = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;
        float tDZ = dir.z != 0 ? Mathf.Abs(1f / dir.z) : float.PositiveInfinity;

        float t = 0f;
        while (t <= maxDist)
        {
            if (BlockDatabase.IsSolid(world.GetBlock(p)))
            {
                hit = p;
                return true;
            }

            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                p.x += step.x; t = tMaxX; tMaxX += tDX;
                normal = new Vector3Int(-step.x, 0, 0);
            }
            else if (tMaxY < tMaxZ)
            {
                p.y += step.y; t = tMaxY; tMaxY += tDY;
                normal = new Vector3Int(0, -step.y, 0);
            }
            else
            {
                p.z += step.z; t = tMaxZ; tMaxZ += tDZ;
                normal = new Vector3Int(0, 0, -step.z);
            }
        }
        return false;
    }

    static float TMax(float origin, float dir, int cell)
    {
        if (dir > 0) return (cell + 1 - origin) / dir;
        if (dir < 0) return (origin - cell) / -dir;
        return float.PositiveInfinity;
    }

    // ---------------------------------------------------------------- visuals

    void CreateOutline()
    {
        outline = new GameObject("Block Outline");
        var mf = outline.AddComponent<MeshFilter>();
        var mr = outline.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (outlineMaterial != null) mr.sharedMaterial = outlineMaterial;

        const float e = 0.003f;
        float a = -e, b = 1f + e;
        var v = new[]
        {
            new Vector3(a, a, a), new Vector3(b, a, a), new Vector3(b, a, b), new Vector3(a, a, b),
            new Vector3(a, b, a), new Vector3(b, b, a), new Vector3(b, b, b), new Vector3(a, b, b),
        };
        var idx = new[] { 0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7 };

        var mesh = new Mesh { name = "Block Outline" };
        mesh.vertices = v;
        mesh.SetIndices(idx, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        outline.SetActive(false);
    }

    void CreateCrack()
    {
        crack = new GameObject("Block Crack");
        var mf = crack.AddComponent<MeshFilter>();
        crackRenderer = crack.AddComponent<MeshRenderer>();
        crackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        crackRenderer.receiveShadows = false;

        var assets = GameAssets.Instance;
        if (assets != null && assets.crackMaterial != null)
        {
            crackMaterialInstance = new Material(assets.crackMaterial);
            crackRenderer.sharedMaterial = crackMaterialInstance;
        }

        // Cube 1.004 với UV 0..1 mỗi mặt
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        const float g = 0.002f;
        for (int f = 0; f < 6; f++)
        {
            int start = verts.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3Int c = VoxelData.FaceCorners[f][i];
                verts.Add(new Vector3(c.x * (1 + 2 * g) - g, c.y * (1 + 2 * g) - g, c.z * (1 + 2 * g) - g));
                uvs.Add(VoxelData.FaceUVs[i]);
            }
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }
        var mesh = new Mesh { name = "Crack" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;
        crack.SetActive(false);
    }

    void SetCrack(Vector3Int pos, int stage)
    {
        if (crack == null) return;
        if (stage < 0)
        {
            if (crack.activeSelf) crack.SetActive(false);
            crackStage = -1;
            return;
        }
        crack.SetActive(true);
        crack.transform.position = pos;
        if (stage != crackStage && crackMaterialInstance != null)
        {
            crackStage = stage;
            var stages = GameAssets.Instance != null ? GameAssets.Instance.destroyStages : null;
            if (stages != null && stage < stages.Length && stages[stage] != null)
            {
                crackMaterialInstance.SetTexture("_BaseMap", stages[stage]);
                crackMaterialInstance.mainTexture = stages[stage];
            }
        }
    }
}
