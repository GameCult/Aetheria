using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using NUnit.Framework;
using System;
using System.IO;

public class StarbridgePlayerSeatDocumentTests
{
    [Test]
    public void StarbridgePlayerSeatRoundTripsCrashReconnectState()
    {
        var createdAt = DateTimeOffset.Parse("2026-06-27T10:00:00Z");
        var disconnectedAt = createdAt.AddMinutes(3);
        var reconnectedAt = disconnectedAt.AddMinutes(1);

        var seat = AetheriaRuntimeStarbridgePlayerSeatDocument.Create(
            "seat-alpha",
            "player-7",
            "session-1",
            "scenario-1",
            "run-1",
            AetheriaRuntimeStarbridgePlayerSeatRoles.Support,
            "unity-runtime-old",
            createdAt);
        seat.PlayerDisplayName = "Vega";
        seat.ControlledEntityKey = "entity:ship:7";
        seat.ShipEntityKey = "entity:ship:7";
        seat.CockpitItemKey = "item:cockpit:7";
        seat.EscapePodEntityKey = "entity:pod:7";
        seat.LoadoutDocumentKey = "loadout:ship:7";
        seat.AuthorityLeaseId = "lease:seat-alpha";
        seat.ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Movement, AetheriaRuntimeClaimKinds.Combat };

        seat.MarkDisconnected(disconnectedAt);

        Assert.AreEqual(AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Grace, seat.ConnectionState);
        Assert.IsTrue(seat.IsResumeGraceActive(disconnectedAt.AddSeconds(30), TimeSpan.FromMinutes(2)));
        Assert.IsFalse(seat.IsResumeGraceActive(disconnectedAt.AddMinutes(3), TimeSpan.FromMinutes(2)));

        seat.AttachRuntime("unity-runtime-new", reconnectedAt);

        Assert.AreEqual(AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Connected, seat.ConnectionState);
        Assert.AreEqual("unity-runtime-new", seat.RuntimeId);
        Assert.Contains("unity-runtime-old", seat.PreviousRuntimeIds);
        Assert.AreEqual("entity:ship:7", seat.ControlledEntityKey);
        Assert.AreEqual("item:cockpit:7", seat.CockpitItemKey);
        Assert.AreEqual("entity:pod:7", seat.EscapePodEntityKey);
        Assert.AreEqual("loadout:ship:7", seat.LoadoutDocumentKey);
    }

    [Test]
    public void StarbridgePlayerSeatCanBeReadAsManagedCultMeshDocument()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-starbridge-seat-reactive-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var seat = AetheriaRuntimeStarbridgePlayerSeatDocument.Create(
            "seat-support",
            "player-support",
            "session-1",
            "scenario-1",
            "run-1",
            AetheriaRuntimeStarbridgePlayerSeatRoles.Support,
            "unity-support",
            DateTimeOffset.Parse("2026-06-27T11:00:00Z"));
        seat.CockpitItemKey = "item:cockpit:support";
        seat.EscapePodEntityKey = "entity:pod:support";

        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "unity-support", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        client
            .MutableDocument<AetheriaRuntimeStarbridgePlayerSeatDocument>(
                AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seat.SeatId))
            .ReplaceAsync(seat)
            .GetAwaiter()
            .GetResult();
        var handle = client.Document<AetheriaRuntimeStarbridgePlayerSeatDocument>(
            AetheriaRuntimeVerseRecordKeys.StarbridgePlayerSeat(seat.SeatId));
        using var reactive = handle.Reactive();
        using var aetheriaSeat = client.State.ReactiveStarbridgePlayerSeat(seat.SeatId);

        Assert.AreEqual("seat-support", handle.Latest().SeatId);
        Assert.AreEqual(
            "seat-support",
            ReadLatest(client.State.StarbridgePlayerSeat(seat.SeatId)).SeatId);
        Assert.AreEqual("item:cockpit:support", reactive.Current.CockpitItemKey);
        Assert.AreEqual("item:cockpit:support", aetheriaSeat.Current.CockpitItemKey);
        Assert.AreEqual("entity:pod:support", reactive.Current.EscapePodEntityKey);
        Assert.AreEqual("entity:pod:support", aetheriaSeat.Current.EscapePodEntityKey);
        Assert.AreEqual("unity-support", reactive.Document.Context.RuntimeId);
    }

    private static TDocument ReadLatest<TDocument>(CultMeshDocumentHandle<TDocument> document)
        where TDocument : class
    {
        return document.LatestAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
