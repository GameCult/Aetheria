using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

[JsonObject]
public class WwiseMetadataFile
{
    public WwiseMetaSoundBanksInfo SoundBanksInfo;
}

[JsonObject]
public class WwiseMetaSoundBanksInfo
{
    public WwiseMetaSoundBank[] SoundBanks;

    private Dictionary<uint, WwiseMetaSoundBank> _soundBanks;
    public WwiseMetaSoundBank GetSoundBank(uint id)
    {
        if (_soundBanks == null) _soundBanks = SoundBanks.ToDictionary(sb => sb.Id);
        return _soundBanks[id];
    }

    private Dictionary<string, WwiseMetaSoundBank> _soundBankNames;
    public WwiseMetaSoundBank GetSoundBank(string name)
    {
        if (_soundBankNames == null) _soundBankNames = SoundBanks.ToDictionary(sb => sb.ShortName);
        return _soundBankNames[name];
    }
}

[JsonObject]
public class WwiseMetaSoundBank
{
    public uint Id;
    public string ShortName;
    public string ObjectPath;
    public WwiseMetaObject[] IncludedEvents;
    public WwiseMetaObject[] GameParameters;

    public WwiseMetaObject GetEvent(WeaponAudioEvent weaponAudioEvent)
    {
        return weaponAudioEvent switch
        {
            WeaponAudioEvent.Fire => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_fire")),
            WeaponAudioEvent.Hit => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_hit")),
            WeaponAudioEvent.Miss => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_miss")),
            _ => throw new ArgumentOutOfRangeException(nameof(weaponAudioEvent), weaponAudioEvent, null)
        };
    }

    public WwiseMetaObject GetEvent(ChargedWeaponAudioEvent chargedWeaponAudioEvent)
    {
        return chargedWeaponAudioEvent switch
        {
            ChargedWeaponAudioEvent.Start => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_charge_play")),
            ChargedWeaponAudioEvent.Stop => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_charge_stop")),
            ChargedWeaponAudioEvent.Fail => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_fail")),
            _ => throw new ArgumentOutOfRangeException(nameof(chargedWeaponAudioEvent), chargedWeaponAudioEvent, null)
        };
    }

    public WwiseMetaObject GetEvent(LoopingAudioEvent loopingAudioEvent)
    {
        return loopingAudioEvent switch
        {
            LoopingAudioEvent.Play => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_play")),
            LoopingAudioEvent.Stop => IncludedEvents?.FirstOrDefault(e => e.Name.EndsWith("_stop")),
            _ => throw new ArgumentOutOfRangeException(nameof(loopingAudioEvent), loopingAudioEvent, null)
        };
    }

    public WwiseMetaObject GetParameter(SpecialAudioParameter audioParameter)
    {
        return audioParameter switch
        {
            SpecialAudioParameter.ShipVelocity => GameParameters?.FirstOrDefault(p => p.Name == "ship_velocity"),
            SpecialAudioParameter.ChargeLevel => GameParameters?.FirstOrDefault(p => p.Name == "charge_level"),
            SpecialAudioParameter.TargetLock => GameParameters?.FirstOrDefault(p => p.Name == "target_locking"),
            SpecialAudioParameter.Intensity => GameParameters?.FirstOrDefault(p => p.Name == "sfx_shared_intensity"),
            _ => throw new ArgumentOutOfRangeException(nameof(audioParameter), audioParameter, null)
        };
    }
}

[JsonObject]
public class WwiseMetaObject
{
    public uint Id;
    public string Name;
    public string ObjectPath;
}