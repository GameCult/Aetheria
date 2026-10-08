/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;

// The catalog the game boots, for tests: ShipModCatalog.ResolveCatalog over GameData/Mods, the call ActionGameManager
// makes. The game skips a broken package so a player still boots; a test never does, so an exclusion throws here and
// a broken first-party package fails the suite instead of vanishing from it.
internal static class TestCatalog
{
    private static readonly Lazy<string> Shared = new Lazy<string>(() =>
    {
        var repo = RunStartTests.FindRepoRoot();
        var work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetheria-testcatalog-" + Guid.NewGuid().ToString("N"))).FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(work, true); } catch (IOException) { } };
        return Resolve(Path.Combine(repo, "GameData", "Aetheria.cc"), Path.Combine(repo, "GameData", "Mods"), work);
    });

    // The repo's catalog with its first-party packages composed in, resolved once per test process.
    public static string Repo => Shared.Value;

    public static string Resolve(string shippedCatalog, string modsRoot, string workDir)
    {
        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(shippedCatalog, Path.Combine(workDir, "derived.cc"), modsRoot);
        if (excluded.Length > 0)
            throw new InvalidOperationException("First-party packages were excluded from the catalog: " +
                string.Join("; ", excluded.Select(entry => entry.Package + ": " + entry.Reason)));
        return catalog;
    }
}
