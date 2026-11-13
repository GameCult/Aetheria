/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

// Substance clean break (aetheria-release:ruling:clean-break-from-substance, cut remove-sbsar-files). The 41 .sbsar
// files, their importer and the SUBSTANCE_PLUGIN_ENABLED define are gone, and nothing that survives may point into
// them. Headless: no Unity is open, so a dangling GUID is caught here instead of rendering pink in the editor.
public sealed class SubstanceCleanBreakTests
{
    // Every guid the removed tree owned, captured from the .meta files before deletion: the 41 .sbsar.meta, the two
    // folder metas (Assets/Materials/Substance.meta, Substance/Metal.meta) and the importer script guid
    // (43a8bbf6..., the `script:` line of every .sbsar.meta, whose plugin was already absent).
    private static readonly string[] Tombstones =
    {
        "06833951ebae4cc45ad88d58cb5571de",
        "06d6a565d43fba5409712ce9a10bfbef",
        "07f679eadba9fb14abac26b1e165f7d5",
        "092bdd8d243488141aeab5bb0d72429a",
        "09aee558716a08d4c9806b9b4273a209",
        "0b5dfc86c0681a3459a57d197804191b",
        "0be5fb1df83ac8e499a3f6a060041e49",
        "130f566ffa0b2424fbb47de2194c136a",
        "180b85899b4b1f848b348230339ce984",
        "1c858fec5b74c7c46b1712494192b6b5",
        "26a894e543f3ed44893b1620707ca62c",
        "2b42584452fe0cc4fa4e8a115230b156",
        "2be0f689f5b01be4aa1032bc86f15eab",
        "2e9a9dbacde66a446852af882f33e9e2",
        "43a8bbf65be290d44ba52b94476242b9",
        "4e39297ae4fe9ee4b803c04eabb69da7",
        "50192727b92e0af4cbe5c773bec3537f",
        "504c7bfe0d50ff64a88b8eac2ceb46f8",
        "53adf8e53222e6f4791d7bf2110fb70c",
        "58e16b7dbc1c31f4585e11544cdce127",
        "6b2eb94965ca27542b8627d3521f5b5b",
        "7363fcc4a3c41a349b29de7ec34555be",
        "7de1c11cf7591ec48bdb53e9818172f5",
        "8249e9ffcbd97c84d8ce73343e156c82",
        "885e680924fc12c439a842a2142d4b78",
        "9458f33b8e7e51a42aa19686b9779d82",
        "9aa51bbe4dd46874a906608c34a739e4",
        "9d0963470d3545a4e932ed4ea95b9d62",
        "9fc901afb8a434e46b9405eaec2df5a4",
        "a1d39ea984432bf44b4a0686afe2bf1a",
        "a74970be876d3554ea0f279f39314c9c",
        "afbd691d0ea2a5a47b5a43b244593a56",
        "b2bec4b400153d745b2915f1c43e5c96",
        "b7e648471e9dfc44490bd41973711541",
        "c00b5a4f1e4743a4ca59b8030251f900",
        "c5f2177e1f35daa499f4d7e0b7f3370a",
        "ca8f080ab60226e4097841eee2bfa8db",
        "cbc61995a46604441ab9de4f1c68e290",
        "d15fc45ed0000934d946b5f271f637ef",
        "ddc6970cc883b9845bdadc91983130c2",
        "deb7e15e5f95294498d18869d9f11302",
        "e7758cad28ae98f478940e55222419a6",
        "ea7457b9a4f0bcb43becaa33de541cbd",
        "ef9a54b8dacea9f44be70da43472f701",
    };

    private static readonly string[] ScannedExtensions =
    {
        ".mat", ".prefab", ".unity", ".asset", ".meta", ".controller", ".anim", ".overrideController", ".mixer",
        ".lighting", ".flare", ".shader", ".json",
    };

    private static readonly string[] ExcludedDirs = { ".git", "node_modules", ".voidbot" };

    private static readonly string[] RepointedMaterials =
    {
        "Assets/Materials/Steel.mat", "Assets/Materials/Aluminium brushed.mat", "Assets/Materials/Silver.mat",
        "Assets/Materials/Steel parkerized.mat", "Assets/Materials/Cockpit.mat", "Assets/Materials/Metal 003.mat",
        "Assets/Materials/Car Paint Red.mat", "Assets/Materials/Car Paint Magenta.mat",
        "Assets/Materials/Car Paint White.mat", "Assets/Materials/Car Paint White Small.mat",
        "Assets/Materials/Radiators.mat", "Assets/Materials/Station Lights.mat", "Assets/Materials/Mine Glow.mat",
        "Assets/Models/Discobolus/Discobolus Marble.mat", "Assets/Materials/Pickup Trim.mat",
    };

    private static IEnumerable<string> Walk(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir)) yield return file;
        foreach (var sub in Directory.EnumerateDirectories(dir))
            if (!ExcludedDirs.Contains(Path.GetFileName(sub)))
                foreach (var file in Walk(sub)) yield return file;
    }

    [Fact]
    public void NoSbsarFileOrMetaRemains()
    {
        var root = RestoredHullsTests.FindRepoRoot();
        var stray = Walk(root).Where(f => f.EndsWith(".sbsar", StringComparison.OrdinalIgnoreCase)
                                           || f.EndsWith(".sbsar.meta", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f)).ToList();
        Assert.True(stray.Count == 0, $"{stray.Count} Substance archive files remain, first: {stray.FirstOrDefault()}");
        Assert.False(Directory.Exists(Path.Combine(root, "Assets", "Materials", "Substance")));
    }

    [Fact]
    public void NoSurvivingFileNamesARemovedSubstanceGuid()
    {
        var root = RestoredHullsTests.FindRepoRoot();
        var files = new[] { "Assets", "Packages", "ProjectSettings" }
            .Select(d => Path.Combine(root, d)).Where(Directory.Exists)
            .SelectMany(Walk)
            .Concat(Directory.Exists(Path.Combine(root, "GameData"))
                ? Directory.EnumerateFiles(Path.Combine(root, "GameData"), "*.cc") : Enumerable.Empty<string>())
            .Where(f => f.EndsWith(".cc") || ScannedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        var hits = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllText(file, Encoding.Latin1).Split('\n');
            for (var i = 0; i < lines.Length; i++)
                foreach (var guid in Tombstones)
                    if (lines[i].Contains(guid, StringComparison.Ordinal))
                        hits.Add($"{Path.GetRelativePath(root, file)}:{i + 1} names {guid}");
        }
        Assert.True(hits.Count == 0, $"{hits.Count} references to removed Substance guids:\n" + string.Join("\n", hits.Take(40)));
    }

    [Fact]
    public void NoSubstanceBuildSurface()
    {
        var root = RestoredHullsTests.FindRepoRoot();
        Assert.DoesNotContain("SUBSTANCE", File.ReadAllText(Path.Combine(root, "ProjectSettings", "ProjectSettings.asset")), StringComparison.Ordinal);
        foreach (var name in new[] { "manifest.json", "packages-lock.json" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Packages", name));
            Assert.DoesNotContain("substance", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("allegorithmic", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("sbsar", File.ReadAllText(Path.Combine(root, ".gitattributes")), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(root, "Assets", "Allegorithmic")));
    }

    [Fact]
    public void EveryReplacementTextureResolves()
    {
        var root = RestoredHullsTests.FindRepoRoot();
        var guidToMeta = new Dictionary<string, string>();
        foreach (var meta in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.meta", SearchOption.AllDirectories))
        {
            var guid = File.ReadLines(meta).FirstOrDefault(l => l.StartsWith("guid: "))?.Substring("guid: ".Length).Trim();
            if (guid != null) guidToMeta[guid] = Path.GetRelativePath(root, meta).Replace('\\', '/');
        }

        var texture = new Regex(@"m_Texture:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        var problems = new List<string>();
        foreach (var rel in RepointedMaterials)
        {
            var path = Path.Combine(root, rel);
            if (!File.Exists(path)) { problems.Add($"{rel} does not exist"); continue; }
            var text = File.ReadAllText(path);
            var start = text.IndexOf("m_TexEnvs:", StringComparison.Ordinal);
            var end = text.IndexOf("m_Floats:", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, $"{rel} has no m_TexEnvs section");
            foreach (Match m in texture.Matches(text.Substring(start, end - start)))
            {
                if (m.Groups[1].Value == "0") continue;
                var guid = m.Groups[2].Value;
                if (!guidToMeta.TryGetValue(guid, out var meta)) problems.Add($"{rel} names {guid}, which has no .meta under Assets/");
                else if (meta.StartsWith("Assets/Materials/Substance", StringComparison.Ordinal)) problems.Add($"{rel} names {guid} inside the removed Substance tree");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
