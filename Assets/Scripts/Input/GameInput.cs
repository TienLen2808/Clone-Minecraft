using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Gộp input bàn phím/chuột (PC) và cảm ứng (điện thoại) về một chỗ.
/// MobileControls ghi trạng thái cảm ứng vào các field Mobile* mỗi frame.
public static class GameInput
{
    /// Đang dùng điều khiển cảm ứng (tự bật trên điện thoại, hoặc bật "Force Mobile" trong Editor để thử).
    public static bool MobileMode;

    // ---- Trạng thái do MobileControls ghi
    public static Vector2 MobileMove;
    public static Vector2 MobileLookDegrees;
    public static bool MobileJump, MobileSneak, MobileSprint;
    public static bool MobilePrimaryHeld, MobileSecondaryHeld;
    public static bool MobileTap;                 // chạm nhanh: đánh mob hoặc dùng / đặt block
    public static bool MobileFlyToggle, MobileCameraToggle, MobileInventoryToggle, MobileDrop, MobileClose, MobileQuickEat;
    public static int MobileHotbar = -1;

    /// Chạm trong giao diện (túi đồ, nút): toạ độ GUI đã chia theo UIState.Scale.
    public struct UITap
    {
        public Vector2 position;
        public int button;      // 0 = trái, 1 = phải (giữ lâu)
        public bool shift;      // chạm đúp = chuyển nhanh
        public int frame;
    }
    public static readonly List<UITap> UITaps = new List<UITap>();

    /// Gọi đầu mỗi frame bởi MobileControls (trước mọi script khác).
    public static void ResetMobileFrame()
    {
        MobileLookDegrees = Vector2.zero;
        MobileTap = false;
        MobileFlyToggle = MobileCameraToggle = MobileInventoryToggle = MobileDrop = MobileClose = MobileQuickEat = false;
        MobileHotbar = -1;
        UITaps.RemoveAll(t => t.frame < Time.frameCount - 1);
    }

    static Keyboard Kb => Keyboard.current;
    static Mouse Ms => MobileMode ? null : Mouse.current;

    /// Có đang điều khiển nhân vật không (PC: chuột đã khoá vào game).
    public static bool PointerCaptured => MobileMode || Cursor.lockState == CursorLockMode.Locked;

    // ---------------------------------------------------------------- movement

    public static Vector2 Move
    {
        get
        {
            Vector2 v = MobileMove;
            var kb = Kb;
            if (kb != null)
            {
                if (kb.wKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed) v.x -= 1;
            }
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    /// Độ xoay góc nhìn (độ) trong frame này.
    public static Vector2 LookDegrees(float mouseSensitivity)
    {
        Vector2 d = MobileLookDegrees;
        var ms = Ms;
        if (ms != null && Cursor.lockState == CursorLockMode.Locked) d += ms.delta.ReadValue() * mouseSensitivity;
        return d;
    }

    public static bool Jump => MobileJump || (Kb != null && Kb.spaceKey.isPressed);
    public static bool Sneak => MobileSneak || (Kb != null && Kb.leftShiftKey.isPressed);
    public static bool Sprint => MobileSprint || (Kb != null && Kb.leftCtrlKey.isPressed);
    public static bool FlyToggle => MobileFlyToggle || (Kb != null && Kb.fKey.wasPressedThisFrame);
    public static bool CameraToggle => MobileCameraToggle || (Kb != null && (Kb.f5Key.wasPressedThisFrame || Kb.vKey.wasPressedThisFrame));

    // ---------------------------------------------------------------- actions

    public static bool PrimaryPressed => Ms != null && Ms.leftButton.wasPressedThisFrame;
    public static bool PrimaryHeld => MobilePrimaryHeld || (Ms != null && Ms.leftButton.isPressed);
    public static bool SecondaryPressed => Ms != null && Ms.rightButton.wasPressedThisFrame;
    public static bool SecondaryHeld => MobileSecondaryHeld || (Ms != null && Ms.rightButton.isPressed);
    public static bool MiddlePressed => Ms != null && Ms.middleButton.wasPressedThisFrame;
    public static bool Tap => MobileTap;

    public static bool InventoryToggle => MobileInventoryToggle || (Kb != null && Kb.eKey.wasPressedThisFrame);
    public static bool Close => MobileClose || (Kb != null && (Kb.eKey.wasPressedThisFrame || Kb.escapeKey.wasPressedThisFrame));
    public static bool Escape => Kb != null && Kb.escapeKey.wasPressedThisFrame;
    /// Ăn nhanh: phím R / nút Ăn.
    public static bool QuickEat => MobileQuickEat || (Kb != null && Kb.rKey.wasPressedThisFrame);
    public static bool Drop => MobileDrop || (Kb != null && Kb.qKey.wasPressedThisFrame);
    public static bool DropStack => Kb != null && Kb.qKey.wasPressedThisFrame && Kb.leftCtrlKey.isPressed;
    public static bool HelpToggle => Kb != null && Kb.f1Key.wasPressedThisFrame;

    public static int HotbarSelect
    {
        get
        {
            if (MobileHotbar >= 0) return MobileHotbar;
            var kb = Kb;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            if (kb.digit5Key.wasPressedThisFrame) return 4;
            if (kb.digit6Key.wasPressedThisFrame) return 5;
            if (kb.digit7Key.wasPressedThisFrame) return 6;
            if (kb.digit8Key.wasPressedThisFrame) return 7;
            if (kb.digit9Key.wasPressedThisFrame) return 8;
            return -1;
        }
    }

    public static float Scroll => Ms != null ? Ms.scroll.ReadValue().y : 0f;

    /// Lấy (và xoá) một lần chạm UI nằm trong rect.
    public static bool ConsumeUITap(Rect r, out UITap tap)
    {
        for (int i = 0; i < UITaps.Count; i++)
        {
            if (!r.Contains(UITaps[i].position)) continue;
            tap = UITaps[i];
            UITaps.RemoveAt(i);
            return true;
        }
        tap = default;
        return false;
    }
}
