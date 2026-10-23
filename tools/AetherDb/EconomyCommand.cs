using System;
using System.Collections.Generic;
using System.IO;
using GameCult.Caching;

// One batch of economy content: commodities, recipes, station kinds with stand-ins, outside-market rows, edits to existing items.
public abstract class EconomyBatch
{
    public abstract void Author(EconomyAuthor author);
}

public sealed class EconomyAuthor
{
    public void Put<T>(T document, CultRecordKey key) where T : class => throw new NotImplementedException();
    public void Edit<T>(string name, Action<T> edit) where T : ItemData => throw new NotImplementedException();
}

public static class EconomyCommand
{
    public static int Run(string[] args) => throw new NotImplementedException();
    public static int Report(CultCache catalog, TextWriter output) => throw new NotImplementedException();
    public static int Author(string catalogPath, IReadOnlyList<EconomyBatch> batches, bool apply, TextWriter output, CultDocumentRegistry registry = null) =>
        throw new NotImplementedException();
}
