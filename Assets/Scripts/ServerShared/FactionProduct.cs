/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// What a manufacturer sells of a generic design: its name, its flavor text, and either a line (how the maker builds each of the
// design's roles) or a bin of one of its own lines (maker-design-tiers-allowed). Which products exist is what a faction makes, so
// market segmentation and regional progression are authored here rather than as duplicate item entries.
[CultDocument("aetheria.factionproductdata", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FactionProductData
{
    [Inspectable, CultName, JsonProperty("name"), Key(1)]
    public string Name;

    [Inspectable, CultInspectorTextArea, JsonProperty("description"), Key(2)]
    public string Description;

    [Inspectable, JsonProperty("design"), Key(3)]
    public CultRecordRef<CraftedItemData> Design;

    [Inspectable, JsonProperty("manufacturer"), Key(4)]
    public CultRecordRef<Faction> Manufacturer;

    [Inspectable, JsonProperty("roles"), Key(5)]
    public List<ProductRole> Roles = new List<ProductRole>();

    // This line's own final test (maker-design-tiers-allowed): a premium line is a better assembly as well as better parts.
    // Null assembles as its maker does. A binned product leaves it null and reads its line's.
    [Inspectable, JsonProperty("assembly"), Key(6)]
    public AssemblyDistribution Assembly;

    // Set on a binned product: the line it bins and the floor above which it takes that line's output. A bin authors no Roles and
    // no Assembly; the mint derives both from the line.
    [Inspectable, JsonProperty("bin"), Key(7)]
    public ProductBin Bin;
}

// How good this product's part is in one of its design's roles: a pseudo-gaussian distribution whose mean is
// the technology the manufacturer puts in and whose standard deviation is their quality control. A market
// segment is a bin of the line (ProductBin), not a second product with a role pumped up; what makes a part better is the
// flavor text's to tell, because a part here carries quality and nothing else.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ProductRole
{
    [Inspectable, JsonProperty("role"), Key(0)]
    public string Role;

    [Inspectable, JsonProperty("mean"), Key(1)]
    public float Mean = .5f;

    [Inspectable, JsonProperty("standardDeviation"), Key(2)]
    public float StandardDeviation = .15f;
}

// A bin of one of the maker's lines (ruling maker-design-tiers-allowed): the line it bins (the same design and maker, itself
// unbinned) and the floor, in standard deviations above the mean of each of the line's distributions, below which the bin
// takes nothing. 0 is the top half of the line, .67 the top quarter.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ProductBin
{
    [Inspectable, JsonProperty("line"), Key(0)]
    public CultRecordRef<FactionProductData> Line;

    [Inspectable, JsonProperty("floor"), Key(1)]
    public float Floor;
}

// How well a maker puts the thing together: the technology and quality control of final assembly. A lot's own quality is drawn
// from it, and it is the ceiling on every role the lot fills. Defaults are a middling maker.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AssemblyDistribution
{
    [Inspectable, JsonProperty("mean"), Key(0)]
    public float Mean = .5f;

    [Inspectable, JsonProperty("standardDeviation"), Key(1)]
    public float StandardDeviation = .15f;
}
