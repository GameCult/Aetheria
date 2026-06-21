extern alias PackageUnity;

using Aetheria.State;
using Aetheria.State.Unity;
using PackageUnity::GameCult.Aetheria.State.Unity;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var client = await AetheriaRuntimeCatalogClient.OpenAsync(statePath);
var catalog = client.ReadCatalog();
var packageCatalog = AetheriaRuntimeCatalogStore.OpenReadOnly(statePath);
var packageSurfaces = AetheriaRuntimeCatalogStore.ReadEveSurfaces(statePath);
var surface = await client.ReadCatalogSurfaceAsync();
var commitSmokeDirectory = Path.Combine(Path.GetTempPath(), "aetheria-state-unity-smoke", Guid.NewGuid().ToString("N"));
var commitSmokeStatePath = Path.Combine(commitSmokeDirectory, "aetheria-world.cc");
Directory.CreateDirectory(commitSmokeDirectory);

if (catalog.Items.Count != 115)
{
    throw new InvalidOperationException($"Expected 115 runtime catalog items, found {catalog.Items.Count}.");
}

if (packageCatalog.Items.Count != catalog.Items.Count ||
    packageCatalog.Corporations.Count != catalog.Corporations.Count ||
    packageCatalog.NameFiles.Count != catalog.NameFiles.Count)
{
    throw new InvalidOperationException(
        $"Package catalog store mismatch: {packageCatalog.Items.Count}/{packageCatalog.Corporations.Count}/{packageCatalog.NameFiles.Count} " +
        $"!= {catalog.Items.Count}/{catalog.Corporations.Count}/{catalog.NameFiles.Count}.");
}

if (catalog.TradeItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no trade items.");
}

if (catalog.EquipmentItems.Count == 0)
{
    throw new InvalidOperationException("Runtime catalog has no equipment items.");
}

var shaped = catalog.Items.FirstOrDefault(item => item.ShapeCells.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed shape masks.");
if (string.IsNullOrWhiteSpace(shaped.ItemKey))
{
    throw new InvalidOperationException($"Runtime shape sample has no typed item key: {shaped.Name}.");
}

var packageShaped = packageCatalog.FindItem(shaped.ItemKey);
if (packageShaped == null ||
    packageShaped.Name != shaped.Name ||
    packageShaped.ShapeCells.Count != shaped.ShapeCells.Count)
{
    throw new InvalidOperationException($"Package catalog store did not read the expected typed item payload for {shaped.ItemKey}.");
}

if (shaped.ShapeCells.Count != shaped.OccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime shape mask count mismatch for {shaped.Name}: cells={shaped.ShapeCells.Count}, occupied={shaped.OccupiedCells}.");
}

if (shaped.ShapeCells.Any(cell => cell.X < 0 || cell.Y < 0 || cell.X >= shaped.ShapeWidth || cell.Y >= shaped.ShapeHeight))
{
    throw new InvalidOperationException($"Runtime shape mask has out-of-bounds cells for {shaped.Name}.");
}

var interior = catalog.Items.FirstOrDefault(item => item.InteriorShapeCells.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed interior shape masks.");
if (interior.InteriorShapeCells.Count != interior.InteriorOccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime interior shape mask count mismatch for {interior.Name}: cells={interior.InteriorShapeCells.Count}, occupied={interior.InteriorOccupiedCells}.");
}

var hardpointHost = catalog.Items.FirstOrDefault(item => item.Hardpoints.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed hardpoints.");
var hardpoint = hardpointHost.Hardpoints.First();
if (hardpoint.ShapeCells.Count != hardpoint.OccupiedCells)
{
    throw new InvalidOperationException(
        $"Runtime hardpoint shape count mismatch for {hardpointHost.Name}: cells={hardpoint.ShapeCells.Count}, occupied={hardpoint.OccupiedCells}.");
}

var behaviorKind = catalog.Items.SelectMany(item => item.BehaviorKinds).FirstOrDefault()
    ?? throw new InvalidOperationException("Runtime catalog has no behavior kind fingerprints.");
if (!catalog.FindItemsByBehavior(behaviorKind).Any())
{
    throw new InvalidOperationException($"Runtime catalog behavior lookup failed for {behaviorKind}.");
}

var behaviorHost = catalog.Items.FirstOrDefault(item => item.BehaviorPayloads.Count > 0)
    ?? throw new InvalidOperationException("Runtime catalog has no typed behavior payloads.");
if (!behaviorHost.BehaviorPayloads.All(payload => behaviorHost.BehaviorKinds.Contains(payload.Kind)))
{
    throw new InvalidOperationException(
        $"Runtime behavior payload kind index mismatch for {behaviorHost.Name}.");
}

var behaviorPayload = behaviorHost.BehaviorPayloads.First();
if (behaviorPayload.Fields.Count == 0)
{
    throw new InvalidOperationException($"Runtime behavior payload has no fields for {behaviorHost.Name}.");
}

var behaviorItemRefsMissingItemKeys = packageCatalog.Items
    .SelectMany(item => item.BehaviorPayloads)
    .Sum(CountRequiredBehaviorItemRefsMissingItemKeys);
if (behaviorItemRefsMissingItemKeys > 0)
{
    throw new InvalidOperationException(
        $"Runtime behavior payload item refs missing item-key projections: {behaviorItemRefsMissingItemKeys}.");
}

var equipment = catalog.EquipmentItems.First();
if (!catalog.FindItemsByHardpoint(equipment.HardpointType).Any())
{
    throw new InvalidOperationException($"Runtime catalog hardpoint lookup failed for {equipment.HardpointType}.");
}

var manufactured = catalog.Items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ManufacturerKey))
    ?? throw new InvalidOperationException("Runtime catalog has no manufactured item.");
if (string.IsNullOrWhiteSpace(manufactured.ManufacturerKey) ||
    catalog.FindCorporation(manufactured.ManufacturerKey) == null ||
    catalog.GetManufacturer(manufactured) == null)
{
    throw new InvalidOperationException($"Runtime catalog typed manufacturer lookup failed for {manufactured.Name}.");
}

var packageCorporation = packageCatalog.Corporations.FirstOrDefault(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileKey));
if (packageCorporation == null)
{
    throw new InvalidOperationException("Package catalog store did not read any corporation name-file links.");
}

var packageNameFile = packageCatalog.GetNameFile(packageCorporation);
if (string.IsNullOrWhiteSpace(packageCorporation.GeonameFileKey) ||
    packageCatalog.FindNameFile(packageCorporation.GeonameFileKey) == null ||
    packageNameFile == null ||
    string.IsNullOrWhiteSpace(packageNameFile.NameFileKey) ||
    packageNameFile.Names.Count == 0 ||
    packageNameFile.Names.Count != packageNameFile.NameCount)
{
    throw new InvalidOperationException("Package catalog store did not read typed corporation/name-file links with full names.");
}

if (packageCorporation.Allegiances.Count == 0 || packageCorporation.Allegiances.Count != packageCorporation.AllegianceCount)
{
    throw new InvalidOperationException("Package catalog store did not read corporation allegiance edges.");
}

if (surface?.Schema != "gamecult.eve.surface.v1" ||
    surface.Surface.Id != AetheriaCatalogSurfaceProjector.SurfaceId)
{
    throw new InvalidOperationException("Runtime catalog client did not read the typed Eve surface.");
}

var packageSurface = packageSurfaces.FirstOrDefault(candidate => candidate.Surface.Id == AetheriaCatalogSurfaceProjector.SurfaceId);
if (packageSurface == null ||
    packageSurface.Schema != "gamecult.eve.surface.v1" ||
    packageSurface.ProviderId != "aetheria" ||
    packageSurface.Surface.Root.Kind != "surface" ||
    packageSurface.Surface.Root.Children.Count == 0 ||
    packageSurface.Commands.All(command => command.Command != AetheriaRuntimeCatalogCommands.Refresh))
{
    throw new InvalidOperationException("Package runtime store did not read the typed Eve surface contract from CultCache.");
}

try
{
    var eveCommand = AetheriaRuntimeEveCommands.SubmitCatalogCommand(
        commitSmokeStatePath,
        AetheriaRuntimeCatalogCommands.Refresh,
        "aetheria-state-unity-smoke");
    await using var eveCommandNode = await AetheriaStateNode.OpenAsync(
        commitSmokeStatePath,
        "aetheria-unity-runtime-eve-command-smoke");
    await eveCommandNode.SubmitEveCommandAsync(new GameCult.Aetheria.State.Unity.AetheriaRuntimeEveCommandDocument
    {
        Schema = eveCommand.Schema,
        CommandId = eveCommand.CommandId,
        ProviderId = eveCommand.ProviderId,
        SurfaceId = eveCommand.SurfaceId,
        Command = eveCommand.Command,
        IssuedAtUtc = eveCommand.IssuedAtUtc,
        ClientId = eveCommand.ClientId,
        PlayerSettings = new GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommandBody
        {
            PlayerName = eveCommand.PlayerSettings.PlayerName
        },
        InputSettings = new GameCult.Aetheria.State.Unity.AetheriaRuntimeInputSettingsCommandBody
        {
            ActionName = eveCommand.InputSettings.ActionName,
            BindingIndex = eveCommand.InputSettings.BindingIndex,
            InputSystemPath = eveCommand.InputSettings.InputSystemPath,
            Enabled = eveCommand.InputSettings.Enabled
        }
    });
    var eveSubmitted = eveCommandNode.ReadObservedEveCommands();
    if (eveSubmitted.Count != 1 ||
        eveSubmitted[0].Schema != AetheriaRuntimeEveCommandClient.CommandSchema ||
        eveSubmitted[0].CommandId != eveCommand.CommandId ||
        eveSubmitted[0].ProviderId != "aetheria" ||
        eveSubmitted[0].SurfaceId != AetheriaRuntimeCatalogCommands.SurfaceId ||
        eveSubmitted[0].Command != AetheriaRuntimeCatalogCommands.Refresh ||
        eveSubmitted[0].PlayerSettings == null ||
        eveSubmitted[0].InputSettings == null)
    {
        throw new InvalidOperationException("Runtime Eve command state record did not preserve typed command envelopes separately from state commits.");
    }
}
finally
{
    if (Environment.GetEnvironmentVariable("AETHERIA_KEEP_SMOKE_STATE") == "1")
        Console.Error.WriteLine($"Kept smoke state: {commitSmokeDirectory}");
    else
        Directory.Delete(commitSmokeDirectory, true);
}

Console.WriteLine($"Aetheria Unity runtime catalog smoke passed: {statePath}");
Console.WriteLine($"Items/trade/equipment: {catalog.Items.Count}/{catalog.TradeItems.Count}/{catalog.EquipmentItems.Count}");
Console.WriteLine($"Shape mask sample: {shaped.Name} {shaped.ShapeWidth}x{shaped.ShapeHeight}/{shaped.ShapeCells.Count}");
Console.WriteLine($"Interior/hardpoint sample: {interior.Name} {interior.InteriorShapeCells.Count}; {hardpointHost.Name} {hardpoint.Type} {hardpoint.ShapeCells.Count}");
Console.WriteLine($"Behavior payload sample: {behaviorHost.Name} {behaviorPayload.Kind}/{behaviorPayload.Fields.Count}");
Console.WriteLine($"Behavior sample: {behaviorKind}");
Console.WriteLine($"Eve surface: {surface.Surface.Id}");
Console.WriteLine($"Package Eve surfaces: {packageSurfaces.Count}");
Console.WriteLine("Runtime Eve command smoke: surface command document is a typed state record");

static int CountRequiredBehaviorItemRefsMissingItemKeys(AetheriaRuntimeBehaviorPayload payload)
{
    return payload.Fields.Count(field =>
        IsBehaviorItemRefField(payload.Kind, field.Key) &&
        IsNonEmptyLegacyRefMissingItemKey(field.Value));
}

static bool IsBehaviorItemRefField(string behaviorKind, int fieldKey)
{
    return (string.Equals(behaviorKind, "ItemUsage", StringComparison.OrdinalIgnoreCase) && fieldKey == 1) ||
           (IsWeaponBehavior(behaviorKind) && fieldKey == 12);
}

static bool IsWeaponBehavior(string behaviorKind)
{
    return behaviorKind is "GuidedWeapon" or "InstantWeapon" or "ConstantWeapon" or "ChargedWeapon" or "AutoWeapon" or "LockWeapon";
}

static bool IsNonEmptyLegacyRefMissingItemKey(AetheriaRuntimeBehaviorValue value)
{
    return string.Equals(value.Kind, "legacy-id", StringComparison.OrdinalIgnoreCase) &&
           !IsEmptyLegacyId(value.LegacyIdValue) &&
           string.IsNullOrWhiteSpace(value.ItemKeyValue);
}

static bool IsEmptyLegacyId(string legacyId)
{
    return string.IsNullOrWhiteSpace(legacyId) ||
           (Guid.TryParse(legacyId, out var parsed) && parsed == Guid.Empty);
}
