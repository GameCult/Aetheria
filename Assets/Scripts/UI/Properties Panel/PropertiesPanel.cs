/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class PropertiesPanel : MonoBehaviour
{
	public RectTransform Spacer;
	public DropdownMenu Dropdown;
    public TextMeshProUGUI Title;
    public RectTransform Section;
    public PropertiesList List;
    public Property Property;
    public PropertyLabel PropertyLabel;
    public AttributeProperty Attribute;
    public InputField InputField;
    public RangedFloatField RangedFloatField;
    public RangedFloatField ProgressField;
    public EnumField EnumField;
    public BoolField BoolField;
    public PropertyButton PropertyButton;
    public ButtonField ButtonField;
    public IncrementField IncrementField;
    public StatSheet StatSheet;
    public CurveField CurveField;
    public WeaponGroupAssignment WeaponGroupAssignment;
    public RectTransform Content;
    public RectTransform DragParent;
    
    [HideInInspector] public ActionGameManager GameManager;

    protected List<GameObject> Properties = new List<GameObject>();
    protected event Action RefreshPropertyValues;
    protected bool RadioSelection = false;

    private RectTransform _dragObject;
    

    protected event Action<GameObject> OnPropertyAdded;
    protected Action OnPropertiesChanged;

    public int Children => Properties.Count;
    
    public void Awake()
    {
	    OnPropertyAdded += go => go.SetActive(true);
    }

    public virtual void Update()
    {
	    RefreshValues();
    }

    private void OnDestroy()
    {
    }

    // private void OnDisable()
    // {
	   //  RemoveListener();
    // }

    public void Clear()
    {
	    //RemoveListener();
        foreach(var property in Properties)
            Destroy(property);
        Title.text = "Properties";
        Properties.Clear();
        RefreshPropertyValues = null;
        RadioSelection = false;
    }

    public RectTransform AddSpacer()
    {
	    var spacer = Instantiate(Spacer, Content ?? transform);
	    Properties.Add(spacer.gameObject);
	    OnPropertyAdded?.Invoke(spacer.gameObject);
	    return spacer;
    }

    public RectTransform AddSection(string name)
    {
        var section = Instantiate(Section, Content ?? transform);
        section.GetComponentInChildren<TextMeshProUGUI>().text = name;
        Properties.Add(section.gameObject);
        OnPropertyAdded?.Invoke(section.gameObject);
        return section;
    }

    public Property AddProperty(Func<string> read)
    {
	    if (read == null) throw new ArgumentException("Attempted to add property with null read function!");
	    
	    var property = Instantiate(Property, Content ?? transform);
	    property.Label.text = read();

		RefreshPropertyValues += () => property.Label.text = read();
	    Properties.Add(property.gameObject);
	    OnPropertyAdded?.Invoke(property.gameObject);
	    return property;
    }

    public Property AddProperty(string name, Func<string> read = null)
    {
	    Property property;
	    if(read != null)
			property = Instantiate(PropertyLabel, Content ?? transform);
	    else
		    property = Instantiate(Property, Content ?? transform);
        property.Label.text = name;

        if (read != null)
	        RefreshPropertyValues += () => ((PropertyLabel) property).Value.text = read.Invoke();
        Properties.Add(property.gameObject);
        OnPropertyAdded?.Invoke(property.gameObject);
        return property;
    }

    public StatSheet AddStatSheet()
    {
	    var sheet = Instantiate(StatSheet, Content ?? transform);
	    Properties.Add(sheet.gameObject);
        OnPropertyAdded?.Invoke(sheet.gameObject);
        RefreshPropertyValues += () => sheet.RefreshValues();
        return sheet;
    }

    public PropertiesList AddList(string name) //, IEnumerable<(string, Func<string>)> elements)
    {
        var list = Instantiate(List, Content ?? transform);
        list.GameManager = GameManager;
        list.Dropdown = Dropdown;
        list.Title.text = name;
        // foreach (var element in elements)
        // {
        //     var item = Instantiate(PropertyPrefab, list);
        //     item.Name.text = element.Item1;
        //     item.Value.text = element.Item2();
        //     item.ValueFunction = element.Item2;
        //     Properties.Add(item.gameObject);
        // }
        //RefreshPropertyValues += () => list.RefreshValues();
        Properties.Add(list.gameObject);
        OnPropertyAdded?.Invoke(list.gameObject);
        return list;
    }

    public AttributeProperty AddPersonalityProperty(PersonalityAttribute attribute, Func<float> read)
    {
        var attributeInstance = Instantiate(Attribute, Content ?? transform);
        attributeInstance.Title.text = attribute.Name;
        attributeInstance.HighLabel.text = attribute.HighName;
        attributeInstance.LowLabel.text = attribute.LowName;
        RefreshPropertyValues += () => attributeInstance.Slider.value = read();
        Properties.Add(attributeInstance.gameObject);
        OnPropertyAdded?.Invoke(attributeInstance.gameObject);
        return attributeInstance;
    }

    public CurveField AddCurveField()
    {
	    var curveInstance = Instantiate(CurveField, Content ?? transform);
	    Properties.Add(curveInstance.gameObject);
	    return curveInstance;
    }

    public virtual PropertyButton AddButton(string name, Action onClick)
    {
	    var button = Instantiate(PropertyButton, Content ?? transform);
	    button.Label.text = name;
	    button.Button.interactable = onClick != null;
	    button.Button.onClick.AddListener(() => onClick());
	    Properties.Add(button.gameObject);
	    OnPropertyAdded?.Invoke(button.gameObject);
	    return button;
    }

    public void AddButton(string name, string label, Action onClick)
    {
	    var button = Instantiate(ButtonField, Content ?? transform);
	    button.Label.text = name;
	    button.ButtonLabel.text = label;
	    button.Button.interactable = onClick != null;
	    button.Button.onClick.AddListener(() => onClick());
	    Properties.Add(button.gameObject);
	    OnPropertyAdded?.Invoke(button.gameObject);
    }
	
	public void AddField(string name, Func<string> read, Action<string> write)
	{
		var field = Instantiate(InputField, Content ?? transform);
		field.Label.text = name;
		field.Field.contentType = TMP_InputField.ContentType.Standard;
		field.Field.onValueChanged.AddListener(val => write(val));
		// field.Field.text = read();
		RefreshPropertyValues += () =>
		{
			var s = read();
			if (field.Field.text != s)
			{
				field.Field.text = s;
				field.gameObject.SetActive(false);
				Observable.NextFrame().Subscribe(_ =>
				{
					if(field != null)
						OnPropertyAdded?.Invoke(field.gameObject);
				});
			}
		};
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}

	public void AddField(string name, Func<float> read, Action<float> write)
	{
		var field = Instantiate(InputField, Content ?? transform);
		field.Label.text = name;
		field.Field.contentType = TMP_InputField.ContentType.DecimalNumber;
		field.Field.onValueChanged.AddListener(val => write(float.Parse(val)));
		RefreshPropertyValues += () =>
		{
			var s = read().ToString(CultureInfo.InvariantCulture);
			if (field.Field.text != s)
			{
				field.Field.text = s;
				field.gameObject.SetActive(false);
				Observable.NextFrame().Subscribe(_ => OnPropertyAdded?.Invoke(field.gameObject));
			}
		};
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddField(string name, Func<int> read, Action<int> write)
	{
		var field = Instantiate(InputField, Content ?? transform);
		field.Label.text = name;
		field.Field.contentType = TMP_InputField.ContentType.IntegerNumber;
		field.Field.onValueChanged.AddListener(val => write(int.Parse(val)));
		RefreshPropertyValues += () =>
		{
			var s = read().ToString(CultureInfo.InvariantCulture);
			if (field.Field.text != s)
			{
				field.Field.text = s;
				field.gameObject.SetActive(false);
				Observable.NextFrame().Subscribe(_ => OnPropertyAdded?.Invoke(field.gameObject));
			}
		};
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddIncrementField(string name, Func<int> read, Action<int> write, Func<int> min, Func<int> max)
	{
		var field = Instantiate(IncrementField, Content ?? transform);
		field.Label.text = name;
		field.Increment.onClick.AddListener(() => write(read() + 1));
		field.Decrement.onClick.AddListener(() => write(read() - 1));
		RefreshPropertyValues += () =>
		{
			var val = read();
			var minval = min();
			var maxval = max();
			if (val < minval || val > maxval)
				write(val = clamp(val, minval, maxval));
			field.Value.text = val.ToString(CultureInfo.InvariantCulture);
			field.Increment.interactable = val == maxval;
			field.Decrement.interactable = val == minval;
		};
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddField(string name, Func<float> read, Action<float> write, float min, float max)
	{
		var field = Instantiate(RangedFloatField, Content ?? transform);
		field.Label.text = name;
		field.Slider.wholeNumbers = false;
		field.Slider.minValue = min;
		field.Slider.maxValue = max;
		field.Slider.onValueChanged.AddListener(val => write(val));
		RefreshPropertyValues += () => field.Slider.value = read();
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddProgressField(string name, Func<float> read)
	{
		var field = Instantiate(ProgressField, Content ?? transform);
		field.Label.text = name;
		field.Slider.wholeNumbers = false;
		field.Slider.minValue = 0;
		field.Slider.maxValue = 1;
		//field.Slider.onValueChanged.AddListener(val => write(val));
		RefreshPropertyValues += () => field.Slider.value = read();
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddField(string name, Func<int> read, Action<int> write, int min, int max)
	{
		var field = Instantiate(RangedFloatField, Content ?? transform);
		field.Label.text = name;
		field.Slider.wholeNumbers = true;
		field.Slider.minValue = min;
		field.Slider.maxValue = max;
		field.Slider.onValueChanged.AddListener(val => write(Mathf.RoundToInt(val)));
		RefreshPropertyValues += () => field.Slider.value = read();
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddField(string name, Func<bool> read, Action<bool> write)
	{
		var field = Instantiate(BoolField, Content ?? transform);
		field.Label.text = name;
		field.Toggle.onValueChanged.AddListener(val => write(val));
		RefreshPropertyValues += () => field.Toggle.isOn = read();
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	public void AddField(string name, Func<int> read, Action<int> write, string[] enumOptions)
	{
		var field = Instantiate(EnumField, Content ?? transform);
		field.Label.text = name;
		field.Dropdown.onClick.AddListener(() =>
		{
			var selected = read();
			Dropdown.gameObject.SetActive(true);
			Dropdown.Clear();
			for (int i = 0; i < enumOptions.Length; i++)
			{
				var index = i;
				Dropdown.AddOption(enumOptions[i], () => write(index), index == selected);
			}

			Dropdown.Show((RectTransform) field.Dropdown.transform);
		});
		RefreshPropertyValues += () => field.DropdownLabel.text = enumOptions[read()];
		Properties.Add(field.gameObject);
		OnPropertyAdded?.Invoke(field.gameObject);
	}
	
	// public void Inspect(Entity entity)
	// {
 //        Clear();
 //        Title.text = entity.Name;
 //        var hullData = Context.GetData(entity.Hull) as HullData;
 //        AddSection(
 //            hullData.HullType == HullType.Ship ? "Ship" :
 //            hullData.HullType == HullType.Station ? "Station" :
 //            "Platform");
 //        //AddList(hullData.Name).Inspect(hull, entity);
 //        //PropertiesPanel.AddProperty("Hull", () => $"{hullData.Name}");
 //        AddEntityProperties(entity);
 //        //cargoList.SetExpanded(false,true);
 //        
 //        RefreshValues();
	// }

	// private void AddEntityProperties(Entity entity)
	// {
	// 	AddField("Name", () => entity.Name, name => entity.Name = name);
	// 	AddProperty("Mass", () => $"{entity.Mass.SignificantDigits(Context.GameplaySettings.SignificantDigits)}");
	// }

	private void AddItemProperties(ItemInstance item)
	{
		var data = item.Data.Value;
		
		AddProperty(data.Description);
		
		if (item is SimpleCommodity simpleCommodity)
			AddProperty("Quantity", () => simpleCommodity.Quantity.ToString());
		
		var sheet = AddStatSheet();
		var manufacturer = ActionGameManager.CultCache.Get<Faction>(data.Manufacturer);
		sheet.AddStat("Manufacturer", () => manufacturer?.Name ?? "GameCult");
		sheet.AddStat("Mass", () => ActionGameManager.PlayerSettings.Format(GameManager.ItemManager.GetMass(item)));
		
		//AddProperty("Thermal Mass", () => Context.GetThermalMass(item).SignificantDigits(Context.GameplaySettings.SignificantDigits));
	}

	private void AddEquippableItemProperties(EquippableItem item, Func<PerformanceStat, float> statValueFunction)
	{
		if (item.Durability < .01f)
		{
			return;
		}

		var gearData = GameManager.ItemManager.GetData(item);

		var sheet = AddStatSheet();
		foreach (var behavior in gearData.Behaviors)
		{
			if (behavior is StatModifierData statMod)
			{
				sheet.AddStat($"{statMod.Stat.Target.SplitCamelCase()}:{statMod.Stat.Stat.SplitCamelCase()}", () => $"{(statMod.Type == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.PlayerSettings.Format(statValueFunction(statMod.Modifier))}");
			}
			else
			{
				var type = behavior.GetType();
				if (type.GetCustomAttribute(typeof(RuntimeInspectable)) == null) continue;
				foreach (var field in type.GetFields().Where(f => f.GetCustomAttribute<RuntimeInspectable>() != null))
				{
					var fieldType = field.FieldType;
					if (fieldType == typeof(float))
						sheet.AddStat(field.Name.SplitCamelCase(), () => $"{ActionGameManager.PlayerSettings.Format((float) field.GetValue(behavior))}");
					else if (fieldType == typeof(int))
						sheet.AddStat(field.Name.SplitCamelCase(), () => $"{(int) field.GetValue(behavior)}");
					else if (fieldType == typeof(PerformanceStat))
					{
						var stat = (PerformanceStat) field.GetValue(behavior);
						sheet.AddStat(field.Name.SplitCamelCase(), () => $"{ActionGameManager.PlayerSettings.Format(statValueFunction(stat))}");
					}
				}
			}
		}

		if (gearData.Behaviors.FirstOrDefault(b => b is WeaponData) is WeaponData weapon)
		{
			var range = AddCurveField();
			range.Show("Damage Range", weapon.DamageCurve, t => ActionGameManager.PlayerSettings.Format(lerp(statValueFunction(weapon.MinRange), statValueFunction(weapon.Range), t)));
		}
	}

	private string GetTitle(EquippableItem item)
	{
		var data = item.Data.Value;
		var (tier, upgrades) = GameManager.ItemManager.GetTier(item);
		return
			$"<color=#{ColorUtility.ToHtmlStringRGB(tier.Color.ToColor())}>{data.Name}</color><smallcaps><size=60%> ({tier.Name}{new string('+', upgrades)})";
	}

	public void Inspect(EquippedItem item)
	{
		Clear();
		if (item?.EquippableItem == null) return;
		
		Title.text = GetTitle(item.EquippableItem);
		
		AddItemProperties(item.EquippableItem);
		AddSpacer();

		if (item.GetBehavior<Weapon>() != null)
		{
			var weaponGroups = Instantiate(WeaponGroupAssignment, Content ?? transform);
			weaponGroups.Inspect(item);
			var dragOffset = Vector2.zero;
			Transform dragObject = null;
			weaponGroups.OnBeginDragAsObservable().Subscribe(x =>
			{
				//Debug.Log($"Began dragging weapon group {x.group}");
				GameManager.BeginDrag(new WeaponGroupDragObject(x.group));
				dragObject = Instantiate(weaponGroups.Groups[x.group], DragParent, true).transform;
				dragOffset = (Vector2)dragObject.position - x.pointerEventData.position;
			});
			weaponGroups.OnDragAsObservable().Subscribe(x =>
			{
				dragObject.position = x.pointerEventData.position + dragOffset;
			});
			weaponGroups.OnEndDragAsObservable().Subscribe(x =>
			{
				//Debug.Log($"Ended dragging weapon group {x.group}");
				GameManager.EndDrag();
				Destroy(dragObject.gameObject);
			});
			Properties.Add(weaponGroups.gameObject);
			OnPropertyAdded?.Invoke(weaponGroups.gameObject);
		}
		
		var gearData = GameManager.ItemManager.GetData(item.EquippableItem);
		var statusSheet = AddStatSheet();
		if (item.EquippableItem.Durability < .01f)
			statusSheet.AddStat("Durability", () => "Item Destroyed!");
		else statusSheet.AddStat("Durability", () => $"{(int)(item.EquippableItem.Durability / gearData.Durability * 100)}%");
		statusSheet.AddStat("Temperature", () => ActionGameManager.PlayerSettings.FormatTemperature(item.Temperature));
		
		var heatCurve = AddCurveField();
		heatCurve.Show(
			"Thermal Performance", 
			gearData.HeatPerformanceCurve, 
			t => ActionGameManager.PlayerSettings.FormatTemperature(lerp(gearData.MinimumTemperature, gearData.MaximumTemperature, t)), 
			true);
		RefreshPropertyValues += () => heatCurve.SetCurrent(unlerp(gearData.MinimumTemperature, gearData.MaximumTemperature, item.Temperature));
		AddEquippableItemProperties(item.EquippableItem, item.Evaluate);
		AddSpacer();
		
		AddField("Override Shutdown", () => item.EquippableItem.OverrideShutdown, b => item.EquippableItem.OverrideShutdown = b);
		
		foreach (var behavior in item.Behaviors)
		{
			switch (behavior)
			{
				case Thermotoggle thermotoggle when thermotoggle.ThermotoggleData.Adjustable:
					AddField("Target Temperature",
						() => thermotoggle.TargetTemperature,
						temp => thermotoggle.TargetTemperature = temp);
					break;
			}
		}

		RefreshValues();
	}

	public void Inspect(ItemInstance item)
	{
		Clear();
		
		AddItemProperties(item);
		
		if (item is EquippableItem gear)
		{
			Title.text = GetTitle(gear);
			AddSpacer();
			var gearData = GameManager.ItemManager.GetData(gear);
			var statusSheet = AddStatSheet();
			statusSheet.AddStat("Durability", () => $"{(int)(gear.Durability / gearData.Durability * 100)}%");
			var heatCurve = AddCurveField();
			heatCurve.Show(
				"Thermal Performance", 
				gearData.HeatPerformanceCurve, 
				t => ActionGameManager.PlayerSettings.FormatTemperature(lerp(gearData.MinimumTemperature, gearData.MaximumTemperature, t)), 
				true);
			AddEquippableItemProperties(gear, stat => GameManager.ItemManager.Evaluate(stat, gear));
		}
		
		RefreshValues();
	}

	public void Inspect(ItemData data)
	{
		AddProperty("Type", () => data.Name);
		AddProperty(data.Description).Label.fontStyle = FontStyles.Normal;
		if (data is EquippableItemData gearData)
		{
			AddProperty("Durability", () => ActionGameManager.PlayerSettings.Format(gearData.Durability));
			var sheet = AddStatSheet();
			foreach (var behavior in gearData.Behaviors)
			{
				if (behavior is StatModifierData statMod)
				{
					if(Math.Abs(statMod.Modifier.Min - statMod.Modifier.Max) < .001f)
						sheet.AddStat($"{statMod.Stat.Target}:{statMod.Stat.Stat}", () => $"{(statMod.Type == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.PlayerSettings.Format(statMod.Modifier.Min)}");
					else
						sheet.AddStat($"{statMod.Stat.Target}:{statMod.Stat.Stat}", () => $"{(statMod.Type == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.PlayerSettings.Format(statMod.Modifier.Min)}-{ActionGameManager.PlayerSettings.Format(statMod.Modifier.Max)}");
				}
				var type = behavior.GetType();
				if (type.GetCustomAttribute(typeof(RuntimeInspectable)) != null)
				{
					foreach (var field in type.GetFields().Where(f => f.GetCustomAttribute<RuntimeInspectable>() != null))
					{
						var fieldType = field.FieldType;
						if (fieldType == typeof(float))
							sheet.AddStat(field.Name, () => $"{ActionGameManager.PlayerSettings.Format((float) field.GetValue(behavior))}");
						else if (fieldType == typeof(int))
							sheet.AddStat(field.Name, () => $"{(int) field.GetValue(behavior)}");
						else if (fieldType == typeof(PerformanceStat))
						{
							var stat = (PerformanceStat) field.GetValue(behavior);
							if(Math.Abs(stat.Min - stat.Max) < .001f)
								sheet.AddStat(field.Name, () => $"{ActionGameManager.PlayerSettings.Format(stat.Min)}");
							else
								sheet.AddStat(field.Name, () => $"{ActionGameManager.PlayerSettings.Format(stat.Min)}-{ActionGameManager.PlayerSettings.Format(stat.Max)}");
						}
					}
				}
			}
		}
	}

	public void Inspect(object obj, bool inspectablesOnly = false, bool readWrite = false, bool topLevel = true)
	{
		var fields = obj.GetType().GetFields();
		foreach (var field in fields)
			Inspect(obj, field, inspectablesOnly, readWrite);
		
		// if(topLevel)
		// 	foreach (var field in _propertyFields) 
		// 		field.transform.SetSiblingIndex(_propertyFields.IndexOf(field));
	
		RefreshValues();
	}
	
	public void Inspect(object obj, FieldInfo field, bool inspectablesOnly = false, bool readWrite = false)
	{
		var inspectable = field.GetCustomAttribute<InspectableAttribute>();
		if (inspectable == null && inspectablesOnly) return;
		var type = field.FieldType;
		
		if (type == typeof(float))
		{
			if (readWrite)
			{
				if (inspectable is InspectableRangedFloatAttribute ranged)
					AddField(field.Name.SplitCamelCase(), () => (float) field.GetValue(obj), f => field.SetValue(obj, f),
						ranged.Min, ranged.Max);
				else
					AddField(field.Name.SplitCamelCase(), () => (float) field.GetValue(obj), f => field.SetValue(obj, f));
			} else AddProperty(field.Name.SplitCamelCase(), () => ((float) field.GetValue(obj)).ToString("0.##"));
		} 
		else if (type == typeof(int))
		{
			if (readWrite)
			{
				if (inspectable is InspectableRangedIntAttribute ranged)
					AddField(field.Name.SplitCamelCase(), () => (int) field.GetValue(obj), f => field.SetValue(obj, f),
						ranged.Min, ranged.Max);
				else
					AddField(field.Name.SplitCamelCase(), () => (int) field.GetValue(obj), f => field.SetValue(obj, f));
			} else AddProperty(field.Name.SplitCamelCase(), () => ((int) field.GetValue(obj)).ToString());
		}
		else if (type.IsEnum)
		{
			if (readWrite)
				AddField(field.Name.SplitCamelCase(), () => (int) field.GetValue(obj), i => field.SetValue(obj, i),
					Enum.GetNames(field.FieldType));
			else
				AddProperty(field.Name.SplitCamelCase(), () => Enum.GetName(type, field.GetValue(obj)).SplitCamelCase());
		}
		else if (type == typeof(string))
		{
			if (readWrite)
				AddField(field.Name.SplitCamelCase(), () => (string) field.GetValue(obj), i => field.SetValue(obj, i));
			else
				AddProperty(field.Name.SplitCamelCase(), () => (string) field.GetValue(obj));
		}
		//else if (field.FieldType == typeof(Color)) Inspect(field.Name, () => (Color) field.GetValue(obj), c => field.SetValue(obj, c));
		else if (field.FieldType == typeof(bool)) AddField(field.Name, () => (bool) field.GetValue(obj), b => field.SetValue(obj, b));
		else if (type.GetCustomAttribute<InspectableAttribute>() != null)
		{
			// 	if(!_propertyFields.Any() || !_propertyFields.Last().gameObject.name.ToUpper().Contains("DIVIDER"))
			// 		_propertyFields.Add(Divider.Instantiate<Prototype>());
			var list = AddList(field.Name);
			list.Inspect(field.GetValue(obj), inspectablesOnly, readWrite, false);
		}
		else 
			Debug.Log($"Field \"{field.Name}\" has unknown type {field.FieldType.Name}. No inspector was generated.");
	}

	public void RefreshValues()
	{
		RefreshPropertyValues?.Invoke();
	}
}
