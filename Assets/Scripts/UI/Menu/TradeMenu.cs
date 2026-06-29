using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class TradeMenu : MonoBehaviour
{
    public ConfirmationDialog Dialog;
    public UnityEngine.UI.Button NewFilterButton;
    public Prototype FilterPrototype;
    public SizeFilter MinimumSizeFilter;
    public SizeFilter MaximumSizeFilter;
    public Spreadsheet Spreadsheet;
    public TextMeshProUGUI TargetCargoLabel;
    public UnityEngine.UI.Button FoldoutButton;
    public TextMeshProUGUI CreditsLabel;

    private string _targetCargoEntityKey = "";
    private int _targetCargoIndex = -1;
    private string _targetCargoLabel = "Docking Bay";
    private (ItemFilter filter, HardpointType type) _hardpointFilter;
    private (ItemFilter filter, SimpleCommodityCategory type) _commodityFilter;
    private (ItemFilter filter, CompoundCommodityCategory type) _compoundCommodityFilter;
    private List<BehaviorFilter> _behaviorFilters = new List<BehaviorFilter>();
    private UIDocument _cargoSelectorSurfaceDocument;
    private UIDocument _filterSurfaceDocument;
    private UIDocument _rowActionSurfaceDocument;
    private UIDocument _tradeItemSurfaceDocument;
    private AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private AetheriaUnityObservedDockingIndex _observedDockingIndex;
    private readonly AetheriaEveUnitySurfaceChrome _cargoSelectorSurfaceChrome = PanelChrome(360f, 420f, Align.FlexEnd);
    private readonly AetheriaEveUnitySurfaceChrome _filterSurfaceChrome = PanelChrome(420f, 520f, Align.FlexStart);
    private readonly AetheriaEveUnitySurfaceChrome _rowActionSurfaceChrome = PanelChrome(320f, 360f, Align.FlexStart);
    private readonly AetheriaEveUnitySurfaceChrome _tradeItemSurfaceChrome = PanelChrome(420f, 520f, Align.FlexStart);
    private AetheriaRuntimeTradeCargoSelectorSurfaceModel _cargoSelectorSurfaceModel;
    private AetheriaRuntimeStationCargoTargetRow[] _cargoSelectorStationRefitTargets =
        Array.Empty<AetheriaRuntimeStationCargoTargetRow>();
    private AetheriaRuntimeTradeFilterSurfaceModel _filterSurfaceModel;
    private Action[] _rowActionCallbacks = Array.Empty<Action>();
    private AetheriaRuntimeTradeRowActionSurfaceModel _rowActionSurfaceModel;
    
    public EquippedCargoBay Inventory { get; set; }

    public void SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        if (!ReferenceEquals(_observedEntityIndex, observedEntityIndex))
        {
            _observedDockingIndex = null;
        }

        _observedEntityIndex = observedEntityIndex;
    }
    
    private void OnEnable()
    {
        if (!TryResolveCurrentDocking(out var docking) ||
            docking.IsDocked != true ||
            string.IsNullOrWhiteSpace(docking.DockParentEntityKey) ||
            docking.DockingBayIndex < 0)
        {
            return;
        }

        SetTargetCargo(docking.DockParentEntityKey, docking.DockingBayIndex, "Docking Bay");
        HideCargoSelectorSurface();
        HideFilterSurface();
        HideRowActionSurface();
        HideTradeItemDetailsSurface();
        UpdateCreditsLabel();
        
        MinimumSizeFilter.Width.onEndEdit.RemoveAllListeners();
        MinimumSizeFilter.Width.onEndEdit.AddListener(_ => Populate());
        
        MinimumSizeFilter.Height.onEndEdit.RemoveAllListeners();
        MinimumSizeFilter.Height.onEndEdit.AddListener(_ => Populate());
        
        MaximumSizeFilter.Width.onEndEdit.RemoveAllListeners();
        MaximumSizeFilter.Width.onEndEdit.AddListener(_ => Populate());
        
        MaximumSizeFilter.Height.onEndEdit.RemoveAllListeners();
        MaximumSizeFilter.Height.onEndEdit.AddListener(_ => Populate());

        NewFilterButton.onClick.RemoveAllListeners();
        NewFilterButton.onClick.AddListener(() =>
        {
            RenderFilterSurface();
        });
        Populate();
    }

    void Populate()
    {
        var columns = new List<(string name, int size, Func<TradeRow, Func<string>> output, Func<TradeRow, IComparable> sortKey)>();
        
        columns.Add(("Name", 3,
            x => () => !string.IsNullOrWhiteSpace(x.TierColorHex) ?
                $"<color=#{x.TierColorHex}>{x.Name}" :
                x.Name,
            x => x.Name));
        if(_hardpointFilter.filter==null)
            columns.Add(("Type", 2,
                x => () =>
                {
                    if (x.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory _))
                        return x.TypedItem.SimpleCommodityCategory;
                    if (x.TypedItem.TryGetCompoundCommodityCategory(out CompoundCommodityCategory _))
                        return x.TypedItem.CompoundCommodityCategory;
                    if (x.TypedItem.TryGetHardpointType(out HardpointType hardpointType)) return Enum.GetName(typeof(HardpointType), hardpointType);
                    return "None";
                },
                x =>
                {
                    if (x.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory simpleCategory))
                        return (int)simpleCategory;
                    var offset = Enum.GetValues(typeof(SimpleCommodityCategory)).Length;
                    if(x.TypedItem.TryGetCompoundCommodityCategory(out CompoundCommodityCategory compoundCategory))
                        return (int)compoundCategory + offset;
                    offset += Enum.GetValues(typeof(CompoundCommodityCategory)).Length;
                    if (x.TypedItem.TryGetHardpointType(out HardpointType hardpointType)) return (int) hardpointType + offset;
                    return 0;
                }));
        columns.Add(("Mass", 1,
            x => () => FormatValue(x.Mass),
            x => x.Mass));
        columns.Add(("Price", 1,
            x => () => x.Price.ToString("N0"),
            x => x.Price));
        columns.Add(("Size", 1,
            x => () => $"{x.ShapeWidth}x{x.ShapeHeight}",
            x => x.ShapeWidth * x.ShapeHeight));
        
        var items = BuildStationStockRows();
        
        if (MinimumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MinimumSizeFilter.Width.text.Length > 0 && i.ShapeWidth < int.Parse(MinimumSizeFilter.Width.text) ||
                 MinimumSizeFilter.Height.text.Length > 0 && i.ShapeHeight < int.Parse(MinimumSizeFilter.Height.text)));
        
        if (MaximumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MaximumSizeFilter.Width.text.Length > 0 && i.ShapeWidth > int.Parse(MaximumSizeFilter.Width.text) ||
                 MaximumSizeFilter.Height.text.Length > 0 && i.ShapeHeight > int.Parse(MaximumSizeFilter.Height.text)));
        
        if(_commodityFilter.filter != null)
            items = items.Where(i => i.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory category) && category == _commodityFilter.type);
        
        if(_compoundCommodityFilter.filter != null)
            items = items.Where(i => i.TypedItem.TryGetCompoundCommodityCategory(out CompoundCommodityCategory category) && category == _compoundCommodityFilter.type);
        
        if (_hardpointFilter.filter != null)
            items = items.Where(i => i.TypedItem.TryGetHardpointType(out HardpointType hardpointType) && hardpointType == _hardpointFilter.type);
        
        foreach (var behaviorFilter in _behaviorFilters)
        {
            items = items.Where(i => HasTypedBehavior(i.TypedItem, behaviorFilter));
            
			foreach (var field in behaviorFilter.Metadata.DisplayFields)
			{
				if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.Number)
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => FormatValue((float)value);
                    }, x =>
                    {
                        return (float)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				else if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.Temperature)
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => FormatTemperature((float)value);
                    }, x =>
                    {
                        return (float)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				else if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.Integer)
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => ((int)value).ToString();
                    }, x =>
                    {
                        return (int)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				else if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat)
				{
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => FormatValue((float)value);
                    }, x =>
                    {
                        return (float)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				}
			}
        }
        
        columns.Add(("Owned", 1,
            x => () => x.OwnedQuantity.ToString(),
            x => x.OwnedQuantity));
        
        Spreadsheet.ShowData(
            columns.Select(x => x.name).ToArray(),
            columns.Select(x => x.size).ToArray(),
            items.Select(i => new SpreadsheetEntryRow
            {
                Columns = columns.Select(x => new SpreadsheetEntryColumn
                {
                    Output = x.output(i),
                    SortKey = x.sortKey(i)
                }).ToArray(),
                OnClick = () => RenderTradeItemDetailsSurface(i.TypedItem),
                OnDoubleClick = () =>
                {
                    Buy(i, 1);
                    Populate();
                },
                OnRightClick = () =>
                {
                    if (i.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory _))
                    {
                        RenderRowActionSurface(
                            $"Buying {i.Name}",
                            ("Buy Quantity", () => ShowBuyQuantityDialog(i)));
                    }
                }
            }));
    }

    private IEnumerable<TradeRow> BuildStationStockRows()
    {
        return (StationRefitSnapshot()?.StationStock ?? Array.Empty<AetheriaRuntimeStationStockItem>())
            .Select(stock =>
            {
                var typedItem = FindTypedTradeItem(stock.ItemKey);
                return new TradeRow(
                    stock,
                    typedItem,
                    TradeItemValue(stock, typedItem));
            })
            .Where(row => row.TypedItem != null)
            .Where(PassesTypedTradeFilters);
    }

    private bool PassesTypedTradeFilters(TradeRow row)
    {
        var typedItem = row.TypedItem;
        if (typedItem == null)
        {
            return true;
        }

        if (MinimumSizeFilter.gameObject.activeSelf &&
            (MinimumSizeFilter.Width.text.Length > 0 && typedItem.ShapeWidth < int.Parse(MinimumSizeFilter.Width.text) ||
             MinimumSizeFilter.Height.text.Length > 0 && typedItem.ShapeHeight < int.Parse(MinimumSizeFilter.Height.text)))
        {
            return false;
        }

        if (MaximumSizeFilter.gameObject.activeSelf &&
            (MaximumSizeFilter.Width.text.Length > 0 && typedItem.ShapeWidth > int.Parse(MaximumSizeFilter.Width.text) ||
             MaximumSizeFilter.Height.text.Length > 0 && typedItem.ShapeHeight > int.Parse(MaximumSizeFilter.Height.text)))
        {
            return false;
        }

        if (_hardpointFilter.filter != null &&
            !string.Equals(typedItem.HardpointType, _hardpointFilter.type.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        return _behaviorFilters.All(filter => HasTypedBehavior(typedItem, filter));
    }

    private AetheriaRuntimeCatalogItem FindTypedTradeItem(string itemKey)
    {
        return CatalogSnapshot()?.FindItem(itemKey ?? "");
    }

    private AetheriaRuntimeTradeItemValue TradeItemValue(
        AetheriaRuntimeStationStockItem stock,
        AetheriaRuntimeCatalogItem typedItem)
    {
        return AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
            typedItem,
            TradeItemCommit(stock),
            CatalogSnapshot()?.TradeValueSettings);
    }

    private static AetheriaRuntimeLoadoutItemCommit? TradeItemCommit(AetheriaRuntimeStationStockItem stock)
    {
        if (stock == null || string.IsNullOrWhiteSpace(stock.ItemKey))
            return null;

        return AetheriaRuntimeDaemonTradeItemQueries.CraftedItemCommit(
            stock.ItemKey,
            stock.Quality,
            stock.Durability);
    }

    private static double GetTypedBehaviorNumber(TradeRow row, BehaviorFilter behaviorFilter, AetheriaRuntimeBehaviorFieldMetadata field)
    {
        var payload = FindTypedBehaviorPayload(row.TypedItem, behaviorFilter);
        var payloadField = payload?.Fields.FirstOrDefault(candidate => candidate.Key == field.Key);
        if (payloadField == null)
        {
            return 0;
        }

        if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat)
        {
            return payloadField.Value.Children.Count > 1
                ? payloadField.Value.Children[1].NumberValue
                : 0;
        }

        return payloadField.Value.NumberValue;
    }

    private static AetheriaRuntimeBehaviorPayload FindTypedBehaviorPayload(AetheriaRuntimeCatalogItem typedItem, BehaviorFilter behaviorFilter)
    {
        if (typedItem == null)
        {
            return null;
        }

        return typedItem.BehaviorPayloads.FirstOrDefault(payload => TypedBehaviorMatches(payload, behaviorFilter));
    }

    private static bool HasTypedBehavior(AetheriaRuntimeCatalogItem typedItem, BehaviorFilter behaviorFilter)
    {
        return typedItem?.BehaviorPayloads.Any(payload => TypedBehaviorMatches(payload, behaviorFilter)) ?? false;
    }

    private static bool TypedBehaviorMatches(AetheriaRuntimeBehaviorPayload payload, BehaviorFilter behaviorFilter)
    {
        return AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(payload.Kind, behaviorFilter.Kind);
    }

    private sealed class BehaviorFilter
    {
        public BehaviorFilter(ItemFilter filter, AetheriaRuntimeBehaviorMetadata metadata)
        {
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public ItemFilter Filter { get; }
        public AetheriaRuntimeBehaviorMetadata Metadata { get; }
        public string Kind => Metadata.Kind;
    }

    private sealed class TradeRow
    {
        public TradeRow(
            AetheriaRuntimeStationStockItem stock,
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeTradeItemValue tradeValue)
        {
            Stock = stock;
            TypedItem = typedItem;
            TradeValue = tradeValue;
        }

        public AetheriaRuntimeStationStockItem Stock { get; }

        public AetheriaRuntimeCatalogItem TypedItem { get; }

        public AetheriaRuntimeTradeItemValue TradeValue { get; }

        public string ItemKey => Stock?.ItemKey ?? "";

        public string Name => !string.IsNullOrWhiteSpace(TypedItem?.Name) ? TypedItem.Name : "Unknown Item";

        public float Mass => TypedItem != null ? (float)TypedItem.Mass : 0f;

        public int Price => TradeValue.Price;

        public int OwnedQuantity => Stock?.OwnedQuantity ?? 0;

        public string TierColorHex => TradeValue.TierColorHex;

        public int ShapeWidth => TypedItem != null && TypedItem.ShapeWidth > 0 ? TypedItem.ShapeWidth : 0;

        public int ShapeHeight => TypedItem != null && TypedItem.ShapeHeight > 0 ? TypedItem.ShapeHeight : 0;

        public bool IsHull => !string.IsNullOrWhiteSpace(TypedItem?.HullType);

    }
    
    private void UpdateCreditsLabel()
    {
        if (CreditsLabel != null)
        {
            CreditsLabel.text = StationRefitSnapshot()?.Credits.ToString("N0") ?? "0";
        }
    }

    private void Buy(TradeRow row, int quantity)
    {
        if (row?.TypedItem == null || row.Stock == null)
        {
            ShowUnableToBuy("Missing typed trade row!");
            return;
        }

        var createsDockedShip = !string.IsNullOrWhiteSpace(row.TypedItem.HullType);
        if (createsDockedShip &&
            !string.Equals(row.TypedItem.HullType, nameof(HullType.Ship), StringComparison.Ordinal))
        {
            ShowUnableToBuy("Unsupported hull purchase!");
            return;
        }

        RequestTradePurchase(row, quantity, createsDockedShip);
    }

    private void RequestTradePurchase(
        TradeRow row,
        int quantity,
        bool createsDockedShip)
    {
        if (row?.Stock == null ||
            string.IsNullOrWhiteSpace(row.ItemKey) ||
            quantity <= 0 ||
            row.Price < 0)
        {
            return;
        }

        var totalPrice = (long)quantity * row.Price;
        if (totalPrice > int.MaxValue)
            return;

        var stationRefit = StationRefitSnapshot();
        var stationEntityKey = stationRefit?.DockParentEntityKey ?? "";
        var stationCargoIndex = row.Stock.CargoBayIndex;
        var sourcePosition = new int2(row.Stock.X, row.Stock.Y);
        if (string.IsNullOrWhiteSpace(stationEntityKey) ||
            stationCargoIndex < 0 ||
            sourcePosition.x < 0 ||
            sourcePosition.y < 0)
        {
            return;
        }

        var targetEntityKey = _targetCargoEntityKey ?? "";
        var targetCargoIndex = _targetCargoIndex;

        if (createsDockedShip)
        {
            if (!TryResolveCurrentDockingTargetEntityKey(out targetEntityKey))
            {
                return;
            }

            targetCargoIndex = -1;
        }
        else if (string.IsNullOrWhiteSpace(targetEntityKey) ||
                 targetCargoIndex < 0)
        {
            return;
        }

        var purchaseKind = createsDockedShip
            ? "docked_ship"
            : row.TypedItem.TryGetSimpleCommodityCategory(out SimpleCommodityCategory _)
                ? "commodity"
                : "crafted";

        TrySubmitOperation(
            operations => operations.TradePurchase(
                purchaseKind,
                row.ItemKey,
                quantity,
                row.Price,
                (int)totalPrice,
                stationEntityKey,
                stationCargoIndex,
                targetEntityKey,
                targetCargoIndex,
                sourcePosition.x,
                sourcePosition.y,
                createsDockedShip),
            "trade purchase");
    }

    private bool TryResolveCurrentDockingTargetEntityKey(out string targetEntityKey)
    {
        targetEntityKey = "";
        if (!TryResolveCurrentDocking(out var docking) ||
            docking.IsDocked != true ||
            string.IsNullOrWhiteSpace(docking.DockParentEntityKey))
        {
            return false;
        }

        targetEntityKey = docking.DockParentEntityKey;
        return true;
    }

    private AetheriaRuntimeStationRefitDocument StationRefitSnapshot()
    {
        return TryResolveObservedDockingIndex(out var dockingIndex)
            ? dockingIndex.StationRefitSnapshot()
            : null;
    }

    private bool TryResolveCurrentDocking(out AetheriaRuntimeCurrentDockingDocument docking)
    {
        docking = null;
        return TryResolveObservedDockingIndex(out var dockingIndex) &&
               dockingIndex.TryResolveCurrentDocking(out docking);
    }

    private bool TryResolveObservedDockingIndex(out AetheriaUnityObservedDockingIndex dockingIndex)
    {
        dockingIndex = null;
        if (_observedEntityIndex == null)
            return false;

        dockingIndex = _observedDockingIndex ??= new AetheriaUnityObservedDockingIndex(_observedEntityIndex);
        return true;
    }

    private bool TryResolveStationRefitCargoTarget(
        string entityKey,
        int cargoBayIndex,
        out AetheriaRuntimeStationCargoTargetRow target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(entityKey) ||
            cargoBayIndex < 0)
        {
            return false;
        }

        target = (_cargoSelectorStationRefitTargets ?? Array.Empty<AetheriaRuntimeStationCargoTargetRow>())
            .FirstOrDefault(option =>
                option != null &&
                string.Equals(option.EntityKey, entityKey, StringComparison.Ordinal) &&
                option.BayIndex == cargoBayIndex);
        return target != null;
    }

    private bool IsTargetCargoBayKey(string entityKey, int cargoBayIndex)
    {
        return string.Equals(_targetCargoEntityKey, entityKey, StringComparison.Ordinal) &&
               _targetCargoIndex == cargoBayIndex;
    }

    private bool TrySubmitOperation(
        Action<AetheriaControl> submit,
        string label)
    {
        if (submit == null)
            return false;

        try
        {
            submit(AetheriaUnityRuntimeClientProvider.Control("unity-trade"));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon trade {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void ClearClientCaches()
    {
        _observedDockingIndex = null;
    }

    private AetheriaRuntimeCatalogSnapshot CatalogSnapshot()
    {
        try
        {
            return AetheriaUnityRuntimeClientProvider
                .RuntimeState("unity-trade")
                .CurrentCatalog();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria runtime catalog for trade menu: {ex.Message}");
            return null;
        }
    }

    private AetheriaRuntimePlayerSettingsDocument PlayerSettingsSnapshot()
    {
        try
        {
            return AetheriaUnityRuntimeClientProvider
                .RuntimeState("unity-trade")
                .CurrentPlayerSettings();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria player settings for trade menu: {ex.Message}");
            return null;
        }
    }

    private string ResolveManufacturerName(AetheriaRuntimeCatalogItem item)
    {
        return CatalogSnapshot()?.GetManufacturer(item)?.Name ?? "GameCult";
    }

    private string FormatValue(float value)
    {
        var settings = PlayerSettingsSnapshot();
        var significantDigits = settings?.SignificantDigits ?? 3;
        var magnitude = value == 0.0f ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
        var digits = significantDigits - magnitude;
        if (digits < 0)
            digits = 0;

        var formatted = value.ToString($"N{digits}", CultureInfo.CurrentCulture);
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var decimalSeparator = Convert.ToChar(separator);
        return formatted.Contains(separator)
            ? formatted.TrimEnd('0').TrimEnd(decimalSeparator)
            : formatted;
    }

    private string FormatTemperature(float value)
    {
        var unit = PlayerSettingsSnapshot()?.TemperatureUnit ?? nameof(TemperatureUnit.Celsius);
        if (string.Equals(unit, nameof(TemperatureUnit.Kelvin), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value)} K";
        if (string.Equals(unit, nameof(TemperatureUnit.Fahrenheit), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value * (9f / 5) - 459.67f)} F";

        return $"{FormatValue(value - 273.15f)} C";
    }

    private void ShowUnableToBuy(string reason)
    {
        Dialog.Clear();
        Dialog.Title.text = $"Unable to buy: {reason}";
        Dialog.Show();
        Dialog.MoveToCursor();
    }

    private void ShowBuyQuantityDialog(TradeRow row)
    {
        int quantity = 1;
        Dialog.Clear();
        Dialog.Title.text = $"Buying {row?.Name ?? "Unknown Item"}";
        Dialog.AddField(
            "Quantity",
            () => quantity,
            q => quantity = q);
        Dialog.Show(() =>
        {
            Buy(row, quantity);
            Populate();
        });
        Dialog.MoveToCursor();
    }

    private void RenderTradeItemDetailsSurface(AetheriaRuntimeCatalogItem item)
    {
        if (item == null)
            return;

        _tradeItemSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _tradeItemSurfaceDocument,
            "Aetheria Trade Item Details Surface",
            AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Build(
                item,
                ResolveManufacturerName(item),
                FormatValue,
                FormatTemperature),
            HandleTradeItemDetailsSurfaceCommand,
            _tradeItemSurfaceChrome,
            sortingOrder: 1003);
    }

    private void HandleTradeItemDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeTradeItemDetailsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown trade item details command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeItemDetailsCommandKind.Close)
        {
            HideTradeItemDetailsSurface();
            return;
        }

        Debug.LogWarning($"Unknown trade item details command: {request?.Command}");
    }

    private void HideTradeItemDetailsSurface()
    {
        if (_tradeItemSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_tradeItemSurfaceDocument);
    }

    void Start()
    {
        FoldoutButton.onClick.AddListener(() =>
        {
            RenderCargoSelectorSurface();
        });
    }

    void Update()
    {
        
    }

    private void RenderFilterSurface()
    {
        _filterSurfaceModel = BuildTradeFilterSurfaceModel();

        _filterSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _filterSurfaceDocument,
            "Aetheria Trade Filter Surface",
            _filterSurfaceModel.Document,
            HandleFilterSurfaceCommand,
            _filterSurfaceChrome,
            sortingOrder: 1001);
    }

    private void HandleFilterSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeTradeInteractionSurfaceCommands.TryReadFilter(request, out var command))
        {
            Debug.LogWarning($"Unknown trade filter command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeInteractionCommandKind.Close)
        {
            HideFilterSurface();
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeInteractionCommandKind.Select &&
            _filterSurfaceModel?.TryResolve(command.Command, out var selection) == true)
        {
            ExecuteTradeFilterSelection(selection);
            HideFilterSurface();
            return;
        }

        Debug.LogWarning($"Unknown trade filter command: {request?.Command}");
    }

    private void HideFilterSurface()
    {
        if (_filterSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_filterSurfaceDocument);
    }

    private AetheriaRuntimeTradeFilterSurfaceModel BuildTradeFilterSurfaceModel()
    {
        var options = new List<AetheriaRuntimeTradeFilterOption>();
        options.AddRange(((HardpointType[])Enum.GetValues(typeof(HardpointType)))
            .Where(type => _hardpointFilter.filter == null || type != _hardpointFilter.type)
            .Select(type => new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.Hardpoint,
                type.ToString(),
                FormatTypeName(type.ToString()))));
        options.AddRange(((SimpleCommodityCategory[])Enum.GetValues(typeof(SimpleCommodityCategory)))
            .Where(type => _commodityFilter.filter == null || type != _commodityFilter.type)
            .Select(type => new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity,
                type.ToString(),
                FormatTypeName(type.ToString()))));
        options.AddRange(((CompoundCommodityCategory[])Enum.GetValues(typeof(CompoundCommodityCategory)))
            .Where(type => _compoundCommodityFilter.filter == null || type != _compoundCommodityFilter.type)
            .Select(type => new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity,
                type.ToString(),
                FormatTypeName(type.ToString()))));
        options.AddRange(AetheriaRuntimeBehaviorMetadataCatalog.All
            .Where(option => _behaviorFilters.All(filter => filter.Kind != option.Kind))
            .OrderBy(option => option.Kind, StringComparer.Ordinal)
            .Select(option => new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.Behavior,
                option.Kind,
                FormatTypeName(option.Kind))));

        if (!MinimumSizeFilter.gameObject.activeSelf)
        {
            options.Add(new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.MinimumSize,
                "minimum",
                "Minimum Size"));
        }

        if (!MaximumSizeFilter.gameObject.activeSelf)
        {
            options.Add(new AetheriaRuntimeTradeFilterOption(
                AetheriaRuntimeTradeFilterSelectionKind.MaximumSize,
                "maximum",
                "Maximum Size"));
        }

        return AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildFilters(
            BuildFilterSummary(),
            options,
            DateTime.UtcNow.ToString("O"));
    }

    private void ExecuteTradeFilterSelection(AetheriaRuntimeTradeFilterSelection selection)
    {
        switch (selection.Kind)
        {
            case AetheriaRuntimeTradeFilterSelectionKind.Hardpoint:
                if (Enum.TryParse(selection.Token, out HardpointType hardpointType))
                {
                    ApplyHardpointFilter(hardpointType);
                }
                return;
            case AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity:
                if (Enum.TryParse(selection.Token, out SimpleCommodityCategory simpleCategory))
                {
                    ApplySimpleCommodityFilter(simpleCategory);
                }
                return;
            case AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity:
                if (Enum.TryParse(selection.Token, out CompoundCommodityCategory compoundCategory))
                {
                    ApplyCompoundCommodityFilter(compoundCategory);
                }
                return;
            case AetheriaRuntimeTradeFilterSelectionKind.Behavior:
                var metadata = AetheriaRuntimeBehaviorMetadataCatalog.All
                    .FirstOrDefault(option => string.Equals(option.Kind, selection.Token, StringComparison.Ordinal));
                if (metadata != null)
                {
                    ApplyBehaviorFilter(metadata);
                }
                return;
            case AetheriaRuntimeTradeFilterSelectionKind.MinimumSize:
                EnableMinimumSizeFilter();
                return;
            case AetheriaRuntimeTradeFilterSelectionKind.MaximumSize:
                EnableMaximumSizeFilter();
                return;
        }
    }

    private void RenderRowActionSurface(string title, params (string Label, Action Action)[] actions)
    {
        _rowActionCallbacks = actions
            .Select(action => action.Action)
            .ToArray();
        _rowActionSurfaceModel = AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildRowActions(
            title,
            actions.Select((action, index) => new AetheriaRuntimeTradeRowActionOption(index, action.Label)),
            DateTime.UtcNow.ToString("O"));

        _rowActionSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _rowActionSurfaceDocument,
            "Aetheria Trade Row Action Surface",
            _rowActionSurfaceModel.Document,
            HandleRowActionSurfaceCommand,
            _rowActionSurfaceChrome,
            sortingOrder: 1002);
    }

    private void HandleRowActionSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeTradeInteractionSurfaceCommands.TryReadRowAction(request, out var command))
        {
            Debug.LogWarning($"Unknown trade row action command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeInteractionCommandKind.Close)
        {
            HideRowActionSurface();
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeInteractionCommandKind.Select &&
            _rowActionSurfaceModel?.TryResolve(command.Command, out var selection) == true &&
            selection.Index >= 0 &&
            selection.Index < _rowActionCallbacks.Length)
        {
            _rowActionCallbacks[selection.Index]?.Invoke();
            HideRowActionSurface();
            return;
        }

        Debug.LogWarning($"Unknown trade row action command: {request?.Command}");
    }

    private void HideRowActionSurface()
    {
        if (_rowActionSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_rowActionSurfaceDocument);
    }

    private void ApplyHardpointFilter(HardpointType hardpointType)
    {
        EnsureHardpointFilter();
        ClearCommodityFilters();
        _hardpointFilter.filter.Label.text = $"Hardpoint: {Enum.GetName(typeof(HardpointType), hardpointType)}";
        _hardpointFilter.type = hardpointType;
        Populate();
    }

    private void ApplySimpleCommodityFilter(SimpleCommodityCategory category)
    {
        EnsureCommodityFilter();
        ClearHardpointFilter();
        ClearCompoundCommodityFilter();
        _commodityFilter.filter.Label.text = $"Simple Commodity: {Enum.GetName(typeof(SimpleCommodityCategory), category)}";
        _commodityFilter.type = category;
        Populate();
    }

    private void ApplyCompoundCommodityFilter(CompoundCommodityCategory category)
    {
        EnsureCompoundCommodityFilter();
        ClearHardpointFilter();
        ClearCommodityFilter();
        _compoundCommodityFilter.filter.Label.text = $"Compound Commodity: {Enum.GetName(typeof(CompoundCommodityCategory), category)}";
        _compoundCommodityFilter.type = category;
        Populate();
    }

    private void ApplyBehaviorFilter(AetheriaRuntimeBehaviorMetadata metadata)
    {
        var matchingType = _behaviorFilters.FirstOrDefault(filter =>
            AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(filter.Kind, metadata.Kind) ||
            AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(metadata.Kind, filter.Kind));
        if (matchingType?.Filter != null)
        {
            matchingType.Filter.DisableButton.onClick.Invoke();
        }

        var filter = FilterPrototype.Instantiate<ItemFilter>();
        filter.Label.text = FormatTypeName(metadata.Kind);
        var behaviorFilter = new BehaviorFilter(filter, metadata);
        filter.OnDisable += () =>
        {
            _behaviorFilters.Remove(behaviorFilter);
            Populate();
        };
        _behaviorFilters.Add(behaviorFilter);
        Populate();
    }

    private void EnableMinimumSizeFilter()
    {
        MinimumSizeFilter.gameObject.SetActive(true);
        MinimumSizeFilter.OnDisable += () => Populate();
        Populate();
    }

    private void EnableMaximumSizeFilter()
    {
        MaximumSizeFilter.gameObject.SetActive(true);
        MaximumSizeFilter.OnDisable += () => Populate();
        Populate();
    }

    private void EnsureHardpointFilter()
    {
        if (_hardpointFilter.filter != null)
            return;

        _hardpointFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
        _hardpointFilter.filter.OnDisable += () =>
        {
            _hardpointFilter.filter = null;
            Populate();
        };
    }

    private void EnsureCommodityFilter()
    {
        if (_commodityFilter.filter != null)
            return;

        _commodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
        _commodityFilter.filter.OnDisable += () =>
        {
            _commodityFilter.filter = null;
            Populate();
        };
    }

    private void EnsureCompoundCommodityFilter()
    {
        if (_compoundCommodityFilter.filter != null)
            return;

        _compoundCommodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
        _compoundCommodityFilter.filter.OnDisable += () =>
        {
            _compoundCommodityFilter.filter = null;
            Populate();
        };
    }

    private void ClearHardpointFilter()
    {
        if (_hardpointFilter.filter == null)
            return;

        _hardpointFilter.filter.GetComponent<Prototype>().ReturnToPool();
        _hardpointFilter.filter = null;
    }

    private void ClearCommodityFilter()
    {
        if (_commodityFilter.filter == null)
            return;

        _commodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
        _commodityFilter.filter = null;
    }

    private void ClearCompoundCommodityFilter()
    {
        if (_compoundCommodityFilter.filter == null)
            return;

        _compoundCommodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
        _compoundCommodityFilter.filter = null;
    }

    private void ClearCommodityFilters()
    {
        ClearCommodityFilter();
        ClearCompoundCommodityFilter();
    }

    private string BuildFilterSummary()
    {
        var activeFilters = new List<string>();
        if (_hardpointFilter.filter != null)
        {
            activeFilters.Add($"Gear: {Enum.GetName(typeof(HardpointType), _hardpointFilter.type)}");
        }

        if (_commodityFilter.filter != null)
        {
            activeFilters.Add($"Simple: {Enum.GetName(typeof(SimpleCommodityCategory), _commodityFilter.type)}");
        }

        if (_compoundCommodityFilter.filter != null)
        {
            activeFilters.Add($"Compound: {Enum.GetName(typeof(CompoundCommodityCategory), _compoundCommodityFilter.type)}");
        }

        activeFilters.AddRange(_behaviorFilters.Select(filter => $"Behavior: {FormatTypeName(filter.Kind)}"));

        if (MinimumSizeFilter.gameObject.activeSelf)
        {
            activeFilters.Add("Minimum size active");
        }

        if (MaximumSizeFilter.gameObject.activeSelf)
        {
            activeFilters.Add("Maximum size active");
        }

        return activeFilters.Count == 0
            ? "No active filters"
            : string.Join(" | ", activeFilters);
    }

    private void RenderCargoSelectorSurface()
    {
        _cargoSelectorSurfaceModel = BuildTradeCargoSelectorSurfaceModel();

        _cargoSelectorSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _cargoSelectorSurfaceDocument,
            "Aetheria Trade Cargo Selector Surface",
            _cargoSelectorSurfaceModel.Document,
            HandleCargoSelectorSurfaceCommand,
            _cargoSelectorSurfaceChrome);
    }

    private void HandleCargoSelectorSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeTradeCargoSelectorSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown trade cargo selector command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeCargoSelectorCommandKind.Close)
        {
            HideCargoSelectorSurface();
            return;
        }

        if (command.Kind == AetheriaRuntimeTradeCargoSelectorCommandKind.Select &&
            _cargoSelectorSurfaceModel?.TryResolve(command.Command, out var selection) == true)
        {
            ApplyCargoSelection(selection);
            HideCargoSelectorSurface();
            Populate();
            return;
        }

        Debug.LogWarning($"Unknown trade cargo selector command: {request?.Command}");
    }

    private void HideCargoSelectorSurface()
    {
        if (_cargoSelectorSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_cargoSelectorSurfaceDocument);
    }

    private static AetheriaEveUnitySurfaceChrome PanelChrome(float width, float maxWidth, Align alignItems)
    {
        return new AetheriaEveUnitySurfaceChrome
        {
            RootAlignItems = alignItems,
            RootJustifyContent = Justify.FlexStart,
            RootPaddingTop = 16f,
            RootPaddingLeft = alignItems == Align.FlexStart ? 16f : 0f,
            RootPaddingRight = alignItems == Align.FlexEnd ? 16f : 0f,
            Width = width,
            MinWidth = 0f,
            MaxWidth = maxWidth,
            PaddingLeft = 18f,
            PaddingRight = 18f,
            PaddingTop = 18f,
            PaddingBottom = 18f
        };
    }

    private AetheriaRuntimeTradeCargoSelectorSurfaceModel BuildTradeCargoSelectorSurfaceModel()
    {
        var targets = new List<AetheriaRuntimeTradeCargoModelOption>();
        var stationRefit = StationRefitSnapshot();
        if (stationRefit?.IsDocked == true &&
            !string.IsNullOrWhiteSpace(stationRefit.DockParentEntityKey) &&
            stationRefit.DockingBayIndex >= 0)
        {
            targets.Add(new AetheriaRuntimeTradeCargoModelOption(
                AetheriaRuntimeTradeCargoTargetKind.DockingBay,
                "Docking Bay",
                stationRefit.DockParentEntityKey,
                bayIndex: stationRefit.DockingBayIndex,
                isCurrent: IsTargetCargoBayKey(stationRefit.DockParentEntityKey, stationRefit.DockingBayIndex)));
        }

        _cargoSelectorStationRefitTargets = (stationRefit?.CargoTargets ??
                Array.Empty<AetheriaRuntimeStationCargoTargetRow>())
            .ToArray();
        targets.AddRange(_cargoSelectorStationRefitTargets
            .Select(target => new AetheriaRuntimeTradeCargoModelOption(
                target.Kind,
                target.Label,
                target.EntityKey,
                target.TargetIndex,
                target.BayIndex,
                IsTargetCargoBayKey(target.EntityKey, target.BayIndex))));

        return AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Build(
            _targetCargoLabel ?? "",
            targets,
            DateTime.UtcNow.ToString("O"));
    }

    private void ApplyCargoSelection(AetheriaRuntimeTradeCargoSelection selection)
    {
        switch (selection.Kind)
        {
            case AetheriaRuntimeTradeCargoTargetKind.DockingBay:
                if (TryResolveStationRefitCargoTarget(selection.EntityKey, selection.BayIndex, out var target) &&
                    target.Kind == AetheriaRuntimeTradeCargoTargetKind.DockingBay)
                {
                    SetTargetCargo(selection.EntityKey, selection.BayIndex, selection.Label);
                }
                return;
            case AetheriaRuntimeTradeCargoTargetKind.ShipBay:
                if (TryResolveStationRefitCargoTarget(selection.EntityKey, selection.BayIndex, out target) &&
                    target.Kind == AetheriaRuntimeTradeCargoTargetKind.ShipBay &&
                    target.TargetIndex == selection.ShipIndex)
                {
                    SetTargetCargo(target.EntityKey, selection.BayIndex, selection.Label);
                }
                return;
        }
    }

    private void SetTargetCargo(string entityKey, int cargoBayIndex, string label)
    {
        _targetCargoEntityKey = entityKey ?? "";
        _targetCargoIndex = cargoBayIndex;
        _targetCargoLabel = string.IsNullOrWhiteSpace(label) ? "Docking Bay" : label;
        if (TargetCargoLabel != null)
        {
            TargetCargoLabel.text = _targetCargoLabel;
        }
    }

    private static string FormatTypeName(string typeName)
    {
        var value = typeName ?? "";
        var trimmed = value.EndsWith("Data", StringComparison.InvariantCultureIgnoreCase)
            ? value.Substring(0, value.Length - 4)
            : value;
        return SplitCamelCase(trimmed);
    }

    private static string SplitCamelCase(string value)
    {
        return Regex.Replace(
            Regex.Replace(
                value ?? "",
                @"(\P{Ll})(\P{Ll}\p{Ll})",
                "$1 $2"),
            @"(\p{Ll})(\P{Ll})",
            "$1 $2");
    }

    private void OnDisable()
    {
        HideCargoSelectorSurface();
        HideFilterSurface();
        HideRowActionSurface();
        HideTradeItemDetailsSurface();
    }

    private void OnDestroy()
    {
        ClearClientCaches();

        if (_cargoSelectorSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_cargoSelectorSurfaceDocument);
            _cargoSelectorSurfaceDocument = null;
        }

        if (_filterSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_filterSurfaceDocument);
            _filterSurfaceDocument = null;
        }

        if (_rowActionSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_rowActionSurfaceDocument);
            _rowActionSurfaceDocument = null;
        }

        if (_tradeItemSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_tradeItemSurfaceDocument);
            _tradeItemSurfaceDocument = null;
        }
    }
}
