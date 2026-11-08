using System.Collections.Generic;

/// Công thức chế tạo có hình dạng (shaped). Tự cắt khoảng trống, cho phép lật ngang.
public static class CraftingRecipes
{
    class Recipe
    {
        public int w, h;
        public ItemId[] pattern;   // hàng trên xuống, trái sang phải; None = ô trống
        public ItemStack result;
    }

    static readonly List<Recipe> recipes = new List<Recipe>();

    const ItemId _ = ItemId.None;

    static CraftingRecipes()
    {
        var P = ItemId.OakPlanks;
        var S = ItemId.Stick;
        var C = ItemId.Cobblestone;

        Shaped(1, 1, new[] { ItemId.OakLog }, ItemId.OakPlanks, 4);
        Shaped(1, 2, new[] { P, P }, S, 4);
        Shaped(2, 2, new[] { P, P, P, P }, ItemId.CraftingTable);
        Shaped(3, 3, new[] { P, P, P, P, _, P, P, P, P }, ItemId.Chest);
        Shaped(3, 3, new[] { C, C, C, C, _, C, C, C, C }, ItemId.Furnace);
        Shaped(2, 2, new[] { ItemId.Stone, ItemId.Stone, ItemId.Stone, ItemId.Stone }, ItemId.StoneBricks, 4);

        var St = ItemId.String;
        Shaped(3, 3, new[] { _, S, St, S, _, St, _, S, St }, ItemId.Bow);
        Shaped(1, 3, new[] { ItemId.Flint, S, ItemId.Feather }, ItemId.Arrow, 4);

        ToolSet(P, ItemId.WoodPickaxe, ItemId.WoodAxe, ItemId.WoodShovel, ItemId.WoodSword);
        ToolSet(C, ItemId.StonePickaxe, ItemId.StoneAxe, ItemId.StoneShovel, ItemId.StoneSword);
        ToolSet(ItemId.IronIngot, ItemId.IronPickaxe, ItemId.IronAxe, ItemId.IronShovel, ItemId.IronSword);
        ToolSet(ItemId.Diamond, ItemId.DiamondPickaxe, ItemId.DiamondAxe, ItemId.DiamondShovel, ItemId.DiamondSword);
    }

    static void ToolSet(ItemId m, ItemId pickaxe, ItemId axe, ItemId shovel, ItemId sword)
    {
        var S = ItemId.Stick;
        Shaped(3, 3, new[] { m, m, m, _, S, _, _, S, _ }, pickaxe);
        Shaped(2, 3, new[] { m, m, m, S, _, S }, axe);
        Shaped(1, 3, new[] { m, S, S }, shovel);
        Shaped(1, 3, new[] { m, m, S }, sword);
    }

    static void Shaped(int w, int h, ItemId[] pattern, ItemId result, int count = 1)
    {
        recipes.Add(new Recipe { w = w, h = h, pattern = pattern, result = new ItemStack(result, count) });
    }

    /// grid: Inventory 4 ô (2x2) hoặc 9 ô (3x3).
    public static ItemStack Match(Inventory grid, int gridSize)
    {
        // Khung bao các ô có đồ
        int minX = gridSize, minY = gridSize, maxX = -1, maxY = -1;
        for (int y = 0; y < gridSize; y++)
        for (int x = 0; x < gridSize; x++)
        {
            if (grid[y * gridSize + x].IsEmpty) continue;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        if (maxX < 0) return ItemStack.Empty;

        int w = maxX - minX + 1, h = maxY - minY + 1;

        foreach (var r in recipes)
        {
            if (r.w != w || r.h != h) continue;
            if (Matches(r, grid, gridSize, minX, minY, false) || Matches(r, grid, gridSize, minX, minY, true))
                return r.result;
        }
        return ItemStack.Empty;
    }

    static bool Matches(Recipe r, Inventory grid, int gridSize, int ox, int oy, bool mirror)
    {
        for (int y = 0; y < r.h; y++)
        for (int x = 0; x < r.w; x++)
        {
            int px = mirror ? r.w - 1 - x : x;
            ItemId need = r.pattern[y * r.w + px];
            var have = grid[(oy + y) * gridSize + (ox + x)];
            ItemId haveId = have.IsEmpty ? ItemId.None : have.id;
            if (need != haveId) return false;
        }
        return true;
    }

    /// Lấy kết quả: trừ mỗi ô nguyên liệu 1 món.
    public static void ConsumeIngredients(Inventory grid)
    {
        for (int i = 0; i < grid.Size; i++)
        {
            var s = grid.slots[i];
            if (s.IsEmpty) continue;
            s.count--;
            grid.slots[i] = s.count > 0 ? s : ItemStack.Empty;
        }
        grid.NotifyChanged();
    }
}
