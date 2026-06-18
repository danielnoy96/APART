using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PrefabSpriteRecolorTool : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/Generated/RecoloredSprites";
    private const int CustomSpriteAlignment = 9;

    private enum ApplyMode
    {
        SelectedObjectOrSceneInstance,
        PrefabAsset
    }

    private enum RecolorMode
    {
        HueSaturationLightness,
        ReplaceScannedColors
    }

    private enum HslTargetRange
    {
        Master,
        Reds,
        Yellows,
        Greens,
        Cyans,
        Blues,
        Magentas
    }

    [SerializeField] private GameObject targetRoot;
    [SerializeField] private RecolorMode recolorMode = RecolorMode.HueSaturationLightness;
    [SerializeField] private HslTargetRange hslTargetRange = HslTargetRange.Master;
    [SerializeField] private ApplyMode applyMode = ApplyMode.SelectedObjectOrSceneInstance;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private float hueShift;
    [SerializeField] private float saturationShift;
    [SerializeField] private float lightnessShift;
    [SerializeField] private bool colorize;
    [SerializeField] private float colorizeHue;
    [SerializeField] private int colorTolerance = 8;
    [SerializeField] private int maxDisplayedColors = 32;
    [SerializeField] private bool includeInactive = true;

    private readonly List<ColorGroup> colorGroups = new List<ColorGroup>();
    private readonly List<SpriteRenderer> scannedRenderers = new List<SpriteRenderer>();
    private readonly HueRangeInfo[] hueRangeInfos = CreateHueRangeInfos();
    private Vector2 scroll;
    private string scanSummary = "Select a prefab asset or scene object, then scan.";
    private string hslRangeSummary = "Click Scan HSL Ranges to show which color ranges exist in this prefab.";
    private bool scanHasHiddenGroups;
    private int totalFoundGroups;

    [MenuItem("Tools/APART/Prefab Sprite Recolor Tool")]
    public static void Open()
    {
        GetWindow<PrefabSpriteRecolorTool>("Sprite Recolor");
    }

    private void OnEnable()
    {
        if (targetRoot == null && Selection.activeGameObject != null)
        {
            targetRoot = Selection.activeGameObject;
        }
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        bool scanRequested = false;
        bool hslRangeScanRequested = false;
        bool resetRequested = false;
        bool applyRequested = false;

        EditorGUILayout.LabelField("Prefab Sprite Recolor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates duplicate PNG sprite assets with adjusted pixel colors, then assigns those generated sprites. Original source textures are not overwritten.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        targetRoot = (GameObject)EditorGUILayout.ObjectField("Target", targetRoot, typeof(GameObject), true);
        if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
        {
            targetRoot = Selection.activeGameObject;
            ClearScan();
        }
        EditorGUILayout.EndHorizontal();

        recolorMode = (RecolorMode)EditorGUILayout.EnumPopup("Recolor Mode", recolorMode);
        applyMode = (ApplyMode)EditorGUILayout.EnumPopup("Apply To", applyMode);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        if (recolorMode == RecolorMode.HueSaturationLightness)
        {
            hslRangeScanRequested = DrawHueSaturationLightnessControls();
            if (hslRangeScanRequested)
            {
                RunEditorAction(ScanHueRanges);
            }
        }
        else
        {
            colorTolerance = EditorGUILayout.IntSlider("Color Tolerance", colorTolerance, 0, 64);
            maxDisplayedColors = EditorGUILayout.IntSlider("Max Swatches", maxDisplayedColors, 4, 128);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(targetRoot == null))
            {
                if (GUILayout.Button("Scan Sprite Colors", GUILayout.Height(28f)))
                {
                    scanRequested = true;
                }
            }

            using (new EditorGUI.DisabledScope(colorGroups.Count == 0))
            {
                if (GUILayout.Button("Reset Replacements", GUILayout.Height(28f)))
                {
                    resetRequested = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (scanRequested)
            {
                RunEditorAction(ScanColors);
            }

            if (resetRequested)
            {
                ResetReplacements();
            }

            EditorGUILayout.LabelField(scanSummary, EditorStyles.wordWrappedMiniLabel);

            if (scanHasHiddenGroups)
            {
                EditorGUILayout.HelpBox(
                    $"Found {totalFoundGroups} grouped colors. Showing the {colorGroups.Count} most common colors. Increase Max Swatches or Color Tolerance if needed.",
                    MessageType.Warning);
            }

            DrawColorGroups();
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!CanApply()))
        {
            if (GUILayout.Button("Generate Recolored Sprites And Apply", GUILayout.Height(32f)))
            {
                applyRequested = true;
            }
        }

        if (applyRequested)
        {
            RunEditorAction(ApplyRecolor);
        }
    }

    private bool DrawHueSaturationLightnessControls()
    {
        bool scanRequested = false;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hue/Saturation/Lightness", EditorStyles.boldLabel);
        hslTargetRange = (HslTargetRange)EditorGUILayout.EnumPopup("Target Colors", hslTargetRange);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan HSL Ranges", GUILayout.Height(24f)))
        {
            scanRequested = true;
        }

        if (GUILayout.Button("Master", GUILayout.Width(80f), GUILayout.Height(24f)))
        {
            hslTargetRange = HslTargetRange.Master;
        }
        EditorGUILayout.EndHorizontal();

        DrawHueRangeButtons();

        hueShift = EditorGUILayout.Slider("Hue Shift", hueShift, -180f, 180f);
        saturationShift = EditorGUILayout.Slider("Saturation", saturationShift, -100f, 100f);
        lightnessShift = EditorGUILayout.Slider("Lightness", lightnessShift, -100f, 100f);
        colorize = EditorGUILayout.Toggle("Colorize", colorize);
        if (colorize)
        {
            colorizeHue = EditorGUILayout.Slider("Colorize Hue", colorizeHue, 0f, 360f);
        }

        EditorGUILayout.HelpBox(
            "Target Colors controls which hue range changes. Master changes all colors. Range scan is only for choosing and previewing ranges; it is not required before applying.",
            MessageType.None);

        return scanRequested;
    }

    private void DrawHueRangeButtons()
    {
        EditorGUILayout.LabelField(hslRangeSummary, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < hueRangeInfos.Length; i++)
        {
            HslTargetRange range = (HslTargetRange)i;
            HueRangeInfo info = hueRangeInfos[i];
            Color source = info.HasPixels ? info.SourceColor : GetDefaultRangeColor(range);
            Color adjusted = ApplyHueSaturationLightnessPreview(source, range);
            EditorGUILayout.BeginVertical(GUILayout.Width(54f));
            Color previousBackgroundColor = GUI.backgroundColor;
            if (hslTargetRange == range)
            {
                GUI.backgroundColor = new Color(0.75f, 0.9f, 1f, 1f);
            }

            if (GUILayout.Button(GetRangeShortName(range), EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                hslTargetRange = range;
            }

            GUI.backgroundColor = previousBackgroundColor;

            EditorGUILayout.BeginHorizontal(GUILayout.Width(52f));
            Rect sourceRect = GUILayoutUtility.GetRect(22f, 16f, GUILayout.Width(22f), GUILayout.Height(16f));
            EditorGUI.DrawRect(sourceRect, source);
            Rect adjustedRect = GUILayoutUtility.GetRect(22f, 16f, GUILayout.Width(22f), GUILayout.Height(16f));
            EditorGUI.DrawRect(adjustedRect, adjusted);
            EditorGUILayout.EndHorizontal();

            string countText = range == HslTargetRange.Master ? "all" : info.PixelCount.ToString();
            EditorGUILayout.LabelField(countText, EditorStyles.miniLabel, GUILayout.Width(52f));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ScanHueRanges()
    {
        ResetHueRangeInfos();

        if (targetRoot == null)
        {
            hslRangeSummary = "No target selected.";
            return;
        }

        SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive);
        Dictionary<Texture2D, TextureReadData> textureCache = new Dictionary<Texture2D, TextureReadData>();
        int spriteCount = 0;
        int skippedSprites = 0;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            spriteCount++;
            if (!TryGetSpritePixels(renderer.sprite, textureCache, out Color32[] pixels, out _, out _))
            {
                skippedSprites++;
                continue;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a == 0)
                {
                    continue;
                }

                Color color = new Color(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f, 1f);
                RgbToHsl(color, out float hue, out float saturation, out _);
                hueRangeInfos[(int)HslTargetRange.Master].Add(pixel);

                if (saturation <= 0.05f)
                {
                    continue;
                }

                HslTargetRange range = GetNearestHueRange(hue);
                hueRangeInfos[(int)range].Add(pixel);
            }
        }

        for (int i = 0; i < hueRangeInfos.Length; i++)
        {
            hueRangeInfos[i].FinalizeSourceColor();
        }

        hslRangeSummary = $"Scanned {spriteCount} sprites. Color range counts shown under each swatch.";
        if (skippedSprites > 0)
        {
            hslRangeSummary += $" Skipped {skippedSprites} sprites that could not be read.";
        }

        Repaint();
    }

    private bool CanApply()
    {
        if (targetRoot == null)
        {
            return false;
        }

        return recolorMode == RecolorMode.HueSaturationLightness || colorGroups.Count > 0;
    }

    private void RunEditorAction(Action action)
    {
        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Sprite Recolor", exception.Message, "OK");
        }
    }

    private void DrawColorGroups()
    {
        if (colorGroups.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < colorGroups.Count; i++)
        {
            ColorGroup group = colorGroups[i];
            EditorGUILayout.BeginHorizontal();
            Rect sourceRect = GUILayoutUtility.GetRect(32f, 18f, GUILayout.Width(32f), GUILayout.Height(18f));
            EditorGUI.DrawRect(sourceRect, group.SourceColor);
            EditorGUILayout.LabelField($"{group.PixelCount} px", GUILayout.Width(75f));
            group.ReplacementColor = EditorGUILayout.ColorField(GUIContent.none, group.ReplacementColor, false, false, false);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ScanColors()
    {
        colorGroups.Clear();
        scannedRenderers.Clear();
        scanHasHiddenGroups = false;
        totalFoundGroups = 0;

        if (targetRoot == null)
        {
            scanSummary = "No target selected.";
            return;
        }

        SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive);
        Dictionary<Texture2D, TextureReadData> textureCache = new Dictionary<Texture2D, TextureReadData>();
        List<ColorGroup> groups = new List<ColorGroup>();
        int spriteCount = 0;
        int skippedSprites = 0;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            scannedRenderers.Add(renderer);
            spriteCount++;

            if (!TryGetSpritePixels(renderer.sprite, textureCache, out Color32[] pixels, out _, out _))
            {
                skippedSprites++;
                continue;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a == 0)
                {
                    continue;
                }

                AddToNearestGroup(groups, pixel, colorTolerance);
            }
        }

        groups.Sort((a, b) => b.PixelCount.CompareTo(a.PixelCount));
        totalFoundGroups = groups.Count;
        int displayCount = Mathf.Min(maxDisplayedColors, groups.Count);
        for (int i = 0; i < displayCount; i++)
        {
            groups[i].FinalizeSourceColor();
            groups[i].ReplacementColor = groups[i].SourceColor;
            colorGroups.Add(groups[i]);
        }

        scanHasHiddenGroups = groups.Count > colorGroups.Count;
        scanSummary = $"Scanned {spriteCount} sprites from {renderers.Length} SpriteRenderers. Found {groups.Count} grouped colors.";
        if (skippedSprites > 0)
        {
            scanSummary += $" Skipped {skippedSprites} sprites that could not be read.";
        }

        Repaint();
    }

    private void ApplyRecolor()
    {
        if (targetRoot == null)
        {
            EditorUtility.DisplayDialog("Sprite Recolor", "Select a target first.", "OK");
            return;
        }

        if (!HasMeaningfulChange())
        {
            bool continueAnyway = EditorUtility.DisplayDialog(
                "Sprite Recolor",
                "No color settings differ from the source. Generate duplicate sprites anyway?",
                "Generate",
                "Cancel");

            if (!continueAnyway)
            {
                return;
            }
        }

        string normalizedOutputFolder = NormalizeAssetFolder(outputFolder);
        if (string.IsNullOrEmpty(normalizedOutputFolder))
        {
            EditorUtility.DisplayDialog("Sprite Recolor", "Output folder must be inside Assets.", "OK");
            return;
        }

        try
        {
            EnsureAssetFolder(normalizedOutputFolder);

            int changedRenderers;
            if (applyMode == ApplyMode.PrefabAsset)
            {
                changedRenderers = ApplyToPrefabAsset(normalizedOutputFolder);
            }
            else
            {
                changedRenderers = ApplyToObject(targetRoot, normalizedOutputFolder);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Sprite Recolor",
                $"Generated recolored sprites and updated {changedRenderers} SpriteRenderers.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Sprite Recolor", exception.Message, "OK");
        }
    }

    private int ApplyToPrefabAsset(string normalizedOutputFolder)
    {
        string prefabPath = GetPrefabAssetPath(targetRoot);
        if (string.IsNullOrEmpty(prefabPath))
        {
            throw new InvalidOperationException("Apply To Prefab Asset requires selecting a prefab asset or a prefab instance.");
        }

        GameObject loadedRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int changedRenderers = ApplyToObject(loadedRoot, normalizedOutputFolder);
            PrefabUtility.SaveAsPrefabAsset(loadedRoot, prefabPath);
            return changedRenderers;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(loadedRoot);
        }
    }

    private int ApplyToObject(GameObject root, string normalizedOutputFolder)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive);
        Dictionary<Texture2D, TextureReadData> textureCache = new Dictionary<Texture2D, TextureReadData>();
        Dictionary<Sprite, Sprite> generatedSprites = new Dictionary<Sprite, Sprite>();
        int changedRenderers = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            Sprite originalSprite = renderer.sprite;
            if (!generatedSprites.TryGetValue(originalSprite, out Sprite recoloredSprite))
            {
                recoloredSprite = GenerateRecoloredSprite(root.name, originalSprite, normalizedOutputFolder, textureCache);
                generatedSprites.Add(originalSprite, recoloredSprite);
            }

            if (recoloredSprite == null)
            {
                continue;
            }

            Undo.RecordObject(renderer, "Apply recolored sprite");
            renderer.sprite = recoloredSprite;
            EditorUtility.SetDirty(renderer);
            changedRenderers++;
        }

        if (root.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        return changedRenderers;
    }

    private Sprite GenerateRecoloredSprite(
        string targetName,
        Sprite sourceSprite,
        string normalizedOutputFolder,
        Dictionary<Texture2D, TextureReadData> textureCache)
    {
        if (!TryGetSpritePixels(sourceSprite, textureCache, out Color32[] pixels, out int width, out int height))
        {
            Debug.LogWarning($"Could not read sprite pixels for {sourceSprite.name}.", sourceSprite);
            return null;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
            {
                continue;
            }

            if (recolorMode == RecolorMode.ReplaceScannedColors && TryGetReplacement(pixels[i], out Color32 replacement))
            {
                pixels[i] = new Color32(replacement.r, replacement.g, replacement.b, pixels[i].a);
            }
            else if (recolorMode == RecolorMode.HueSaturationLightness)
            {
                pixels[i] = ApplyHueSaturationLightness(pixels[i]);
            }
        }

        Texture2D generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        generatedTexture.SetPixels32(pixels);
        generatedTexture.Apply();

        byte[] png = generatedTexture.EncodeToPNG();
        DestroyImmediate(generatedTexture);

        string targetFolder = $"{normalizedOutputFolder}/{SanitizeFileName(targetName)}";
        EnsureAssetFolder(targetFolder);

        string outputPath = GetUniqueSpritePath(targetFolder, sourceSprite);
        File.WriteAllBytes(AssetPathToFullPath(outputPath), png);
        AssetDatabase.ImportAsset(outputPath);

        ConfigureGeneratedSpriteImporter(outputPath, sourceSprite);
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

        Sprite generatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        if (generatedSprite == null)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(outputPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    generatedSprite = sprite;
                    break;
                }
            }
        }

        return generatedSprite;
    }

    private void ConfigureGeneratedSpriteImporter(string outputPath, Sprite sourceSprite)
    {
        TextureImporter sourceImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sourceSprite.texture)) as TextureImporter;
        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = sourceImporter != null && sourceImporter.mipmapEnabled;
        importer.filterMode = sourceSprite.texture.filterMode;
        importer.wrapMode = sourceSprite.texture.wrapMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = sourceSprite.pixelsPerUnit;
        importer.spritePivot = new Vector2(
            sourceSprite.pivot.x / Mathf.Max(1f, sourceSprite.rect.width),
            sourceSprite.pivot.y / Mathf.Max(1f, sourceSprite.rect.height));
        importer.spriteBorder = sourceSprite.border;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = CustomSpriteAlignment;
        importer.SetTextureSettings(settings);
    }

    private bool TryGetReplacement(Color32 pixel, out Color32 replacement)
    {
        int bestDistance = int.MaxValue;
        Color32 bestColor = default;
        bool hasMatch = false;

        for (int i = 0; i < colorGroups.Count; i++)
        {
            Color32 source = colorGroups[i].SourceColor32;
            int distance = ColorDistance(pixel, source);
            if (distance > colorTolerance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestColor = ColorToColor32(colorGroups[i].ReplacementColor);
            hasMatch = true;
        }

        replacement = bestColor;
        return hasMatch;
    }

    private Color32 ApplyHueSaturationLightness(Color32 pixel)
    {
        Color color = new Color(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f, pixel.a / 255f);
        RgbToHsl(color, out float hue, out float saturation, out float lightness);
        float influence = GetRangeInfluence(hue, saturation, hslTargetRange);
        if (influence <= 0f)
        {
            return pixel;
        }

        if (colorize)
        {
            hue = Wrap01(colorizeHue / 360f);
        }
        else
        {
            hue = Wrap01(hue + hueShift / 360f);
        }

        saturation = AdjustUnit(saturation, saturationShift / 100f);
        lightness = AdjustUnit(lightness, lightnessShift / 100f);

        Color adjusted = HslToRgb(hue, saturation, lightness);
        Color blended = Color.Lerp(color, adjusted, influence);
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(blended.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(blended.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(blended.b) * 255f),
            pixel.a);
    }

    private Color ApplyHueSaturationLightnessPreview(Color source, HslTargetRange previewRange)
    {
        if (hslTargetRange != HslTargetRange.Master && hslTargetRange != previewRange)
        {
            return source;
        }

        Color32 source32 = ColorToColor32(source);
        Color32 adjusted = ApplyHueSaturationLightness(source32);
        return new Color(adjusted.r / 255f, adjusted.g / 255f, adjusted.b / 255f, 1f);
    }

    private static void RgbToHsl(Color color, out float hue, out float saturation, out float lightness)
    {
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        float delta = max - min;

        hue = 0f;
        lightness = (max + min) * 0.5f;

        if (Mathf.Approximately(delta, 0f))
        {
            saturation = 0f;
            return;
        }

        saturation = delta / (1f - Mathf.Abs(2f * lightness - 1f));

        if (Mathf.Approximately(max, color.r))
        {
            hue = ((color.g - color.b) / delta) % 6f;
        }
        else if (Mathf.Approximately(max, color.g))
        {
            hue = ((color.b - color.r) / delta) + 2f;
        }
        else
        {
            hue = ((color.r - color.g) / delta) + 4f;
        }

        hue = Wrap01(hue / 6f);
    }

    private static Color HslToRgb(float hue, float saturation, float lightness)
    {
        hue = Wrap01(hue);
        saturation = Mathf.Clamp01(saturation);
        lightness = Mathf.Clamp01(lightness);

        float chroma = (1f - Mathf.Abs(2f * lightness - 1f)) * saturation;
        float huePrime = hue * 6f;
        float x = chroma * (1f - Mathf.Abs(huePrime % 2f - 1f));
        float r = 0f;
        float g = 0f;
        float b = 0f;

        if (huePrime < 1f)
        {
            r = chroma;
            g = x;
        }
        else if (huePrime < 2f)
        {
            r = x;
            g = chroma;
        }
        else if (huePrime < 3f)
        {
            g = chroma;
            b = x;
        }
        else if (huePrime < 4f)
        {
            g = x;
            b = chroma;
        }
        else if (huePrime < 5f)
        {
            r = x;
            b = chroma;
        }
        else
        {
            r = chroma;
            b = x;
        }

        float match = lightness - chroma * 0.5f;
        return new Color(r + match, g + match, b + match, 1f);
    }

    private static float AdjustUnit(float value, float adjustment)
    {
        value = Mathf.Clamp01(value);
        adjustment = Mathf.Clamp(adjustment, -1f, 1f);

        if (adjustment >= 0f)
        {
            return Mathf.Clamp01(value + (1f - value) * adjustment);
        }

        return Mathf.Clamp01(value * (1f + adjustment));
    }

    private static float Wrap01(float value)
    {
        value %= 1f;
        if (value < 0f)
        {
            value += 1f;
        }

        return value;
    }

    private static float GetRangeInfluence(float hue, float saturation, HslTargetRange targetRange)
    {
        if (targetRange == HslTargetRange.Master)
        {
            return 1f;
        }

        if (saturation <= 0.05f)
        {
            return 0f;
        }

        float center = GetRangeHueDegrees(targetRange);
        float distance = HueDistanceDegrees(hue * 360f, center);
        const float fullInfluenceDegrees = 30f;
        const float featherEndDegrees = 45f;

        if (distance <= fullInfluenceDegrees)
        {
            return 1f;
        }

        if (distance >= featherEndDegrees)
        {
            return 0f;
        }

        return Mathf.InverseLerp(featherEndDegrees, fullInfluenceDegrees, distance);
    }

    private static HslTargetRange GetNearestHueRange(float hue)
    {
        HslTargetRange nearest = HslTargetRange.Reds;
        float nearestDistance = float.MaxValue;
        for (int i = (int)HslTargetRange.Reds; i <= (int)HslTargetRange.Magentas; i++)
        {
            HslTargetRange range = (HslTargetRange)i;
            float distance = HueDistanceDegrees(hue * 360f, GetRangeHueDegrees(range));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = range;
            }
        }

        return nearest;
    }

    private static float GetRangeHueDegrees(HslTargetRange range)
    {
        switch (range)
        {
            case HslTargetRange.Yellows:
                return 60f;
            case HslTargetRange.Greens:
                return 120f;
            case HslTargetRange.Cyans:
                return 180f;
            case HslTargetRange.Blues:
                return 240f;
            case HslTargetRange.Magentas:
                return 300f;
            default:
                return 0f;
        }
    }

    private static float HueDistanceDegrees(float a, float b)
    {
        float distance = Mathf.Abs(Mathf.Repeat(a - b + 180f, 360f) - 180f);
        return distance;
    }

    private static Color GetDefaultRangeColor(HslTargetRange range)
    {
        if (range == HslTargetRange.Master)
        {
            return Color.white;
        }

        return HslToRgb(GetRangeHueDegrees(range) / 360f, 1f, 0.5f);
    }

    private static string GetRangeShortName(HslTargetRange range)
    {
        switch (range)
        {
            case HslTargetRange.Master:
                return "All";
            case HslTargetRange.Yellows:
                return "Yellow";
            case HslTargetRange.Greens:
                return "Green";
            case HslTargetRange.Cyans:
                return "Cyan";
            case HslTargetRange.Blues:
                return "Blue";
            case HslTargetRange.Magentas:
                return "Mag";
            default:
                return "Red";
        }
    }

    private static HueRangeInfo[] CreateHueRangeInfos()
    {
        int count = Enum.GetValues(typeof(HslTargetRange)).Length;
        HueRangeInfo[] infos = new HueRangeInfo[count];
        for (int i = 0; i < count; i++)
        {
            infos[i] = new HueRangeInfo((HslTargetRange)i);
        }

        return infos;
    }

    private void ResetHueRangeInfos()
    {
        for (int i = 0; i < hueRangeInfos.Length; i++)
        {
            hueRangeInfos[i].Reset();
        }
    }

    private static bool TryGetSpritePixels(
        Sprite sprite,
        Dictionary<Texture2D, TextureReadData> textureCache,
        out Color32[] pixels,
        out int width,
        out int height)
    {
        pixels = null;
        width = 0;
        height = 0;

        if (sprite == null || sprite.texture == null)
        {
            return false;
        }

        if (!textureCache.TryGetValue(sprite.texture, out TextureReadData data))
        {
            if (!TryReadTexture(sprite.texture, out data))
            {
                return false;
            }

            textureCache.Add(sprite.texture, data);
        }

        Rect rect = sprite.rect;
        int x = Mathf.RoundToInt(rect.x);
        int y = Mathf.RoundToInt(rect.y);
        width = Mathf.RoundToInt(rect.width);
        height = Mathf.RoundToInt(rect.height);

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        pixels = new Color32[width * height];
        for (int row = 0; row < height; row++)
        {
            int sourceY = y + row;
            for (int column = 0; column < width; column++)
            {
                int sourceX = x + column;
                int destinationIndex = row * width + column;
                if (sourceX < 0 || sourceX >= data.Width || sourceY < 0 || sourceY >= data.Height)
                {
                    pixels[destinationIndex] = new Color32(0, 0, 0, 0);
                    continue;
                }

                pixels[destinationIndex] = data.Pixels[sourceY * data.Width + sourceX];
            }
        }

        return true;
    }

    private static bool TryReadTexture(Texture2D texture, out TextureReadData data)
    {
        data = default;

        try
        {
            data = new TextureReadData(texture.GetPixels32(), texture.width, texture.height);
            return true;
        }
        catch (Exception)
        {
            return TryReadTextureWithTemporaryImporterChange(texture, out data) || TryReadTextureWithRenderTexture(texture, out data);
        }
    }

    private static bool TryReadTextureWithTemporaryImporterChange(Texture2D texture, out TextureReadData data)
    {
        data = default;

        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return false;
        }

        bool previousReadable = importer.isReadable;
        if (previousReadable)
        {
            return false;
        }

        try
        {
            importer.isReadable = true;
            importer.SaveAndReimport();

            Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            data = new TextureReadData(readableTexture.GetPixels32(), readableTexture.width, readableTexture.height);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not temporarily read texture {texture.name}: {exception.Message}", texture);
            return false;
        }
        finally
        {
            importer.isReadable = previousReadable;
            importer.SaveAndReimport();
        }
    }

    private static bool TryReadTextureWithRenderTexture(Texture2D texture, out TextureReadData data)
    {
        data = default;

        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Texture2D readable = null;

        try
        {
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            data = new TextureReadData(readable.GetPixels32(), readable.width, readable.height);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read texture {texture.name}: {exception.Message}", texture);
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null)
            {
                DestroyImmediate(readable);
            }
        }
    }

    private static void AddToNearestGroup(List<ColorGroup> groups, Color32 color, int tolerance)
    {
        int bestIndex = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < groups.Count; i++)
        {
            int distance = ColorDistance(color, groups[i].SourceColor32);
            if (distance > tolerance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = i;
        }

        if (bestIndex >= 0)
        {
            groups[bestIndex].Add(color);
        }
        else
        {
            groups.Add(new ColorGroup(color));
        }
    }

    private static int ColorDistance(Color32 a, Color32 b)
    {
        int r = Mathf.Abs(a.r - b.r);
        int g = Mathf.Abs(a.g - b.g);
        int bDistance = Mathf.Abs(a.b - b.b);
        return Mathf.Max(r, Mathf.Max(g, bDistance));
    }

    private bool HasMeaningfulChange()
    {
        if (recolorMode == RecolorMode.HueSaturationLightness)
        {
            return !Mathf.Approximately(hueShift, 0f)
                || !Mathf.Approximately(saturationShift, 0f)
                || !Mathf.Approximately(lightnessShift, 0f)
                || colorize;
        }

        return HasChangedReplacement();
    }

    private bool HasChangedReplacement()
    {
        for (int i = 0; i < colorGroups.Count; i++)
        {
            if (ColorDistance(colorGroups[i].SourceColor32, ColorToColor32(colorGroups[i].ReplacementColor)) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetReplacements()
    {
        for (int i = 0; i < colorGroups.Count; i++)
        {
            colorGroups[i].ReplacementColor = colorGroups[i].SourceColor;
        }
    }

    private void ClearScan()
    {
        colorGroups.Clear();
        scannedRenderers.Clear();
        scanSummary = "Selection changed. Scan again.";
        hslRangeSummary = "Click Scan HSL Ranges to show which color ranges exist in this prefab.";
        ResetHueRangeInfos();
        scanHasHiddenGroups = false;
        totalFoundGroups = 0;
    }

    private static Color32 ColorToColor32(Color color)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
            255);
    }

    private static string GetPrefabAssetPath(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        string directPath = AssetDatabase.GetAssetPath(target);
        if (!string.IsNullOrEmpty(directPath) && directPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return directPath;
        }

        return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
    }

    private static string GetUniqueSpritePath(string targetFolder, Sprite sourceSprite)
    {
        string guid = "sprite";
        long localId = 0;
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceSprite, out string assetGuid, out long assetLocalId))
        {
            guid = assetGuid.Length > 8 ? assetGuid.Substring(0, 8) : assetGuid;
            localId = assetLocalId;
        }

        string baseName = $"{SanitizeFileName(sourceSprite.name)}_{guid}_{localId}";
        string path = $"{targetFolder}/{baseName}.png";
        return AssetDatabase.GenerateUniqueAssetPath(path);
    }

    private static string NormalizeAssetFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        if (normalized != "Assets" && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return null;
        }

        if (normalized.Contains("/../") || normalized.EndsWith("/..", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal))
        {
            return null;
        }

        return normalized;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string normalized = NormalizeAssetFolder(assetFolder);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new InvalidOperationException("Generated sprite folder must be inside Assets.");
        }

        string fullPath = AssetPathToFullPath(normalized);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        AssetDatabase.Refresh();
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "RecoloredSprite";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = value;
        for (int i = 0; i < invalid.Length; i++)
        {
            sanitized = sanitized.Replace(invalid[i], '_');
        }

        return sanitized.Replace(' ', '_');
    }

    private struct TextureReadData
    {
        public readonly Color32[] Pixels;
        public readonly int Width;
        public readonly int Height;

        public TextureReadData(Color32[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }
    }

    private class HueRangeInfo
    {
        private readonly HslTargetRange range;
        private int pixelCount;
        private long r;
        private long g;
        private long b;
        private Color sourceColor;

        public int PixelCount => pixelCount;
        public bool HasPixels => pixelCount > 0;
        public Color SourceColor => sourceColor;

        public HueRangeInfo(HslTargetRange range)
        {
            this.range = range;
            Reset();
        }

        public void Reset()
        {
            pixelCount = 0;
            r = 0;
            g = 0;
            b = 0;
            sourceColor = GetDefaultRangeColor(range);
        }

        public void Add(Color32 color)
        {
            pixelCount++;
            r += color.r;
            g += color.g;
            b += color.b;
        }

        public void FinalizeSourceColor()
        {
            if (pixelCount <= 0)
            {
                sourceColor = GetDefaultRangeColor(range);
                return;
            }

            sourceColor = new Color(
                (r / (float)pixelCount) / 255f,
                (g / (float)pixelCount) / 255f,
                (b / (float)pixelCount) / 255f,
                1f);
        }
    }

    [Serializable]
    private class ColorGroup
    {
        [SerializeField] private Color sourceColor;
        [SerializeField] private Color32 sourceColor32;
        [SerializeField] private int pixelCount;
        [SerializeField] private long r;
        [SerializeField] private long g;
        [SerializeField] private long b;

        public Color ReplacementColor;

        public Color SourceColor => sourceColor;
        public Color32 SourceColor32 => sourceColor32;
        public int PixelCount => pixelCount;

        public ColorGroup(Color32 initialColor)
        {
            ReplacementColor = initialColor;
            sourceColor = initialColor;
            sourceColor32 = initialColor;
            Add(initialColor);
        }

        public void Add(Color32 color)
        {
            pixelCount++;
            r += color.r;
            g += color.g;
            b += color.b;
            FinalizeSourceColor();
        }

        public void FinalizeSourceColor()
        {
            if (pixelCount <= 0)
            {
                return;
            }

            sourceColor32 = new Color32(
                (byte)(r / pixelCount),
                (byte)(g / pixelCount),
                (byte)(b / pixelCount),
                255);
            sourceColor = sourceColor32;

            if (pixelCount == 1)
            {
                ReplacementColor = sourceColor;
            }
        }
    }
}
