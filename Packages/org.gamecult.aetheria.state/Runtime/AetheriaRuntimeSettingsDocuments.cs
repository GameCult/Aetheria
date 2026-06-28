using System;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [CultDocument("gamecult.aetheria.player_settings", "gamecult.aetheria.player_settings.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimePlayerSettingsDocument
    {
        public const string SchemaId = "gamecult.aetheria.player_settings.v1";

        [Key(0)] public string Schema { get; set; } = SchemaId;
        [Key(1)] public string PlayerName { get; set; } = "";
        [Key(2)] public bool TutorialPassed { get; set; }
        [Key(3)] public AetheriaRuntimeStoryFileHashDocument[] StoryFileHashes { get; set; } =
            Array.Empty<AetheriaRuntimeStoryFileHashDocument>();
        [Key(4)] public string TemperatureUnit { get; set; } = "";
        [Key(5)] public int SignificantDigits { get; set; }
        [Key(6)] public double DefaultShutdownPerformance { get; set; }
        [Key(7)] public string NebulaQuality { get; set; } = "";
        [Key(8)] public bool ShowAsteroidsInMinimap { get; set; }
        [Key(9)] public AetheriaRuntimeInputBindingOverrideDocument[] BindingOverrides { get; set; } =
            Array.Empty<AetheriaRuntimeInputBindingOverrideDocument>();
        [Key(10)] public string[] ActionBarInputs { get; set; } = Array.Empty<string>();

        public static AetheriaRuntimePlayerSettingsDocument FromSnapshot(
            AetheriaRuntimePlayerSettingsSnapshot? snapshot)
        {
            snapshot ??= new AetheriaRuntimePlayerSettingsSnapshot(
                "",
                false,
                Array.Empty<AetheriaRuntimeStoryFileHash>(),
                "",
                0,
                0,
                "",
                false,
                Array.Empty<AetheriaRuntimeInputBindingOverride>(),
                Array.Empty<string>());

            return new AetheriaRuntimePlayerSettingsDocument
            {
                PlayerName = snapshot.PlayerName ?? "",
                TutorialPassed = snapshot.TutorialPassed,
                StoryFileHashes = (snapshot.StoryFileHashes ?? Array.Empty<AetheriaRuntimeStoryFileHash>())
                    .Select(AetheriaRuntimeStoryFileHashDocument.FromSnapshot)
                    .ToArray(),
                TemperatureUnit = snapshot.TemperatureUnit ?? "",
                SignificantDigits = snapshot.SignificantDigits,
                DefaultShutdownPerformance = snapshot.DefaultShutdownPerformance,
                NebulaQuality = snapshot.NebulaQuality ?? "",
                ShowAsteroidsInMinimap = snapshot.ShowAsteroidsInMinimap,
                BindingOverrides = (snapshot.BindingOverrides ?? Array.Empty<AetheriaRuntimeInputBindingOverride>())
                    .Select(AetheriaRuntimeInputBindingOverrideDocument.FromSnapshot)
                    .ToArray(),
                ActionBarInputs = (snapshot.ActionBarInputs ?? Array.Empty<string>())
                    .Where(input => !string.IsNullOrWhiteSpace(input))
                    .ToArray()
            };
        }

        public AetheriaRuntimePlayerSettingsSnapshot ToSnapshot()
        {
            return new AetheriaRuntimePlayerSettingsSnapshot(
                PlayerName ?? "",
                TutorialPassed,
                (StoryFileHashes ?? Array.Empty<AetheriaRuntimeStoryFileHashDocument>())
                    .Select(hash => hash.ToSnapshot())
                    .ToArray(),
                TemperatureUnit ?? "",
                SignificantDigits,
                DefaultShutdownPerformance,
                NebulaQuality ?? "",
                ShowAsteroidsInMinimap,
                (BindingOverrides ?? Array.Empty<AetheriaRuntimeInputBindingOverrideDocument>())
                    .Select(binding => binding.ToSnapshot())
                    .ToArray(),
                ActionBarInputs ?? Array.Empty<string>());
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeStoryFileHashDocument
    {
        [Key(0)] public string StoryPath { get; set; } = "";
        [Key(1)] public string Hash { get; set; } = "";

        public static AetheriaRuntimeStoryFileHashDocument FromSnapshot(AetheriaRuntimeStoryFileHash snapshot)
        {
            return new AetheriaRuntimeStoryFileHashDocument
            {
                StoryPath = snapshot?.StoryPath ?? "",
                Hash = snapshot?.Hash ?? ""
            };
        }

        public AetheriaRuntimeStoryFileHash ToSnapshot()
        {
            return new AetheriaRuntimeStoryFileHash(StoryPath ?? "", Hash ?? "");
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputBindingOverrideDocument
    {
        [Key(0)] public string ActionName { get; set; } = "";
        [Key(1)] public int BindingIndex { get; set; }
        [Key(2)] public string BindingPath { get; set; } = "";

        public static AetheriaRuntimeInputBindingOverrideDocument FromSnapshot(
            AetheriaRuntimeInputBindingOverride snapshot)
        {
            return new AetheriaRuntimeInputBindingOverrideDocument
            {
                ActionName = snapshot?.ActionName ?? "",
                BindingIndex = snapshot?.BindingIndex ?? -1,
                BindingPath = snapshot?.BindingPath ?? ""
            };
        }

        public AetheriaRuntimeInputBindingOverride ToSnapshot()
        {
            return new AetheriaRuntimeInputBindingOverride(ActionName ?? "", BindingIndex, BindingPath ?? "");
        }
    }

    [CultDocument("gamecult.aetheria.verse_host_settings", "gamecult.aetheria.verse_host_settings.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeVerseHostSettingsDocument
    {
        public const string SchemaId = "gamecult.aetheria.verse_host_settings.v1";

        [Key(0)] public string Schema { get; set; } = SchemaId;
        [Key(1)] public string ServiceId { get; set; } = "";
        [Key(2)] public string VerseId { get; set; } = "";
        [Key(3)] public string RootVerse { get; set; } = "";
        [Key(4)] public string CanonicalService { get; set; } = "";
        [Key(5)] public string LocatedService { get; set; } = "";
        [Key(6)] public string CultMeshAddress { get; set; } = "";
        [Key(7)] public string Title { get; set; } = "";
        [Key(8)] public string Visibility { get; set; } = "";
        [Key(9)] public string LastUpdatedAtUtc { get; set; } = "";

        public static AetheriaRuntimeVerseHostSettingsDocument FromSnapshot(
            AetheriaRuntimeVerseHostSettingsSnapshot? snapshot)
        {
            snapshot ??= new AetheriaRuntimeVerseHostSettingsSnapshot("", "", "", "", "", "", "", "", "");
            return new AetheriaRuntimeVerseHostSettingsDocument
            {
                ServiceId = snapshot.ServiceId ?? "",
                VerseId = snapshot.VerseId ?? "",
                RootVerse = snapshot.RootVerse ?? "",
                CanonicalService = snapshot.CanonicalService ?? "",
                LocatedService = snapshot.LocatedService ?? "",
                CultMeshAddress = snapshot.CultMeshAddress ?? "",
                Title = snapshot.Title ?? "",
                Visibility = snapshot.Visibility ?? "",
                LastUpdatedAtUtc = snapshot.LastUpdatedAtUtc ?? ""
            };
        }

        public AetheriaRuntimeVerseHostSettingsSnapshot ToSnapshot()
        {
            return new AetheriaRuntimeVerseHostSettingsSnapshot(
                ServiceId ?? "",
                VerseId ?? "",
                RootVerse ?? "",
                CanonicalService ?? "",
                LocatedService ?? "",
                CultMeshAddress ?? "",
                Title ?? "",
                Visibility ?? "",
                LastUpdatedAtUtc ?? "");
        }
    }
}
