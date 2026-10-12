using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// The one writer of the emoji font asset and of TMP Settings' emoji routing. The font file is the
// output of tools/emoji/fetch_emoji_font.py; the asset is a dynamic COLOR font asset whose atlas pages
// are cache, filled on first use at runtime and cleared here. Every TMP text reaches emoji through ONE
// route, TMP Settings' global fallback list; the emoji fallback list stays empty, and no component names
// an emoji font. (Measured: the global list alone resolves every single code point; the emoji list alone
// leaves 200 text-presentation emoji unresolved. A second route could only disagree with the first.)
// Sequences (ZWJ, skin tone, flag, keycap) resolve only through the emoji font's GSUB ligatures, and TMP applies
// them per component, gated by that component's font features: EnableLigatures writes `liga` into every
// serialized TMP text and into TMP Settings (so a text created later inherits it); there is no global switch.
public static class EmojiFont
{
    public const string FontPath = "Assets/Fonts/Emoji/Twemoji.ttf";
    public const string AssetPath = "Assets/Fonts/Emoji/Emoji.asset";

    // Colour outlines (COLRv0) are rasterised at one size; 109 is the strike size a CBDT set needs.
    const int SamplingPointSize = 64;
    const int AtlasPadding = 4;
    const int AtlasSize = 2048;

    // Menu entry for interactive use; -executeMethod EmojiFont.Build for batchmode, exit 1 on failure.
    [MenuItem("Aetheria/Text/Build Emoji Font")]
    public static void Build()
    {
        var ok = TryBuild(out var message);
        if (ok) Debug.Log(message); else Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool TryBuild(out string message)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            message = $"EmojiFont: {FontPath} is missing; run tools/emoji/fetch_emoji_font.py.";
            return false;
        }

        // An existing asset keeps its GUID, so a second Build changes nothing the settings point at.
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.COLOR,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (asset == null)
            {
                message = $"EmojiFont: TMP could not create a font asset from {FontPath}.";
                return false;
            }
            asset.name = "Emoji";
            AssetDatabase.CreateAsset(asset, AssetPath);
            foreach (var page in asset.atlasTextures)
            {
                page.name = "Emoji Atlas";
                AssetDatabase.AddObjectToAsset(page, asset);
            }
            asset.material.name = "Emoji Atlas Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        // The atlas pages are cache: Build leaves them empty, the same state EmojiCheck restores.
        asset.ClearFontAssetData(true);

        // One route: the global fallback. It also serves text-presentation emoji (U+263A without FE0F).
        TMP_Settings.emojiFallbackTextAssets = new List<TMP_Asset>();
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { asset };
        TMP_Settings.defaultSpriteAsset = null;

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();
        message = "EmojiFont: built " + AssetPath + " and made it TMP Settings' only emoji route (the global fallback).";
        return true;
    }

    // Menu entry for interactive use; -executeMethod EmojiFont.EnableLigatures for batchmode, exit 1 on failure.
    // Idempotent: a second run changes nothing and saves nothing.
    [MenuItem("Aetheria/Text/Enable Ligatures")]
    public static void EnableLigatures()
    {
        try
        {
            var settings = TMP_Settings.fontFeatures;
            var settingsChanged = Enable(settings);
            if (settingsChanged) EditorUtility.SetDirty(TMP_Settings.instance);
            var (files, components) = VisitSerializedTexts(text =>
            {
                var features = text.fontFeatures.ToList();
                if (!Enable(features, text.enableKerning)) return false;
                text.fontFeatures = features;
                EditorUtility.SetDirty(text);
                return true;
            });
            AssetDatabase.SaveAssets();
            Debug.Log($"EmojiFont: liga added to {components} TMP component(s) in {files} file(s)" +
                      (settingsChanged ? " and to TMP Settings." : "; TMP Settings already had it."));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("EmojiFont.EnableLigatures failed: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    // A legacy list ([0], no feature serialized) is first converted to [kern] when kerning is on, as TMP does on load.
    static bool Enable(List<OTL_FeatureTag> features, bool kerning = true)
    {
        if (features.Contains(OTL_FeatureTag.liga)) return false;
        features.RemoveAll(tag => tag == 0);
        if (kerning && !features.Contains(OTL_FeatureTag.kern)) features.Add(OTL_FeatureTag.kern);
        features.Add(OTL_FeatureTag.liga);
        return true;
    }

    // The one walk over every TMP text a serialized asset owns: each prefab and each scene under Assets/, skipping a
    // component that belongs to a prefab instance unless that instance overrides its font features (otherwise it
    // inherits from its source prefab, which is walked itself). `visit` returns whether it changed the component;
    // an asset that changed is saved. Returns the files and components changed.
    public static (int files, int components) VisitSerializedTexts(Func<TMP_Text, bool> visit)
    {
        int files = 0, components = 0;
        bool Owns(TMP_Text text) =>
            !PrefabUtility.IsPartOfPrefabInstance(text) ||
            new SerializedObject(text).FindProperty("m_ActiveFontFeatures")?.prefabOverride == true;
        int Visit(IEnumerable<TMP_Text> texts) => texts.Where(Owns).Count(visit);

        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed = Visit(root.GetComponentsInChildren<TMP_Text>(true));
                if (changed == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                files++;
                components += changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var changed = Visit(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)));
                if (changed == 0) continue;
                EditorSceneManager.SaveScene(scene);
                files++;
                components += changed;
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        return (files, components);
    }
}
