/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// A manufacturer's branded variant of a generic design: its name, its flavor text, and how it builds each of
// the design's roles. Which products exist is what a faction makes, so market segmentation and regional
// progression are authored here rather than as duplicate item entries.
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
}

// How good this product's part is in one of its design's roles: a pseudo-gaussian distribution whose mean is
// the technology the manufacturer puts in and whose standard deviation is their quality control. A market
// segment is a second product with a role pumped up; what makes that part better is the flavor text's to tell,
// because a part here carries quality and nothing else.
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
