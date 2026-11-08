using UnityEngine;

/// Trạng thái UI dùng chung: đang mở túi đồ / rương... thì khoá di chuyển và hiện chuột.
public static class UIState
{
    public static bool IsOpen => InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
    public static int ClosedFrame = -1;     // frame vừa đóng UI (để phím Esc không bị xử lý 2 lần)

    public static bool JustClosed => ClosedFrame == Time.frameCount;

    /// Hệ số phóng to UI theo độ phân giải (màn Retina).
    public static float Scale => Mathf.Max(1f, Mathf.Round(Screen.height / 720f * 2f) / 2f);

    public static void CloseAll()
    {
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) InventoryUI.Instance.Close();
    }
}
