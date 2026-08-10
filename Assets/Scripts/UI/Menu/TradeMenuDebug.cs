using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class TradeMenuDebug : MonoBehaviour
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

    private EquippedCargoBay _targetCargo;
    private (ItemFilter filter, HardpointType type) _hardpointFilter;
    private (ItemFilter filter, SimpleCommodityCategory type) _commodityFilter;
    private (ItemFilter filter, CompoundCommodityCategory type) _compoundCommodityFilter;
    private List<(ItemFilter filter, Type type)> _behaviorFilters = new List<(ItemFilter filter, Type type)>();
    
    private void OnEnable()
    {
        _targetCargo = GameManager.DockingBay;
        TargetCargoLabel.text = "Docking Bay";
        Properties.GameManager = GameManager;
        MinimumSizeFilter.Width.onEndEdit.AddListener(_ => Populate());
        MinimumSizeFilter.Height.onEndEdit.AddListener(_ => Populate());
        MaximumSizeFilter.Width.onEndEdit.AddListener(_ => Populate());
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
                    if (_hardpointFilter.filter == null)
                    {
                        _hardpointFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _hardpointFilter.filter.OnDisable += () =>
                        {
                            _hardpointFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_commodityFilter.filter != null)
                    {
                        _commodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _commodityFilter.filter = null;
                    }
                    if(_compoundCommodityFilter.filter != null)
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
                    if (_commodityFilter.filter == null)
                    {
                        _commodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _commodityFilter.filter.OnDisable += () =>
                        {
                            _commodityFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_hardpointFilter.filter != null)
                    {
                        _hardpointFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _hardpointFilter.filter = null;
                    }
                    if(_compoundCommodityFilter.filter != null)
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
                    if (_compoundCommodityFilter.filter == null)
                    {
                        _compoundCommodityFilter.filter = FilterPrototype.Instantiate<ItemFilter>();
                        _compoundCommodityFilter.filter.OnDisable += () =>
                        {
                            _compoundCommodityFilter.filter = null;
                            Populate();
                        };
                    }
                    if(_hardpointFilter.filter != null)
                    {
                        _hardpointFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _hardpointFilter.filter = null;
                    }
                    if(_commodityFilter.filter != null)
                    {
                        _commodityFilter.filter.GetComponent<Prototype>().ReturnToPool();
                        _commodityFilter.filter = null;
                    }

                    _compoundCommodityFilter.filter.Label.text = $"Hardpoint: {Enum.GetName(typeof(CompoundCommodityCategory), x)}";
                    _compoundCommodityFilter.type = x;
                    Populate();
                }, true)));
            ContextMenu.AddDropdown("Item Behavior", typeof(BehaviorData).GetAllChildClasses()
                .Where(x=> x.GetCustomAttribute<RuntimeInspectable>() != null && _behaviorFilters.All(f => f.type != x))
                .Select<Type, (string, Action, bool)>(x=> (x.Name.FormatTypeName(), () =>
                {
                    var matchingType = _behaviorFilters.FirstOrDefault(y => y.type.IsAssignableFrom(x) || x.IsAssignableFrom(y.type));
                    if (matchingType.filter != null) matchingType.filter.DisableButton.onClick.Invoke();
                    var filter = FilterPrototype.Instantiate<ItemFilter>();
                    filter.Label.text = x.Name.FormatTypeName();
                    filter.OnDisable += () =>
                    {
                        _behaviorFilters.Remove((filter, x));
                        Populate();
                    };
                    _behaviorFilters.Add((filter, x));
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
        var columns = new List<(string name, int size, Func<ItemData, Func<string>> output, Func<ItemData, IComparable> sortKey)>();
        
        columns.Add(("Name", 3,
            data => () => data.Name, 
            data => data.Name));
        if(_hardpointFilter.filter==null)
            columns.Add(("Type", 2,
                data => () =>
                {
                    if (data is SimpleCommodityData s) return Enum.GetName(typeof(SimpleCommodityCategory), s.Category);
                    if(data is CompoundCommodityData c) return Enum.GetName(typeof(CompoundCommodityCategory), c.Category);
                    if(data is EquippableItemData e) return Enum.GetName(typeof(HardpointType), e.HardpointType);
                    return "None";
                }, 
                data => 
                {
                    if (data is SimpleCommodityData s) return (int) s.Category;
                    var offset = Enum.GetValues(typeof(SimpleCommodityCategory)).Length;
                    if(data is CompoundCommodityData c) return (int) c.Category + offset;
                    offset += Enum.GetValues(typeof(CompoundCommodityCategory)).Length;
                    if(data is EquippableItemData e) return (int) e.HardpointType + offset;
                    return 0;
                }));
        columns.Add(("Mass", 1,
            data => () => ActionGameManager.PlayerSettings.Format(data.Mass), 
            data => data.Mass));
        columns.Add(("Price", 1,
            data => () => data.Price.ToString("N0"),
            data => data.Price));
        columns.Add(("Size", 1,
            data => () => $"{data.Shape.Width}x{data.Shape.Height}", 
            data => data.Shape.Width*data.Shape.Height));
        
        var items = GameManager.ItemManager.ItemData.GetAll<ItemData>().Where(i=>i.Price!=0);
        
        if (MinimumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MinimumSizeFilter.Width.text.Length > 0 && i.Shape.Width < int.Parse(MinimumSizeFilter.Width.text) ||
                 MinimumSizeFilter.Height.text.Length > 0 && i.Shape.Height < int.Parse(MinimumSizeFilter.Height.text)));
        
        if (MaximumSizeFilter.gameObject.activeSelf)
            items = items.Where(i =>
                !(MaximumSizeFilter.Width.text.Length > 0 && i.Shape.Width > int.Parse(MaximumSizeFilter.Width.text) ||
                 MaximumSizeFilter.Height.text.Length > 0 && i.Shape.Height > int.Parse(MaximumSizeFilter.Height.text)));
        
        if(_commodityFilter.filter != null)
            items = items.Where(i => i is SimpleCommodityData s && s.Category == _commodityFilter.type);
        
        if(_compoundCommodityFilter.filter != null)
            items = items.Where(i => i is CompoundCommodityData c && c.Category == _compoundCommodityFilter.type);
        
        if (_hardpointFilter.filter != null)
            items = items.Where(i => i is EquippableItemData e && e.HardpointType == _hardpointFilter.type);
        
        foreach (var (_, type) in _behaviorFilters)
        {
            items = items.Where(i => i is EquippableItemData e && e.Behaviors.Any(b => type.IsInstanceOfType(b)));
            
			foreach (var field in type.GetFields().Where(f => f.GetCustomAttribute<RuntimeInspectable>() != null))
			{
				var fieldType = field.FieldType;
				if (fieldType == typeof(float))
                    columns.Add((field.Name, 1, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return () => ActionGameManager.PlayerSettings.Format((float) field.GetValue(behavior));
                    }, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return (float) field.GetValue(behavior);
                    }));
				else if (fieldType == typeof(int))
                    columns.Add((field.Name, 1, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return () => ((int) field.GetValue(behavior)).ToString();
                    }, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return (int) field.GetValue(behavior);
                    }));
				else if (fieldType == typeof(PerformanceStat))
				{
                    columns.Add((field.Name, 1, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return () => ActionGameManager.PlayerSettings.Format(((PerformanceStat) field.GetValue(behavior)).Max);
                    }, data =>
                    {
                        var behavior = ((EquippableItemData) data).Behaviors.FirstOrDefault(b => type.IsInstanceOfType(b));
                        return ((PerformanceStat) field.GetValue(behavior)).Max;
                    }));
				}
			}
        }
        
        columns.Add(("Owned", 1,
            data => () =>
            {
                if (data is HullData)
                    return GameManager.DockedEntity.Children.Count(s => s.Hull.Data.LinkID == data.ID && s is Ship {IsPlayerShip: true}).ToString();
                if(data is SimpleCommodityData)
                    return (_targetCargo.ItemsOfType.ContainsKey(data.ID) ? _targetCargo.ItemsOfType[data.ID].Cast<SimpleCommodity>().Sum(s=>s.Quantity) : 0).ToString();
                return (_targetCargo.ItemsOfType.ContainsKey(data.ID) ? _targetCargo.ItemsOfType[data.ID].Count : 0).ToString();
            }, 
            data =>
            {
                if (data is HullData)
                    return GameManager.DockedEntity.Children.Count(s => s.Hull.Data.LinkID == data.ID && s is Ship {IsPlayerShip: true});
                if(data is SimpleCommodityData)
                    return _targetCargo.ItemsOfType.ContainsKey(data.ID) ? _targetCargo.ItemsOfType[data.ID].Cast<SimpleCommodity>().Sum(s=>s.Quantity) : 0;
                return _targetCargo.ItemsOfType.ContainsKey(data.ID) ? _targetCargo.ItemsOfType[data.ID].Count : 0;
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
                OnClick = () =>
                {
                    Properties.Inspect(i);
                },
                OnDoubleClick = () =>
                {
                    switch (i)
                    {
                        case HullData h:
                            Buy(h);
                            break;
                        case CraftedItemData c:
                            Buy(c,1);
                            break;
                        case SimpleCommodityData s:
                            Buy(s, 1);
                            break;
                    }

                    Populate();
                },
                OnRightClick = () =>
                {
                    if (!(i is HullData))
                    {
                        ContextMenu.Clear();
                        ContextMenu.AddOption("Buy Quantity",
                            () =>
                            {
                                int quantity = 1;
                                Dialog.Clear();
                                Dialog.Title.text = $"Buying {i.Name}";
                                Dialog.AddField("Quantity", () => quantity, q => quantity = min(q, GameManager.Credits/i.Price));
                                Dialog.Show(() =>
                                {
                                    switch (i)
                                    {
                                        case CraftedItemData c:
                                            Buy(c,quantity);
                                            break;
                                        case SimpleCommodityData s:
                                            Buy(s, quantity);
                                            break;
                                    }

                                    Populate();
                                });
                                Dialog.MoveToCursor();
                            });
                        ContextMenu.Show();
                    }
                }
            }));
    }

    private void Buy(HullData data)
    {
        if (data.Price < GameManager.Credits)
        {
            var hull = GameManager.ItemManager.CreateInstance(data) as EquippableItem;
            Entity entity;
            if(data.HullType==HullType.Ship)
                entity = new Ship(GameManager.ItemManager, GameManager.Zone, hull, GameManager.Settings.GameplaySettings.DefaultEntitySettings) {IsPlayerShip = true};
            else entity = new OrbitalEntity(GameManager.ItemManager, GameManager.Zone, hull, Guid.Empty, GameManager.Settings.GameplaySettings.DefaultEntitySettings);
            entity.SetParent(GameManager.DockedEntity);
            
            GameManager.Credits -= data.Price;
        }
        else
        {
            Dialog.Clear();
            Dialog.Title.text = "Unable to buy: Insufficient Credits!";
            Dialog.Show();
            Dialog.MoveToCursor();
        }
    }

    private void Buy(CraftedItemData data, int quantity)
    {
        for (int i = 0; i < quantity; i++)
        {
            if (data.Price < GameManager.Credits)
            {
                if (_targetCargo.TryStore(GameManager.ItemManager.CreateInstance(data)))
                {
                    GameManager.Credits -= data.Price;
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

    private void Buy(SimpleCommodityData data, int quantity)
    {
        // Up-rounded integer division from https://stackoverflow.com/a/503201
        int lots = (quantity - 1) / data.MaxStack + 1;
        int remaining = quantity;
        for (int i = 0; i < lots; i++)
        {
            int q = min(remaining, data.MaxStack);
            if (q * data.Price < GameManager.Credits)
            {
                if (_targetCargo.TryStore(GameManager.ItemManager.CreateInstance(data, q)))
                {
                    GameManager.Credits -= q * data.Price;
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
                    ContextMenu.AddOption($"{ship.Name} Bay {bay.index}",
                        () =>
                        {
                            _targetCargo = bay.bay;
                            TargetCargoLabel.text = $"{ship.Name} Bay {bay.index}";
                        });
            }
        });
    }

    void Update()
    {
        
    }
}
