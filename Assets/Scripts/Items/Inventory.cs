using System;

[Serializable]
public struct ItemStack
{
    public ItemId id;
    public int count;
    public int damage;   // độ hao mòn đã dùng (công cụ)

    public static readonly ItemStack Empty = default;

    public ItemStack(ItemId id, int count = 1, int damage = 0)
    {
        this.id = id;
        this.count = count;
        this.damage = damage;
    }

    public bool IsEmpty => id == ItemId.None || count <= 0;
    public ItemDef Def => ItemDatabase.Get(id);
    public int MaxStack => Def?.maxStack ?? 64;

    public bool CanStackWith(ItemStack other) =>
        !IsEmpty && !other.IsEmpty && id == other.id && damage == 0 && other.damage == 0 && MaxStack > 1;

    public ItemStack WithCount(int c) => c <= 0 ? Empty : new ItemStack(id, c, damage);

    public override string ToString() => IsEmpty ? "Empty" : $"{ItemDatabase.Name(id)} x{count}";
}

/// Một túi chứa N ô (túi người chơi, rương, lưới chế tạo, lò nung).
public class Inventory
{
    public readonly ItemStack[] slots;
    public event Action Changed;

    public Inventory(int size)
    {
        slots = new ItemStack[size];
    }

    public int Size => slots.Length;

    public ItemStack this[int i]
    {
        get => slots[i];
        set
        {
            slots[i] = value.IsEmpty ? ItemStack.Empty : value;
            Changed?.Invoke();
        }
    }

    public void NotifyChanged() => Changed?.Invoke();

    /// Thêm vào túi, ưu tiên gộp vào ô cùng loại. Trả về phần không chứa được.
    public ItemStack Add(ItemStack stack, int from = 0, int to = -1)
    {
        if (stack.IsEmpty) return ItemStack.Empty;
        if (to < 0) to = slots.Length;
        int max = stack.MaxStack;

        // Gộp
        if (max > 1)
        {
            for (int i = from; i < to && stack.count > 0; i++)
            {
                var s = slots[i];
                if (!s.CanStackWith(stack) || s.count >= max) continue;
                int move = Math.Min(max - s.count, stack.count);
                s.count += move;
                stack.count -= move;
                slots[i] = s;
            }
        }

        // Ô trống
        for (int i = from; i < to && stack.count > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int move = Math.Min(max, stack.count);
            slots[i] = new ItemStack(stack.id, move, stack.damage);
            stack.count -= move;
        }

        Changed?.Invoke();
        return stack.count > 0 ? stack : ItemStack.Empty;
    }

    public int Count(ItemId id)
    {
        int n = 0;
        foreach (var s in slots) if (s.id == id) n += s.count;
        return n;
    }

    public int FindSlot(ItemId id, int from = 0, int to = -1)
    {
        if (to < 0) to = slots.Length;
        for (int i = from; i < to; i++) if (slots[i].id == id && !slots[i].IsEmpty) return i;
        return -1;
    }

    /// Bớt n món ở ô i.
    public void Consume(int i, int n = 1)
    {
        var s = slots[i];
        s.count -= n;
        slots[i] = s.count > 0 ? s : ItemStack.Empty;
        Changed?.Invoke();
    }

    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = ItemStack.Empty;
        Changed?.Invoke();
    }

    public bool IsEmpty()
    {
        foreach (var s in slots) if (!s.IsEmpty) return false;
        return true;
    }
}
