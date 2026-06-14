using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class TradeMenu : MonoBehaviour
{
    public ActionGameManager GameManager;
    public ContextMenu ContextMenu;
    public ConfirmationDialog Dialog;
    public Button NewFilterButton;
    public Prototype FilterPrototype;
    public SizeFilter MinimumSizeFilter;
    public SizeFilter MaximumSizeFilter;
    public PropertiesPanel Properties;
    public Spreadsheet Spreadsheet;
    public TextMeshProUGUI TargetCargoLabel;
    public Button FoldoutButton;
    public TextMeshProUGUI CreditsLabel;

    private EquippedCargoBay _targetCargo;
    private (ItemFilter filter, HardpointType type) _hardpointFilter;
    private (ItemFilter filter, SimpleCommodityCategory type) _commodityFilter;
    private (ItemFilter filter, CompoundCommodityCategory type) _compoundCommodityFilter;
    private List<BehaviorFilter> _behaviorFilters = new List<BehaviorFilter>();
    
    public EquippedCargoBay Inventory { get; set; }
    
    private void OnEnable()
    {
        if (GameManager.DockedEntity == null) return;
        _targetCargo = GameManager.DockingBay;
        TargetCargoLabel.text = "Docking Bay";
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
        
        NewFilterButton.onClick.AddListener(() =>
        {
            ContextMenu.Clear();
            IEnumerable<HardpointType> hardpointTypes = (HardpointType[]) Enum.GetValues(typeof(HardpointType));
            if (_hardpointFilter.filter != null)
                hardpointTypes = hardpointTypes.Where(x => x != _hardpointFilter.type);
            
            IEnumerable<SimpleCommodityCategory> commodityTypes = (SimpleCommodityCategory[]) Enum.GetValues(typeof(SimpleCommodityCategory));
            if (_commodityFilter.filter != null)
                commodityTypes = commodityTypes.Where(x => x != _commodityFilter.type);
            
            IEnumerable<CompoundCommodityCategory> compoundCommodityTypes = (CompoundCommodityCategory[]) Enum.GetValues(typeof(CompoundCommodityCategory));
            if (_compoundCommodityFilter.filter != null)
                compoundCommodityTypes = compoundCommodityTypes.Where(x => x != _compoundCommodityFilter.type);
            
            ContextMenu.AddDropdown("Gear Type", hardpointTypes
                .Select<HardpointType, (string, Action, bool)>(x => (Enum.GetName(typeof(HardpointType), x), () =>
                {
                    if (!_hardpointFilter.filter)
                    {
                        _hardpointFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _hardpointFilter.filter.OnDisable += () =>
                        {
                            _hardpointFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_commodityFilter.filter)
                    {
                        _commodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _commodityFilter.filter = null;
                    }
                    if(_compoundCommodityFilter.filter)
                    {
                        _compoundCommodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _compoundCommodityFilter.filter = null;
                    }

                    _hardpointFilter.filter.Label.text = $"Hardpoint: {Enum.GetName(typeof(HardpointType), x)}";
                    _hardpointFilter.type = x;
                    Populate();
                }, true)));
            ContextMenu.AddDropdown("Simple Commodity", commodityTypes
                .Select<SimpleCommodityCategory, (string, Action, bool)>(x => (Enum.GetName(typeof(SimpleCommodityCategory), x), () =>
                {
                    if (!_commodityFilter.filter)
                    {
                        _commodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _commodityFilter.filter.OnDisable += () =>
                        {
                            _commodityFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_hardpointFilter.filter)
                    {
                        _hardpointFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _hardpointFilter.filter = null;
                    }
                    if(_compoundCommodityFilter.filter)
                    {
                        _compoundCommodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _compoundCommodityFilter.filter = null;
                    }

                    _commodityFilter.filter.Label.text = $"Hardpoint: {Enum.GetName(typeof(SimpleCommodityCategory), x)}";
                    _commodityFilter.type = x;
                    Populate();
                }, true)));
            ContextMenu.AddDropdown("Compound Commodity", compoundCommodityTypes
                .Select<CompoundCommodityCategory, (string, Action, bool)>(x => (Enum.GetName(typeof(CompoundCommodityCategory), x), () =>
                {
                    if (!_compoundCommodityFilter.filter)
                    {
                        _compoundCommodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _compoundCommodityFilter.filter.OnDisable += () =>
                        {
                            _compoundCommodityFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_hardpointFilter.filter)
                    {
                        _hardpointFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _hardpointFilter.filter = null;
                    }
                    if(_commodityFilter.filter)
                    {
                        _commodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _commodityFilter.filter = null;
                    }

                    _compoundCommodityFilter.filter.Label.text = $"Hardpoint: {Enum.GetName(typeof(CompoundCommodityCategory), x)}";
                    _compoundCommodityFilter.type = x;
                    Populate();
                }, true)));
            ContextMenu.AddDropdown("Item Behavior", AetheriaRuntimeBehaviorMetadataCatalog.All
                .Where(option => _behaviorFilters.All(filter => filter.Kind != option.Kind))
                .OrderBy(option => option.Kind, StringComparer.Ordinal)
                .Select<AetheriaRuntimeBehaviorMetadata, (string, Action, bool)>(x=> (x.Kind.FormatTypeName(), () =>
                {
                    var matchingType = _behaviorFilters.FirstOrDefault(y =>
                        AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(y.Kind, x.Kind) ||
                        AetheriaRuntimeBehaviorMetadataCatalog.IsKindOrDescendant(x.Kind, y.Kind));
                    if (matchingType?.Filter != null) matchingType.Filter.DisableButton.onClick.Invoke();
                    var filter = FilterPrototype.Instantiate<ItemFilter>();
                    filter.Label.text = x.Kind.FormatTypeName();
                    var behaviorFilter = new BehaviorFilter(filter, x);
                    filter.OnDisable += () =>
                    {
                        _behaviorFilters.Remove(behaviorFilter);
                        Populate();
                    };
                    _behaviorFilters.Add(behaviorFilter);
                    Populate();
                }, true)));
            if(!MinimumSizeFilter.gameObject.activeSelf)
                ContextMenu.AddOption("Minimum Size",
                    () =>
                    {
                        MinimumSizeFilter.gameObject.SetActive(true);
                        MinimumSizeFilter.OnDisable += () => Populate();
                    });
            if(!MaximumSizeFilter.gameObject.activeSelf)
                ContextMenu.AddOption("Maximum Size",
                    () =>
                    {
                        MaximumSizeFilter.gameObject.SetActive(true);
                        MaximumSizeFilter.OnDisable += () => Populate();
                    });
            ContextMenu.Show();
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
                        ContextMenu.Clear();
                        ContextMenu.AddOption("Buy Quantity",
                            () =>
                            {
                                int quantity = 1;
                                Dialog.Clear();
                                Dialog.Title.text = $"Buying {i.Name}";
                                Dialog.AddField("Quantity", 
                                    () => quantity, 
                                    q => quantity = min(min(q, GameManager.Credits / i.Price), s.Quantity));
                                Dialog.Show(() =>
                                {
                                    Buy(s,quantity);

                                    Populate();
                                });
                                Dialog.MoveToCursor();
                            });
                        ContextMenu.Show();
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
        if (price < GameManager.Credits)
        {
            if (!string.IsNullOrWhiteSpace(typedItem?.HullType))
            {
                if (!string.Equals(typedItem.HullType, nameof(HullType.Ship), StringComparison.Ordinal))
                    throw new ArgumentException("Attempted to buy non-ship hull from station, WTF are you doing?!");

                var ship = new Ship(GameManager.ItemManager, GameManager.Zone, item as EquippableItem, GameManager.NewEntitySettings) { IsPlayerShip = true };
                ship.SetParent(GameManager.DockedEntity);

                GameManager.Credits -= price;
                UpdateCreditsLabel();
            }
            else if (Inventory.TryTransferItem(_targetCargo, item))
            {
                GameManager.Credits -= price;
                UpdateCreditsLabel();
            }
            else
            {
                Dialog.Clear();
                Dialog.Title.text = "Unable to buy: Insufficient Cargo Space!";
                Dialog.Show();
                Dialog.MoveToCursor();
                return;
            }
        }
        else
        {
            Dialog.Clear();
            Dialog.Title.text = "Unable to buy: Insufficient Credits!";
            Dialog.Show();
            Dialog.MoveToCursor();
            return;
        }
    }

    private void Buy(SimpleCommodity simpleCommodity, int quantity)
    {
        var typedItem = FindTypedTradeItem(simpleCommodity);
        if (typedItem == null)
        {
            ShowUnableToBuy("Missing typed trade row!");
            return;
        }

        var maxStack = typedItem.MaxStack > 0 ? typedItem.MaxStack : 1;
        var price = typedItem.Price;
        // Up-rounded integer division from https://stackoverflow.com/a/503201
        int lots = (quantity - 1) / maxStack + 1;
        int remaining = quantity;
        for (int i = 0; i < lots; i++)
        {
            int q = min(remaining, maxStack);
            if (q * price < GameManager.Credits)
            {
                if (Inventory.TryTransferItem(_targetCargo, simpleCommodity, quantity))
                {
                    GameManager.Credits -= q * price;
                    UpdateCreditsLabel();
                    remaining -= q;
                }
                else
                {
                    Dialog.Clear();
                    Dialog.Title.text = "Unable to buy: Insufficient Cargo Space!";
                    Dialog.Show();
                    Dialog.MoveToCursor();
                    return;
                }
            }
            else
            {
                Dialog.Clear();
                Dialog.Title.text = "Unable to buy: Insufficient Credits!";
                Dialog.Show();
                Dialog.MoveToCursor();
                return;
            }
        }
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

    void Start()
    {
        FoldoutButton.onClick.AddListener(() =>
        {
            ContextMenu.Clear();
            if(_targetCargo != GameManager.DockingBay)
                ContextMenu.AddOption("Docking Bay",
                    () =>
                    {
                        _targetCargo = GameManager.DockingBay;
                        TargetCargoLabel.text = "Docking Bay";
                    });
            foreach (var ship in GameManager.CurrentEntity.Parent.Children.Where(e => e is Ship {IsPlayerShip: true}))
            {
                foreach (var bay in ship.CargoBays.Select((bay, index) => (bay, index)))
                {
                    if(_targetCargo != bay.bay)
                    {
                        ContextMenu.AddOption($"{ship.Name} Bay {bay.index+1}",
                            () =>
                            {
                                _targetCargo = bay.bay;
                                TargetCargoLabel.text = $"{ship.Name} Bay {bay.index+1}";
                            });
                    }
                }
            }
            ContextMenu.Show();
        });
    }

    void Update()
    {
        
    }
}
