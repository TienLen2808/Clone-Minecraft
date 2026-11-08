using System;
using System.Collections.Generic;

public enum BlockId : byte
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    Cobblestone,
    Sand,
    Gravel,
    Bedrock,
    OakLog,
    OakPlanks,
    OakLeaves,
    Glass,
    Bricks,
    StoneBricks,
    CoalOre,
    IronOre,
    GoldOre,
    DiamondOre,
    CraftingTable,
    Chest,
    Furnace,
    FurnaceLit,
    WhiteWool,
}

public enum ToolType
{
    None,
    Pickaxe,
    Axe,
    Shovel,
    Sword,
}

public class BlockDef
{
    public BlockId id;
    public string displayName;
    public bool isSolid;        // có va chạm / chặn raycast
    public bool isOpaque;       // che hoàn toàn mặt khối bên cạnh + tạo AO
    public bool cullSameType;   // ẩn mặt giữa 2 khối cùng loại (kính)
    public string[] faceTextureNames = new string[6];
    public int[] faceTextures = new int[6];  // slice trong Texture2DArray

    // Survival
    public float hardness = 1f;              // < 0 = không phá được
    public ToolType tool = ToolType.None;    // công cụ đào nhanh
    public int requiredTier;                 // 0 = tay không cũng rơi đồ; 1 gỗ, 2 đá, 3 sắt, 4 kim cương
    public ItemId drop;                      // None = rơi chính nó
    public int dropMin = 1, dropMax = 1;
    public float dropChance = 1f;
    public ItemId altDrop;                   // rơi thay thế ngẫu nhiên (sỏi -> đá lửa)
    public float altDropChance;
    public bool noDrop;                      // kính...
    public bool interactable;                // chuột phải mở UI
}

/// Danh sách block. Tên texture là tên file trong Assets/Textures/Minecraft/blocks (không đuôi).
/// Thêm block mới: thêm vào enum BlockId + Register() bên dưới, rồi chạy menu Minecraft > Rebuild Block Textures.
public static class BlockDatabase
{
    public const string TextureFolder = "Assets/Textures/Minecraft/blocks";

    public static readonly BlockDef[] Blocks;
    public static readonly string[] TextureNames;   // thứ tự = slice index trong Texture2DArray

    static readonly List<string> textureList = new List<string>();
    static readonly Dictionary<string, int> textureIndex = new Dictionary<string, int>();

    static BlockDatabase()
    {
        Blocks = new BlockDef[Enum.GetValues(typeof(BlockId)).Length];

        Register(BlockId.Air, "Air", null, solid: false, opaque: false).hardness = 0f;

        var b = Register(BlockId.Grass, "Grass Block", TopSideBottom("grass_carried", "grass_side_carried", "dirt"));
        Survival(b, 0.6f, ToolType.Shovel, 0, ItemId.Dirt);

        Survival(Register(BlockId.Dirt, "Dirt", All("dirt")), 0.5f, ToolType.Shovel);
        Survival(Register(BlockId.Stone, "Stone", All("stone")), 1.5f, ToolType.Pickaxe, 1, ItemId.Cobblestone);
        Survival(Register(BlockId.Cobblestone, "Cobblestone", All("cobblestone")), 2f, ToolType.Pickaxe, 1);
        Survival(Register(BlockId.Sand, "Sand", All("sand")), 0.5f, ToolType.Shovel);
        b = Register(BlockId.Gravel, "Gravel", All("gravel"));
        Survival(b, 0.6f, ToolType.Shovel);
        b.altDrop = ItemId.Flint;
        b.altDropChance = 0.15f;
        Survival(Register(BlockId.Bedrock, "Bedrock", All("bedrock")), -1f);
        Survival(Register(BlockId.OakLog, "Oak Log", TopSideBottom("log_oak_top", "log_oak", "log_oak_top")), 2f, ToolType.Axe);
        Survival(Register(BlockId.OakPlanks, "Oak Planks", All("planks_oak")), 2f, ToolType.Axe);

        b = Register(BlockId.OakLeaves, "Oak Leaves", All("leaves_oak_carried"), opaque: false);
        Survival(b, 0.2f, ToolType.None, 0, ItemId.Apple);
        b.dropChance = 0.06f;

        b = Register(BlockId.Glass, "Glass", All("glass"), opaque: false, cullSame: true);
        Survival(b, 0.3f);
        b.noDrop = true;

        Survival(Register(BlockId.Bricks, "Bricks", All("brick")), 2f, ToolType.Pickaxe, 1);
        Survival(Register(BlockId.StoneBricks, "Stone Bricks", All("stonebrick")), 1.5f, ToolType.Pickaxe, 1);
        Survival(Register(BlockId.CoalOre, "Coal Ore", All("coal_ore")), 3f, ToolType.Pickaxe, 1, ItemId.Coal);
        Survival(Register(BlockId.IronOre, "Iron Ore", All("iron_ore")), 3f, ToolType.Pickaxe, 2);
        Survival(Register(BlockId.GoldOre, "Gold Ore", All("gold_ore")), 3f, ToolType.Pickaxe, 3);
        Survival(Register(BlockId.DiamondOre, "Diamond Ore", All("diamond_ore")), 3f, ToolType.Pickaxe, 3, ItemId.Diamond);

        b = Register(BlockId.CraftingTable, "Crafting Table", new[]
        {
            "crafting_table_side", "crafting_table_side", "crafting_table_top",
            "planks_oak", "crafting_table_front", "crafting_table_front",
        });
        Survival(b, 2.5f, ToolType.Axe);
        b.interactable = true;

        b = Register(BlockId.Chest, "Chest", new[]
        {
            "chest_side", "chest_side", "chest_top", "chest_top", "chest_front", "chest_side",
        });
        Survival(b, 2.5f, ToolType.Axe);
        b.interactable = true;

        b = Register(BlockId.Furnace, "Furnace", new[]
        {
            "furnace_side", "furnace_side", "furnace_top", "furnace_top", "furnace_front_off", "furnace_side",
        });
        Survival(b, 3.5f, ToolType.Pickaxe, 1);
        b.interactable = true;

        b = Register(BlockId.FurnaceLit, "Furnace", new[]
        {
            "furnace_side", "furnace_side", "furnace_top", "furnace_top", "furnace_front_on", "furnace_side",
        });
        Survival(b, 3.5f, ToolType.Pickaxe, 1, ItemId.Furnace);
        b.interactable = true;

        Survival(Register(BlockId.WhiteWool, "White Wool", All("wool_colored_white")), 0.8f);

        TextureNames = textureList.ToArray();
    }

    static string[] All(string name) => new[] { name, name, name, name, name, name };

    static string[] TopSideBottom(string top, string side, string bottom) =>
        new[] { side, side, top, bottom, side, side };

    static void Survival(BlockDef def, float hardness, ToolType tool = ToolType.None, int tier = 0,
                         ItemId drop = ItemId.None)
    {
        def.hardness = hardness;
        def.tool = tool;
        def.requiredTier = tier;
        def.drop = drop;
    }

    static BlockDef Register(BlockId id, string displayName, string[] faces,
                             bool solid = true, bool opaque = true, bool cullSame = false)
    {
        var def = new BlockDef
        {
            id = id,
            displayName = displayName,
            isSolid = solid,
            isOpaque = opaque,
            cullSameType = cullSame,
        };

        if (faces != null)
        {
            for (int f = 0; f < 6; f++)
            {
                string name = faces[f];
                if (!textureIndex.TryGetValue(name, out int index))
                {
                    index = textureList.Count;
                    textureList.Add(name);
                    textureIndex[name] = index;
                }
                def.faceTextureNames[f] = name;
                def.faceTextures[f] = index;
            }
        }

        Blocks[(int)id] = def;
        return def;
    }

    public static BlockDef Get(BlockId id) => Blocks[(int)id];
    public static bool IsOpaque(BlockId id) => Blocks[(int)id].isOpaque;
    public static bool IsSolid(BlockId id) => Blocks[(int)id].isSolid;

    /// Thời gian đào (giây) theo công thức Minecraft.
    public static float BreakTime(BlockId id, ItemDef tool)
    {
        var def = Get(id);
        if (def.hardness < 0f) return float.PositiveInfinity;
        if (def.hardness == 0f) return 0f;

        bool rightTool = tool != null && def.tool != ToolType.None && tool.tool == def.tool;
        float speed = rightTool ? tool.miningSpeed : 1f;
        bool canHarvest = CanHarvest(id, tool);
        return def.hardness * (canHarvest ? 1.5f : 5f) / speed;
    }

    public static bool CanHarvest(BlockId id, ItemDef tool)
    {
        var def = Get(id);
        if (def.requiredTier <= 0) return true;
        return tool != null && tool.tool == def.tool && tool.tier >= def.requiredTier;
    }
}
