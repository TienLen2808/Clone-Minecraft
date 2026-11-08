using UnityEngine;

/// Toàn bộ asset game cần lúc chạy. File Assets/Generated/GameAssets.asset được tạo bởi menu Minecraft > Setup World Scene.
[CreateAssetMenu(menuName = "Minecraft/Game Assets")]
public class GameAssets : ScriptableObject
{
    public static GameAssets Instance;

    [Tooltip("Phiên bản dữ liệu - Editor tự dựng lại khi code yêu cầu bản mới hơn")]
    public int dataVersion;

    [Header("Blocks")]
    public Texture2DArray blockTextures;
    public Material voxelMaterial;
    public Material outlineMaterial;
    public Material crackMaterial;          // URP Unlit trong suốt, đổi _BaseMap theo destroyStages
    public Texture2D[] destroyStages;       // 10 ảnh vết nứt

    [Header("Items")]
    public Material itemSpriteMaterial;     // template URP Unlit cutout cho item rơi dạng sprite
    public Texture2D[] itemIcons;           // index = (int)ItemId

    [Header("Entities")]
    public Material mobCutoutMaterial;      // template URP Lit alpha clip
    public Material mobOpaqueMaterial;      // template URP Lit (texture .tga dùng alpha làm mask)
    public Texture2D steveSkin;
    public Texture2D pig, cow, sheep, chicken, zombie, wolf, wolfTame, wolfAngry;
    public Texture2D spider, creeper, skeleton, polarBear;

    [Header("Weather")]
    public Material rainMaterial;           // URP Particles/Unlit trong suốt

    [Header("HUD")]
    public Texture2D heartFull, heartHalf, heartEmpty;
    public Texture2D hungerFull, hungerHalf, hungerEmpty;

    void OnEnable()
    {
        // Asset được scene tham chiếu sẽ load trước mọi Awake -> các script dùng được ngay
        if (Instance == null) Instance = this;
    }

    public Texture2D Icon(ItemId id)
    {
        int i = (int)id;
        return itemIcons != null && i >= 0 && i < itemIcons.Length ? itemIcons[i] : null;
    }
}
