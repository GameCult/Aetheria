/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class MiningToolData : BehaviorData
{
    [Inspectable, JsonProperty("dps"), Key(1)]
    public PerformanceStat DamagePerSecond = new PerformanceStat();
    
    [Inspectable, JsonProperty("efficiency"), Key(2)]
    public PerformanceStat Efficiency = new PerformanceStat();
    
    [Inspectable, JsonProperty("penetration"), Key(3)]
    public PerformanceStat Penetration = new PerformanceStat();
    
    [Inspectable, JsonProperty("range"), Key(4)]
    public PerformanceStat Range = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new MiningTool(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new MiningTool(this, item);
    }
}

public class MiningTool : Behavior
{
    public CultRecordKey AsteroidBelt;
    public int Asteroid;
    
    private MiningToolData _data;
    public float Range { get; private set; }

    public MiningTool(MiningToolData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public MiningTool(MiningToolData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Range = Evaluate(_data.Range);
        if (!AsteroidBelt.IsSet() || !Entity.Zone.AsteroidExists(AsteroidBelt, Asteroid))
            return false;

        var pose = Entity.Zone.ChunkPose(AsteroidBelt, Asteroid);
        if (length(Entity.Position.xz - pose.xy) - pose.w < Range)
        {
            Entity.Zone.MineAsteroid(
                Entity,
                AsteroidBelt,
                Asteroid,
                Evaluate(_data.DamagePerSecond) * dt,
                Evaluate(_data.Efficiency),
                Evaluate(_data.Penetration));
            return true;
        }

        return false;
    }
}