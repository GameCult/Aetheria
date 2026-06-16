using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class TradeMenu : MonoBehaviour
{
    private const string CargoSelectorSurfaceType = "surface-state";
    private const string CargoSelectorSurfaceSchema = "gamecult.eve.surface.v1";
    private const string CargoSelectorSurfaceProviderId = "aetheria";
    private const string CargoSelectorSurfaceProviderKind = "trade.menu";
    private const string CargoSelectorSurfaceId = "aetheria.trade.target_cargo_selector";
    private const string CloseCargoSelectorCommand = "aetheria.trade.target_cargo_selector.close";
    private const string FilterSurfaceId = "aetheria.trade.filter_selector";
    private const string CloseFilterSurfaceCommand = "aetheria.trade.filter_selector.close";
    private const string RowActionSurfaceId = "aetheria.trade.row_actions";
    private const string CloseRowActionSurfaceCommand = "aetheria.trade.row_actions.close";

    public ActionGameManager GameManager;
    public ConfirmationDialog Dialog;
    public UnityEngine.UI.Button NewFilterButton;
    public Prototype FilterPrototype;
    public SizeFilter MinimumSizeFilter;
    public SizeFilter MaximumSizeFilter;
    public PropertiesPanel Properties;
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
        Properties.GameManager = GameManager;
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
                OnClick = () => Properties.Inspect(i.TypedItem),
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

        if (price > GameManager.Credits)
        {
            ShowUnableToBuy("Insufficient Credits!");
            return;
        }

        if (!GameManager.CommitTradePurchase(Inventory, _targetCargo, item, price, isShipHull))
        {
            ShowUnableToBuy(isShipHull ? "Unable to create ship!" : "Insufficient Cargo Space!");
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
        var clampedQuantity = min(quantity, simpleCommodity.Quantity);
        var totalPrice = (long)clampedQuantity * price;
        if (totalPrice > GameManager.Credits)
        {
            ShowUnableToBuy("Insufficient Credits!");
            return;
        }

        if (!GameManager.CommitTradePurchase(Inventory, _targetCargo, simpleCommodity, clampedQuantity, price))
        {
            ShowUnableToBuy("Insufficient Cargo Space!");
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
            q => quantity = min(min(q, GameManager.Credits / price), simpleCommodity.Quantity));
        Dialog.Show(() =>
        {
            Buy(simpleCommodity, quantity);
            Populate();
        });
        Dialog.MoveToCursor();
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

        var document = ResolveFilterSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexStart;
        root.style.justifyContent = Justify.FlexStart;
        root.style.paddingTop = 16;
        root.style.paddingLeft = 16;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 420;
        shell.style.maxWidth = 520;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildFilterSurfaceDefinition(), HandleFilterSurfaceCommand));
    }

    private void BuildFilterSurfaceCommands()
    {
        _filterSurfaceCommands.Clear();

        foreach (var hardpointType in ((HardpointType[])Enum.GetValues(typeof(HardpointType)))
                     .Where(type => _hardpointFilter.filter == null || type != _hardpointFilter.type))
        {
            var command = $"{FilterSurfaceId}.hardpoint.{hardpointType}";
            _filterSurfaceCommands[command] = () => ApplyHardpointFilter(hardpointType);
        }

        foreach (var commodityType in ((SimpleCommodityCategory[])Enum.GetValues(typeof(SimpleCommodityCategory)))
                     .Where(type => _commodityFilter.filter == null || type != _commodityFilter.type))
        {
            var command = $"{FilterSurfaceId}.simple.{commodityType}";
            _filterSurfaceCommands[command] = () => ApplySimpleCommodityFilter(commodityType);
        }

        foreach (var commodityType in ((CompoundCommodityCategory[])Enum.GetValues(typeof(CompoundCommodityCategory)))
                     .Where(type => _compoundCommodityFilter.filter == null || type != _compoundCommodityFilter.type))
        {
            var command = $"{FilterSurfaceId}.compound.{commodityType}";
            _filterSurfaceCommands[command] = () => ApplyCompoundCommodityFilter(commodityType);
        }

        foreach (var metadata in AetheriaRuntimeBehaviorMetadataCatalog.All
                     .Where(option => _behaviorFilters.All(filter => filter.Kind != option.Kind))
                     .OrderBy(option => option.Kind, StringComparer.Ordinal))
        {
            var command = $"{FilterSurfaceId}.behavior.{metadata.Kind}";
            _filterSurfaceCommands[command] = () => ApplyBehaviorFilter(metadata);
        }

        if (!MinimumSizeFilter.gameObject.activeSelf)
        {
            _filterSurfaceCommands[$"{FilterSurfaceId}.size.minimum"] = EnableMinimumSizeFilter;
        }

        if (!MaximumSizeFilter.gameObject.activeSelf)
        {
            _filterSurfaceCommands[$"{FilterSurfaceId}.size.maximum"] = EnableMaximumSizeFilter;
        }
    }

    private void HandleFilterSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, CloseFilterSurfaceCommand, StringComparison.Ordinal))
        {
            HideFilterSurface();
            return;
        }

        if (_filterSurfaceCommands.TryGetValue(request.Command, out var action))
        {
            action();
            HideFilterSurface();
            return;
        }

        Debug.LogWarning($"Unknown trade filter command: {request.Command}");
    }

    private void HideFilterSurface()
    {
        if (_filterSurfaceDocument == null)
            return;

        _filterSurfaceDocument.rootVisualElement.Clear();
        _filterSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveFilterSurfaceDocument()
    {
        if (_filterSurfaceDocument != null)
            return _filterSurfaceDocument;

        var host = new GameObject("Aetheria Trade Filter Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1001;
        host.SetActive(false);
        _filterSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildFilterSurfaceDefinition()
    {
        var cards = new List<EveSurfaceComponent>
        {
            Card(
                $"{FilterSurfaceId}.summary",
                "Trade Filters",
                Text(
                    $"{FilterSurfaceId}.note",
                    "Trade still owns filter state and population. This surface just removes the old context-menu shell."),
                Text(
                    $"{FilterSurfaceId}.active",
                    BuildFilterSummary()))
        };

        var hardpointButtons = _filterSurfaceCommands.Keys
            .Where(command => command.StartsWith($"{FilterSurfaceId}.hardpoint.", StringComparison.Ordinal))
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => Button(
                command,
                command.Split('.').Last().FormatTypeName(),
                command))
            .ToArray();
        if (hardpointButtons.Length > 0)
        {
            cards.Add(Card($"{FilterSurfaceId}.hardpoint.card", "Gear Type", ButtonColumn($"{FilterSurfaceId}.hardpoint.options", hardpointButtons)));
        }

        var simpleButtons = _filterSurfaceCommands.Keys
            .Where(command => command.StartsWith($"{FilterSurfaceId}.simple.", StringComparison.Ordinal))
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => Button(
                command,
                command.Split('.').Last().FormatTypeName(),
                command))
            .ToArray();
        if (simpleButtons.Length > 0)
        {
            cards.Add(Card($"{FilterSurfaceId}.simple.card", "Simple Commodity", ButtonColumn($"{FilterSurfaceId}.simple.options", simpleButtons)));
        }

        var compoundButtons = _filterSurfaceCommands.Keys
            .Where(command => command.StartsWith($"{FilterSurfaceId}.compound.", StringComparison.Ordinal))
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => Button(
                command,
                command.Split('.').Last().FormatTypeName(),
                command))
            .ToArray();
        if (compoundButtons.Length > 0)
        {
            cards.Add(Card($"{FilterSurfaceId}.compound.card", "Compound Commodity", ButtonColumn($"{FilterSurfaceId}.compound.options", compoundButtons)));
        }

        var behaviorButtons = _filterSurfaceCommands.Keys
            .Where(command => command.StartsWith($"{FilterSurfaceId}.behavior.", StringComparison.Ordinal))
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => Button(
                command,
                command.Split('.').Last().FormatTypeName(),
                command))
            .ToArray();
        if (behaviorButtons.Length > 0)
        {
            cards.Add(Card($"{FilterSurfaceId}.behavior.card", "Item Behavior", ButtonColumn($"{FilterSurfaceId}.behavior.options", behaviorButtons)));
        }

        var sizeButtons = _filterSurfaceCommands.Keys
            .Where(command => command.StartsWith($"{FilterSurfaceId}.size.", StringComparison.Ordinal))
            .OrderBy(command => command, StringComparer.Ordinal)
            .Select(command => Button(
                command,
                command.EndsWith(".minimum", StringComparison.Ordinal) ? "Minimum Size" : "Maximum Size",
                command))
            .ToArray();
        if (sizeButtons.Length > 0)
        {
            cards.Add(Card($"{FilterSurfaceId}.size.card", "Size", ButtonColumn($"{FilterSurfaceId}.size.options", sizeButtons)));
        }

        cards.Add(ButtonRow($"{FilterSurfaceId}.actions", Button($"{FilterSurfaceId}.close", "Close", CloseFilterSurfaceCommand)));

        return new EveSurfaceDocument(
            CargoSelectorSurfaceType,
            CargoSelectorSurfaceSchema,
            CargoSelectorSurfaceProviderId,
            CargoSelectorSurfaceProviderKind,
            "Trade Filter Selector",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                FilterSurfaceId,
                Node(
                    $"{FilterSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    cards.ToArray()),
                Array.Empty<EveStyleToken>()),
            _filterSurfaceCommands.Keys
                .Select(command => new EveCommandTemplate(command, command.Split('.').Last().FormatTypeName(), "unity-uitoolkit"))
                .Append(new EveCommandTemplate(CloseFilterSurfaceCommand, "Close", "unity-uitoolkit"))
                .ToArray());
    }

    private void RenderRowActionSurface(string title, params (string Label, Action Action)[] actions)
    {
        _rowActionTitle = title;
        BuildRowActionSurfaceCommands(actions);

        var document = ResolveRowActionSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexStart;
        root.style.justifyContent = Justify.FlexStart;
        root.style.paddingTop = 16;
        root.style.paddingLeft = 16;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 320;
        shell.style.maxWidth = 360;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildRowActionSurfaceDefinition(), HandleRowActionSurfaceCommand));
    }

    private void BuildRowActionSurfaceCommands(IEnumerable<(string Label, Action Action)> actions)
    {
        _rowActionSurfaceCommands.Clear();

        foreach (var actionEntry in actions.Select((entry, index) => (entry, index)))
        {
            var command = $"{RowActionSurfaceId}.action_{actionEntry.index}";
            _rowActionSurfaceCommands[command] = actionEntry.entry.Action;
        }
    }

    private void HandleRowActionSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, CloseRowActionSurfaceCommand, StringComparison.Ordinal))
        {
            HideRowActionSurface();
            return;
        }

        if (_rowActionSurfaceCommands.TryGetValue(request.Command, out var action))
        {
            action();
            HideRowActionSurface();
            return;
        }

        Debug.LogWarning($"Unknown trade row action command: {request.Command}");
    }

    private void HideRowActionSurface()
    {
        if (_rowActionSurfaceDocument == null)
            return;

        _rowActionSurfaceDocument.rootVisualElement.Clear();
        _rowActionSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveRowActionSurfaceDocument()
    {
        if (_rowActionSurfaceDocument != null)
            return _rowActionSurfaceDocument;

        var host = new GameObject("Aetheria Trade Row Action Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1002;
        host.SetActive(false);
        _rowActionSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildRowActionSurfaceDefinition()
    {
        var commands = _rowActionSurfaceCommands.Keys.OrderBy(command => command, StringComparer.Ordinal).ToArray();
        var buttons = commands
            .Select(command => Button(command, command.EndsWith("action_0", StringComparison.Ordinal) ? "Buy Quantity" : command.Split('.').Last(), command))
            .ToArray();

        return new EveSurfaceDocument(
            CargoSelectorSurfaceType,
            CargoSelectorSurfaceSchema,
            CargoSelectorSurfaceProviderId,
            CargoSelectorSurfaceProviderKind,
            "Trade Row Actions",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                RowActionSurfaceId,
                Node(
                    $"{RowActionSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    Card(
                        $"{RowActionSurfaceId}.card",
                        "Trade Action",
                        Text($"{RowActionSurfaceId}.title", _rowActionTitle),
                        Text(
                            $"{RowActionSurfaceId}.note",
                            "Trade still owns the quantity dialog and purchase commit. This surface just removes the old right-click context-menu shell."),
                        ButtonColumn($"{RowActionSurfaceId}.options", buttons),
                        ButtonRow($"{RowActionSurfaceId}.actions", Button($"{RowActionSurfaceId}.close", "Close", CloseRowActionSurfaceCommand)))),
                Array.Empty<EveStyleToken>()),
            commands
                .Select(command => new EveCommandTemplate(command, command.EndsWith("action_0", StringComparison.Ordinal) ? "Buy Quantity" : command.Split('.').Last(), "unity-uitoolkit"))
                .Append(new EveCommandTemplate(CloseRowActionSurfaceCommand, "Close", "unity-uitoolkit"))
                .ToArray());
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

        var document = ResolveCargoSelectorSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexEnd;
        root.style.justifyContent = Justify.FlexStart;
        root.style.paddingTop = 16;
        root.style.paddingRight = 16;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 360;
        shell.style.maxWidth = 420;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildCargoSelectorSurfaceDefinition(), HandleCargoSelectorSurfaceCommand));
    }

    private void BuildCargoSelectionCommands()
    {
        _cargoSelectionCommands.Clear();

        if (GameManager.DockingBay != null && _targetCargo != GameManager.DockingBay)
        {
            _cargoSelectionCommands["aetheria.trade.target_cargo_selector.docking_bay"] =
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

                var command = $"aetheria.trade.target_cargo_selector.ship_{ship.shipIndex}_bay_{bay.index}";
                _cargoSelectionCommands[command] = (bay.cargoBay, $"{ship.ship.Name} Bay {bay.index + 1}");
            }
        }
    }

    private void HandleCargoSelectorSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, CloseCargoSelectorCommand, StringComparison.Ordinal))
        {
            HideCargoSelectorSurface();
            return;
        }

        if (_cargoSelectionCommands.TryGetValue(request.Command, out var option))
        {
            _targetCargo = option.Cargo;
            TargetCargoLabel.text = option.Label;
            HideCargoSelectorSurface();
            Populate();
            return;
        }

        Debug.LogWarning($"Unknown trade cargo selector command: {request.Command}");
    }

    private void HideCargoSelectorSurface()
    {
        if (_cargoSelectorSurfaceDocument == null)
            return;

        _cargoSelectorSurfaceDocument.rootVisualElement.Clear();
        _cargoSelectorSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveCargoSelectorSurfaceDocument()
    {
        if (_cargoSelectorSurfaceDocument != null)
            return _cargoSelectorSurfaceDocument;

        var host = new GameObject("Aetheria Trade Cargo Selector Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _cargoSelectorSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildCargoSelectorSurfaceDefinition()
    {
        var buttons = _cargoSelectionCommands
            .OrderBy(pair => pair.Value.Label, StringComparer.Ordinal)
            .Select(pair => Button(
                $"{CargoSelectorSurfaceId}.{pair.Key.Split('.').Last()}",
                pair.Value.Label,
                pair.Key))
            .ToArray();

        return new EveSurfaceDocument(
            CargoSelectorSurfaceType,
            CargoSelectorSurfaceSchema,
            CargoSelectorSurfaceProviderId,
            CargoSelectorSurfaceProviderKind,
            "Trade Target Cargo Selector",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                CargoSelectorSurfaceId,
                Node(
                    $"{CargoSelectorSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    Card(
                        $"{CargoSelectorSurfaceId}.card",
                        "Target Cargo",
                        Metric($"{CargoSelectorSurfaceId}.current", "Current", TargetCargoLabel.text ?? ""),
                        Text(
                            $"{CargoSelectorSurfaceId}.note",
                            "Trade still owns local presentation state here; this surface cuts out the old context-menu option list."),
                        ButtonColumn($"{CargoSelectorSurfaceId}.options", buttons),
                        ButtonRow(
                            $"{CargoSelectorSurfaceId}.actions",
                            Button($"{CargoSelectorSurfaceId}.close", "Close", CloseCargoSelectorCommand)))),
                Array.Empty<EveStyleToken>()),
            _cargoSelectionCommands.Keys
                .Select(command => new EveCommandTemplate(command, _cargoSelectionCommands[command].Label, "unity-uitoolkit"))
                .Append(new EveCommandTemplate(CloseCargoSelectorCommand, "Close", "unity-uitoolkit"))
                .ToArray());
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
    }

    private void OnDestroy()
    {
        if (_cargoSelectorSurfaceDocument != null)
        {
            Destroy(_cargoSelectorSurfaceDocument.gameObject);
            _cargoSelectorSurfaceDocument = null;
        }

        if (_filterSurfaceDocument != null)
        {
            Destroy(_filterSurfaceDocument.gameObject);
            _filterSurfaceDocument = null;
        }

        if (_rowActionSurfaceDocument != null)
        {
            Destroy(_rowActionSurfaceDocument.gameObject);
            _rowActionSurfaceDocument = null;
        }
    }
}
