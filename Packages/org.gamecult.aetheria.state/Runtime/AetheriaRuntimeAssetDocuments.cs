using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeAssetKinds
    {
        public const string Texture = "texture";
        public const string Sprite = "sprite";
        public const string Mesh = "mesh";
        public const string Material = "material";
        public const string Shader = "shader";
        public const string ComputeShader = "compute-shader";
        public const string Audio = "audio";
        public const string Prefab = "prefab";
        public const string VolumeProfile = "unity.volume-profile";
        public const string Font = "font";
    }

    public static class AetheriaRuntimeAssetTransports
    {
        public const string CultMesh = "cultmesh";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAssetRef
    {
        [Key(0)]
        public string AssetKey { get; set; } = "";

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string Uri { get; set; } = "";

        [Key(3)]
        public string Transport { get; set; } = AetheriaRuntimeAssetTransports.CultMesh;

        [Key(4)]
        public string ContentHash { get; set; } = "";

        [Key(5)]
        public string MimeType { get; set; } = "";

        [Key(6)]
        public IReadOnlyDictionary<string, string> Metadata { get; set; } =
            new Dictionary<string, string>();

        public static AetheriaRuntimeAssetRef Empty(string kind = "")
        {
            return new AetheriaRuntimeAssetRef { Kind = kind ?? "" };
        }

        public static AetheriaRuntimeAssetRef FromKey(
            string key,
            string kind,
            string? uri = null,
            string transport = AetheriaRuntimeAssetTransports.CultMesh,
            string? mimeType = null)
        {
            key ??= "";
            return new AetheriaRuntimeAssetRef
            {
                AssetKey = key,
                Kind = kind ?? "",
                Uri = string.IsNullOrWhiteSpace(uri) ? key : uri ?? "",
                Transport = string.IsNullOrWhiteSpace(transport)
                    ? AetheriaRuntimeAssetTransports.CultMesh
                    : transport,
                MimeType = mimeType ?? ""
            };
        }
    }

    [CultDocument("gamecult.aetheria.asset_manifest", "gamecult.aetheria.asset_manifest.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeAssetManifestDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.AssetManifest;

        [Key(1)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(2)]
        public string RunId { get; set; } = "";

        [Key(3)]
        public string BaseUri { get; set; } = "cultmesh://aetheria.local/assets";

        [Key(4)]
        public IReadOnlyList<AetheriaRuntimeAssetManifestEntry> Assets { get; set; } =
            Array.Empty<AetheriaRuntimeAssetManifestEntry>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAssetManifestEntry
    {
        [Key(0)]
        public AetheriaRuntimeAssetRef Ref { get; set; } = new AetheriaRuntimeAssetRef();

        [Key(1)]
        public long SizeBytes { get; set; } = -1;

        [Key(2)]
        public int Width { get; set; }

        [Key(3)]
        public int Height { get; set; }

        [Key(4)]
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    }
}
