/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;

// The catalog the game boots, for tests: ShipModCatalog.ResolveCatalog over GameData/Mods, the call ActionGameManager
// makes. The game skips a broken package so a player still boots; a test never does, so an exclusion throws here and
// a broken first-party package fails the suite instead of vanishing from it.
internal static class TestCatalog
{
    private static readonly Lazy<string> Shared = new Lazy<string>(() => ForRepo(RunStartTests.FindRepoRoot()));

    // The repo's catalog with its first-party packages composed in, resolved once per test process.
    public static string Repo => Shared.Value;

    // The shipped assembly's own [CultDocument] types and nothing the test assembly registers (CultDocumentRegistry.ForTypes
    // does not re-scan the AppDomain), so this assembly's fixture documents never become catalog globals the real catalog
    // must hold. The one registry every test that opens a real catalog, and the composition behind Repo, uses.
    public static CultDocumentRegistry Registry() => CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false })
        .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));

    // The catalog of the repository at repoRoot: <root>/GameData/Aetheria.cc plus <root>/GameData/Mods, composed into a
    // per-call temp directory that goes away with the process.
    public static string ForRepo(string repoRoot)
    {
        var work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetheria-testcatalog-" + Guid.NewGuid().ToString("N"))).FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(work, true); } catch (IOException) { } };
        return Resolve(Path.Combine(repoRoot, "GameData", "Aetheria.cc"), Path.Combine(repoRoot, "GameData", "Mods"), work);
    }

    public static string Resolve(string shippedCatalog, string modsRoot, string workDir)
    {
        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(shippedCatalog, Path.Combine(workDir, "derived.cc"), modsRoot, Registry());
        if (excluded.Length > 0)
            throw new InvalidOperationException("First-party packages were excluded from the catalog: " +
                string.Join("; ", excluded.Select(entry => entry.Package + ": " + entry.Reason)));
        return catalog;
    }
}
