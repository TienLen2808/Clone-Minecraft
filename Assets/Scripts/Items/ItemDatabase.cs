using System.Collections.Generic;

/// ItemId: 1..255 trùng với BlockId (item của block), từ 256 trở đi là item thường.
public enum ItemId : ushort
{
    None = 0,

    // ---- Item của block (giá trị = BlockId)
    Grass = (ushort)BlockId.Grass,
    Dirt = (ushort)BlockId.Dirt,
    Stone = (ushort)BlockId.Stone,
    Cobblestone = (ushort)BlockId.Cobblestone,
    Sand = (ushort)BlockId.Sand,
    Gravel = (ushort)BlockId.Gravel,
    Bedrock = (ushort)BlockId.Bedrock,
    OakLog = (ushort)BlockId.OakLog,
    OakPlanks = (ushort)BlockId.OakPlanks,
    OakLeaves = (ushort)BlockId.OakLeaves,
    Glass = (ushort)BlockId.Glass,
    Bricks = (ushort)BlockId.Bricks,
    StoneBricks = (ushort)BlockId.StoneBricks,
    CoalOre = (ushort)BlockId.CoalOre,
    IronOre = (ushort)BlockId.IronOre,
    GoldOre = (ushort)BlockId.GoldOre,
    DiamondOre = (ushort)BlockId.DiamondOre,
    CraftingTable = (ushort)BlockId.CraftingTable,
    Chest = (ushort)BlockId.Chest,
    Furnace = (ushort)BlockId.Furnace,
    FurnaceLit = (ushort)BlockId.FurnaceLit,
    WhiteWool = (ushort)BlockId.WhiteWool,

    // ---- Item thường
    Stick = 256,
    Coal,
    Charcoal,
    IronIngot,
    GoldIngot,
    Diamond,

    WoodPickaxe, WoodAxe, WoodShovel, WoodSword,
    StonePickaxe, StoneAxe, StoneShovel, StoneSword,
    IronPickaxe, IronAxe, IronShovel, IronSword,
    DiamondPickaxe, DiamondAxe, DiamondShovel, DiamondSword,

    RawPorkchop, CookedPorkchop,
    RawBeef, Steak,
    RawChicken, CookedChicken,
    RawMutton, CookedMutton,
    RottenFlesh,
    Apple,

    Leather,
    Feather,
    Bone,

    String,
    SpiderEye,
    Gunpowder,
    Flint,
    Arrow,
    Bow,
    RawCod,
    CookedCod,
}

public class ItemDef
{
    public ItemId id;
    public string displayName;
    public string iconPath;          // vd "blocks/dirt" hoặc "items/stick" (trong Assets/Textures/Minecraft/, không đuôi)
    public int maxStack = 64;
    public BlockId placeBlock = BlockId.Air;

    // Công cụ / vũ khí
    public ToolType tool = ToolType.None;
    public int tier;
    public float miningSpeed = 1f;
    public float attackDamage = 1f;
    public int durability;           // 0 = không hao mòn

    // Thức ăn
    public int food;
    public float saturation;

    // Lò nung
    public ItemId smeltResult = ItemId.None;
    public float fuelSeconds;

    public bool isBow;

    public bool IsBlock => placeBlock != BlockId.Air;
    public bool IsFood => food > 0;
    public bool IsTool => tool != ToolType.None;
}

public static class ItemDatabase
{
    public const string TextureRoot = "Assets/Textures/Minecraft";

    static readonly Dictionary<ItemId, ItemDef> items = new Dictionary<ItemId, ItemDef>();
    public static readonly List<ItemDef> All = new List<ItemDef>();
    public static readonly int MaxId;

    static ItemDatabase()
    {
        // ---- Item của block: tự sinh từ BlockDatabase
        foreach (var block in BlockDatabase.Blocks)
        {
            if (block == null || block.id == BlockId.Air || block.id == BlockId.FurnaceLit) continue;
            Add(new ItemDef
            {
                id = (ItemId)block.id,
                displayName = block.displayName,
                iconPath = "blocks/" + block.faceTextureNames[VoxelData.North],
                placeBlock = block.id,
            });
        }

        // Nhiên liệu là block
        Get(ItemId.OakLog).fuelSeconds = 15f;
        Get(ItemId.OakPlanks).fuelSeconds = 15f;
        Get(ItemId.CraftingTable).fuelSeconds = 15f;
        Get(ItemId.Chest).fuelSeconds = 15f;

        // Nung block
        Get(ItemId.Cobblestone).smeltResult = ItemId.Stone;
        Get(ItemId.Sand).smeltResult = ItemId.Glass;
        Get(ItemId.IronOre).smeltResult = ItemId.IronIngot;
        Get(ItemId.GoldOre).smeltResult = ItemId.GoldIngot;
        Get(ItemId.OakLog).smeltResult = ItemId.Charcoal;

        // ---- Nguyên liệu
        Simple(ItemId.Stick, "Stick", "stick").fuelSeconds = 5f;
        Simple(ItemId.Coal, "Coal", "coal").fuelSeconds = 80f;
        Simple(ItemId.Charcoal, "Charcoal", "charcoal").fuelSeconds = 80f;
        Simple(ItemId.IronIngot, "Iron Ingot", "iron_ingot");
        Simple(ItemId.GoldIngot, "Gold Ingot", "gold_ingot");
        Simple(ItemId.Diamond, "Diamond", "diamond");
        Simple(ItemId.Leather, "Leather", "leather");
        Simple(ItemId.Feather, "Feather", "feather");
        Simple(ItemId.Bone, "Bone", "bone");
        Simple(ItemId.String, "String", "string");
        Simple(ItemId.Gunpowder, "Gunpowder", "gunpowder");
        Simple(ItemId.Flint, "Flint", "flint");
        Simple(ItemId.Arrow, "Arrow", "arrow");

        var bow = Simple(ItemId.Bow, "Bow", "bow_standby");
        bow.maxStack = 1;
        bow.durability = 384;
        bow.isBow = true;

        // ---- Công cụ: tier, tốc độ đào, độ bền
        Tools("Wooden", "wood", 1, 2f, 59, ItemId.WoodPickaxe, ItemId.WoodAxe, ItemId.WoodShovel, ItemId.WoodSword);
        Tools("Stone", "stone", 2, 4f, 131, ItemId.StonePickaxe, ItemId.StoneAxe, ItemId.StoneShovel, ItemId.StoneSword);
        Tools("Iron", "iron", 3, 6f, 250, ItemId.IronPickaxe, ItemId.IronAxe, ItemId.IronShovel, ItemId.IronSword);
        Tools("Diamond", "diamond", 4, 8f, 1561, ItemId.DiamondPickaxe, ItemId.DiamondAxe, ItemId.DiamondShovel, ItemId.DiamondSword);
        Get(ItemId.WoodPickaxe).fuelSeconds = Get(ItemId.WoodAxe).fuelSeconds =
            Get(ItemId.WoodShovel).fuelSeconds = Get(ItemId.WoodSword).fuelSeconds = 10f;

        // ---- Thức ăn: điểm đói, độ no
        Food(ItemId.RawPorkchop, "Raw Porkchop", "porkchop_raw", 3, 1.8f).smeltResult = ItemId.CookedPorkchop;
        Food(ItemId.CookedPorkchop, "Cooked Porkchop", "porkchop_cooked", 8, 12.8f);
        Food(ItemId.RawBeef, "Raw Beef", "beef_raw", 3, 1.8f).smeltResult = ItemId.Steak;
        Food(ItemId.Steak, "Steak", "beef_cooked", 8, 12.8f);
        Food(ItemId.RawChicken, "Raw Chicken", "chicken_raw", 2, 1.2f).smeltResult = ItemId.CookedChicken;
        Food(ItemId.CookedChicken, "Cooked Chicken", "chicken_cooked", 6, 7.2f);
        Food(ItemId.RawMutton, "Raw Mutton", "mutton_raw", 2, 1.2f).smeltResult = ItemId.CookedMutton;
        Food(ItemId.CookedMutton, "Cooked Mutton", "mutton_cooked", 6, 9.6f);
        Food(ItemId.RottenFlesh, "Rotten Flesh", "rotten_flesh", 4, 0.8f);
        Food(ItemId.Apple, "Apple", "apple", 4, 2.4f);
        Food(ItemId.SpiderEye, "Spider Eye", "spider_eye", 2, 3.2f);
        Food(ItemId.RawCod, "Raw Cod", "fish_raw", 2, 0.4f).smeltResult = ItemId.CookedCod;
        Food(ItemId.CookedCod, "Cooked Cod", "fish_cooked", 5, 6f);

        int max = 0;
        foreach (var d in All) if ((int)d.id > max) max = (int)d.id;
        MaxId = max;
    }

    static ItemDef Add(ItemDef def)
    {
        items[def.id] = def;
        All.Add(def);
        return def;
    }

    static ItemDef Simple(ItemId id, string name, string icon) =>
        Add(new ItemDef { id = id, displayName = name, iconPath = "items/" + icon });

    static ItemDef Food(ItemId id, string name, string icon, int food, float saturation)
    {
        var d = Simple(id, name, icon);
        d.food = food;
        d.saturation = saturation;
        return d;
    }

    static void Tools(string material, string prefix, int tier, float speed, int durability,
                      ItemId pickaxe, ItemId axe, ItemId shovel, ItemId sword)
    {
        Tool(pickaxe, material + " Pickaxe", prefix + "_pickaxe", ToolType.Pickaxe, tier, speed, durability, 1f + tier);
        Tool(axe, material + " Axe", prefix + "_axe", ToolType.Axe, tier, speed, durability, 2f + tier);
        Tool(shovel, material + " Shovel", prefix + "_shovel", ToolType.Shovel, tier, speed, durability, 1f + tier);
        Tool(sword, material + " Sword", prefix + "_sword", ToolType.Sword, tier, 1.5f, durability, 3f + tier);
    }

    static void Tool(ItemId id, string name, string icon, ToolType type, int tier, float speed, int durability, float damage)
    {
        var d = Simple(id, name, icon);
        d.maxStack = 1;
        d.tool = type;
        d.tier = tier;
        d.miningSpeed = speed;
        d.durability = durability;
        d.attackDamage = damage;
    }

    // ---------------------------------------------------------------- public

    public static ItemDef Get(ItemId id) => items.TryGetValue(id, out var d) ? d : null;

    public static ItemId FromBlock(BlockId id) => id == BlockId.FurnaceLit ? ItemId.Furnace : (ItemId)id;

    public static string Name(ItemId id) => Get(id)?.displayName ?? id.ToString();
}
