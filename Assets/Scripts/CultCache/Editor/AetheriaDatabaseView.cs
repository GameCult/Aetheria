using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AetheriaDatabaseView : DatabaseListView
{
    // Singleton to avoid multiple instances of window. 
    private static DatabaseListView _instance;
    private string tableName;
    public static DatabaseListView Instance => _instance ? _instance : GetWindow<AetheriaDatabaseView>();
    [MenuItem("Window/Aetheria/Database Tools")]
    static void Init() => Instance.Show();
    private void Awake()
    {
        _instance = this;
    }

    protected override string DatabaseName => "Aetheria";

    protected override string FilePath => new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData").FullName;

    protected override DatabaseEntryGroup[] Groupers => new DatabaseEntryGroup[]
    {
        new DatabaseEntryGroup<SimpleCommodityData,SimpleCommodityCategory>(
            data => data.Category, 
            category => Enum.GetName(typeof(SimpleCommodityCategory), category),
            (data, category) => data.Category = category ),
        new DatabaseEntryGroup<CompoundCommodityData, CompoundCommodityCategory>(
            data => data.Category, 
            category => Enum.GetName(typeof(CompoundCommodityCategory), category),
            (data, category) => data.Category = category ),
        new DatabaseEntryGroup<GearData, HardpointType>(
            data => data.HardpointType,
            type => Enum.GetName(typeof(HardpointType), type),
            (data, type) => data.Hardpoint = type),
        new DatabaseEntryGroup<GearData, Guid>(
            data => data.Manufacturer,
            factionID => factionID == Guid.Empty ? "Default" : cultCache.Get<Faction>(factionID).ShortName,
            (data, faction) => data.Manufacturer = faction),
        new DatabaseEntryGroup<HullData, HullType>(
            data => data.HullType,
            type => Enum.GetName(typeof(HullType), type),
            (data, type) => data.HullType = type)
    };
}