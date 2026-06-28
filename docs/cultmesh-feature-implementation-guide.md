# Implementing Aetheria Features With CultMesh

Date: 2026-06-28

This guide explains the preferred path for adding a new daemon-owned simulation
state feature and consuming it from Unity. It is written for the post-Stage-7
Aetheria shape: the daemon owns simulation truth, CultMesh owns typed state
access and sync, and Unity is a renderer/input client.

The ergonomic rule is simple:

```text
daemon simulation -> typed document -> AetheriaClientState -> Unity reactive typed document
Unity input -> typed operation -> daemon command apply -> next typed document
```

Callers should not walk record keys, schemas, frame rows, local facade indexes,
or protocol layers to get one gameplay fact. If a gameplay/UI caller needs a
state value, it should hold a managed reactive typed document or a named typed
client handle. If a renderer needs hot entity data, it should use the daemon
SoA/native view path.

## Vocabulary

`CultMeshDocumentHandle<TDocument>`

The named typed document handle exposed by the Aetheria client facade. Handles
own the document identity, schema identity, routing, projection source metadata,
and latest/reactive access.

`CultMeshReactiveDocument<TDocument>`

The managed reactive typed document a client holds while it needs live state.
Use this directly in Unity callers for single-document state. Dispose it with
the component or binding lifetime.

`LatestAsync()`

One-shot read. Use in async bootstrap, tests, and non-frame-blocking setup.

`Reactive()`

Live read. Use for Unity presentation state, HUDs, panels, render settings, and
anything that should update as the daemon publishes.

`AetheriaRuntimeDaemonFrameDocument`

The broad authoritative frame publication. It is a source for projected client
documents, not the normal public client API.

`AetheriaRuntimeDaemonSoaViewDocument`

The high-performance current-zone slab descriptor. Use this for render/physics
hot paths, not for ordinary UI.

`AetheriaRuntimeDaemonCommandDocument`

The typed daemon command envelope. Unity should submit through `AetheriaControl`
or `AetheriaRuntimeDaemonOperationClient`, not manually construct raw command
documents in presentation code.

## Choosing The Shape

Before adding a feature, choose one of these shapes.

Use a direct typed document when:

- the value is moderate size;
- clients need latest/watch semantics;
- the data is useful across Unity, RTS, Eve, tests, or tooling;
- the data changes at daemon tick or command-application cadence.

Use a viewport/query document when:

- the data depends on a camera, zone, entity, selection, or other request key;
- clients should not receive the whole world;
- the result is still ordinary structured data.

Use SoA/native view when:

- the data is hot per-frame render/physics data;
- Unity jobs/Burst/Ymir need columnar access;
- row count is large enough that object graphs are the wrong shape.

Use a daemon command operation when:

- Unity/RTS input asks the daemon to mutate simulation truth;
- the operation needs authority, idempotency, acceptance, rejection, or frame
  accounting.

Do not add:

- Unity-local gameplay truth;
- new `AetheriaRuntime*Session` wrappers for single-document access;
- public string command names plus payload dictionaries;
- UI code that reads daemon frames and joins rows by hand;
- helper names that hide multi-hop state access behind "facade" or "projector"
  unless the class is a temporary render adapter fenced by the verifier.

## Example Feature

This guide uses a concrete example: `ZoneDefenseStatus`.

The daemon computes a per-current-zone defense status:

- current zone index;
- base shield ratio;
- incoming hostile count;
- active turret count;
- current alert level.

Unity reads this document to show HUD/UI state. Unity can also submit a typed
operation to set the desired alert level. The daemon applies that operation and
publishes a new status on the next tick.

The same pattern applies to heat support state, station service state, mission
wave status, scenario progression, commander infrastructure state, or any other
simulation feature.

## Step 1: Define The Typed Document

Add the document to the runtime package, because Unity, daemon, tests, and RTS
all need the same type:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs
```

For larger features, create a dedicated file in the same package and keep the
schema constant in `AetheriaRuntimeDaemonSchemas`.

```csharp
public static class AetheriaRuntimeDaemonSchemas
{
    public const string ZoneDefenseStatus =
        "gamecult.aetheria.zone_defense_status.v1";
}

[MessagePackObject]
[CultDocument(AetheriaRuntimeDaemonSchemas.ZoneDefenseStatus)]
public sealed class AetheriaRuntimeZoneDefenseStatusDocument
{
    [Key(0)]
    public int ZoneIndex { get; set; } = -1;

    [Key(1)]
    public double BaseShieldRatio { get; set; }

    [Key(2)]
    public int IncomingHostileCount { get; set; }

    [Key(3)]
    public int ActiveTurretCount { get; set; }

    [Key(4)]
    public string AlertLevel { get; set; } = "";
}
```

Rules:

- Give every document a stable `gamecult.aetheria.*.v1` schema.
- Use explicit `[Key]` slots. Never renumber existing slots.
- Add new fields at the end.
- Default values must produce a safe empty document.
- Prefer primitive/string/array/document-row types that cross runtimes cleanly.
- If RTS/Electron consumes it, regenerate bindings after changing slots.

## Step 2: Register The Document Type

Add the document to the registry:

```text
Aetheria.State/AetheriaDocumentRegistry.cs
```

```csharp
typeof(AetheriaRuntimeZoneDefenseStatusDocument),
```

This lets CultCache/CultNet/CultMesh know how to serialize, deserialize, and
bind the type across the local state node and network-facing surfaces.

## Step 3: Decide Whether It Is Source State Or Projected State

Most Unity-consumed simulation state is projected from daemon-owned run/frame
state. That is the right default: the daemon tick owns the authoritative
simulation, and CultMesh exposes a typed document derived from it.

Use a projected document when the state can be derived from the latest frame:

```text
latest daemon frame -> ZoneDefenseStatus document
```

Use a direct mutable/source document when the state is an independent domain
object such as settings, scenario config, player seat state, or a durable
session record.

For the example, `ZoneDefenseStatus` is projected from the frame.

## Step 4: Add The Projection Function

Projection code should live in the runtime package near related runtime
projection code:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs
```

Example:

```csharp
public static AetheriaRuntimeZoneDefenseStatusDocument ProjectZoneDefenseStatus(
    AetheriaRuntimeDaemonFrameDocument frame)
{
    var zone = frame?.Zones?
        .FirstOrDefault(candidate => candidate != null &&
            candidate.ZoneIndex == frame.CurrentZoneIndex);

    if (zone == null)
        return new AetheriaRuntimeZoneDefenseStatusDocument();

    return new AetheriaRuntimeZoneDefenseStatusDocument
    {
        ZoneIndex = zone.ZoneIndex,
        BaseShieldRatio = ComputeBaseShieldRatio(zone),
        IncomingHostileCount = CountIncomingHostiles(zone),
        ActiveTurretCount = CountActiveTurrets(zone),
        AlertLevel = ResolveAlertLevel(zone)
    };
}
```

Guidelines:

- Keep projection deterministic.
- Do not read Unity objects.
- Do not mutate the frame.
- Keep heavy per-frame render data out of this path; publish SoA columns
  instead.
- If projection needs catalog/loadout/session input, pass it explicitly from
  `AetheriaRuntimeManagedClientInputs`, as station refit and Starbridge summary
  already do.

## Step 5: Publish The Document Through The Verse Client

Expose the projected document in:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseClient.cs
```

In the `CreateClientState` document list, add a `ProjectedDocument`:

```csharp
ProjectedDocument(
    "aetheria.zone.defense_status",
    frame => Task.FromResult(
        AetheriaRuntimeRtsProjection.ProjectZoneDefenseStatus(frame)),
    AetheriaRuntimeDaemonSchemas.ZoneDefenseStatus),
```

This is where CultMesh gets the important metadata:

- document id;
- Verse/runtime identity;
- schema id;
- projection sources;
- latest read;
- watch stream;
- route hint.

Do not make Unity call `ProjectZoneDefenseStatus` directly. Unity asks
CultMesh for the typed document. CultMesh owns the managed sync path.

## Step 6: Add The Handle To `AetheriaClientState`

Add a `CultMeshDocumentHandle<AetheriaRuntimeZoneDefenseStatusDocument>` to:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaClientState.cs
```

Constructor parameter:

```csharp
CultMeshDocumentHandle<AetheriaRuntimeZoneDefenseStatusDocument> zoneDefenseStatus,
```

Property:

```csharp
public CultMeshDocumentHandle<AetheriaRuntimeZoneDefenseStatusDocument>
    ZoneDefenseStatus { get; }
```

Assignment:

```csharp
ZoneDefenseStatus = zoneDefenseStatus
    ?? throw new ArgumentNullException(nameof(zoneDefenseStatus));
```

Register it in the document catalog:

```csharp
_documents = CultMesh.Documents(
    ...
    ZoneDefenseStatus,
    ...
);
```

Convenience methods are fine when they preserve the typed shape:

```csharp
public Task<AetheriaRuntimeZoneDefenseStatusDocument>
    LatestZoneDefenseStatusAsync()
{
    return ZoneDefenseStatus.LatestAsync();
}

public CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument>
    ReactiveZoneDefenseStatus(CultMeshReactiveDocumentOptions? options = null)
{
    return ZoneDefenseStatus.Reactive(options);
}
```

Avoid introducing a `AetheriaRuntimeZoneDefenseStatusSession` unless it composes
multiple documents and genuinely owns aggregate behavior. A single reactive
document should remain a single reactive document.

## Step 7: Add A Typed Unity Read

In Unity, hold the reactive typed document for the component lifetime.

```csharp
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;

public sealed class ZoneDefenseHud : MonoBehaviour
{
    private string _clientStatePath = "";
    private CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument>
        _zoneDefenseStatus;

    private void Update()
    {
        var status = ResolveZoneDefenseStatus()?.Current;
        if (status == null)
            return;

        RenderAlertLevel(status.AlertLevel);
        RenderHostiles(status.IncomingHostileCount);
        RenderShield(status.BaseShieldRatio);
    }

    private CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument>
        ResolveZoneDefenseStatus()
    {
        if (_zoneDefenseStatus != null)
            return _zoneDefenseStatus;

        try
        {
            _zoneDefenseStatus = ResolveClient()
                .State
                .ReactiveZoneDefenseStatus();
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"Failed to bind Aetheria zone defense status: {ex.Message}");
        }

        return _zoneDefenseStatus;
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(
            AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath,
                StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            ClearStateDocuments();
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(
            stateBoot,
            "unity-zone-defense-hud");
    }

    private void ClearStateDocuments()
    {
        _zoneDefenseStatus?.Dispose();
        _zoneDefenseStatus = null;
    }

    private void OnDestroy()
    {
        ClearStateDocuments();
    }
}
```

Unity rules:

- Store `CultMeshReactiveDocument<T>` fields, not sessions, for single docs.
- Dispose reactive documents.
- Recreate them when the client state path changes.
- Use `.Current` in `Update()` or render methods.
- Prefer `LatestAsync()` during async setup; avoid blocking reads on hot paths.
- Do not read `AetheriaRuntimeDaemonFrameDocument` unless the class is an
  internal render adapter or diagnostic.
- Do not project daemon state in Unity UI code.

## Step 8: Add A Typed Operation For Interaction

If Unity needs to interact with the state, add a typed daemon command path.

First extend command kinds and operation ids:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs
```

```csharp
public enum AetheriaRuntimeDaemonCommandKinds
{
    ...
    SetZoneAlertLevel = 42
}
```

Add payload slots to `AetheriaRuntimeDaemonCommandDocument`:

```csharp
[Key(N)]
public string ZoneAlertLevel { get; set; } = "";
```

Then add a typed operation method:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaControl.cs
```

Example public shape:

```csharp
public AetheriaRuntimeDaemonCommandEnvelope SetZoneAlertLevel(
    string alertLevel)
{
    return Submit(command =>
    {
        command.Kind = AetheriaRuntimeDaemonCommandKinds.SetZoneAlertLevel;
        command.ZoneAlertLevel = alertLevel ?? "";
    });
}
```

Unity should call the typed operation:

```csharp
Client.Control.SetZoneAlertLevel("red");
```

Unity should not call:

```csharp
Submit("set_zone_alert_level", new Dictionary<string, object> { ... });
```

## Step 9: Apply The Operation In The Daemon

Apply commands in:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperations.cs
```

The apply path should:

- validate authority/session/frame assumptions;
- validate command payload;
- mutate the run/checkpoint/intents;
- return applied or rejected command ids;
- leave the next tick to publish the derived document.

Sketch:

```csharp
case AetheriaRuntimeDaemonCommandKinds.SetZoneAlertLevel:
    if (!TrySetZoneAlertLevel(run, command.ZoneAlertLevel, out var diagnostic))
    {
        Reject(command, diagnostic);
        break;
    }

    Accept(command);
    break;
```

Prefer command effects that update daemon-owned run state or intent state. Do
not mutate Unity objects. Do not make the Unity caller update its own UI state
optimistically unless that optimism goes through the managed reactive document
prediction/reconciliation path.

## Step 10: Publish Hot Render Data Through SoA When Needed

If the new feature is needed for thousands of entities every frame, do not add
large arrays to a HUD-style document. Publish columns in:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaDocuments.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaFramePublisher.cs
```

Then consume through:

```text
Assets/Scripts/Gameplay/AetheriaDaemonObserver.cs
Assets/Scripts/Gameplay/AetheriaDaemonRenderNativeView.cs
Assets/Scripts/Zone Display/ZoneRenderer.cs
```

Use SoA for data like:

- entity transforms;
- render group;
- visibility;
- physics body columns;
- LOD;
- mass/radius/inverse mass;
- per-entity heat or shield values if the renderer needs them in bulk.

Use ordinary typed documents for data like:

- selected object;
- current docking;
- station services;
- HUD summary;
- scenario state;
- command boundary;
- player settings.

## Step 11: Add Tests

At minimum, add or extend tests in:

```text
Assets/Scripts/Tests/DaemonRuntimeDocumentTests.cs
```

Cover:

- schema document construction;
- projection from a representative daemon frame;
- `AetheriaClientState.Document<T>()` or named handle access;
- `Reactive()` current value behavior;
- command creation;
- command application;
- rejected command behavior if validation matters.

For a projected document, a test should prove:

```csharp
var status = client.State.Latest<AetheriaRuntimeZoneDefenseStatusDocument>();
Assert.AreEqual(expected, status.AlertLevel);
```

If the document is TS-visible, regenerate and verify RTS bindings:

```powershell
cd .\Aetheria.Rts.Web
npm run generate:rts-bindings
npm run check:rts-bindings
```

## Step 12: Add Verifier Coverage

Add migration fences to:

```text
Aetheria.State.Verify/Program.cs
```

Verifier coverage should assert the desired ergonomic shape and forbid the old
path:

```csharp
if (!zoneDefenseHud.Contains(
        "CultMeshReactiveDocument<AetheriaRuntimeZoneDefenseStatusDocument>",
        StringComparison.Ordinal) ||
    !zoneDefenseHud.Contains(".ReactiveZoneDefenseStatus()",
        StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "ZoneDefenseHud must read zone defense through a managed reactive typed document.");
}

if (zoneDefenseHud.Contains("AetheriaRuntimeZoneDefenseStatusSession",
        StringComparison.Ordinal) ||
    zoneDefenseHud.Contains("AetheriaRuntimeRtsProjection.ProjectZoneDefenseStatus",
        StringComparison.Ordinal) ||
    zoneDefenseHud.Contains("AetheriaRuntimeDaemonFrameDocument",
        StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "ZoneDefenseHud still reconstructs zone defense state instead of reading the typed CultMesh document.");
}
```

The verifier should remember architecture, not implementation trivia. Guard
against:

- Unity reading raw frames for ordinary state;
- Unity calling projection helpers;
- new single-document session wrappers;
- public string command/payload APIs;
- reintroduced local save files for daemon-owned state;
- renderer-local facade indexes outside render adapter internals.

## Step 13: Run The Gates

Minimum lane for a C# runtime/Unity state feature:

```powershell
dotnet build .\Aetheria.Shared.Unity.csproj --no-restore --nologo -v:quiet
dotnet run --project .\Aetheria.State.Verify\Aetheria.State.Verify.csproj --no-restore
```

If daemon authority or command behavior changed:

```powershell
dotnet run --project .\Aetheria.State.AuthoritySmoke\Aetheria.State.AuthoritySmoke.csproj --no-restore
```

If document slots consumed by RTS changed:

```powershell
cd .\Aetheria.Rts.Web
npm run generate:rts-bindings
npm run check:rts-bindings
```

If SoA/native view changed:

```powershell
dotnet build .\GameCult.Aetheria.State.Unity.csproj --no-restore -v:minimal
dotnet build .\Aetheria.State.Unity.Smoke\Aetheria.State.Unity.Smoke.csproj --no-restore -v:minimal
```

The verifier often updates `GameData/aetheria-world.cc`. Restore it unless the
state seed was intentionally regenerated:

```powershell
git restore -- GameData/aetheria-world.cc
```

## Ergonomic CultMesh Checklist

Use this checklist before opening a PR.

- The feature has a typed document or typed operation.
- The document has a stable schema id.
- The document type is registered.
- The daemon owns mutation.
- Projection is daemon/runtime code, not Unity UI code.
- Unity reads with `CultMeshReactiveDocument<T>` for single-document state.
- Unity uses SoA/native views for hot render/physics state.
- Unity submits interaction through typed `AetheriaControl` or operation client
  methods.
- No Unity caller manually joins daemon frame rows, record keys, schema slots,
  and facade indexes to get one value.
- No new public string command names or payload dictionaries.
- No new `AetheriaRuntime*Session` wrapper exists just to expose `.Current`.
- Tests cover projection/read/write behavior.
- Verifier blocks the old path.
- Generated bindings are refreshed when public document slots change.

## Anti-Patterns

Do not do this:

```csharp
var frame = client.State.LatestDaemonFrame();
var row = frame.Zones[0].Entities.First(entity => entity.EntityKey == key);
var unityEntity = observedIndex.TryResolveEntityByRecordKey(row.EntityKey, out var e)
    ? e
    : null;
```

Do this:

```csharp
using var selected = client.State
    .Details
    .ReactiveSelectedObject(entityIndex);
Render(selected.Current);
```

Do not do this:

```csharp
using var session = client.State.ObserveCatalog();
var item = session.Current.FindItem(itemKey);
```

For a single document, do this:

```csharp
using var catalog = client.State.ReactiveCatalog();
var item = catalog.Current?.FindItem(itemKey);
```

Do not do this:

```csharp
Submit("set-alert-level", new Dictionary<string, object>
{
    ["level"] = "red"
});
```

Do this:

```csharp
client.Control.SetZoneAlertLevel("red");
```

Do not do this:

```csharp
var status = AetheriaRuntimeRtsProjection.ProjectZoneDefenseStatus(frame);
```

in Unity presentation code.

Do this:

```csharp
using var status = client.State.ReactiveZoneDefenseStatus();
```

## Where To Put Things

Typed documents and schema constants:

```text
Packages/org.gamecult.aetheria.state/Runtime/
```

Registry:

```text
Aetheria.State/AetheriaDocumentRegistry.cs
```

Projection functions:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeRtsProjection.cs
```

CultMesh document publication:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeVerseClient.cs
```

Client accessors:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaClientState.cs
```

Operations:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonDocuments.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperationClient.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaControl.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonOperations.cs
```

Daemon tick/publications:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonTickRunner.cs
```

SoA/native render state:

```text
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaDocuments.cs
Packages/org.gamecult.aetheria.state/Runtime/AetheriaRuntimeDaemonSoaFramePublisher.cs
Assets/Scripts/Gameplay/AetheriaDaemonRenderNativeView.cs
```

Unity callers:

```text
Assets/Scripts/
```

Tests:

```text
Assets/Scripts/Tests/DaemonRuntimeDocumentTests.cs
Aetheria.State.AuthoritySmoke/
Aetheria.State.Unity.Smoke/
```

Verifier:

```text
Aetheria.State.Verify/Program.cs
```

## Final Mental Model

CultMesh should make remote/stateful gameplay feel local without making clients
pretend they own authority they do not have.

For reads, the caller names the state and receives a typed value:

```csharp
using var status = client.State.ReactiveZoneDefenseStatus();
Render(status.Current);
```

For writes, the caller names the domain action and receives an operation
receipt/command envelope:

```csharp
var receipt = client.Control.SetZoneAlertLevel("red");
```

For hot rendering, the caller asks for a native view:

```csharp
var view = observer.LastObservedState?.SoaIndex;
```

Everything between those calls and the daemon is CultMesh's job: routing,
replication, cache hydration, projection updates, schema identity, local versus
network access, and eventually prediction/reconciliation. Aetheria code should
read like Aetheria, not like a protocol walk.
