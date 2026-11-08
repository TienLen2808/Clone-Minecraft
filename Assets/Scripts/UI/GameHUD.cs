using System.Collections.Generic;
using UnityEngine;

/// HUD: hotbar, thanh máu, thanh đói, crosshair, giờ trong game, thông báo, màn hình chết.
public class GameHUD : MonoBehaviour
{
    static GameHUD instance;
    static readonly List<(string text, float time)> toasts = new List<(string, float)>();

    PlayerInventory inventory;
    PlayerStats stats;
    PlayerController controller;
    BlockInteraction interaction;

    int lastSelected = -1;
    ItemId lastHeld;
    float heldNameTimer;
    bool showHelp = true;
    bool warnedHunger, warnedHealth;

    static GUIStyle labelStyle, centerStyle, countStyle, bigStyle, smallStyle, buttonStyle;

    void Awake()
    {
        instance = this;
        toasts.Clear();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        interaction = GetComponent<BlockInteraction>();
    }

    public static void Toast(string text)
    {
        toasts.Add((text, Time.time));
        if (toasts.Count > 4) toasts.RemoveAt(0);
    }

    void Update()
    {
        if (inventory == null) return;
        if (inventory.Selected != lastSelected || inventory.Held.id != lastHeld)
        {
            lastSelected = inventory.Selected;
            lastHeld = inventory.Held.id;
            heldNameTimer = 2f;
        }
        heldNameTimer -= Time.deltaTime;

        if (GameInput.HelpToggle) showHelp = !showHelp;

        // Nhắc người mới
        if (stats != null && !PlayerStats.IsDead && controller != null && controller.IsSpawned)
        {
            string eatKey = GameInput.MobileMode ? "nút ĂN" : "phím R";
            if (stats.Hunger <= 14 && !warnedHunger)
            {
                warnedHunger = true;
                Toast(inventory.BestFoodSlot() >= 0
                    ? $"Bạn đang đói! Bấm {eatKey} để ăn - no thì máu mới tự hồi."
                    : "Bạn đang đói! Giết heo/bò/gà/cừu lấy thịt, nướng trong lò cho no lâu.");
            }
            if (stats.Hunger >= 18) warnedHunger = false;

            if (stats.Health <= 6f && !warnedHealth)
            {
                warnedHealth = true;
                Toast($"Máu thấp! Tránh xa quái và bấm {eatKey} để ăn cho no, máu sẽ tự hồi.");
            }
            if (stats.Health >= 14f) warnedHealth = false;
        }
    }

    // ---------------------------------------------------------------- draw helpers (dùng chung với InventoryUI)

    public static void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        labelStyle.normal.textColor = Color.white;
        centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        countStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.LowerRight, fontSize = 12, fontStyle = FontStyle.Bold };
        bigStyle = new GUIStyle(centerStyle) { fontSize = 30 };
        smallStyle = new GUIStyle(labelStyle) { fontSize = 10 };
        smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
    }

    public static GUIStyle Label => labelStyle;
    public static GUIStyle Center => centerStyle;

    public static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    public static void ShadowLabel(Rect r, string text, GUIStyle style)
    {
        var c = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
        style.normal.textColor = c;
        GUI.Label(r, text, style);
    }

    /// Vẽ icon + số lượng + thanh độ bền.
    public static void DrawItem(Rect r, ItemStack s)
    {
        if (s.IsEmpty) return;
        var assets = GameAssets.Instance;
        var icon = assets != null ? assets.Icon(s.id) : null;
        float pad = r.width * 0.12f;
        var ir = new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f);
        if (icon != null) GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit);
        else ShadowLabel(ir, ItemDatabase.Name(s.id), smallStyle);

        if (s.count > 1)
            ShadowLabel(new Rect(r.x, r.y, r.width - 2f, r.height - 1f), s.count.ToString(), countStyle);

        var def = s.Def;
        if (def != null && def.durability > 0 && s.damage > 0)
        {
            float f = 1f - (float)s.damage / def.durability;
            var bar = new Rect(r.x + pad, r.yMax - pad - 1f, r.width - pad * 2f, 2f);
            Fill(new Rect(bar.x, bar.y, bar.width, 2f), Color.black);
            Fill(new Rect(bar.x, bar.y, bar.width * f, 2f), Color.Lerp(Color.red, Color.green, f));
        }
    }

    // ---------------------------------------------------------------- HUD

    void OnGUI()
    {
        EnsureStyles();
        GUI.depth = 10;
        float scale = UIState.Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float W = Screen.width / scale, H = Screen.height / scale;

        if (GameAssets.Instance == null)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.1f, 0f, 0f, 0.85f));
            ShadowLabel(new Rect(0, H / 2 - 40, W, 40), "Thiếu dữ liệu game!", bigStyle);
            ShadowLabel(new Rect(0, H / 2 + 6, W, 24),
                "Thoát Play, chọn menu  Minecraft > Setup World Scene  rồi bấm Play lại.", centerStyle);
            return;
        }

        if (controller != null && !controller.IsSpawned)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.15f, 0.12f, 0.1f, 1f));
            ShadowLabel(new Rect(0, H / 2 - 20, W, 40), "Đang tạo thế giới...", bigStyle);
            return;
        }

        // Nháy đỏ khi bị đánh
        if (stats != null && stats.DamageFlash > 0f)
            Fill(new Rect(0, 0, W, H), new Color(0.8f, 0f, 0f, stats.DamageFlash * 0.25f));

        DrawCrosshair(W, H);
        DrawHotbar(W, H);
        DrawStats(W, H);
        DrawInfo(W, H);
        DrawToasts(W, H);

        if (PlayerStats.IsDead) DrawDeathScreen(W, H);
    }

    void DrawCrosshair(float W, float H)
    {
        if (UIState.IsOpen || PlayerStats.IsDead) return;
        if (controller != null && controller.Mode != CameraMode.FirstPerson) return;
        float cx = W / 2f, cy = H / 2f;
        Fill(new Rect(cx - 7, cy - 1, 14, 2), new Color(1, 1, 1, 0.85f));
        Fill(new Rect(cx - 1, cy - 7, 2, 14), new Color(1, 1, 1, 0.85f));

        // Tiến độ ăn / kéo cung
        if (interaction != null && (interaction.IsEating || interaction.IsDrawingBow))
        {
            float p = interaction.IsEating ? interaction.EatProgress : interaction.DrawProgress;
            var r = new Rect(cx - 20, cy + 14, 40, 4);
            Fill(r, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(r.x, r.y, r.width * p, r.height),
                 interaction.IsEating ? new Color(0.9f, 0.7f, 0.3f) : (p >= 1f ? Color.white : new Color(0.7f, 0.85f, 1f)));
        }
    }

    const float Slot = 40f, Gap = 2f;

    /// Vị trí ô hotbar i (toạ độ GUI) - MobileControls dùng để bắt chạm.
    public static Rect HotbarSlotRect(int i, float W, float H)
    {
        float total = PlayerInventory.HotbarSize * Slot + (PlayerInventory.HotbarSize - 1) * Gap;
        float x0 = (W - total) / 2f, y0 = H - Slot - 6f;
        return new Rect(x0 + i * (Slot + Gap), y0, Slot, Slot);
    }

    void DrawHotbar(float W, float H)
    {
        if (inventory == null) return;
        float total = PlayerInventory.HotbarSize * Slot + (PlayerInventory.HotbarSize - 1) * Gap;
        float x0 = (W - total) / 2f, y0 = H - Slot - 6f;

        Fill(new Rect(x0 - 3, y0 - 3, total + 6, Slot + 6), new Color(0, 0, 0, 0.45f));
        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            var r = new Rect(x0 + i * (Slot + Gap), y0, Slot, Slot);
            Fill(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            DrawItem(r, inventory.Inventory[i]);
            if (i == inventory.Selected) Border(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), 2f, Color.white);
        }

        if (heldNameTimer > 0f && !inventory.Held.IsEmpty)
        {
            var c = centerStyle.normal.textColor;
            centerStyle.normal.textColor = new Color(1, 1, 1, Mathf.Clamp01(heldNameTimer));
            ShadowLabel(new Rect(0, y0 - 52, W, 20), ItemDatabase.Name(inventory.Held.id), centerStyle);
            centerStyle.normal.textColor = c;
        }

        if (controller != null && controller.IsFlying)
            ShadowLabel(new Rect(0, y0 - 98f, W, 20), "ĐANG BAY (bấm F để tắt)", centerStyle);

        // Gợi ý thao tác theo ngữ cảnh
        string hint = interaction != null ? interaction.ContextHint : "";
        if (!string.IsNullOrEmpty(hint) && !UIState.IsOpen && !PlayerStats.IsDead)
        {
            var size = labelStyle.CalcSize(new GUIContent(hint));
            var r = new Rect((W - size.x) / 2f - 8f, y0 - 76f, size.x + 16f, 20f);
            Fill(r, new Color(0f, 0f, 0f, 0.45f));
            ShadowLabel(new Rect(r.x + 8f, r.y + 2f, size.x + 4f, 18f), hint, labelStyle);
        }
    }

    void DrawStats(float W, float H)
    {
        if (stats == null) return;
        var a = GameAssets.Instance;
        float total = PlayerInventory.HotbarSize * Slot + (PlayerInventory.HotbarSize - 1) * Gap;
        float x0 = (W - total) / 2f, y = H - Slot - 6f - 22f;
        const float icon = 16f, step = 16f;

        // Máu (trái)
        for (int i = 0; i < 10; i++)
        {
            var r = new Rect(x0 + i * step, y, icon, icon);
            float hp = stats.Health - i * 2f;
            DrawIcon(r, a != null ? a.heartEmpty : null, new Color(0.2f, 0.2f, 0.2f));
            if (hp >= 2f) DrawIcon(r, a != null ? a.heartFull : null, Color.red);
            else if (hp >= 1f) DrawIcon(r, a != null ? a.heartHalf : null, new Color(0.7f, 0f, 0f));
        }

        // Đói (phải, từ phải sang trái)
        for (int i = 0; i < 10; i++)
        {
            var r = new Rect(x0 + total - icon - i * step, y, icon, icon);
            int hunger = stats.Hunger - i * 2;
            DrawIcon(r, a != null ? a.hungerEmpty : null, new Color(0.2f, 0.2f, 0.2f));
            if (hunger >= 2) DrawIcon(r, a != null ? a.hungerFull : null, new Color(0.8f, 0.5f, 0.2f));
            else if (hunger >= 1) DrawIcon(r, a != null ? a.hungerHalf : null, new Color(0.6f, 0.35f, 0.1f));
        }
    }

    static void DrawIcon(Rect r, Texture2D tex, Color fallback)
    {
        if (tex != null) GUI.DrawTexture(r, tex);
        else Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), fallback);
    }

    void DrawInfo(float W, float H)
    {
        var cycle = DayNightCycle.Instance;
        if (cycle != null)
        {
            var weather = WeatherSystem.Instance;
            string text = $"Ngày {cycle.Day} · {cycle.Clock}" + (weather != null ? " · " + weather.DisplayName : "");
            float w = labelStyle.CalcSize(new GUIContent(text)).x;
            ShadowLabel(new Rect(W - w - 10, 6, w + 4, 18), text, labelStyle);
        }

        if (showHelp && !GameInput.MobileMode)
        {
            ShadowLabel(new Rect(8, 6, W - 150, 18),
                "E túi đồ · Giữ trái đào/đánh · Phải dùng/đặt · R ăn · Q vứt · F5/V góc nhìn"
                + (controller != null && controller.CanFly ? " · F bay" : "") + " · F6 đổi thời tiết · F1 ẩn hướng dẫn",
                smallStyle);
        }

        // Máu mob đang nhắm
        var mob = interaction != null ? interaction.TargetMob : null;
        if (mob != null && !UIState.IsOpen)
        {
            string name = mob.DisplayName;
            ShadowLabel(new Rect(0, 24, W, 18), $"{name}  HP {Mathf.CeilToInt(mob.Health)}/{mob.MaxHealth}", centerStyle);
        }
    }

    void DrawToasts(float W, float H)
    {
        float y = H * 0.25f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            float age = Time.time - toasts[i].time;
            if (age > 3.5f)
            {
                toasts.RemoveAt(i);
                continue;
            }
        }
        foreach (var t in toasts)
        {
            float age = Time.time - t.time;
            var c = centerStyle.normal.textColor;
            centerStyle.normal.textColor = new Color(1f, 1f, 0.6f, Mathf.Clamp01(3.5f - age));
            ShadowLabel(new Rect(0, y, W, 20), t.text, centerStyle);
            centerStyle.normal.textColor = c;
            y += 22f;
        }
    }

    void DrawDeathScreen(float W, float H)
    {
        Fill(new Rect(0, 0, W, H), new Color(0.5f, 0f, 0f, 0.55f));
        ShadowLabel(new Rect(0, H * 0.3f, W, 40), "Bạn đã chết!", bigStyle);
        ShadowLabel(new Rect(0, H * 0.3f + 44, W, 20), stats != null ? stats.DeathMessage : "", centerStyle);
        var btn = new Rect(W / 2 - 100, H * 0.5f, 200, 40);
        bool tapped = GameInput.ConsumeUITap(btn, out _);
        if ((GUI.Button(btn, "Hồi sinh", buttonStyle) || tapped) && stats != null)
            stats.Respawn();
    }
}
