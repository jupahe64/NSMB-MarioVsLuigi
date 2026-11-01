using UnityEditor;

namespace NsmbDs.Editor {
    /// <summary>Keeps extracted tile atlases pixel exact.</summary>
    public sealed class NsmbTextureImportRules : AssetPostprocessor {
        private void OnPreprocessTexture() {
            if (!assetPath.StartsWith(NsmbImporter.ExtractedRoot)) {
                return;
            }

            TextureImporter importer = (TextureImporter) assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = NsmbUnits.PixelsPerUnit;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            // Backdrop layers are drawn tiled, which needs a full-rect sprite mesh.
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
