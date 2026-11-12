using System;
using System.IO;
using System.Linq;
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

        [Fact]
        public void EveryLicenceFileUnderAssetsIsCredited()
        {
            var root = Root();
            var found = Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta"))
                .Where(path => { var name = Path.GetFileName(path); return name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Attribution", StringComparison.OrdinalIgnoreCase); })
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToArray();
            Assert.Contains(Credits.TwemojiFile, found);
            var credited = Load().Select(credit => credit.LicenceFile).ToHashSet();
            Assert.Empty(found.Where(path => !credited.Contains(path)));
            Assert.Empty(credited.Where(path => !File.Exists(Path.Combine(root, path))));
        }

        [Fact]
        public void TwemojiComesFirstWithTheHoldersItsLicenceNames()
        {
            var twemoji = Load()[0];
            Assert.Equal("Twemoji", twemoji.Name);
            Assert.Contains("CC BY 4.0", twemoji.Licence);
            Assert.Contains("Jason Sofonia & Justine De Caires", Read(Credits.TwemojiFile));
            Assert.Contains("Jason Sofonia & Justine De Caires", twemoji.Notice);
            Assert.Contains("Twitter", twemoji.Notice);
        }

        [Fact]
        public void EveryIconCreditInTheAttributionFileIsShown()
        {
            var lines = Credits.NounProjectLines(Read(Credits.NounProjectFile)).ToArray();
            Assert.True(lines.Length > 50);
            var notice = Load().Single(credit => credit.LicenceFile == Credits.NounProjectFile).Notice.Split('\n');
            Assert.Equal(lines, notice);
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
            Assert.Contains("Playdead", Read(Credits.PlaydeadFile));
            Assert.Contains("Playdead", Load().Single(credit => credit.LicenceFile == Credits.PlaydeadFile).Notice);
        }

        [Fact]
        public void AnEmptyEntryIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Credits.Validate(new[] { new Credit { Name = "x", Licence = "", Notice = "n", LicenceFile = "f" } }));
        }

        // The Unity menu is not compilable headlessly, so this reads its source: the main menu must offer Credits and
        // the Credits screen must render the Shared list. It proves the wiring exists, not that the screen draws.
        [Fact]
        public void MainMenuOffersACreditsPathThatRendersTheList()
        {
            var menu = Read("Assets/Scripts/UI/MainMenu.cs");
            var main = menu.Substring(menu.IndexOf("private void ShowMain()", StringComparison.Ordinal));
            main = main.Substring(0, main.IndexOf("private void ShowScenarios()", StringComparison.Ordinal));
            Assert.Contains("AddButton(\"Credits\"", main);
            Assert.Contains("ShowCredits()", main);
            Assert.Contains("Credits.All(", menu);
        }
    }
}
