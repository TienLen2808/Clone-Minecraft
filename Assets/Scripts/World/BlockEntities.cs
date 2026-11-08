using UnityEngine;

/// Dữ liệu gắn với một block cụ thể (rương, lò nung).
public abstract class BlockEntity
{
    public Vector3Int position;
    public abstract Inventory[] Inventories { get; }
}

public class ChestEntity : BlockEntity
{
    public readonly Inventory inventory = new Inventory(27);
    public override Inventory[] Inventories => new[] { inventory };
}

/// Lò nung: ô 0 = nguyên liệu, ô 1 = nhiên liệu, ô 2 = thành phẩm.
public class FurnaceEntity : BlockEntity
{
    public const int Input = 0, Fuel = 1, Output = 2;
    public const float CookTime = 10f;

    public readonly Inventory slots = new Inventory(3);
    public float burnTimeLeft;
    public float burnTimeTotal = 1f;
    public float cookProgress;   // 0..CookTime

    public override Inventory[] Inventories => new[] { slots };
    public bool IsBurning => burnTimeLeft > 0f;
    public float BurnFraction => burnTimeTotal > 0f ? burnTimeLeft / burnTimeTotal : 0f;
    public float CookFraction => cookProgress / CookTime;

    bool CanSmelt(out ItemStack result)
    {
        result = ItemStack.Empty;
        var input = slots[Input];
        if (input.IsEmpty) return false;
        var def = input.Def;
        if (def == null || def.smeltResult == ItemId.None) return false;

        result = new ItemStack(def.smeltResult, 1);
        var output = slots[Output];
        if (output.IsEmpty) return true;
        return output.id == result.id && output.count < output.MaxStack;
    }

    /// Trả về true nếu trạng thái cháy thay đổi (để đổi block Furnace <-> FurnaceLit).
    public bool Tick(float dt)
    {
        bool wasBurning = IsBurning;
        bool canSmelt = CanSmelt(out var result);

        if (!IsBurning && canSmelt)
        {
            var fuel = slots[Fuel];
            float seconds = fuel.Def?.fuelSeconds ?? 0f;
            if (seconds > 0f)
            {
                burnTimeLeft = burnTimeTotal = seconds;
                slots.Consume(Fuel);
            }
        }

        if (IsBurning)
        {
            burnTimeLeft = Mathf.Max(0f, burnTimeLeft - dt);
            if (canSmelt)
            {
                cookProgress += dt;
                if (cookProgress >= CookTime)
                {
                    cookProgress = 0f;
                    slots.Consume(Input);
                    var output = slots[Output];
                    slots[Output] = output.IsEmpty ? result : output.WithCount(output.count + 1);
                }
            }
            else cookProgress = 0f;
        }
        else
        {
            cookProgress = Mathf.Max(0f, cookProgress - dt * 2f);
        }

        return wasBurning != IsBurning;
    }
}
