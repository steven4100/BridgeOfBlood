using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class SpriteAtlasBuilder
{
    private const string DatabasePath = "Assets/Rendering/SpriteRenderDB.asset";
    private const string AtlasPath = "Assets/Rendering/GeneratedAtlas.png";
    private const int AtlasPadding = 2;
    private const int MaxAtlasSize = 4096;

    [MenuItem("Tools/BridgeOfBlood/Rebuild Sprite Rendering Data")]
    public static void RebuildSpriteRenderingData()
    {
        var visuals = FindAllSpriteEntityVisuals();
        if (visuals.Count == 0)
        {
            Debug.LogWarning("SpriteAtlasBuilder: No SpriteEntityVisual assets found.");
            return;
        }

        var textures = ExtractReadableTextures(visuals, out var slotsPerVisual, out var importersToRevert);

        try
        {
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] uvRects = atlas.PackTextures(textures.ToArray(), AtlasPadding, MaxAtlasSize);

            SaveAtlasToDisk(atlas, AtlasPath);
            Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            ConfigureAtlasImportSettings(AtlasPath);
            var savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            var frames = BuildSpriteFrames(uvRects);
            var database = CreateOrLoadDatabase();
            database.atlas = savedAtlas;
            database.frames = frames;
            EditorUtility.SetDirty(database);

            AssignBakedFrameIndices(visuals, slotsPerVisual);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"SpriteAtlasBuilder: Packed {textures.Count} atlas slots from {visuals.Count} visuals into {savedAtlas.width}x{savedAtlas.height}.");
        }
        finally
        {
            RevertTextureImporters(importersToRevert);
        }
    }

    private static List<SpriteEntityVisual> FindAllSpriteEntityVisuals()
    {
        var result = new List<SpriteEntityVisual>();
        string[] guids = AssetDatabase.FindAssets("t:SpriteEntityVisual");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var visual = AssetDatabase.LoadAssetAtPath<SpriteEntityVisual>(path);
            if (visual != null && visual.sprite != null)
                result.Add(visual);
        }
        return result;
    }

    private static List<Texture2D> ExtractReadableTextures(
        List<SpriteEntityVisual> visuals,
        out List<int> slotsPerVisual,
        out List<(string path, bool wasReadable)> importersToRevert)
    {
        importersToRevert = new List<(string, bool)>();
        var textures = new List<Texture2D>();
        slotsPerVisual = new List<int>(visuals.Count);

        foreach (var visual in visuals)
        {
            Sprite sprite = visual.sprite;
            Texture2D srcTex = sprite.texture;
            string texPath = AssetDatabase.GetAssetPath(srcTex);
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;

            if (importer != null && !importer.isReadable)
            {
                importersToRevert.Add((texPath, false));
                importer.isReadable = true;
                importer.SaveAndReimport();
                srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                sprite = LoadSpriteFromAsset(visual);
            }

            int frameCount = Mathf.Max(1, visual.frameCount);
            if (frameCount <= 1)
            {
                textures.Add(CropSpriteRect(sprite));
                slotsPerVisual.Add(1);
            }
            else
            {
                Rect r = sprite.rect;
                int totalW = Mathf.FloorToInt(r.width);
                int cellW = totalW / frameCount;
                if (cellW <= 0)
                {
                    Debug.LogWarning(
                        $"SpriteAtlasBuilder: '{visual.name}' frameCount={frameCount} is too large for sprite width {totalW}; using single crop.");
                    textures.Add(CropSpriteRect(sprite));
                    slotsPerVisual.Add(1);
                    continue;
                }

                if (cellW * frameCount != totalW)
                {
                    Debug.LogWarning(
                        $"SpriteAtlasBuilder: '{visual.name}' sprite rect width {totalW} is not evenly divisible by frameCount {frameCount}; using floor cell width {cellW}.");
                }

                for (int f = 0; f < frameCount; f++)
                    textures.Add(CropSpriteHorizontalFrame(sprite, f, cellW));
                slotsPerVisual.Add(frameCount);
            }
        }

        return textures;
    }

    private static Sprite LoadSpriteFromAsset(SpriteEntityVisual visual)
    {
        string spritePath = AssetDatabase.GetAssetPath(visual.sprite);
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
        foreach (var asset in allAssets)
        {
            if (asset is Sprite s && s.name == visual.sprite.name)
                return s;
        }
        return visual.sprite;
    }

    /// <summary>
    /// Extracts the sprite's rect region into a standalone RGBA32 texture,
    /// handling sprites that are sub-rects of a larger sheet.
    /// </summary>
    private static Texture2D CropSpriteRect(Sprite sprite)
    {
        Rect r = sprite.rect;
        int x = Mathf.FloorToInt(r.x);
        int y = Mathf.FloorToInt(r.y);
        int w = Mathf.FloorToInt(r.width);
        int h = Mathf.FloorToInt(r.height);

        Color[] pixels = sprite.texture.GetPixels(x, y, w, h);
        var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
        cropped.SetPixels(pixels);
        cropped.Apply();
        return cropped;
    }

    private static Texture2D CropSpriteHorizontalFrame(Sprite sprite, int frameIndex, int cellWidth)
    {
        Rect r = sprite.rect;
        int x = Mathf.FloorToInt(r.x) + frameIndex * cellWidth;
        int y = Mathf.FloorToInt(r.y);
        int h = Mathf.FloorToInt(r.height);

        Color[] pixels = sprite.texture.GetPixels(x, y, cellWidth, h);
        var cropped = new Texture2D(cellWidth, h, TextureFormat.RGBA32, false);
        cropped.SetPixels(pixels);
        cropped.Apply();
        return cropped;
    }

    private static void SaveAtlasToDisk(Texture2D atlas, string path)
    {
        byte[] png = atlas.EncodeToPNG();
        string fullPath = Path.Combine(Application.dataPath, "..", path);
        fullPath = Path.GetFullPath(fullPath);
        string dir = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(fullPath, png);
    }

    private static void ConfigureAtlasImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static SpriteFrame[] BuildSpriteFrames(Rect[] uvRects)
    {
        var frames = new SpriteFrame[uvRects.Length];
        for (int i = 0; i < uvRects.Length; i++)
        {
            Rect r = uvRects[i];
            frames[i] = new SpriteFrame
            {
                uvMin = new float2(r.xMin, r.yMin),
                uvMax = new float2(r.xMax, r.yMax)
            };
        }
        return frames;
    }

    private static SpriteRenderDatabase CreateOrLoadDatabase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SpriteRenderDatabase>(DatabasePath);
        if (existing != null) return existing;

        var db = ScriptableObject.CreateInstance<SpriteRenderDatabase>();
        AssetDatabase.CreateAsset(db, DatabasePath);
        return db;
    }

    private static void AssignBakedFrameIndices(List<SpriteEntityVisual> visuals, List<int> slotsPerVisual)
    {
        int cursor = 0;
        for (int i = 0; i < visuals.Count; i++)
        {
            var v = visuals[i];
            v.bakedFrameIndex = cursor;
            cursor += slotsPerVisual[i];
            EditorUtility.SetDirty(v);
        }
    }

    private static void RevertTextureImporters(List<(string path, bool wasReadable)> importers)
    {
        foreach (var (path, wasReadable) in importers)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.isReadable = wasReadable;
            importer.SaveAndReimport();
        }
    }
}
