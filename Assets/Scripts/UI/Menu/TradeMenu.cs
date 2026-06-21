using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public ActionGameManager GameManager;
    public ConfirmationDialog Dialog;
    public UnityEngine.UI.Button NewFilterButton;
    public Prototype FilterPrototype;
    public SizeFilter MinimumSizeFilter;
    public SizeFilter MaximumSizeFilter;
    public Spreadsheet Spreadsheet;
    public TextMeshProUGUI TargetCargoLabel;
    public UnityEngine.UI.Button FoldoutButton;
    public TextMeshProUGUI CreditsLabel;

    private EquippedCargoBay _targetCargo;
    private (ItemFilter filter, HardpointType type) _hardpointFilter;
    private (ItemFilter filter, SimpleCommodityCategory type) _commodityFilter;
    private (ItemFilter filter, CompoundCommodityCategory type) _compoundCommodityFilter;
    private List<BehaviorFilter> _behaviorFilters = new List<BehaviorFilter>();
    private readonly Dictionary<string, Action> _filterSurfaceCommands = new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly Dictionary<string, Action> _rowActionSurfaceCommands = new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly Dictionary<string, (EquippedCargoBay Cargo, string Label)> _cargoSelectionCommands =
        new Dictionary<string, (EquippedCargoBay Cargo, string Label)>(StringComparer.Ordinal);
    private UIDocument _cargoSelectorSurfaceDocument;
    private UIDocument _filterSurfaceDocument;
    private UIDocument _rowActionSurfaceDocument;
    private UIDocument _tradeItemSurfaceDocument;
    private readonly AetheriaEveUnitySurfaceChrome _cargoSelectorSurfaceChrome = PanelChrome(360f, 420f, Align.FlexEnd);
    private readonly AetheriaEveUnitySurfaceChrome _filterSurfaceChrome = PanelChrome(420f, 520f, Align.FlexStart);
    private readonly AetheriaEveUnitySurfaceChrome _rowActionSurfaceChrome = PanelChrome(320f, 360f, Align.FlexStart);
    private readonly AetheriaEveUnitySurfaceChrome _tradeItemSurfaceChrome = PanelChrome(420f, 520f, Align.FlexStart);
    private string _rowActionTitle = "Trade Actions";
    
    public EquippedCargoBay Inventory { get; set; }
    
    private void OnEnable()
    {
        if (GameManager.DockedEntity == null) return;
        _targetCargo = GameManager.DockingBay;
        TargetCargoLabel.text = "Docking Bay";
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
            x => () => x.Item is CraftedItemInstance craftedItemInstance ?
                $"<color=#{ColorUtility.ToHtmlStringRGB(GameManager.ItemManager.GetTier(craftedItemInstance).tier.Color.ToColor())}>{x.Name}" :
                x.Name,
            x => x.Name));
        if(_hardpointFilter.filter==null)
            columns.Add(("Type", 2,
                x => () =>
                {
                    if (x.TryGetTypedSimpleCommodityCategory(out _))
                        return x.TypedItem.SimpleCommodityCategory;
                    if (x.TryGetTypedCompoundCommodityCategory(out _))
                        return x.TypedItem.CompoundCommodityCategory;
                    if (x.TryGetTypedHardpoint(out var hardpointType)) return Enum.GetName(typeof(HardpointType), hardpointType);
                    return "None";
                },
                x =>
                {
                    if (x.TryGetTypedSimpleCommodityCategory(out var simpleCategory))
                        return (int)simpleCategory;
                    var offset = Enum.GetValues(typeof(SimpleCommodityCategory)).Length;
                    if(x.TryGetTypedCompoundCommodityCategory(out var compoundCategory))
                        return (int)compoundCategory + offset;
                    offset += Enum.GetValues(typeof(CompoundCommodityCategory)).Length;
                    if (x.TryGetTypedHardpoint(out var hardpointType)) return (int) hardpointType + offset;
                    return 0;
                }));
        columns.Add(("Mass", 1,
            x => () => ActionGameManager.RuntimePlayerSettings.Format(x.Mass),
            x => x.Mass));
        columns.Add(("Price", 1,
            x => () => x.Price.ToString("N0"),
            x => x.Price));
        columns.Add(("Size", 1,
            x => () => $"{x.ShapeWidth}x{x.ShapeHeight}",
            x => x.ShapeWidth * x.ShapeHeight));
        
        var items = Inventory.Cargo.Keys
            .Where(PassesTypedTradeFilters)
            .Select(item => new TradeRow(item, FindTypedTradeItem(item), GameManager.ItemManager));
        
        if (MinimumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MinimumSizeFilter.Width.text.Length > 0 && i.ShapeWidth < int.Parse(MinimumSizeFilter.Width.text) ||
                 MinimumSizeFilter.Height.text.Length > 0 && i.ShapeHeight < int.Parse(MinimumSizeFilter.Height.text)));
        
        if (MaximumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MaximumSizeFilter.Width.text.Length > 0 && i.ShapeWidth > int.Parse(MaximumSizeFilter.Width.text) ||
                 MaximumSizeFilter.Height.text.Length > 0 && i.ShapeHeight > int.Parse(MaximumSizeFilter.Height.text)));
        
        if(_commodityFilter.filter != null)
            items = items.Where(i => i.TryGetTypedSimpleCommodityCategory(out var category) && category == _commodityFilter.type);
        
        if(_compoundCommodityFilter.filter != null)
            items = items.Where(i => i.TryGetTypedCompoundCommodityCategory(out var category) && category == _compoundCommodityFilter.type);
        
        if (_hardpointFilter.filter != null)
            items = items.Where(i => i.TryGetTypedHardpoint(out var hardpointType) && hardpointType == _hardpointFilter.type);
        
        foreach (var behaviorFilter in _behaviorFilters)
        {
            items = items.Where(i => HasTypedBehavior(i.TypedItem, behaviorFilter));
            
			foreach (var field in behaviorFilter.Metadata.DisplayFields)
			{
				if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.Number)
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => ActionGameManager.RuntimePlayerSettings.Format((float)value);
                    }, x =>
                    {
                        return (float)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				else if (field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.Temperature)
                    columns.Add((field.Name, 1, x =>
                    {
                        var value = GetTypedBehaviorNumber(x, behaviorFilter, field);
                        return () => ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)value);
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
                        return () => ActionGameManager.RuntimePlayerSettings.Format((float)value);
                    }, x =>
                    {
                        return (float)GetTypedBehaviorNumber(x, behaviorFilter, field);
                    }));
				}
			}
        }
        
        columns.Add(("Owned", 1,
            x => () =>
            {
                if (x.IsHull)
                    return GameManager.DockedEntity.Children.Count(s => s.Hull.ItemKey == x.ItemKey && s is Ship {IsPlayerShip: true}).ToString();
                if(x.Item is SimpleCommodity)
                    return (_targetCargo.ItemsOfType.ContainsKey(x.ItemKey) ? _targetCargo.ItemsOfType[x.ItemKey].Cast<SimpleCommodity>().Sum(s=>s.Quantity) : 0).ToString();
                return (_targetCargo.ItemsOfType.ContainsKey(x.ItemKey) ? _targetCargo.ItemsOfType[x.ItemKey].Count : 0).ToString();
            }, 
            x =>
            {
                if (x.IsHull)
                    return GameManager.DockedEntity.Children.Count(s => s.Hull.ItemKey == x.ItemKey && s is Ship {IsPlayerShip: true});
                if(x.Item is SimpleCommodity)
                    return _targetCargo.ItemsOfType.ContainsKey(x.ItemKey) ? _targetCargo.ItemsOfType[x.ItemKey].Cast<SimpleCommodity>().Sum(s=>s.Quantity) : 0;
                return _targetCargo.ItemsOfType.ContainsKey(x.ItemKey) ? _targetCargo.ItemsOfType[x.ItemKey].Count : 0;
            }));
        
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
                    switch (i.Item)
                    {
                        case CraftedItemInstance c:
                            Buy(c);
                            break;
                        case SimpleCommodity s:
                            Buy(s, 1);
                            break;
                    }

                    Populate();
                },
                OnRightClick = () =>
                {
                    if (i.Item is SimpleCommodity s)
                    {
                        RenderRowActionSurface(
                            $"Buying {i.Name}",
                            ("Buy Quantity", () => ShowBuyQuantityDialog(i.Name, i.Price, s)));
                    }
                }
            }));
    }

    private bool PassesTypedTradeFilters(ItemInstance item)
    {
        var typedItem = FindTypedTradeItem(item);
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

    private static AetheriaRuntimeCatalogItem FindTypedTradeItem(ItemInstance item)
    {
        return ActionGameManager.RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
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
        private readonly ItemManager _itemManager;

        public TradeRow(ItemInstance item, AetheriaRuntimeCatalogItem typedItem, ItemManager itemManager)
        {
            Item = item;
            TypedItem = typedItem;
            _itemManager = itemManager;
        }

        public ItemInstance Item { get; }

        public AetheriaRuntimeCatalogItem TypedItem { get; }

        public string ItemKey => Item?.ItemKey ?? "";

        public string Name => !string.IsNullOrWhiteSpace(TypedItem?.Name) ? TypedItem.Name : "Unknown Item";

        public float Mass => TypedItem != null ? (float)TypedItem.Mass : 0f;

        public int Price
        {
            get
            {
                if (Item is CraftedItemInstance craftedItemInstance)
                    return TypedItem != null
                        ? (int)(_itemManager.GameplaySettings.QualityPriceModifier.Evaluate(craftedItemInstance.Quality) * TypedItem.Price)
                        : 0;

                return TypedItem != null ? TypedItem.Price : 0;
            }
        }

        public int ShapeWidth => TypedItem != null && TypedItem.ShapeWidth > 0 ? TypedItem.ShapeWidth : 0;

        public int ShapeHeight => TypedItem != null && TypedItem.ShapeHeight > 0 ? TypedItem.ShapeHeight : 0;

        public bool IsHull => !string.IsNullOrWhiteSpace(TypedItem?.HullType);

        public bool TryGetTypedSimpleCommodityCategory(out SimpleCommodityCategory category)
        {
            category = SimpleCommodityCategory.Minerals;
            return !string.IsNullOrWhiteSpace(TypedItem?.SimpleCommodityCategory) &&
                   Enum.TryParse(TypedItem.SimpleCommodityCategory, true, out category);
        }

        public bool TryGetTypedCompoundCommodityCategory(out CompoundCommodityCategory category)
        {
            category = CompoundCommodityCategory.Wearables;
            return !string.IsNullOrWhiteSpace(TypedItem?.CompoundCommodityCategory) &&
                   Enum.TryParse(TypedItem.CompoundCommodityCategory, true, out category);
        }

        public bool TryGetTypedHardpoint(out HardpointType hardpointType)
        {
            hardpointType = HardpointType.Hull;
            return !string.IsNullOrWhiteSpace(TypedItem?.HardpointType) &&
                   Enum.TryParse(TypedItem.HardpointType, true, out hardpointType);
        }
    }
    
    private void UpdateCreditsLabel()
    {
        if (CreditsLabel != null)
        {
            CreditsLabel.text = GameManager.Credits.ToString("N0");
        }
    }

    private void Buy(CraftedItemInstance item)
    {
        var typedItem = FindTypedTradeItem(item);
        if (typedItem == null)
        {
            ShowUnableToBuy("Missing typed trade row!");
            return;
        }

        var price = GetTypedTradePrice(item, typedItem);
        var isShipHull = !string.IsNullOrWhiteSpace(typedItem?.HullType);
        if (isShipHull &&
            !string.Equals(typedItem.HullType, nameof(HullType.Ship), StringComparison.Ordinal))
        {
            ShowUnableToBuy("Unsupported hull purchase!");
            return;
        }

        if (!GameManager.RequestTradePurchase(Inventory, _targetCargo, item, price, isShipHull))
        {
            ShowUnableToBuy("Purchase request rejected!");
            return;
        }

        UpdateCreditsLabel();
    }

    private void Buy(SimpleCommodity simpleCommodity, int quantity)
    {
        var typedItem = FindTypedTradeItem(simpleCommodity);
        if (typedItem == null)
        {
            ShowUnableToBuy("Missing typed trade row!");
            return;
        }

        var price = typedItem.Price;
        if (!GameManager.RequestTradePurchase(Inventory, _targetCargo, simpleCommodity, quantity, price))
        {
            ShowUnableToBuy("Purchase request rejected!");
            return;
        }

        UpdateCreditsLabel();
    }

    private int GetTypedTradePrice(CraftedItemInstance item, AetheriaRuntimeCatalogItem typedItem)
    {
        return (int)(GameManager.ItemManager.GameplaySettings.QualityPriceModifier.Evaluate(item.Quality) * typedItem.Price);
    }

    private void ShowUnableToBuy(string reason)
    {
        Dialog.Clear();
        Dialog.Title.text = $"Unable to buy: {reason}";
        Dialog.Show();
        Dialog.MoveToCursor();
    }

    private void ShowBuyQuantityDialog(string itemName, int price, SimpleCommodity simpleCommodity)
    {
        int quantity = 1;
        Dialog.Clear();
        Dialog.Title.text = $"Buying {itemName}";
        Dialog.AddField(
            "Quantity",
            () => quantity,
            q => quantity = q);
        Dialog.Show(() =>
        {
            Buy(simpleCommodity, quantity);
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
            AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Build(ProjectTradeItemDetailsSurfaceState(item)),
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

    private AetheriaRuntimeTradeItemDetailsSurfaceState ProjectTradeItemDetailsSurfaceState(AetheriaRuntimeCatalogItem item)
    {
        var durability = "";
        var thermalRange = "";
        var behaviorSections = Array.Empty<AetheriaRuntimeTradeItemSection>();
        if (!string.IsNullOrWhiteSpace(item.HardpointType))
        {
            durability = ActionGameManager.RuntimePlayerSettings.Format((float)item.Durability);
            thermalRange = FormatTemperatureRange(item);
            behaviorSections = ProjectTradeItemBehaviorSections(item).ToArray();
        }

        return new AetheriaRuntimeTradeItemDetailsSurfaceState(
            item.Name,
            item.Description ?? "",
            ActionGameManager.RuntimeCatalog?.GetManufacturer(item)?.Name ?? "GameCult",
            ActionGameManager.RuntimePlayerSettings.Format((float)item.Mass),
            item.Price,
            durability,
            thermalRange,
            behaviorSections,
            DateTime.UtcNow.ToString("O"));
    }

    private IEnumerable<AetheriaRuntimeTradeItemSection> ProjectTradeItemBehaviorSections(AetheriaRuntimeCatalogItem item)
    {
        foreach (var behavior in item.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
        {
            if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
            {
                var statReference = AetheriaRuntimeBehaviorValueReader.ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                var modifier = AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                var modifierType = AetheriaRuntimeBehaviorValueReader.ReadEnum(
                    FindTypedBehaviorField(behavior, 3)?.Value,
                    StatModifierType.Constant);
                yield return new AetheriaRuntimeTradeItemSection(
                    $"{AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                    "Stat Modifier",
                    new[]
                    {
                        new AetheriaRuntimeTradeItemMetric(
                            $"{AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.target",
                            $"{statReference.Target.SplitCamelCase()}:{statReference.Stat.SplitCamelCase()}",
                            $"{(modifierType == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.RuntimePlayerSettings.Format(modifier.Min)}")
                    });
                continue;
            }

            var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
            if (metadata == null)
                continue;

            var fields = metadata.DisplayFields
                .Select(field => ProjectTradeItemBehaviorMetric(behavior, field))
                .Where(metric => metric != null)
                .ToArray();

            if (fields.Length == 0)
                continue;

            yield return new AetheriaRuntimeTradeItemSection(
                $"{AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}",
                behavior.Kind.FormatTypeName(),
                fields);
        }
    }

    private AetheriaRuntimeTradeItemMetric ProjectTradeItemBehaviorMetric(
        AetheriaRuntimeBehaviorPayload behavior,
        AetheriaRuntimeBehaviorFieldMetadata field)
    {
        var payloadField = FindTypedBehaviorField(behavior, field.Key);
        if (payloadField == null)
            return null;

        string value;
        switch (field.ValueKind)
        {
            case AetheriaRuntimeBehaviorFieldValueKind.Number:
                value = ActionGameManager.RuntimePlayerSettings.Format((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Temperature:
                value = ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Integer:
                value = ((int)payloadField.Value.NumberValue).ToString();
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat:
                value = ActionGameManager.RuntimePlayerSettings.Format(AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(payloadField.Value).Min);
                break;
            default:
                return null;
        }

        return new AetheriaRuntimeTradeItemMetric(
            $"{AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
            field.Name.SplitCamelCase(),
            value);
    }

    private static string FormatTemperatureRange(AetheriaRuntimeCatalogItem item)
    {
        if (item.MaximumTemperature > item.MinimumTemperature)
        {
            return
                $"{ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)item.MinimumTemperature)} to " +
                $"{ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)item.MaximumTemperature)}";
        }

        return "No typed thermal range";
    }

    private static AetheriaRuntimeBehaviorField FindTypedBehaviorField(AetheriaRuntimeBehaviorPayload behavior, int? key)
    {
        return key == null
            ? null
            : behavior.Fields.FirstOrDefault(field => field.Key == key.Value);
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
        BuildFilterSurfaceCommands();

        _filterSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _filterSurfaceDocument,
            "Aetheria Trade Filter Surface",
            AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildFilter(ProjectTradeFilterSurfaceState()),
            HandleFilterSurfaceCommand,
            _filterSurfaceChrome,
            sortingOrder: 1001);
    }

    private void BuildFilterSurfaceCommands()
    {
        _filterSurfaceCommands.Clear();

        foreach (var hardpointType in ((HardpointType[])Enum.GetValues(typeof(HardpointType)))
                     .Where(type => _hardpointFilter.filter == null || type != _hardpointFilter.type))
        {
            var command = AetheriaRuntimeTradeInteractionSurfaceBuilder.HardpointFilterCommand(hardpointType.ToString());
            _filterSurfaceCommands[command] = () => ApplyHardpointFilter(hardpointType);
        }

        foreach (var commodityType in ((SimpleCommodityCategory[])Enum.GetValues(typeof(SimpleCommodityCategory)))
                     .Where(type => _commodityFilter.filter == null || type != _commodityFilter.type))
        {
            var command = AetheriaRuntimeTradeInteractionSurfaceBuilder.SimpleCommodityFilterCommand(commodityType.ToString());
            _filterSurfaceCommands[command] = () => ApplySimpleCommodityFilter(commodityType);
        }

        foreach (var commodityType in ((CompoundCommodityCategory[])Enum.GetValues(typeof(CompoundCommodityCategory)))
                     .Where(type => _compoundCommodityFilter.filter == null || type != _compoundCommodityFilter.type))
        {
            var command = AetheriaRuntimeTradeInteractionSurfaceBuilder.CompoundCommodityFilterCommand(commodityType.ToString());
            _filterSurfaceCommands[command] = () => ApplyCompoundCommodityFilter(commodityType);
        }

        foreach (var metadata in AetheriaRuntimeBehaviorMetadataCatalog.All
                     .Where(option => _behaviorFilters.All(filter => filter.Kind != option.Kind))
                     .OrderBy(option => option.Kind, StringComparer.Ordinal))
        {
            var command = AetheriaRuntimeTradeInteractionSurfaceBuilder.BehaviorFilterCommand(metadata.Kind);
            _filterSurfaceCommands[command] = () => ApplyBehaviorFilter(metadata);
        }

        if (!MinimumSizeFilter.gameObject.activeSelf)
        {
            _filterSurfaceCommands[AetheriaRuntimeTradeInteractionSurfaceBuilder.MinimumSizeFilterCommand()] = EnableMinimumSizeFilter;
        }

        if (!MaximumSizeFilter.gameObject.activeSelf)
        {
            _filterSurfaceCommands[AetheriaRuntimeTradeInteractionSurfaceBuilder.MaximumSizeFilterCommand()] = EnableMaximumSizeFilter;
        }
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
            _filterSurfaceCommands.TryGetValue(command.Command, out var action))
        {
            action();
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

    private AetheriaRuntimeTradeFilterSurfaceState ProjectTradeFilterSurfaceState()
    {
        var groups = new List<AetheriaRuntimeTradeSurfaceGroup>();
        AddTradeFilterGroup(groups, "hardpoint", "Gear Type", command => command.Contains(".hardpoint."));
        AddTradeFilterGroup(groups, "simple", "Simple Commodity", command => command.Contains(".simple."));
        AddTradeFilterGroup(groups, "compound", "Compound Commodity", command => command.Contains(".compound."));
        AddTradeFilterGroup(groups, "behavior", "Item Behavior", command => command.Contains(".behavior."));
        AddTradeFilterGroup(
            groups,
            "size",
            "Size",
            command => command.Contains(".size."),
            command => command.EndsWith(".minimum", StringComparison.Ordinal) ? "Minimum Size" : "Maximum Size");

        return new AetheriaRuntimeTradeFilterSurfaceState(
            BuildFilterSummary(),
            groups,
            DateTime.UtcNow.ToString("O"));
    }

    private void AddTradeFilterGroup(
        List<AetheriaRuntimeTradeSurfaceGroup> groups,
        string key,
        string title,
        Func<string, bool> commandFilter,
        Func<string, string> labelFactory = null)
    {
        var options = _filterSurfaceCommands.Keys
            .Where(commandFilter)
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => new AetheriaRuntimeTradeSurfaceOption(
                command,
                labelFactory?.Invoke(command) ?? command.Split('.').Last().FormatTypeName(),
                command))
            .ToArray();

        if (options.Length == 0)
            return;

        groups.Add(new AetheriaRuntimeTradeSurfaceGroup(
            $"{AetheriaRuntimeTradeInteractionSurfaceBuilder.FilterSurfaceId}.{key}.card",
            title,
            options));
    }

    private void RenderRowActionSurface(string title, params (string Label, Action Action)[] actions)
    {
        _rowActionTitle = title;
        BuildRowActionSurfaceCommands(actions);

        _rowActionSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _rowActionSurfaceDocument,
            "Aetheria Trade Row Action Surface",
            AetheriaRuntimeTradeInteractionSurfaceBuilder.BuildRowActions(ProjectTradeRowActionSurfaceState()),
            HandleRowActionSurfaceCommand,
            _rowActionSurfaceChrome,
            sortingOrder: 1002);
    }

    private void BuildRowActionSurfaceCommands(IEnumerable<(string Label, Action Action)> actions)
    {
        _rowActionSurfaceCommands.Clear();

        foreach (var actionEntry in actions.Select((entry, index) => (entry, index)))
        {
            var command = AetheriaRuntimeTradeInteractionSurfaceBuilder.RowActionCommand(actionEntry.index);
            _rowActionSurfaceCommands[command] = actionEntry.entry.Action;
        }
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
            _rowActionSurfaceCommands.TryGetValue(command.Command, out var action))
        {
            action();
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

    private AetheriaRuntimeTradeRowActionSurfaceState ProjectTradeRowActionSurfaceState()
    {
        var actions = _rowActionSurfaceCommands.Keys
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => new AetheriaRuntimeTradeSurfaceOption(
                command,
                command.EndsWith("action_0", StringComparison.Ordinal) ? "Buy Quantity" : command.Split('.').Last(),
                command))
            .ToArray();

        return new AetheriaRuntimeTradeRowActionSurfaceState(
            _rowActionTitle,
            actions,
            DateTime.UtcNow.ToString("O"));
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
        filter.Label.text = metadata.Kind.FormatTypeName();
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

        activeFilters.AddRange(_behaviorFilters.Select(filter => $"Behavior: {filter.Kind.FormatTypeName()}"));

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
        BuildCargoSelectionCommands();

        _cargoSelectorSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _cargoSelectorSurfaceDocument,
            "Aetheria Trade Cargo Selector Surface",
            AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Build(ProjectTradeCargoSelectorSurfaceState()),
            HandleCargoSelectorSurfaceCommand,
            _cargoSelectorSurfaceChrome);
    }

    private void BuildCargoSelectionCommands()
    {
        _cargoSelectionCommands.Clear();

        if (GameManager.DockingBay != null && _targetCargo != GameManager.DockingBay)
        {
            _cargoSelectionCommands[AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.DockingBay] =
                (GameManager.DockingBay, "Docking Bay");
        }

        if (GameManager.CurrentEntity?.Parent == null)
            return;

        foreach (var ship in GameManager.CurrentEntity.Parent.Children
                     .Where(entity => entity is Ship { IsPlayerShip: true })
                     .Cast<Ship>()
                     .Select((ship, shipIndex) => (ship, shipIndex)))
        {
            foreach (var bay in ship.ship.CargoBays.Select((cargoBay, index) => (cargoBay, index)))
            {
                if (_targetCargo == bay.cargoBay)
                    continue;

                var command = AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.ShipBayCommand(ship.shipIndex, bay.index);
                _cargoSelectionCommands[command] = (bay.cargoBay, $"{ship.ship.Name} Bay {bay.index + 1}");
            }
        }
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
            _cargoSelectionCommands.TryGetValue(command.Command, out var option))
        {
            _targetCargo = option.Cargo;
            TargetCargoLabel.text = option.Label;
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

    private AetheriaRuntimeTradeCargoSelectorSurfaceState ProjectTradeCargoSelectorSurfaceState()
    {
        var targets = _cargoSelectionCommands
            .OrderBy(pair => pair.Value.Label, StringComparer.Ordinal)
            .Select(pair => new AetheriaRuntimeTradeCargoTargetOption(
                $"{AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.SurfaceId}.{pair.Key.Split('.').Last()}",
                pair.Value.Label,
                pair.Key))
            .ToArray();

        return new AetheriaRuntimeTradeCargoSelectorSurfaceState(
            TargetCargoLabel.text ?? "",
            targets,
            DateTime.UtcNow.ToString("O"));
    }

    private static EveSurfaceComponent Card(
        string id,
        string title,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "card", new[] { ("title", title) }, children);
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", new[] { ("label", label), ("value", value) });
    }

    private static EveSurfaceComponent Text(string id, string value)
    {
        return Node(id, "text", new[] { ("value", value) });
    }

    private static EveSurfaceComponent Button(string id, string label, string command)
    {
        return Node(id, "control.button", new[] { ("label", label), ("command", command) });
    }

    private static EveSurfaceComponent ButtonRow(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
    }

    private static EveSurfaceComponent ButtonColumn(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "column", Array.Empty<(string Key, string Value)>(), children);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        IEnumerable<(string Key, string Value)> props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
            children ?? Array.Empty<EveSurfaceComponent>());
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
