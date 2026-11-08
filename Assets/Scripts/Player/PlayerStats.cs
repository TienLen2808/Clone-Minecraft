using UnityEngine;

/// Máu, độ đói, độ no (saturation) theo luật Minecraft; sát thương khi ngã, chết & hồi sinh.
public class PlayerStats : MonoBehaviour
{
    public const float MaxHealth = 20f;
    public const int MaxHunger = 20;

    public static PlayerStats Instance { get; private set; }
    public static bool IsDead { get; private set; }

    public float Health { get; private set; } = MaxHealth;
    public int Hunger { get; private set; } = MaxHunger;
    public float Saturation { get; private set; } = 5f;
    public float DamageFlash { get; private set; }      // 0..1 cho HUD nháy đỏ
    public string DeathMessage { get; private set; } = "";

    public bool CanSprint => Hunger > 6;

    float exhaustion;
    float regenTimer, starveTimer, invulnTimer;
    PlayerController controller;
    PlayerInventory inventory;

    void Awake()
    {
        Instance = this;
        IsDead = false;
        controller = GetComponent<PlayerController>();
        inventory = GetComponent<PlayerInventory>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        DamageFlash = Mathf.Max(0f, DamageFlash - dt * 2.5f);
        invulnTimer -= dt;
        if (IsDead || controller == null || !controller.IsSpawned) return;

        // Mệt khi di chuyển
        if (controller.IsGroundedMoving)
            AddExhaustion(controller.HorizontalSpeed * dt * (controller.IsSprinting ? 0.1f : 0.01f));

        // Đổi mệt -> giảm no -> giảm đói
        while (exhaustion >= 4f)
        {
            exhaustion -= 4f;
            if (Saturation > 0f) Saturation = Mathf.Max(0f, Saturation - 1f);
            else Hunger = Mathf.Max(0, Hunger - 1);
        }

        // Hồi máu khi no
        if (Hunger >= 18 && Health < MaxHealth)
        {
            regenTimer += dt;
            float interval = (Hunger >= 20 && Saturation > 0f) ? 0.5f : 4f;
            if (regenTimer >= interval)
            {
                regenTimer = 0f;
                Health = Mathf.Min(MaxHealth, Health + 1f);
                AddExhaustion(6f);
            }
        }
        else regenTimer = 0f;

        // Đói lả: mất máu (dừng ở nửa tim)
        if (Hunger <= 0)
        {
            starveTimer += dt;
            if (starveTimer >= 4f)
            {
                starveTimer = 0f;
                if (Health > 1f) Damage(1f, transform.position, null, false, "chết đói");
            }
        }
        else starveTimer = 0f;
    }

    public void AddExhaustion(float amount) => exhaustion = Mathf.Min(40f, exhaustion + amount);

    /// Trả về true nếu nhận sát thương.
    public bool Damage(float amount, Vector3 sourcePos, Mob attacker, bool knockback = true, string cause = null)
    {
        if (IsDead || invulnTimer > 0f || amount <= 0f) return false;
        if (controller != null && controller.IsFlying && attacker == null && cause == "ngã") return false;

        Health -= amount;
        invulnTimer = 0.5f;
        DamageFlash = 1f;
        AddExhaustion(0.1f);

        if (knockback && controller != null)
        {
            Vector3 dir = transform.position - sourcePos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) controller.AddKnockback(dir.normalized * 6f + Vector3.up * 5f);
        }

        if (attacker != null) Wolf.OnOwnerHurtBy(attacker);

        if (Health <= 0f)
        {
            Health = 0f;
            Die(cause ?? (attacker != null ? "bị " + MobName(attacker) + " giết" : "chết"));
        }
        return true;
    }

    static string MobName(Mob m)
    {
        return m.DisplayName;
    }

    /// Gọi khi chạm đất sau khi rơi.
    public void OnLanded(float fallDistance)
    {
        float dmg = Mathf.Floor(fallDistance - 3f);
        if (dmg > 0f) Damage(dmg, transform.position, null, false, "ngã");
    }

    public bool CanEat(ItemDef food) => food != null && food.IsFood && Hunger < MaxHunger;

    public void Eat(ItemDef food)
    {
        Hunger = Mathf.Min(MaxHunger, Hunger + food.food);
        Saturation = Mathf.Min(Hunger, Saturation + food.saturation);
    }

    void Die(string cause)
    {
        IsDead = true;
        DeathMessage = "Bạn đã " + cause + "!";
        if (inventory != null) inventory.DropAll();
        UIState.CloseAll();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Respawn()
    {
        Health = MaxHealth;
        Hunger = MaxHunger;
        Saturation = 5f;
        exhaustion = 0f;
        IsDead = false;
        if (controller != null) controller.Respawn();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
