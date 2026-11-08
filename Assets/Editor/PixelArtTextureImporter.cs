using UnityEditor;
using UnityEngine;

/// Tự động set import settings kiểu Minecraft (pixel art sắc nét)
/// cho mọi texture nằm trong Assets/Textures/
public class PixelArtTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;          // không blur pixel
        importer.mipmapEnabled = false;                   // tránh viền lem giữa các ô trong atlas
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;                       // cho phép đọc pixel từ script nếu cần
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
    }
}
