$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

& node scripts/generate-rts-bindings.mjs --check
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: generated RTS bindings are stale."
}

$forbidden = @(
    'window\.aetheriaRts\.command',
    'window\.aetheriaRts\.viewport',
    'ipcMain\.handle\("aetheria-rts:command',
    'ipcMain\.handle\("aetheria-rts:viewport',
    'function createCommandDocument',
    'export type RtsCommandRequest',
    'public async command\(',
    'public async viewport\('
)

$forbiddenPattern = ($forbidden -join '|')
$forbiddenHits = & rg -n $forbiddenPattern Client Electron 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: public generic RTS command/viewport surface still exists.`n$forbiddenHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for forbidden public surfaces."
}

$required = @(
    'mapViewport',
    'objectsViewport',
    'gravityViewport',
    'selectedObject',
    'inventory',
    'daemonHealth',
    'authorityStatus',
    'starbridgeSession',
    'setMoveVector',
    'setTarget',
    'surfaceCatalogIndex',
    'AetheriaRtsIpcChannels'
)

foreach ($symbol in $required) {
    & rg -q $symbol Client Electron
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected typed RTS client symbol '$symbol' was not found."
    }
}

if (-not (Get-Content Client/app.ts -Raw).Contains('window.aetheriaRts.surfaceCatalogIndex()')) {
    Write-Error "Stage 7B verifier failed: RTS renderer does not read the shared CultMesh surface catalog index."
}

$rendererText = Get-Content Client/app.ts -Raw
if (-not $rendererText.Contains('surface.routeHint.kind') -or -not $rendererText.Contains('surface.sources.length')) {
    Write-Error "Stage 7B verifier failed: RTS renderer does not expose CultMesh surface route and source metadata."
}

if (-not $rendererText.Contains('latestOperationReceipt') -or
    -not $rendererText.Contains('receipt.commandId') -or
    -not $rendererText.Contains('receipt.operationId') -or
    -not $rendererText.Contains('receipt.accepted') -or
    -not $rendererText.Contains('receipt.route.kind')) {
    Write-Error "Stage 7B verifier failed: RTS renderer does not expose CultMesh operation receipt metadata."
}

if (-not (Get-Content wwwroot/index.html -Raw).Contains('runtime-surface-details')) {
    Write-Error "Stage 7B verifier failed: RTS renderer does not expose runtime surface diagnostics in the shell."
}

$transportLayoutPatterns = @(
    '\[[0-9]+\]',
    'new Array<unknown>',
    'command\[',
    'value\[',
    'bounds\[',
    'viewportSchema',
    'commandSchema',
    'createBaseCommandDocument',
    'decodeViewObject',
    'decodeInventoryItem',
    'decodeGravityInfluence',
    'decodeBodyView',
    'fetchViewportDocument',
    'fetchDocument\(',
    'sendSnapshotRequest',
    'schemaIds',
    'recordKeys',
    'CultNetSnapshotResponseRawMessage',
    'AetheriaRtsSchemas\.rtsViewport'
)

$transportLayoutPattern = ($transportLayoutPatterns -join '|')
$transportLayoutHits = & rg -n $transportLayoutPattern Electron/aetheria-cultmesh.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: RTS CultMesh transport wrapper still owns document layout/codec details.`n$transportLayoutHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for transport wrapper layout checks."
}

$remotePublicationReaderPatterns = @(
    '@msgpack/msgpack',
    'sendSnapshotRequest',
    'snapshot_response_raw',
    'CultNetSnapshotResponseRawMessage',
    'recordKeys',
    'decode\('
)

$remotePublicationReaderPattern = ($remotePublicationReaderPatterns -join '|')
$remotePublicationReaderHits = & rg -n $remotePublicationReaderPattern Electron/aetheria-remote-publication-reader.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: RTS remote publication reader still owns raw snapshot/codec details.`n$remotePublicationReaderHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for remote publication reader layout checks."
}

$requiredBindingSymbols = @(
    'createAetheriaRuntimeRtsOperationHandles',
    'createAetheriaRuntimeRtsQueryHandles',
    'AetheriaRtsSchemas'
)

foreach ($symbol in $requiredBindingSymbols) {
    & rg -q $symbol Electron/aetheria-rts-bindings.ts
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected RTS binding symbol '$symbol' was not found."
    }
}

$requiredGeneratedBindingSymbols = @(
    'AetheriaRtsIpcChannels',
    'registerAetheriaRtsIpcHandlers',
    'export type AetheriaRtsMainClient',
    'aetheria-rts:map-viewport',
    'aetheria-rts:objects-viewport',
    'aetheria-rts:viewport-feed',
    'aetheria-rts:viewport-feed-update',
    'aetheria-rts:set-move-vector',
    'export type AetheriaRuntimeSelectedObjectRequest',
    'export type AetheriaRuntimeViewportFeedRequest',
    'export type AetheriaRuntimeViewportFeedSnapshot',
    'aetheriaRuntimeDaemonCommandDocumentSlots',
    'encodeSetMoveVectorCommand',
    'encodeSetTargetCommand',
    'AetheriaRuntimeRtsProjectionSources',
    'AetheriaRuntimeRtsProjectionDiagnostic',
    'AetheriaRuntimeRtsLiveFeedDiagnostic',
    'AetheriaRuntimeRtsSurfaceCatalogDiagnostic',
    'AetheriaRuntimeRtsDocuments',
    'AetheriaRuntimeRtsDocumentResolvers',
    'CultMeshSurfaceCatalogDiagnostic',
    'CultMeshSurfaceCatalogIndexDiagnostic',
    'CultMeshOperationReceipt',
    'aetheria-rts:surface-catalog',
    'aetheria-rts:surface-catalog-index',
    'surfaceCatalogDiagnostics(): CultMeshSurfaceCatalogDiagnostic',
    'surfaceCatalogIndexDiagnostics(): CultMeshSurfaceCatalogIndexDiagnostic',
    'ipcMain.handle(AetheriaRtsIpcChannels.surfaceCatalog',
    'ipcMain.handle(AetheriaRtsIpcChannels.surfaceCatalogIndex',
    'CultMesh.projectionSource',
    'CultMesh.projectionRecipe',
    'CultMeshProjectionSource',
    'routeHint: CultMeshRouteHint',
    'CultMeshQueryWatcher',
    'AetheriaRuntimeRtsQueryWatchers',
    'createAetheriaRuntimeRtsDocuments',
    'CultMesh.document',
    'CultMesh.bindDocument',
    'CultMesh.describeSurface(documents.daemonFrame)',
    'routeHint, watchProjection',
    'watchProjection: watchers.objectsViewport',
    '.asQuerySurface()',
    'describeAetheriaRuntimeRtsQueryHandles',
    'describeAetheriaRuntimeRtsLiveFeedSurface',
    'describeAetheriaRuntimeRtsSurfaceCatalog',
    'CultMesh.describeQuerySurface',
    'CultMesh.describeLiveFeed',
    'CultMesh.describeSurfaceCatalog',
    'CultMesh.describeSurface',
    'CultMesh.describeSurface(operations.setMoveVector)',
    'CultMesh.describeSurface(operations.setTarget)',
    'CultMesh.operation',
    'CultMesh.operationReceipt',
    'AetheriaRuntimeRtsOperationHandles',
    'AetheriaRuntimeRtsVerseFacade',
    'createAetheriaRuntimeRtsVerseFacade',
    'CultMesh.bindQuery',
    'CultMesh.bindOperation',
    'zone: (_zoneId =',
    'visibleWithin',
    'entity: (entityKey: string)',
    'pilot',
    'selectedObject: CultMesh.projectionRecipe',
    'inventory: CultMesh.projectionRecipe',
    'daemonHealth: CultMesh.projectionRecipe',
    'authorityStatus: CultMesh.projectionRecipe',
    'starbridgeSession: CultMesh.projectionRecipe'
)

foreach ($symbol in $requiredGeneratedBindingSymbols) {
    & rg -F -q $symbol Electron/aetheria-rts-generated-bindings.ts
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected generated RTS binding symbol '$symbol' was not found."
    }
}

$forbiddenGeneratedQuerySurfaceHits = & rg -n 'CultMesh\.query\s*[<(]' Electron/aetheria-rts-generated-bindings.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: generated RTS read surfaces should use CultMesh projection recipes, not raw query wrappers.`n$forbiddenGeneratedQuerySurfaceHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for generated projection recipe checks."
}

$forbiddenGeneratedContextPlumbingHits = & rg -n 'CultMesh\.(queryContextFromVerse|operationContextFromVerse)|\.execute\([^,\n]+,\s*queryContext\(|\.invoke\([^,\n]+,\s*operationContext\(' Electron/aetheria-rts-generated-bindings.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: generated RTS facade should bind surfaces to Verse contexts instead of threading context plumbing.`n$forbiddenGeneratedContextPlumbingHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for generated Verse binding checks."
}

& rg -q 'createAetheriaRuntimeRtsQueryHandles\(' Electron/aetheria-cultmesh.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: RTS client is not creating generated query handles."
}

$clientText = Get-Content Electron/aetheria-cultmesh.ts -Raw

if (-not $clientText.Contains('this.documents = createAetheriaRuntimeRtsDocuments(') -or
    -not $clientText.Contains('this.queryVerse.context.routeHint,')) {
    Write-Error "Stage 7B verifier failed: RTS client is not creating generated document handles with its shared Verse route."
}

if (-not $clientText.Contains('daemonFrame: async () => this.fetchLatestFrameDocument()') -or
    -not $clientText.Contains('authorityPolicy: async () => this.fetchAuthorityPolicyDocument()')) {
    Write-Error "Stage 7B verifier failed: RTS client document handles are not resolving through daemon publication readers."
}

if (-not $clientText.Contains('this.verse = CultMesh.verse("aetheria.local", this.runtimeId)')) {
    Write-Error "Stage 7B verifier failed: RTS client should bind to the shared CultMesh Verse primitive instead of constructing local contexts directly."
}

if (-not $clientText.Contains('? this.verse.withRoute("network", this.publications.statePathDescription)') -or
    -not $clientText.Contains(': this.verse.withRoute("shared-memory", this.publications.statePathDescription)')) {
    Write-Error "Stage 7B verifier failed: RTS client is not deriving publication-mode Verse views for projection reads."
}

if (-not $clientText.Contains('this.commandVerse = this.verse') -or
    -not $clientText.Contains('.withRoute("network", this.endpoint)') -or
    -not $clientText.Contains('.withClaim("commander-control"')) {
    Write-Error "Stage 7B verifier failed: RTS client is not deriving a network Verse view with an explicit command authority claim."
}

if (-not $clientText.Contains('this.publications.statePathDescription')) {
    Write-Error "Stage 7B verifier failed: RTS client projection route metadata does not include the local publication source description."
}

if (-not $clientText.Contains('return this.queryVerse.queryContext();')) {
    Write-Error "Stage 7B verifier failed: RTS query call sites should derive query contexts from the shared Verse primitive."
}

if (-not $clientText.Contains('this.aetheria = createAetheriaRuntimeRtsVerseFacade(')) {
    Write-Error "Stage 7B verifier failed: RTS client should bind generated Aetheria domain sugar to its Verse contexts."
}

if (-not $clientText.Contains('return this.aetheria.zone().objects.visibleWithin(request);') -or
    -not $clientText.Contains('return this.aetheria.entity(request.actorEntityKey).pilot.move(') -or
    -not $clientText.Contains('return this.aetheria.entity(request.actorEntityKey).pilot.target(')) {
    Write-Error "Stage 7B verifier failed: RTS client should route viewport and pilot operations through generated Verse domain sugar."
}

$forbiddenRawContextHits = & rg -n 'CultMesh\.(operationContext|queryContext)\(this\.runtimeId' Electron/aetheria-cultmesh.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: RTS client still constructs raw runtime-id contexts instead of using the shared Verse primitive.`n$forbiddenRawContextHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for raw context checks."
}

if (-not $clientText.Contains('projectionDiagnostics()')) {
    Write-Error "Stage 7B verifier failed: RTS client does not expose generated projection diagnostics."
}

if (-not $clientText.Contains('describeAetheriaRuntimeRtsQueryHandles(this.queries)')) {
    Write-Error "Stage 7B verifier failed: RTS projection diagnostics should be read from generated query handle metadata."
}

if (-not $clientText.Contains('liveFeedDiagnostics()')) {
    Write-Error "Stage 7B verifier failed: RTS client does not expose generated live feed diagnostics."
}

if (-not $clientText.Contains('describeAetheriaRuntimeRtsLiveFeedSurface(this.createViewportFeed())')) {
    Write-Error "Stage 7B verifier failed: RTS live feed diagnostics should be read from generated live feed metadata."
}

if (-not $clientText.Contains('surfaceCatalogDiagnostics()')) {
    Write-Error "Stage 7B verifier failed: RTS client does not expose a unified CultMesh surface catalog diagnostic."
}

if (-not $clientText.Contains('surfaceCatalogIndexDiagnostics()')) {
    Write-Error "Stage 7B verifier failed: RTS client does not expose a grouped CultMesh surface catalog diagnostic."
}

if (-not $clientText.Contains('CultMesh.surfaceCatalogIndex(this.surfaceCatalogDiagnostics())')) {
    Write-Error "Stage 7B verifier failed: RTS grouped surface catalog should use the shared CultMesh catalog index primitive."
}

if (-not $clientText.Contains('describeAetheriaRuntimeRtsSurfaceCatalog(this.queries, this.operations, this.documents)')) {
    Write-Error "Stage 7B verifier failed: RTS surface catalog should be read from generated query, operation, and document metadata."
}

$forbiddenDirectClientOperationHits = & rg -n 'operations\.set(MoveVector|Target)\.invoke' Electron/aetheria-cultmesh.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: RTS client still invokes operation handles directly instead of generated domain sugar.`n$forbiddenDirectClientOperationHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for direct operation handle checks."
}

if (-not $clientText.Contains('CultMesh.liveFeed<AetheriaRuntimeViewportFeedRequest, AetheriaRuntimeViewportFeedSnapshot>')) {
    Write-Error "Stage 7B verifier failed: RTS viewport feed should use the shared CultMesh live feed primitive."
}

$requiredGeneratedPreloadSymbols = @(
    '<auto-generated',
    'channels.mapViewport',
    'channels.objectsViewport',
    'channels.gravityViewport',
    'watchViewportFeed',
    'channels.viewportFeedUpdate',
    'channels.viewportFeedStop',
    'channels.setMoveVector',
    'channels.setTarget',
    'channels.surfaceCatalog',
    'channels.surfaceCatalogIndex'
)

foreach ($symbol in $requiredGeneratedPreloadSymbols) {
    & rg -F -q $symbol Electron/preload.cjs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected generated preload symbol '$symbol' was not found."
    }
}

$forbiddenMainIpcStringHits = & rg -n 'ipcMain\.handle\("aetheria-rts:' Electron/main.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: Electron main still registers raw RTS IPC channel strings.`n$forbiddenMainIpcStringHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for raw Electron main IPC checks."
}

$forbiddenMainGeneratedHandlerMirrors = @(
    'AetheriaRtsIpcChannels\.mapViewport',
    'AetheriaRtsIpcChannels\.objectsViewport',
    'AetheriaRtsIpcChannels\.setMoveVector'
)
$forbiddenMainGeneratedHandlerPattern = ($forbiddenMainGeneratedHandlerMirrors -join '|')
$forbiddenMainGeneratedHandlerHits = & rg -n $forbiddenMainGeneratedHandlerPattern Electron/main.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: Electron main still mirrors generated IPC handler registration.`n$forbiddenMainGeneratedHandlerHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for generated IPC handler mirror checks."
}

& rg -q 'registerAetheriaRtsIpcHandlers' Electron/main.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: Electron main is not using generated RTS IPC handler registration."
}

$forbiddenClientDirectProjectionReturns = @(
    'return projectSelectedObjectFromFrame',
    'return projectInventoryFromFrame',
    'return projectDaemonHealth',
    'return projectAuthorityStatus',
    'return projectStarbridgeSessionSummary'
)
$forbiddenClientDirectProjectionPattern = ($forbiddenClientDirectProjectionReturns -join '|')
$forbiddenClientDirectProjectionHits = & rg -n $forbiddenClientDirectProjectionPattern Electron/aetheria-cultmesh.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: RTS client read methods bypass generated query handles.`n$forbiddenClientDirectProjectionHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for direct projection return checks."
}

$requiredRendererContractSymbols = @(
    'export type AetheriaRtsApi',
    'export type ViewportResponse',
    'export type ObjectsViewportResponse',
    'export type GravityViewportResponse',
    'export type AetheriaRuntimeViewportFeedRequest',
    'export type AetheriaRuntimeViewportFeedSnapshot',
    'watchViewportFeed',
    'export type AetheriaRuntimeSetMoveVectorRequest',
    'export type AetheriaRuntimeSetTargetRequest'
)

foreach ($symbol in $requiredRendererContractSymbols) {
    & rg -q $symbol Client/aetheria-rts-contract.ts
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected generated renderer contract symbol '$symbol' was not found."
    }
}

$forbiddenRendererAppPatterns = @(
    '^type ViewportResponse =',
    '^type ObjectsViewportResponse =',
    '^type GravityViewportResponse =',
    '^type AetheriaRtsApi =',
    '^type ViewObject =',
    '^type InventoryItem ='
)

$forbiddenRendererAppPattern = ($forbiddenRendererAppPatterns -join '|')
$forbiddenRendererAppHits = & rg -n $forbiddenRendererAppPattern Client/app.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: renderer still owns duplicated RTS contract types.`n$forbiddenRendererAppHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for duplicated renderer contract checks."
}

$forbiddenRendererPollingHits = & rg -n 'setInterval|objectsViewport\(viewport\)|gravityViewport\(viewport\)' Client/app.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: renderer still owns the RTS viewport polling loop instead of subscribing to the generated feed.`n$forbiddenRendererPollingHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for renderer polling checks."
}

& rg -q 'watchViewportFeed' Client/app.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: renderer is not subscribing to the generated viewport feed."
}

$forbiddenBindingPatterns = @(
    'decodeViewportDocument',
    'viewportRecordKey',
    'aetheriaRuntimeRtsViewportDocumentSlots',
    'aetheriaRuntimeRtsViewportObjectSlots',
    '^export type ViewportResponse =',
    '^export type ObjectsViewportResponse =',
    '^export type GravityViewportResponse =',
    '^export type ViewObject =',
    '^export type InventoryItem =',
    '^export type SelectedObjectRequest ='
)

$forbiddenBindingPattern = ($forbiddenBindingPatterns -join '|')
$forbiddenBindingHits = & rg -n $forbiddenBindingPattern Electron/aetheria-rts-bindings.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: stale remote viewport decoder surface still exists in RTS bindings.`n$forbiddenBindingHits"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for stale binding checks."
}

& rg -q 'projectViewportFromFrame' Electron/aetheria-rts-local-projection.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: local RTS frame projection entry point was not found."
}

$requiredProjectionSymbols = @(
    'projectObjectsViewportFromFrame',
    'projectGravityViewportFromFrame',
    'AetheriaRtsSchemas.objectsViewport',
    'AetheriaRtsSchemas.gravityViewport',
    'projectSelectedObjectFromFrame',
    'projectInventoryFromFrame',
    'AetheriaRtsSchemas.selectedObject',
    'AetheriaRtsSchemas.inventory',
    'projectDaemonHealth',
    'projectAuthorityStatus',
    'projectStarbridgeSessionSummary',
    'AetheriaRtsSchemas.starbridgeSessionSummary'
)

foreach ($symbol in $requiredProjectionSymbols) {
    & rg -q $symbol Electron/aetheria-rts-local-projection.ts
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected local RTS projection symbol '$symbol' was not found."
    }
}

& rg -q 'CultMesh.documentFromSingleFile' Electron/aetheria-local-publication-reader.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: local publications should be read through CultMesh document handles."
}

$forbiddenLocalPublicationReaderPlumbing = & rg -n 'SingleFileMessagePackBackingStore|decode\(|pullAll\(' Electron/aetheria-local-publication-reader.ts 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Error "Stage 7B verifier failed: local publication reader still owns backing-store or MessagePack plumbing.`n$forbiddenLocalPublicationReaderPlumbing"
}
if ($LASTEXITCODE -gt 1) {
    Write-Error "Stage 7B verifier could not run rg for local publication reader plumbing checks."
}

& rg -q 'readStarbridgeSessionSummary' Electron/aetheria-local-publication-reader.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: local Starbridge summary publication reader was not found."
}

& rg -q '.daemon.starbridge.session.cc' Electron/aetheria-local-publication-reader.ts
if ($LASTEXITCODE -ne 0) {
    Write-Error "Stage 7B verifier failed: RTS client is not reading the daemon Starbridge sidecar publication."
}

$requiredGeneratedBindingSymbols = @(
    'aetheriaRuntimeDaemonCommandDocumentSlots',
    'aetheriaRuntimeDaemonFrameDocumentSlots',
    'aetheriaRuntimeDaemonHealthDocumentSlots',
    'aetheriaRuntimeRtsViewportDocumentSlots',
    'aetheriaRuntimeObjectsViewportDocumentSlots',
    'aetheriaRuntimeGravityViewportDocumentSlots',
    'aetheriaRuntimeSelectedObjectDocumentSlots',
    'aetheriaRuntimeInventoryDocumentSlots',
    'aetheriaRuntimeStarbridgeScenarioDocumentSlots',
    'aetheriaRuntimeStarbridgeSessionDocumentSlots',
    'aetheriaRuntimeStarbridgeSessionSummaryDocumentSlots',
    'aetheriaRuntimeStarbridgeStationStockItemSlots',
    'aetheriaRuntimeStarbridgeWaveDefinitionSlots',
    'aetheriaRuntimeStarbridgeWaveForecastSlots',
    'aetheriaRuntimeStarbridgeRuntimeRoleSlots',
    'aetheriaRuntimeStarbridgeBaseStatusSlots',
    'aetheriaRuntimeRtsViewportBoundsSlots',
    'aetheriaRuntimeRtsViewportObjectSlots',
    'aetheriaRuntimeVerseAuthorityPolicyDocumentSlots',
    'aetheriaRuntimeAuthorityRuleSlots',
    'aetheriaRuntimeAuthorityLeaseDocumentSlots',
    'aetheriaRuntimeRunCheckpointCommitSlots',
    'aetheriaRuntimeZoneSnapshotCommitSlots',
    'aetheriaRuntimeEntitySnapshotCommitSlots',
    'aetheriaRuntimeBodySnapshotCommitSlots',
    'aetheriaRuntimeEntityStatGridCommitSlots',
    'aetheriaRuntimeCargoBayLoadoutCommitSlots',
    'aetheriaRuntimeLoadoutItemSlotCommitSlots',
    'aetheriaRuntimeLoadoutItemCommitSlots',
    'AetheriaRuntimeDaemonCommandKinds',
    'AetheriaRtsSchemas'
)

foreach ($symbol in $requiredGeneratedBindingSymbols) {
    & rg -q $symbol Electron/aetheria-rts-generated-bindings.ts
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Stage 7B verifier failed: expected generated RTS binding symbol '$symbol' was not found."
    }
}

Write-Host "Stage 7B RTS client verifier passed."
