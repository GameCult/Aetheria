using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using TMPro;
using UniRx;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using Random = UnityEngine.Random;
using float2 = Unity.Mathematics.float2;

public class SectorMap : MonoBehaviour
{
    public Camera InfluenceCamera;
    public Prototype InfluenceRendererPrototype;
    public MeshRenderer SectorRenderer;
    public Prototype ZonePrototype;
    public Prototype LinkPrototype;
    public Prototype IconPrototype;
    public Prototype IconBackgroundPrototype;
    public Material ZonePrimaryMaterial;
    public Material ZoneSecondaryMaterial;
    public Material ZoneLinkMaterial;
    public float MegaPrimaryBoost = .75f;
    public float MegaSecondaryBoost = 2f;
    public float MegaLinkBoost = 2f;
    public Texture2D EntranceIcon;
    public Texture2D ExitIcon;
    public Texture2D BossIcon;
    public Texture2D HomeIcon;
    public Prototype LegendPrototype;
    public float IconDistance = 1;
    public float IconBackgroundSize = 3;
    public float LabelDistance = .4f;
    public float LinkWidth;
    public float CriticalLinkWidth;
    public AnimationCurve IconScaleAnimation;
    public Color PlayerLocationLabelColor;
    public float PlayerLocationIconSize = 1.25f;

    public Subject<GalaxyZone> ZoneClicked = new Subject<GalaxyZone>();

    private GalaxyZone _currentPlayerLocation;
    private HashSet<GalaxyZone> _revealedZones = new HashSet<GalaxyZone>();
    private HashSet<(GalaxyZone, GalaxyZone)> _revealedLinks = new HashSet<(GalaxyZone, GalaxyZone)>();

    private Dictionary<Faction, (MeshRenderer influenceRenderer, RenderTexture influence, Material primaryMaterial, Material secondaryMaterial, Material linkMaterial)> _factionMaterials =
        new Dictionary<Faction, (MeshRenderer influenceRenderer, RenderTexture influence, Material primaryMaterial, Material secondaryMaterial, Material linkMaterial)>();

    private Dictionary<GalaxyZone, SectorZoneUI> _zoneInstances = new Dictionary<GalaxyZone, SectorZoneUI>();
    private Queue<IEnumerable<GalaxyZone>> _queuedZoneReveals = new Queue<IEnumerable<GalaxyZone>>();

    public void MarkPlayerLocation(GalaxyZone zone)
    {
        if (_currentPlayerLocation != null)
        {
            _zoneInstances[_currentPlayerLocation].Label.color = Color.white;
            _zoneInstances[_currentPlayerLocation].Label.fontStyle = FontStyles.Normal;
            _zoneInstances[_currentPlayerLocation].IconContainer.localScale = Vector3.one;
        }

        _currentPlayerLocation = zone;
        if(_zoneInstances.ContainsKey(_currentPlayerLocation))
        {
            MarkPlayerLocation(_zoneInstances[_currentPlayerLocation]);
        }
    }

    private void MarkPlayerLocation(SectorZoneUI zoneUI)
    {
        zoneUI.Label.color = PlayerLocationLabelColor;
        zoneUI.Label.fontStyle = FontStyles.Bold;
        zoneUI.IconContainer.localScale = new Vector3(PlayerLocationIconSize, PlayerLocationIconSize, 1);
    }

    public void QueueZoneReveal(IEnumerable<GalaxyZone> zones)
    {
        _queuedZoneReveals.Enqueue(zones);
    }

    public void StartReveal(float linkDuration, float iconDuration)
    {
        if (_queuedZoneReveals.Count > 0)
        {
            StartCoroutine(RevealZone(_queuedZoneReveals.Dequeue(), linkDuration, iconDuration));
        }
    }

    public IEnumerator RevealZone(IEnumerable<GalaxyZone> zones, float linkDuration, float iconDuration)
    {
        var linksToReveal = new List<(float2 start, float2 end, Transform linkInstance, bool critical)>();
        var zoneTransforms = new List<Transform>();
        var zoneInstanceScale = ZonePrototype.transform.localScale;

        foreach (var zone in zones)
        {
            if (!_revealedZones.Contains(zone))
            {
                var zoneInstance = ZonePrototype.Instantiate<SectorZoneUI>();
                zoneInstance.Clickable.OnClick += (clickableCollider, data, ray, hit) => ZoneClicked.OnNext(zone);
                if(zone == _currentPlayerLocation)
                    MarkPlayerLocation(zoneInstance);
                _zoneInstances[zone] = zoneInstance;
                var zoneInstanceTransform = zoneInstance.transform;
                zoneTransforms.Add(zoneInstanceTransform);
                zoneInstanceTransform.localPosition = new Vector3(zone.Position.x, zone.Position.y);
                if(zone.Owner != null)
                {
                    zoneInstance.Primary.sharedMaterial = _factionMaterials[zone.Owner].primaryMaterial;
                    zoneInstance.Secondary.sharedMaterial = _factionMaterials[zone.Owner].secondaryMaterial;
                    if(zone == ActionGameManager.CurrentGalaxy.HomeZones[zone.Owner])
                        zoneInstance.Secondary.transform.localScale = Vector3.one * 4;
                }
                else
                {
                    zoneInstance.Secondary.gameObject.SetActive(false);
                }

                var isOnCriticalPath = ActionGameManager.CurrentGalaxy.ExitPath?.Contains(zone) ?? false;
                foreach (var adjacentZone in zone.AdjacentZones)
                {
                    // If the adjacent zone is in the set already revealed, then show the link
                    if (_revealedZones.Contains(adjacentZone) && 
                        !(_revealedLinks.Contains((zone, adjacentZone)) || 
                          _revealedLinks.Contains((adjacentZone, zone))))
                    {
                        var link = LinkPrototype.Instantiate<Transform>();
                        var critical = isOnCriticalPath && (ActionGameManager.CurrentGalaxy.ExitPath?.Contains(adjacentZone) ?? false);
                        linksToReveal.Add((zone.Position, adjacentZone.Position, link, critical));
                        if (zone.Owner != null && zone.Owner == adjacentZone.Owner)
                            link.GetComponent<MeshRenderer>().sharedMaterial = _factionMaterials[zone.Owner].linkMaterial;

                        var dir = normalize(zone.Position - adjacentZone.Position);
                        link.rotation = Quaternion.Euler(0,0,atan2(dir.y,dir.x) * Mathf.Rad2Deg);
                        
                        placeLink(zone.Position, adjacentZone.Position, link, 0, critical);
                    }
                }
                _revealedZones.Add(zone);

                // Determine center of mass for adjacent zone links, used for placement of label and icons
                var linkDirection = normalize(zone.AdjacentZones
                    .Aggregate(float2.zero, (v, adjacentZone) => v + normalize(adjacentZone.Position - zone.Position)));
                
                var zoneText = zoneInstance.Label;
                zoneText.text = zone.Name;
                var zoneTextTransform = zoneText.GetComponent<RectTransform>();
                zoneTextTransform.pivot = new Vector2(sign(linkDirection.x)/2+.5f,sign(linkDirection.y)/2+.5f);
                zoneTextTransform.localPosition = new Vector3(-linkDirection.x * LabelDistance, -linkDirection.y * LabelDistance, -1);

                if (zone == ActionGameManager.CurrentGalaxy.Entrance)
                {
                    var iconInstance = IconPrototype.Instantiate<MeshRenderer>();
                    iconInstance.material.mainTexture = EntranceIcon;
                    var iconTransform = iconInstance.transform;
                    iconTransform.SetParent(zoneInstanceTransform);
                    iconTransform.localScale = Vector3.one;
                    iconTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance);
                }

                if (zone == ActionGameManager.CurrentGalaxy.Exit)
                {
                    var iconInstance = IconPrototype.Instantiate<MeshRenderer>();
                    iconInstance.material.mainTexture = ExitIcon;
                    var iconTransform = iconInstance.transform;
                    iconTransform.SetParent(zoneInstanceTransform);
                    iconTransform.localScale = Vector3.one;
                    iconTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance);
                }

                var homeMega = ActionGameManager.CurrentGalaxy.HomeZones.Keys
                    .FirstOrDefault(m => ActionGameManager.CurrentGalaxy.HomeZones[m] == zone);
                if (homeMega != null)
                {
                    var backgroundInstance = IconBackgroundPrototype.Instantiate<MeshRenderer>();
                    var iconInstance = IconPrototype.Instantiate<MeshRenderer>();
                    
                    backgroundInstance.material.SetColor("_Color", homeMega.SecondaryColor.ToColor());
                    iconInstance.material.mainTexture = HomeIcon;
                    iconInstance.material.SetColor("_Color", homeMega.PrimaryColor.ToColor());
                    
                    var backgroundTransform = backgroundInstance.transform;
                    var iconTransform = iconInstance.transform;
                    
                    backgroundTransform.SetParent(zoneInstanceTransform);
                    iconTransform.SetParent(zoneInstanceTransform);
                    
                    backgroundTransform.localScale = Vector3.one * IconBackgroundSize;
                    iconTransform.localScale = Vector3.one;
                    
                    backgroundTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance, .5f);
                    iconTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance);
                }

                var bossMega = ActionGameManager.CurrentGalaxy.BossZones.Keys
                    .FirstOrDefault(m => ActionGameManager.CurrentGalaxy.BossZones[m] == zone);
                if (bossMega != null)
                {
                    var backgroundInstance = IconBackgroundPrototype.Instantiate<MeshRenderer>();
                    var iconInstance = IconPrototype.Instantiate<MeshRenderer>();
                    
                    backgroundInstance.material.SetColor("_Color", bossMega.SecondaryColor.ToColor());
                    iconInstance.material.mainTexture = BossIcon;
                    iconInstance.material.SetColor("_Color", bossMega.PrimaryColor.ToColor());
                    
                    var backgroundTransform = backgroundInstance.transform;
                    var iconTransform = iconInstance.transform;
                    
                    backgroundTransform.SetParent(zoneInstanceTransform);
                    iconTransform.SetParent(zoneInstanceTransform);
                    
                    backgroundTransform.localScale = Vector3.one * IconBackgroundSize;
                    iconTransform.localScale = Vector3.one;
                    
                    if (homeMega != null)
                    {
                        backgroundTransform.localPosition = new Vector3(linkDirection.x * IconDistance, linkDirection.y * IconDistance, .5f);
                        iconTransform.localPosition = new Vector3(linkDirection.x * IconDistance, linkDirection.y * IconDistance);
                    }
                    else
                    {
                        backgroundTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance, .5f);
                        iconTransform.localPosition = new Vector3(-linkDirection.x * IconDistance, -linkDirection.y * IconDistance);
                    }
                }
            }
        }
        foreach (var tr in zoneTransforms) tr.gameObject.SetActive(false);

        // Local function for animating the placement of a link
        void placeLink(float2 start, float2 end, Transform link, float t, bool critical)
        {
            var pos = lerp(start, end, t / 2);
            link.localPosition = new Vector3(pos.x, pos.y);

            var localScale = link.localScale;
            localScale = new Vector3(length(start - end)*t, critical ? CriticalLinkWidth : LinkWidth, 1);
            link.localScale = localScale;
        }

        var startTime = Time.time;
        while (Time.time - startTime < linkDuration)
        {
            var t = (Time.time - startTime) / linkDuration;
            foreach (var (start, end, link, critical) in linksToReveal) placeLink(end, start, link, t, critical);

            yield return null;
        }
        foreach (var (start, end, link, critical) in linksToReveal) placeLink(start, end, link, 1, critical);

        foreach (var tr in zoneTransforms) tr.gameObject.SetActive(true);
        startTime = Time.time;
        while (Time.time - startTime < iconDuration)
        {
            var t = (Time.time - startTime) / iconDuration;
            foreach (var tr in zoneTransforms) tr.localScale = zoneInstanceScale * IconScaleAnimation.Evaluate(t);
            RenderInfluence();

            yield return null;
        }
        foreach (var tr in zoneTransforms) tr.localScale = zoneInstanceScale;

        StartReveal(linkDuration, iconDuration);
    }

    public void Start()
    {
        if (ActionGameManager.CurrentGalaxy == null)
        {
            gameObject.SetActive(false);
            return;
        }
        foreach (var mega in ActionGameManager.CurrentGalaxy.Factions)
        {
            var influenceTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.RHalf);
            var influenceRenderer = InfluenceRendererPrototype.Instantiate<MeshRenderer>();
            
            influenceRenderer.material.mainTexture = influenceTexture;
            influenceRenderer.material.SetColor("_Color1", mega.PrimaryColor.ToColor());
            influenceRenderer.material.SetColor("_Color2", mega.SecondaryColor.ToColor());
            influenceRenderer.material.SetFloat("_FillTilt", Random.value * PI);
            
            var primary = Instantiate(ZonePrimaryMaterial);
            primary.SetColor("_Color", (mega.PrimaryColor * MegaPrimaryBoost).ToColor());
            
            var secondary = Instantiate(ZoneSecondaryMaterial);
            secondary.SetColor("_Color", (mega.SecondaryColor * MegaSecondaryBoost).ToColor());
            
            var link = Instantiate(ZoneLinkMaterial);
            link.SetColor("_Color", (mega.PrimaryColor * MegaLinkBoost).ToColor());
            
            _factionMaterials.Add(mega, (influenceRenderer, influenceTexture, primary, secondary, link));
            
            var legendElement = LegendPrototype.Instantiate<LegendElement>();
            legendElement.Primary.color = mega.PrimaryColor.ToColor();
            legendElement.Secondary.color = mega.SecondaryColor.ToColor();
            legendElement.Label.text = mega.ShortName;
        }

        SectorRenderer.material.SetFloat("CloudAmplitude", ActionGameManager.CurrentGalaxy.Background.CloudAmplitude);
        SectorRenderer.material.SetFloat("CloudExponent", ActionGameManager.CurrentGalaxy.Background.CloudExponent);
        SectorRenderer.material.SetFloat("NoisePosition", ActionGameManager.CurrentGalaxy.Background.NoisePosition);
        SectorRenderer.material.SetFloat("NoiseAmplitude", ActionGameManager.CurrentGalaxy.Background.NoiseAmplitude);
        SectorRenderer.material.SetFloat("NoiseOffset", ActionGameManager.CurrentGalaxy.Background.NoiseOffset);
        SectorRenderer.material.SetFloat("NoiseGain", ActionGameManager.CurrentGalaxy.Background.NoiseGain);
        SectorRenderer.material.SetFloat("NoiseLacunarity", ActionGameManager.CurrentGalaxy.Background.NoiseLacunarity);
        SectorRenderer.material.SetFloat("NoiseFrequency", ActionGameManager.CurrentGalaxy.Background.NoiseFrequency);
    }

    private void RenderInfluence()
    {
        // Render Influence Textures
        foreach (var mega in ActionGameManager.CurrentGalaxy.Factions)
        {
            foreach (var zone in ActionGameManager.CurrentGalaxy.Zones)
            {
                if(_zoneInstances.ContainsKey(zone))
                {
                    var instance = _zoneInstances[zone];
                    var influence = 0f;
                    if (zone.Factions.Length > 0)
                    {
                        if (zone.Factions.Contains(mega))
                        {
                            influence = 10;
                            if (zone.Owner != mega)
                                influence *= .5f;
                        }
                        else influence = -10;
                    }

                    instance.Influence.material.SetFloat("_Depth", influence);
                }
            }

            InfluenceCamera.targetTexture = _factionMaterials[mega].influence;
            InfluenceCamera.Render();
        }
    }
}
