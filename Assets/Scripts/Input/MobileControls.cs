using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// Điều khiển cảm ứng kiểu Minecraft Bedrock:
///  - Nửa trái: joystick ảo (đẩy hết cỡ lên trên = chạy nhanh).
///  - Nửa phải: vuốt để nhìn; chạm nhanh = đặt block / dùng / đánh mob; chạm giữ = đào (cầm đồ ăn thì ăn, cầm cung thì kéo, thả ra để bắn).
///  - Nút: Nhảy (giữ khi bay = bay lên), Lén (bật/tắt, khi bay = hạ xuống), Bay, Góc nhìn, Túi đồ, Vứt.
///  - Túi đồ: chạm = nhấc/đặt cả chồng, giữ lâu = lấy nửa / đặt từng cái.
/// Tự bật trên Android/iOS. Bật "Force In Editor" để thử bằng chuột trong Editor.
[DefaultExecutionOrder(-500)]
public class MobileControls : MonoBehaviour
{
    [SerializeField] bool forceInEditor = false;
    [SerializeField] float lookSensitivity = 0.28f;    // độ / đơn vị GUI
    [SerializeField] float joystickRadius = 55f;
    [SerializeField] float holdTime = 0.3f;
    [SerializeField] float tapMoveTolerance = 14f;

    enum Role { None, Joystick, Look, Jump, Sneak, Fly, Camera, Inventory, Drop, Eat, UI }

    class TouchState
    {
        public Role role;
        public Vector2 start, last;
        public float startTime;
        public float moved;
        public bool holdTriggered;
    }

    readonly Dictionary<int, TouchState> touches = new Dictionary<int, TouchState>();

    PlayerController controller;
    PlayerInventory inventory;
    bool sneakToggled;
    bool touchSimulationEnabled;
    Vector2 joyCenter, joyKnob;
    bool joyActive;
    static Texture2D circleTex;

    // Layout (toạ độ GUI)
    Rect jumpRect, sneakRect, flyRect, cameraRect, inventoryRect, dropRect, eatRect;
    bool showEat;
    float W, H;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inventory = GetComponent<PlayerInventory>();

        GameInput.MobileMode = Application.isMobilePlatform || (Application.isEditor && forceInEditor);
        if (!GameInput.MobileMode) return;

        EnhancedTouchSupport.Enable();
        if (Application.isEditor)
        {
            TouchSimulation.Enable();     // chuột giả lập ngón tay
            touchSimulationEnabled = true;
        }

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        if (touchSimulationEnabled) TouchSimulation.Disable();
    }

    // ---------------------------------------------------------------- input

    void Update()
    {
        GameInput.ResetMobileFrame();
        if (!GameInput.MobileMode) return;

        float scale = UIState.Scale;
        W = Screen.width / scale;
        H = Screen.height / scale;
        Layout();

        bool uiMode = UIState.IsOpen || PlayerStats.IsDead || (controller != null && !controller.IsSpawned);

        GameInput.MobileMove = Vector2.zero;
        GameInput.MobileJump = false;
        GameInput.MobileSprint = false;
        GameInput.MobilePrimaryHeld = false;
        GameInput.MobileSecondaryHeld = false;
        joyActive = false;

        var heldDef = inventory != null ? inventory.HeldDef : null;
        bool holdingFood = heldDef != null && (heldDef.IsFood || heldDef.isBow);   // giữ = ăn / kéo cung

        foreach (var t in ETouch.activeTouches)
        {
            Vector2 pos = new Vector2(t.screenPosition.x, Screen.height - t.screenPosition.y) / scale;
            int id = t.touchId;

            if (t.phase == TouchPhase.Began || !touches.ContainsKey(id))
            {
                var st = new TouchState { start = pos, last = pos, startTime = Time.unscaledTime };
                st.role = uiMode ? Role.UI : PickRole(pos);
                touches[id] = st;
                OnPress(st);
            }

            var s = touches[id];
            Vector2 delta = pos - s.last;
            s.moved += delta.magnitude;
            s.last = pos;
            float held = Time.unscaledTime - s.startTime;
            bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;

            switch (s.role)
            {
                case Role.Joystick:
                {
                    joyActive = true;
                    joyCenter = s.start;
                    Vector2 off = pos - s.start;
                    Vector2 v = Vector2.ClampMagnitude(off / joystickRadius, 1f);
                    joyKnob = s.start + v * joystickRadius;
                    GameInput.MobileMove = new Vector2(v.x, -v.y);
                    // Đẩy hết cỡ về phía trước = chạy nhanh
                    GameInput.MobileSprint = off.magnitude > joystickRadius * 1.15f && -off.y > Mathf.Abs(off.x) * 2f;
                    break;
                }

                case Role.Look:
                {
                    if (!ended) GameInput.MobileLookDegrees += new Vector2(delta.x, -delta.y) * lookSensitivity;

                    if (!s.holdTriggered && held >= holdTime && s.moved < tapMoveTolerance) s.holdTriggered = true;
                    if (s.holdTriggered && !ended)
                    {
                        if (holdingFood) GameInput.MobileSecondaryHeld = true;
                        else GameInput.MobilePrimaryHeld = true;
                    }
                    if (ended && !s.holdTriggered && held < holdTime && s.moved < tapMoveTolerance)
                        GameInput.MobileTap = true;
                    break;
                }

                case Role.Jump:
                    if (!ended) GameInput.MobileJump = true;
                    break;

                case Role.UI:
                {
                    // Giữ lâu = chuột phải; chạm nhanh = chuột trái
                    if (!s.holdTriggered && held >= 0.45f && s.moved < tapMoveTolerance)
                    {
                        s.holdTriggered = true;
                        AddUITap(s.start, 1);
                    }
                    if (ended && !s.holdTriggered && s.moved < tapMoveTolerance * 2f)
                        AddUITap(s.start, 0);
                    break;
                }
            }

            if (ended) touches.Remove(id);
        }

        // Dọn ngón tay không còn trong danh sách (mất focus...)
        if (ETouch.activeTouches.Count == 0 && touches.Count > 0) touches.Clear();

        GameInput.MobileSneak = sneakToggled;
    }

    static void AddUITap(Vector2 pos, int button)
    {
        GameInput.UITaps.Add(new GameInput.UITap { position = pos, button = button, frame = Time.frameCount });
    }

    Role PickRole(Vector2 p)
    {
        if (jumpRect.Contains(p)) return Role.Jump;
        if (sneakRect.Contains(p)) return Role.Sneak;
        if (controller != null && controller.CanFly && flyRect.Contains(p)) return Role.Fly;
        if (cameraRect.Contains(p)) return Role.Camera;
        if (inventoryRect.Contains(p)) return Role.Inventory;
        if (dropRect.Contains(p)) return Role.Drop;
        if (showEat && eatRect.Contains(p)) return Role.Eat;

        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            if (!GameHUD.HotbarSlotRect(i, W, H).Contains(p)) continue;
            GameInput.MobileHotbar = i;
            return Role.None;
        }

        if (p.x < W * 0.4f && p.y > H * 0.3f) return Role.Joystick;
        return Role.Look;
    }

    void OnPress(TouchState s)
    {
        switch (s.role)
        {
            case Role.Sneak: sneakToggled = !sneakToggled; break;
            case Role.Fly: GameInput.MobileFlyToggle = true; break;
            case Role.Camera: GameInput.MobileCameraToggle = true; break;
            case Role.Inventory: GameInput.MobileInventoryToggle = true; break;
            case Role.Drop: GameInput.MobileDrop = true; break;
            case Role.Eat: GameInput.MobileQuickEat = true; break;
        }
    }

    // ---------------------------------------------------------------- layout & draw

    void Layout()
    {
        float scale = UIState.Scale;
        Rect safe = Screen.safeArea;
        float right = (Screen.width - safe.xMax) / scale;
        float bottom = safe.yMin / scale;
        float top = (Screen.height - safe.yMax) / scale;

        const float B = 58f, gap = 12f;
        jumpRect = new Rect(W - right - 24f - B, H - bottom - 70f - B, B, B);
        sneakRect = new Rect(jumpRect.x - B - gap, jumpRect.y + B * 0.35f, B, B);
        flyRect = new Rect(jumpRect.x, jumpRect.y - B - gap, B, B);
        cameraRect = new Rect(W - right - 12f - 48f, top + 30f, 48f, 36f);

        Rect first = GameHUD.HotbarSlotRect(0, W, H);
        Rect last = GameHUD.HotbarSlotRect(PlayerInventory.HotbarSize - 1, W, H);
        inventoryRect = new Rect(last.xMax + 8f, last.y, 44f, last.height);
        dropRect = new Rect(first.x - 52f, first.y, 44f, first.height);
        eatRect = new Rect(last.xMax + 8f, last.y - 56f, 52f, 48f);

        // Nút Ăn chỉ hiện khi đói và có đồ ăn
        var st = PlayerStats.Instance;
        showEat = st != null && st.Hunger < PlayerStats.MaxHunger && inventory != null && inventory.BestFoodSlot() >= 0;
    }

    void OnGUI()
    {
        if (!GameInput.MobileMode) return;
        if (UIState.IsOpen || PlayerStats.IsDead || controller == null || !controller.IsSpawned) return;

        GameHUD.EnsureStyles();
        GUI.depth = 5;
        float scale = UIState.Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        // Joystick
        Vector2 baseCenter = joyActive ? joyCenter : new Vector2(110f, H - 130f);
        Vector2 knob = joyActive ? joyKnob : baseCenter;
        DrawCircle(baseCenter, joystickRadius, new Color(1f, 1f, 1f, joyActive ? 0.22f : 0.12f));
        DrawCircle(knob, joystickRadius * 0.45f, new Color(1f, 1f, 1f, joyActive ? 0.55f : 0.3f));

        bool flying = controller.IsFlying;
        Button(jumpRect, flying ? "Lên" : "Nhảy", GameInput.MobileJump);
        Button(sneakRect, flying ? "Xuống" : "Lén", sneakToggled);
        if (controller.CanFly) Button(flyRect, "Bay", flying);
        Button(cameraRect, "Góc", false);
        Button(inventoryRect, "Túi", false);
        Button(dropRect, "Vứt", false);
        if (showEat) Button(eatRect, "ĂN", GameInput.MobileQuickEat);
    }

    static void Button(Rect r, string label, bool active)
    {
        GameHUD.Fill(r, active ? new Color(1f, 1f, 1f, 0.45f) : new Color(0f, 0f, 0f, 0.35f));
        GameHUD.Border(r, 2f, new Color(1f, 1f, 1f, 0.5f));
        GameHUD.ShadowLabel(r, label, GameHUD.Center);
    }

    static void DrawCircle(Vector2 center, float radius, Color c)
    {
        if (circleTex == null) circleTex = MakeCircle(64);
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circleTex);
        GUI.color = old;
    }

    static Texture2D MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            float a = Mathf.Clamp01(r - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }
}
