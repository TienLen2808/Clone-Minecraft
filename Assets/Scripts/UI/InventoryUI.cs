using UnityEngine;

/// Giao diện túi đồ / bàn chế tạo / rương / lò nung, thao tác giống Minecraft:
///  - Chuột trái: nhấc cả chồng / đặt cả chồng / đổi chỗ.
///  - Chuột phải: nhấc một nửa / đặt từng cái.
///  - Shift + chuột trái: chuyển nhanh giữa túi đồ và rương/lò (hoặc hotbar <-> túi).
///  - E hoặc Esc để đóng.
public class InventoryUI : MonoBehaviour
{
    public enum ScreenType { None, Inventory, CraftingTable, Chest, Furnace }

    public static InventoryUI Instance { get; private set; }

    public ScreenType Current { get; private set; } = ScreenType.None;
    public bool IsOpen => Current != ScreenType.None;

    readonly Inventory craft2 = new Inventory(4);
    readonly Inventory craft3 = new Inventory(9);
    ChestEntity chest;
    FurnaceEntity furnace;
    ItemStack cursor;

    PlayerInventory player;
    PlayerController controller;

    // Tooltip của frame hiện tại
    ItemStack hoverStack;

    const float S = 36f;      // kích thước ô
    const float G = 2f;       // khoảng cách

    void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerInventory>();
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (IsOpen)
        {
            if (GameInput.Close) Close();
            return;
        }

        if (GameInput.InventoryToggle && !PlayerStats.IsDead && controller != null && controller.IsSpawned)
            Open(ScreenType.Inventory);
    }

    // ---------------------------------------------------------------- open / close

    void Open(ScreenType type)
    {
        Current = type;
        PlayerController.SetCursorLocked(false);
    }

    public void OpenCraftingTable() => Open(ScreenType.CraftingTable);

    public void OpenChest(ChestEntity c)
    {
        chest = c;
        Open(ScreenType.Chest);
    }

    public void OpenFurnace(FurnaceEntity f)
    {
        furnace = f;
        Open(ScreenType.Furnace);
    }

    public void Close()
    {
        if (!IsOpen) return;

        // Trả lại đồ đang cầm trên chuột và trong lưới chế tạo
        if (!cursor.IsEmpty) player.Give(cursor);
        cursor = ItemStack.Empty;
        ReturnGrid(craft2);
        ReturnGrid(craft3);

        Current = ScreenType.None;
        chest = null;
        furnace = null;
        UIState.ClosedFrame = Time.frameCount;
        if (!PlayerStats.IsDead) PlayerController.SetCursorLocked(true);
    }

    void ReturnGrid(Inventory grid)
    {
        for (int i = 0; i < grid.Size; i++)
        {
            if (grid[i].IsEmpty) continue;
            player.Give(grid[i]);
            grid.slots[i] = ItemStack.Empty;
        }
    }

    // ---------------------------------------------------------------- GUI

    void OnGUI()
    {
        if (!IsOpen || player == null) return;
        GameHUD.EnsureStyles();
        GUI.depth = 0;
        float scale = UIState.Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float W = Screen.width / scale, H = Screen.height / scale;

        hoverStack = ItemStack.Empty;

        GameHUD.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));

        float panelW = 9 * S + 8 * G + 24f;
        float topH = Current == ScreenType.Chest ? 3 * S + 2 * G + 26f : 3 * S + 2 * G + 26f;
        float bottomH = 4 * S + 3 * G + 10f + 26f;
        float panelH = topH + bottomH + 16f;
        var panel = new Rect((W - panelW) / 2f, (H - panelH) / 2f, panelW, panelH);

        GameHUD.Fill(panel, new Color(0.78f, 0.78f, 0.78f, 0.97f));
        GameHUD.Border(panel, 2f, new Color(0.25f, 0.25f, 0.25f));

        // Nút đóng (cần cho điện thoại)
        var closeRect = new Rect(panel.xMax - 30f, panel.y + 4f, 26f, 22f);
        GameHUD.Fill(closeRect, new Color(0.6f, 0.2f, 0.2f));
        GUI.Label(closeRect, "X", GameHUD.Center);
        if (GameInput.ConsumeUITap(closeRect, out _) ||
            (Event.current.type == EventType.MouseDown && closeRect.Contains(Event.current.mousePosition)))
        {
            if (Event.current.type == EventType.MouseDown) Event.current.Use();
            Close();
            return;
        }

        var top = new Rect(panel.x + 12f, panel.y + 8f, panelW - 24f, topH);
        switch (Current)
        {
            case ScreenType.Inventory: DrawCrafting(top, craft2, 2, "Chế tạo"); break;
            case ScreenType.CraftingTable: DrawCrafting(top, craft3, 3, "Bàn chế tạo"); break;
            case ScreenType.Chest: DrawChest(top); break;
            case ScreenType.Furnace: DrawFurnace(top); break;
        }

        DrawPlayerInventory(new Rect(panel.x + 12f, top.yMax + 8f, panelW - 24f, bottomH));

        // Tooltip
        if (!hoverStack.IsEmpty && cursor.IsEmpty)
        {
            var def = hoverStack.Def;
            string text = ItemDatabase.Name(hoverStack.id);
            if (def != null && def.durability > 0) text += $"  ({def.durability - hoverStack.damage}/{def.durability})";
            if (def != null && def.IsFood) text += $"  +{def.food} đói";
            Vector2 m = Event.current.mousePosition;
            var size = GameHUD.Label.CalcSize(new GUIContent(text));
            var r = new Rect(m.x + 12, m.y - 4, size.x + 10, size.y + 4);
            GameHUD.Fill(r, new Color(0.1f, 0f, 0.15f, 0.92f));
            GameHUD.Border(r, 1f, new Color(0.35f, 0.1f, 0.6f));
            GUI.Label(new Rect(r.x + 5, r.y + 2, size.x, size.y), text, GameHUD.Label);
        }

        // Đồ đang cầm theo chuột
        if (!cursor.IsEmpty)
        {
            Vector2 m = Event.current.mousePosition;
            GameHUD.DrawItem(new Rect(m.x - S / 2f, m.y - S / 2f, S, S), cursor);
        }
    }

    void Title(Rect area, string text)
    {
        var st = GameHUD.Label;
        var c = st.normal.textColor;
        st.normal.textColor = new Color(0.25f, 0.25f, 0.25f);
        GUI.Label(new Rect(area.x, area.y, area.width, 18), text, st);
        st.normal.textColor = c;
    }

    void DrawPlayerInventory(Rect area)
    {
        Title(area, "Túi đồ");
        float y = area.y + 20f;
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
        {
            int idx = 9 + row * 9 + col;
            Slot(new Rect(area.x + col * (S + G), y + row * (S + G), S, S), player.Inventory, idx, SlotKind.Player);
        }
        float hy = y + 3 * (S + G) + 8f;
        for (int col = 0; col < 9; col++)
            Slot(new Rect(area.x + col * (S + G), hy, S, S), player.Inventory, col, SlotKind.Player);
    }

    void DrawCrafting(Rect area, Inventory grid, int size, string title)
    {
        Title(area, title);
        float gridW = size * S + (size - 1) * G;
        float x0 = area.x + area.width / 2f - gridW - 30f;
        float y0 = area.y + 20f + (3 - size) * (S + G) / 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            Slot(new Rect(x0 + x * (S + G), y0 + y * (S + G), S, S), grid, y * size + x, SlotKind.Grid);

        float midY = y0 + gridW / 2f;
        GUI.Label(new Rect(x0 + gridW + 12f, midY - 10f, 30f, 20f), "→", GameHUD.Center);

        var result = CraftingRecipes.Match(grid, size);
        var rr = new Rect(x0 + gridW + 50f, midY - S / 2f - 4f, S + 8f, S + 8f);
        ResultSlot(rr, result, grid);
    }

    void DrawChest(Rect area)
    {
        Title(area, "Rương");
        if (chest == null) return;
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
            Slot(new Rect(area.x + col * (S + G), area.y + 20f + row * (S + G), S, S),
                 chest.inventory, row * 9 + col, SlotKind.Container);
    }

    void DrawFurnace(Rect area)
    {
        Title(area, "Lò nung");
        if (furnace == null) return;
        float cx = area.x + area.width / 2f;
        float y0 = area.y + 22f;

        var input = new Rect(cx - 90f, y0, S, S);
        var fuel = new Rect(cx - 90f, y0 + 2 * (S + G), S, S);
        var output = new Rect(cx + 40f, y0 + (S + G) - 4f, S + 8f, S + 8f);

        Slot(input, furnace.slots, FurnaceEntity.Input, SlotKind.Container);
        Slot(fuel, furnace.slots, FurnaceEntity.Fuel, SlotKind.Container);
        Slot(output, furnace.slots, FurnaceEntity.Output, SlotKind.Output);

        // Ngọn lửa (nhiên liệu còn lại)
        var flame = new Rect(input.x + 8f, input.yMax + 6f, S - 16f, S - 12f);
        GameHUD.Fill(flame, new Color(0.45f, 0.45f, 0.45f));
        float fh = flame.height * furnace.BurnFraction;
        GameHUD.Fill(new Rect(flame.x, flame.yMax - fh, flame.width, fh), new Color(1f, 0.55f, 0.1f));

        // Mũi tên tiến độ nung
        var arrow = new Rect(input.xMax + 16f, output.center.y - 5f, output.x - input.xMax - 32f, 10f);
        GameHUD.Fill(arrow, new Color(0.45f, 0.45f, 0.45f));
        GameHUD.Fill(new Rect(arrow.x, arrow.y, arrow.width * furnace.CookFraction, arrow.height), Color.white);

        var st = GameHUD.Label;
        var c = st.normal.textColor;
        st.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
        GUI.Label(new Rect(input.xMax + 6f, input.y + 4f, 120f, 18f), "Nguyên liệu", st);
        GUI.Label(new Rect(fuel.xMax + 6f, fuel.y + 10f, 120f, 18f), "Nhiên liệu", st);
        st.normal.textColor = c;
    }

    // ---------------------------------------------------------------- slots

    enum SlotKind { Player, Container, Grid, Output }

    void SlotBackground(Rect r)
    {
        GameHUD.Fill(r, new Color(0.55f, 0.55f, 0.55f));
        GameHUD.Fill(new Rect(r.x, r.y, r.width, 2f), new Color(0.22f, 0.22f, 0.22f));
        GameHUD.Fill(new Rect(r.x, r.y, 2f, r.height), new Color(0.22f, 0.22f, 0.22f));
        GameHUD.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), Color.white);
        GameHUD.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), Color.white);
    }

    void Slot(Rect r, Inventory inv, int index, SlotKind kind)
    {
        SlotBackground(r);
        var s = inv[index];
        GameHUD.DrawItem(r, s);

        var e = Event.current;
        bool hover = r.Contains(e.mousePosition);
        if (hover)
        {
            GameHUD.Fill(r, new Color(1f, 1f, 1f, 0.25f));
            hoverStack = s;
        }
        if (GameInput.ConsumeUITap(r, out var tap))
        {
            SlotClick(inv, index, kind, tap.button, tap.shift);
            return;
        }
        if (!hover || e.type != EventType.MouseDown || GameInput.MobileMode) return;

        bool shift = e.shift;
        int button = e.button;
        e.Use();
        SlotClick(inv, index, kind, button, shift);
    }

    void SlotClick(Inventory inv, int index, SlotKind kind, int button, bool shift)
    {
        if (kind == SlotKind.Output)
        {
            TakeOutput(inv, index, shift);
            return;
        }

        if (shift && button == 0)
        {
            QuickMove(inv, index, kind);
            return;
        }

        if (button == 0) LeftClick(inv, index);
        else if (button == 1) RightClick(inv, index);
    }

    void LeftClick(Inventory inv, int index)
    {
        var s = inv[index];
        if (cursor.IsEmpty)
        {
            cursor = s;
            inv[index] = ItemStack.Empty;
        }
        else if (s.IsEmpty)
        {
            inv[index] = cursor;
            cursor = ItemStack.Empty;
        }
        else if (s.CanStackWith(cursor))
        {
            int move = Mathf.Min(s.MaxStack - s.count, cursor.count);
            inv[index] = s.WithCount(s.count + move);
            cursor = cursor.WithCount(cursor.count - move);
        }
        else
        {
            inv[index] = cursor;
            cursor = s;
        }
    }

    void RightClick(Inventory inv, int index)
    {
        var s = inv[index];
        if (cursor.IsEmpty)
        {
            if (s.IsEmpty) return;
            int take = (s.count + 1) / 2;
            cursor = s.WithCount(take);
            inv[index] = s.WithCount(s.count - take);
        }
        else if (s.IsEmpty)
        {
            inv[index] = cursor.WithCount(1);
            cursor = cursor.WithCount(cursor.count - 1);
        }
        else if (s.CanStackWith(cursor) && s.count < s.MaxStack)
        {
            inv[index] = s.WithCount(s.count + 1);
            cursor = cursor.WithCount(cursor.count - 1);
        }
        else
        {
            inv[index] = cursor;
            cursor = s;
        }
    }

    void QuickMove(Inventory inv, int index, SlotKind kind)
    {
        var s = inv[index];
        if (s.IsEmpty) return;
        ItemStack rest;

        if (kind == SlotKind.Player)
        {
            if (Current == ScreenType.Chest && chest != null)
                rest = chest.inventory.Add(s);
            else if (Current == ScreenType.Furnace && furnace != null)
            {
                var def = s.Def;
                int target = def != null && def.smeltResult != ItemId.None ? FurnaceEntity.Input
                           : def != null && def.fuelSeconds > 0f ? FurnaceEntity.Fuel : -1;
                rest = target >= 0 ? furnace.slots.Add(s, target, target + 1) : s;
            }
            else
            {
                // Hotbar <-> túi
                bool inHotbar = index < PlayerInventory.HotbarSize;
                rest = inHotbar
                    ? player.Inventory.Add(s, PlayerInventory.HotbarSize, PlayerInventory.Size)
                    : player.Inventory.Add(s, 0, PlayerInventory.HotbarSize);
                if (!rest.IsEmpty && rest.count == s.count) return;
            }
        }
        else
        {
            rest = player.Inventory.Add(s);
        }

        inv[index] = rest;
    }

    void TakeOutput(Inventory inv, int index, bool shift)
    {
        var s = inv[index];
        if (s.IsEmpty) return;
        if (shift)
        {
            inv[index] = player.Inventory.Add(s);
            return;
        }
        if (cursor.IsEmpty)
        {
            cursor = s;
            inv[index] = ItemStack.Empty;
        }
        else if (cursor.CanStackWith(s) && cursor.count + s.count <= cursor.MaxStack)
        {
            cursor = cursor.WithCount(cursor.count + s.count);
            inv[index] = ItemStack.Empty;
        }
    }

    void ResultSlot(Rect r, ItemStack result, Inventory grid)
    {
        SlotBackground(r);
        GameHUD.DrawItem(r, result);

        var e = Event.current;
        bool hover = r.Contains(e.mousePosition);
        if (hover)
        {
            GameHUD.Fill(r, new Color(1f, 1f, 1f, 0.25f));
            hoverStack = result;
        }
        bool clicked = false, shift = false;
        if (GameInput.ConsumeUITap(r, out var tap))
        {
            clicked = true;
            shift = tap.shift;
        }
        else if (hover && e.type == EventType.MouseDown && !GameInput.MobileMode)
        {
            clicked = true;
            shift = e.shift;
            e.Use();
        }
        if (!clicked || result.IsEmpty) return;

        int size = grid.Size == 4 ? 2 : 3;

        if (shift)
        {
            // Chế tạo hết mức có thể vào túi
            for (int i = 0; i < 64; i++)
            {
                var r2 = CraftingRecipes.Match(grid, size);
                if (r2.IsEmpty || !player.CanFit(r2)) break;
                player.Inventory.Add(r2);
                CraftingRecipes.ConsumeIngredients(grid);
            }
            return;
        }

        if (cursor.IsEmpty)
        {
            cursor = result;
            CraftingRecipes.ConsumeIngredients(grid);
        }
        else if (cursor.CanStackWith(result) && cursor.count + result.count <= cursor.MaxStack)
        {
            cursor = cursor.WithCount(cursor.count + result.count);
            CraftingRecipes.ConsumeIngredients(grid);
        }
    }
}
