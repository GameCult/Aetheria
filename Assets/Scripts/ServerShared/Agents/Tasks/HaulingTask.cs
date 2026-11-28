/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// The order key alone: the leg is HaulRun.Leg's, derived from the hold and the order every update, never stored here.
public class HaulingTask : AgentTask
{
    public override TaskType Type => TaskType.Haul;

    [JsonProperty("order"), Key(4)]
    public CultRecordKey Order;
}
