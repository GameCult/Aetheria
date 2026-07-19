using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using NUnit.Framework;

public sealed class AetheriaRuntimeAssetBundleLayoutTests
{
    [Test]
    public void SharedPresentationAssetsHaveStableBundleOwners()
    {
        var manifest = AetheriaRuntimeAssets.ProjectManifest(null);

        Assert.AreEqual(AetheriaRuntimeAssets.UnityShaderBundle, BundleFor("shader.environment.gravity-fog"));
        Assert.AreEqual(AetheriaRuntimeAssets.UnityShaderBundle, BundleFor("texture.environment.volume-dither"));
        Assert.AreEqual(AetheriaRuntimeAssets.UnityUiBundle, BundleFor("map.entity.player"));
        Assert.AreEqual(AetheriaRuntimeAssets.UnityUiBundle, BundleFor("font.ui.primary"));
        Assert.AreEqual(AetheriaRuntimeAssets.UnityCoreBundle, BundleFor("texture.environment.reflection"));
        Assert.AreEqual(AetheriaRuntimeAssets.UnityCoreBundle, BundleFor("texture.core.sun.surface-flow"));

        string BundleFor(string assetKey)
        {
            var entry = manifest.Assets.Single(asset =>
                string.Equals(asset.Ref.AssetKey, assetKey, StringComparison.Ordinal));
            return AetheriaRuntimeAssets.ResolveUnityBundleName(entry);
        }
    }

    [Test]
    public void EntityContentIsPartitionedByAuthoredSource()
    {
        var manifest = AetheriaRuntimeAssets.ProjectManifest(null);
        var player = manifest.Assets.Single(asset => asset.Ref.AssetKey == "prefab.entity.player");
        var ship = manifest.Assets.Single(asset => asset.Ref.AssetKey == "prefab.entity.ship");
        var station = manifest.Assets.Single(asset => asset.Ref.AssetKey == "prefab.entity.station");

        var playerBundle = AetheriaRuntimeAssets.ResolveUnityBundleName(player);
        Assert.AreEqual(playerBundle, AetheriaRuntimeAssets.ResolveUnityBundleName(ship));
        Assert.AreNotEqual(playerBundle, AetheriaRuntimeAssets.ResolveUnityBundleName(station));
        StringAssert.StartsWith("aetheria-content-", playerBundle);
    }
}
