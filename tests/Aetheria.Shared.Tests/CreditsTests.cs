using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Aetheria.Shared.Tests
{
    // The credits list against the repo's own licence files and the main menu that must reach it.
    public class CreditsTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Assets", "Scripts", "UI", "MainMenu.cs"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

        private static System.Collections.Generic.IReadOnlyList<Credit> Load() => Credits.All(Read(Credits.NounProjectFile));

        // A file under Assets that looks like a licence, attribution, notice, readme or sources list. Each one is either
        // credited (the credit names it as its LicenceFile) or reviewed below with the reason it is not.
        private static readonly Regex LicenceLike = new Regex(
            @"^(LICEN[CS]E|COPYING|NOTICE|README|SOURCES|_SOURCES|Attribution)|OFL", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] SkippedDirectories = { "bin", "obj", "Library" };

        // Discovered files whose own text names no holder and no licence, so there is nothing true to credit. A new
        // licence-like file is not here until someone has read it and written the reason.
        private static readonly System.Collections.Generic.Dictionary<string, string> ReviewedNotCredited = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Assets/Scripts/ServerShared/NIH/LinqFaster/readme.txt"] = "usage guide for LinqFaster; names no holder and no licence",
            ["Assets/Plugins/LowPoly_AsteroidsPack/README.pdf"] = "how to customise the asteroid pack; names no holder and no licence",
            ["Assets/Plugins/Celestial Body/Textures/_Sources.txt"] = "URLs of the source textures only; states no licence",
        };

        // Bundled works with no licence-like file at all, found by reading Assets. Their licences are not stated in the
        // repo, so they are not credited; each path must still exist, so this list cannot go stale unnoticed.
        private static readonly string[] UnlicensedWorks =
        {
            "Assets/Fonts/Ubuntu",
            "Assets/Fonts/Retro",
            "Assets/Plugins/Ink",
            "Assets/Plugins/GPUTrail",
            "Assets/Plugins/com.unity.uiextensions",
            "Assets/Shaders/StableFluids",
        };

        // What each credited file itself states, written here independently of the credit text: these strings must
        // appear in the file and in the credit's notice.
        private static readonly (string file, string[] stated)[] Holders =
        {
            (Credits.TwemojiFile, new[] { "Jason Sofonia & Justine De Caires", "Twitter", "CC BY 4.0" }),
            (Credits.PlaydeadFile, new[] { "Playdead" }),
            (Credits.LiberationFile, new[] { "Google Corporation", "Red Hat, Inc." }),
            (Credits.UniRxFile, new[] { "Yoshifumi Kawai" }),
            (Credits.KdTreeFile, new[] { "Vili Vol\u010dini / viliwonka" }),
            (Credits.SimplexNoiseFile, new[] { "Ashima Arts", "Ian McEwan", "Keijiro Takahashi" }),
            (Credits.AmbientCgFile, new[] { "ambientCG" }),
        };

        private static string[] LicenceLikeFiles()
        {
            var root = Root();
            return Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta"))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(path => !path.Split('/').Any(segment => SkippedDirectories.Contains(segment)))
                .Where(path => LicenceLike.IsMatch(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        // The ways a licence-like file can be accounted for; shared so the mutations in the report hit the same rule.
        private static string[] Unaccounted(string[] found, System.Collections.Generic.IEnumerable<string> credited) =>
            found.Where(path => !credited.Contains(path) && !ReviewedNotCredited.ContainsKey(path)).ToArray();

        [Fact]
        public void EveryLicenceLikeFileUnderAssetsIsCreditedOrReviewed()
        {
            var root = Root();
            var found = LicenceLikeFiles();
            Assert.Contains(Credits.TwemojiFile, found);
            Assert.Contains(Credits.LiberationFile, found);
            var credited = Load().Select(credit => credit.LicenceFile).ToArray();
            Assert.Equal(Array.Empty<string>(), Unaccounted(found, credited));
            Assert.Equal(Array.Empty<string>(), credited.Where(path => !File.Exists(Path.Combine(root, path))).ToArray());
            Assert.Equal(Array.Empty<string>(), ReviewedNotCredited.Keys.Where(path => !found.Contains(path)).ToArray());
            Assert.Equal(Array.Empty<string>(), ReviewedNotCredited.Keys.Where(path => credited.Contains(path)).ToArray());
            Assert.Equal(Array.Empty<string>(), UnlicensedWorks.Where(path => !Directory.Exists(Path.Combine(root, path))).ToArray());
        }

        // Every holder the licence file states is in the credit that answers for it; the notices are typed into
        // Credits.cs, so this is what keeps them true to the files.
        [Fact]
        public void EveryCreditShowsTheHoldersItsOwnFileStates()
        {
            var credits = Load();
            Assert.Equal(Holders.Select(h => h.file).Append(Credits.NounProjectFile).OrderBy(f => f, StringComparer.Ordinal),
                credits.Select(credit => credit.LicenceFile).OrderBy(f => f, StringComparer.Ordinal));
            foreach (var (file, stated) in Holders)
            {
                var text = Normal(Read(file));
                var notice = Normal(credits.Single(credit => credit.LicenceFile == file).Notice);
                foreach (var holder in stated)
                {
                    Assert.True(text.Contains(Normal(holder)), $"{file} does not state '{holder}'");
                    Assert.True(notice.Contains(Normal(holder)), $"the credit for {file} omits '{holder}'");
                }
                // And every copyright line the file carries, whatever the table above says.
                foreach (var line in Read(file).Split('\n'))
                {
                    var match = CopyrightLine.Match(line);
                    if (!match.Success) continue;
                    var body = Regex.Replace(match.Groups[1].Value.Replace("[", "").Replace("]", "").Trim(), @"(\s*All rights reserved)?\.?$", "", RegexOptions.IgnoreCase);
                    Assert.True(notice.Contains(Normal(body)), $"the credit for {file} omits its copyright line '{body}'");
                }
            }
        }

        private static readonly Regex CopyrightLine = new Regex(@"copyright\s*(?:\(c\)|\u00a9)?\s*(\[?\d.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Dash variants and case folded, so the file's en dash and the screen's hyphen compare equal.
        private static string Normal(string text) => text.Replace('\u2013', '-').Replace('\u2014', '-').Replace("\r", "").ToLowerInvariant();

        [Fact]
        public void TwemojiComesFirst()
        {
            var twemoji = Load()[0];
            Assert.Equal("Twemoji", twemoji.Name);
            Assert.Contains("CC BY 4.0", twemoji.Licence);
        }

        // The expectations are stated here, not derived from the parser: 81 icon credits between the heading and the
        // "Other" sub-heading's two artists, named at the ends and at the line that was once split across two.
        [Fact]
        public void EveryIconCreditInTheAttributionFileIsShown()
        {
            var text = Read(Credits.NounProjectFile);
            Assert.StartsWith(Credits.NounProjectHeading, text.TrimStart('\uFEFF'));
            var lines = Credits.NounProjectLines(text).ToArray();
            Assert.Equal(82, lines.Length);
            Assert.Equal("Tornado by Bart Laubsch", lines[0]);
            Assert.Equal("Alien Abduction by Bart Laubsch", lines[1]);
            Assert.Equal("Hurricane by icon 54 from Smashicons Space by Ben Davis", lines[2]);
            Assert.Equal("Space 2 - Solid by Ben Davis", lines[3]);
            Assert.Contains("Other", lines);
            Assert.Equal("Missile War Weapon by Freepik", lines[^1]);
            // One credit per line: every line but the sub-heading names an artist and does not stop mid-sentence.
            foreach (var line in lines.Where(line => line != "Other"))
            {
                Assert.Contains(" by ", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotMatch(@"\s(from|by)$", line);
            }
            var notice = Load().Single(credit => credit.LicenceFile == Credits.NounProjectFile).Notice.Split('\n');
            Assert.Equal(lines, notice);
        }

        [Fact]
        public void AnAttributionFileWithoutItsHeadingIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Credits.NounProjectLines("Tornado by Bart Laubsch\nAlien Abduction by Bart Laubsch").ToArray());
            Assert.Throws<InvalidOperationException>(() => Credits.NounProjectLines("Noun Project\n\n").ToArray());
        }

        [Fact]
        public void EntriesNameTheirLicenceAndHolder()
        {
            foreach (var credit in Load())
            {
                Assert.False(string.IsNullOrWhiteSpace(credit.Name));
                Assert.False(string.IsNullOrWhiteSpace(credit.Licence));
                Assert.False(string.IsNullOrWhiteSpace(credit.Notice));
            }
        }

        [Fact]
        public void AnEmptyEntryIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Credits.Validate(new[] { new Credit { Name = "x", Licence = "", Notice = "n", LicenceFile = "f" } }));
        }

        // The Unity menu is not compilable headlessly, so this reads its source with comments removed (a commented-out
        // button or call must not satisfy it): the main menu must offer Credits, and ShowCredits must build the Shared
        // list. It proves the wiring exists, not that the screen draws.
        [Fact]
        public void MainMenuOffersACreditsPathThatRendersTheList()
        {
            var menu = Regex.Replace(Read("Assets/Scripts/UI/MainMenu.cs"), @"/\*.*?\*/|//[^\n]*", "", RegexOptions.Singleline);
            var main = menu.Substring(menu.IndexOf("private void ShowMain()", StringComparison.Ordinal));
            main = main.Substring(0, main.IndexOf("private bool ShowCredits()", StringComparison.Ordinal));
            Assert.Contains("AddButton(\"Credits\"", main);
            Assert.Contains("ShowCredits()", main);
            var credits = menu.Substring(menu.IndexOf("private bool ShowCredits()", StringComparison.Ordinal));
            credits = credits.Substring(0, credits.IndexOf("private void ShowScenarios()", StringComparison.Ordinal));
            Assert.Contains("Credits.All(", credits);
            // A build missing the attribution file refuses with a dialog instead of throwing in the click handler.
            Assert.Contains("attribution == null", credits);
            Assert.Contains("Refuse(", credits);
        }
    }
}
