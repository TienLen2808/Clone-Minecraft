using UnityEngine;

/// Túi đồ người chơi: 36 ô (0-8 là hotbar). Chọn ô bằng 1-9 / cuộn chuột, Q vứt đồ (Ctrl+Q vứt cả chồng).
public class PlayerInventory : MonoBehaviour
{
    public const int HotbarSize = 9;
    public const int Size = 36;

    public static PlayerInventory Instance { get; private set; }

    public readonly Inventory Inventory = new Inventory(Size);
    public int Selected { get; private set; }

    public ItemStack Held => Inventory[Selected];
    public ItemDef HeldDef => Inventory[Selected].IsEmpty ? null : Inventory[Selected].Def;

    PlayerController controller;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (UIState.IsOpen || PlayerStats.IsDead) return;

        int slot = GameInput.HotbarSelect;
        if (slot >= 0) Selected = slot;

        if (GameInput.Drop && !Held.IsEmpty) DropHeld(GameInput.DropStack ? Held.count : 1);

        float scroll = GameInput.Scroll;
        if (scroll > 0.01f) Selected = (Selected + HotbarSize - 1) % HotbarSize;
        else if (scroll < -0.01f) Selected = (Selected + 1) % HotbarSize;
    }

    public void Select(int slot) => Selected = Mathf.Clamp(slot, 0, HotbarSize - 1);

    /// Ô chứa đồ ăn tốt nhất (ưu tiên đồ không phải thịt thối / mắt nhện). -1 nếu không có.
    public int BestFoodSlot()
    {
        int best = -1;
        float bestScore = 0f;
        for (int i = 0; i < Inventory.Size; i++)
        {
            var s = Inventory[i];
            var d = s.IsEmpty ? null : s.Def;
            if (d == null || !d.IsFood) continue;
            float score = d.food + d.saturation * 0.5f;
            if (d.id == ItemId.RottenFlesh || d.id == ItemId.SpiderEye) score *= 0.2f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    public bool CanFit(ItemStack stack)
    {
        foreach (var s in Inventory.slots)
        {
            if (s.IsEmpty) return true;
            if (s.CanStackWith(stack) && s.count < s.MaxStack) return true;
        }
        return false;
    }

    /// Thêm vào túi; phần thừa rơi ra đất.
    public void Give(ItemStack stack)
    {
        var rest = Inventory.Add(stack);
        if (!rest.IsEmpty) Throw(rest, 0.5f);
    }

    public void ConsumeHeld(int n = 1) => Inventory.Consume(Selected, n);

    /// Trừ độ bền công cụ đang cầm. Hỏng thì biến mất.
    public void DamageHeldTool(int amount = 1)
    {
        var s = Held;
        var def = s.Def;
        if (s.IsEmpty || def == null || def.durability <= 0) return;
        s.damage += amount;
        if (s.damage >= def.durability)
        {
            Inventory[Selected] = ItemStack.Empty;
            GameHUD.Toast(def.displayName + " đã hỏng!");
        }
        else Inventory[Selected] = s;
    }

    public void DropHeld(int count)
    {
        var s = Held;
        if (s.IsEmpty) return;
        count = Mathf.Min(count, s.count);
        Throw(s.WithCount(count), 1.5f);
        Inventory.Consume(Selected, count);
    }

    /// Ném vật phẩm về phía trước mặt.
    public void Throw(ItemStack stack, float pickupDelay)
    {
        var world = World.Instance;
        if (world == null || stack.IsEmpty) return;
        Vector3 dir = controller != null ? controller.LookDirection : transform.forward;
        Vector3 origin = (controller != null ? controller.EyePosition : transform.position + Vector3.up * 1.5f) - Vector3.up * 0.3f;
        world.DropItem(origin + dir * 0.3f, stack, dir * 5f + Vector3.up * 1.5f, pickupDelay);
    }

    /// Chết: rơi toàn bộ đồ ra đất.
    public void DropAll()
    {
        var world = World.Instance;
        if (world == null) return;
        for (int i = 0; i < Inventory.Size; i++)
        {
            var s = Inventory[i];
            if (s.IsEmpty) continue;
            world.DropItem(transform.position + Vector3.up, s,
                new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f)), 2f);
        }
        Inventory.Clear();
    }
}
