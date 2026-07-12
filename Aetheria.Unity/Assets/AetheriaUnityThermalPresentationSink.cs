using GameCult.Eve.UnityScene;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class AetheriaUnityThermalPresentationSink : MonoBehaviour,
    IEveUnityThermalPresentationSink, IEveUnityThermalPresentationAssetSink
{
    private Volume _heatstroke;
    private Volume _severeHeatstroke;
    private Volume _hypothermia;
    private Volume _severeHypothermia;
    private Volume _death;

    public EveUnityThermalPresentationFrame LastFrame { get; private set; }

    private void Awake()
    {
        _heatstroke = CreateVolume("Heatstroke");
        _severeHeatstroke = CreateVolume("Severe Heatstroke");
        _hypothermia = CreateVolume("Hypothermia");
        _severeHypothermia = CreateVolume("Severe Hypothermia");
        _death = CreateVolume("Death");
    }

    public void ConfigureThermalPresentationAssets(EveUnityThermalPresentationAssets assets)
    {
        _heatstroke.sharedProfile = assets.Heatstroke as VolumeProfile;
        _severeHeatstroke.sharedProfile = assets.SevereHeatstroke as VolumeProfile;
        _hypothermia.sharedProfile = assets.Hypothermia as VolumeProfile;
        _severeHypothermia.sharedProfile = assets.SevereHypothermia as VolumeProfile;
        _death.sharedProfile = assets.Death as VolumeProfile;
    }

    public void ApplyThermalPresentation(EveUnityThermalPresentationFrame frame)
    {
        LastFrame = frame;
        _heatstroke.weight = frame.HeatstrokeWeight;
        _severeHeatstroke.weight = frame.SevereHeatstrokeWeight;
        _hypothermia.weight = frame.HypothermiaWeight;
        _severeHypothermia.weight = frame.SevereHypothermiaWeight;
        _death.weight = frame.DeathWeight;
    }

    private Volume CreateVolume(string label)
    {
        var root = new GameObject(label);
        root.transform.SetParent(transform, false);
        var volume = root.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100;
        volume.weight = 0;
        return volume;
    }
}
