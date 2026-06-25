using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using TMPro;
using UniRx;
using UnityEngine;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using static Unity.Mathematics.math;
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

    public Subject<int> ZoneClicked = new Subject<int>();

    private int _currentPlayerLocation = -1;
    private readonly HashSet<int> _revealedZones = new HashSet<int>();
    private readonly HashSet<(int, int)> _revealedLinks = new HashSet<(int, int)>();
    private readonly Dictionary<int, (MeshRenderer influenceRenderer, RenderTexture influence, Material primaryMaterial, Material secondaryMaterial, Material linkMaterial)> _factionMaterials =
        new Dictionary<int, (MeshRenderer influenceRenderer, RenderTexture influence, Material primaryMaterial, Material secondaryMaterial, Material linkMaterial)>();
    private readonly Dictionary<int, SectorZoneUI> _zoneInstances = new Dictionary<int, SectorZoneUI>();
    private readonly Queue<IEnumerable<int>> _queuedZoneReveals = new Queue<IEnumerable<int>>();
    private readonly Dictionary<int, AetheriaRuntimeSectorMapZone> _zonesByIndex =
        new Dictionary<int, AetheriaRuntimeSectorMapZone>();
    private AetheriaRuntimeSectorMapDocument _sectorMap;
    private AetheriaClient _client;
    private string _clientStatePath = "";
    private bool _sectorMapLoaded;

    public bool TryMarkPlayerLocation(int zoneIndex)
    {
        EnsureSectorMapLoaded();
        if (!_zonesByIndex.ContainsKey(zoneIndex))
            return false;

        MarkPlayerLocation(zoneIndex);
        return true;
    }

    private void MarkPlayerLocation(int zoneIndex)
    {
        if (_currentPlayerLocation >= 0 && _zoneInstances.TryGetValue(_currentPlayerLocation, out var previous))
        {
            previous.Label.color = Color.white;
            previous.Label.fontStyle = FontStyles.Normal;
            previous.IconContainer.localScale = Vector3.one;
        }

        _currentPlayerLocation = zoneIndex;
        if (_zoneInstances.TryGetValue(_currentPlayerLocation, out var zoneUI))
            MarkPlayerLocation(zoneUI);
    }

    private void MarkPlayerLocation(SectorZoneUI zoneUI)
    {
        zoneUI.Label.color = PlayerLocationLabelColor;
        zoneUI.Label.fontStyle = FontStyles.Bold;
        zoneUI.IconContainer.localScale = new Vector3(PlayerLocationIconSize, PlayerLocationIconSize, 1);
    }

    public void QueueZoneReveal(IEnumerable<int> zoneIndices)
    {
        _queuedZoneReveals.Enqueue(zoneIndices ?? Array.Empty<int>());
    }

    public void StartReveal(float linkDuration, float iconDuration)
    {
        EnsureSectorMapLoaded();
        if (_queuedZoneReveals.Count > 0)
            StartCoroutine(RevealZone(_queuedZoneReveals.Dequeue(), linkDuration, iconDuration));
    }

    public IEnumerator RevealZone(IEnumerable<int> zoneIndices, float linkDuration, float iconDuration)
    {
        var linksToReveal = new List<(float2 start, float2 end, Transform linkInstance, bool critical)>();
        var zoneTransforms = new List<Transform>();
        var zoneInstanceScale = ZonePrototype.transform.localScale;

        foreach (var zoneIndex in zoneIndices ?? Array.Empty<int>())
        {
            if (!_zonesByIndex.TryGetValue(zoneIndex, out var zone) || _revealedZones.Contains(zoneIndex))
                continue;

            var zoneInstance = ZonePrototype.Instantiate<SectorZoneUI>();
            zoneInstance.Clickable.OnClick += (_, _, _, _) => ZoneClicked.OnNext(zone.ZoneIndex);
            if(zone.ZoneIndex == _currentPlayerLocation)
                MarkPlayerLocation(zoneInstance);
            _zoneInstances[zone.ZoneIndex] = zoneInstance;
            var zoneInstanceTransform = zoneInstance.transform;
            zoneTransforms.Add(zoneInstanceTransform);
            zoneInstanceTransform.localPosition = new Vector3((float)zone.X, (float)zone.Y);

            if (zone.OwnerFactionIndex >= 0 && _factionMaterials.TryGetValue(zone.OwnerFactionIndex, out var ownerMaterials))
            {
                zoneInstance.Primary.sharedMaterial = ownerMaterials.primaryMaterial;
                zoneInstance.Secondary.sharedMaterial = ownerMaterials.secondaryMaterial;
            }
            else
            {
                zoneInstance.Secondary.gameObject.SetActive(false);
            }

            foreach (var link in LinksForZone(zone.ZoneIndex))
            {
                var adjacentIndex = link.FromZoneIndex == zone.ZoneIndex ? link.ToZoneIndex : link.FromZoneIndex;
                if (!_zonesByIndex.TryGetValue(adjacentIndex, out var adjacentZone) ||
                    !_revealedZones.Contains(adjacentIndex) ||
                    _revealedLinks.Contains((zone.ZoneIndex, adjacentIndex)) ||
                    _revealedLinks.Contains((adjacentIndex, zone.ZoneIndex)))
                {
                    continue;
                }

                var linkInstance = LinkPrototype.Instantiate<Transform>();
                var critical = zone.Entrance || zone.Exit || adjacentZone.Entrance || adjacentZone.Exit;
                var start = Position(zone);
                var end = Position(adjacentZone);
                linksToReveal.Add((start, end, linkInstance, critical));
                if (zone.OwnerFactionIndex >= 0 &&
                    zone.OwnerFactionIndex == adjacentZone.OwnerFactionIndex &&
                    _factionMaterials.TryGetValue(zone.OwnerFactionIndex, out var factionMaterials))
                {
                    linkInstance.GetComponent<MeshRenderer>().sharedMaterial = factionMaterials.linkMaterial;
                }

                var dir = normalize(start - end);
                linkInstance.rotation = Quaternion.Euler(0, 0, atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                placeLink(start, end, linkInstance, 0, critical);
                _revealedLinks.Add((zone.ZoneIndex, adjacentIndex));
            }

            _revealedZones.Add(zone.ZoneIndex);
            var linkDirection = ResolveLabelDirection(zone);
            var zoneText = zoneInstance.Label;
            zoneText.text = zone.Name;
            var zoneTextTransform = zoneText.GetComponent<RectTransform>();
            zoneTextTransform.pivot = new Vector2(sign(linkDirection.x) / 2 + .5f, sign(linkDirection.y) / 2 + .5f);
            zoneTextTransform.localPosition = new Vector3(-linkDirection.x * LabelDistance, -linkDirection.y * LabelDistance, -1);

            if (zone.Entrance)
                AddZoneIcon(zoneInstanceTransform, EntranceIcon, linkDirection, -1);

            if (zone.Exit)
                AddZoneIcon(zoneInstanceTransform, ExitIcon, linkDirection, zone.Entrance ? 1 : -1);
        }
        foreach (var tr in zoneTransforms) tr.gameObject.SetActive(false);

        void placeLink(float2 start, float2 end, Transform link, float t, bool critical)
        {
            var pos = lerp(start, end, t / 2);
            link.localPosition = new Vector3(pos.x, pos.y);
            link.localScale = new Vector3(length(start - end) * t, critical ? CriticalLinkWidth : LinkWidth, 1);
        }

        var startTime = Time.time;
        while (Time.time - startTime < linkDuration)
        {
            var t = (Time.time - startTime) / linkDuration;
            foreach (var (start, end, link, critical) in linksToReveal)
                placeLink(end, start, link, t, critical);

            yield return null;
        }
        foreach (var (start, end, link, critical) in linksToReveal)
            placeLink(start, end, link, 1, critical);

        foreach (var tr in zoneTransforms)
            tr.gameObject.SetActive(true);

        startTime = Time.time;
        while (Time.time - startTime < iconDuration)
        {
            var t = (Time.time - startTime) / iconDuration;
            foreach (var tr in zoneTransforms)
                tr.localScale = zoneInstanceScale * IconScaleAnimation.Evaluate(t);
            RenderInfluence();

            yield return null;
        }
        foreach (var tr in zoneTransforms)
            tr.localScale = zoneInstanceScale;

        StartReveal(linkDuration, iconDuration);
    }

    public void Start()
    {
        EnsureSectorMapLoaded();
    }

    private void EnsureSectorMapLoaded()
    {
        _sectorMap = ResolveClient()
            .SectorMapAsync()
            .GetAwaiter()
            .GetResult();

        if (_sectorMapLoaded)
            return;

        _sectorMapLoaded = true;

        if (_sectorMap?.Zones == null || _sectorMap.Zones.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        _zonesByIndex.Clear();
        foreach (var zone in _sectorMap.Zones)
            _zonesByIndex[zone.ZoneIndex] = zone;

        foreach (var factionIndex in ResolveFactionIndices())
            AddFactionMaterials(factionIndex);

        foreach (var zoneIndex in _sectorMap.DiscoveredZoneIndices)
            QueueZoneReveal([zoneIndex]);

        if (_queuedZoneReveals.Count == 0)
            QueueZoneReveal(_sectorMap.Zones.Select(zone => zone.ZoneIndex));

        if (_sectorMap.CurrentZoneIndex >= 0)
            TryMarkPlayerLocation(_sectorMap.CurrentZoneIndex);
    }

    private void OnDestroy()
    {
        DisposeClient();
    }

    private void AddFactionMaterials(int factionIndex)
    {
        if (_factionMaterials.ContainsKey(factionIndex))
            return;

        var primaryColor = ResolveFactionColor(factionIndex, secondary: false);
        var secondaryColor = ResolveFactionColor(factionIndex, secondary: true);
        var influenceTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.RHalf);
        var influenceRenderer = InfluenceRendererPrototype.Instantiate<MeshRenderer>();
        influenceRenderer.material.mainTexture = influenceTexture;
        influenceRenderer.material.SetColor("_Color1", primaryColor);
        influenceRenderer.material.SetColor("_Color2", secondaryColor);
        influenceRenderer.material.SetFloat("_FillTilt", Random.value * PI);

        var primary = Instantiate(ZonePrimaryMaterial);
        primary.SetColor("_Color", primaryColor * MegaPrimaryBoost);

        var secondary = Instantiate(ZoneSecondaryMaterial);
        secondary.SetColor("_Color", secondaryColor * MegaSecondaryBoost);

        var link = Instantiate(ZoneLinkMaterial);
        link.SetColor("_Color", primaryColor * MegaLinkBoost);

        _factionMaterials.Add(factionIndex, (influenceRenderer, influenceTexture, primary, secondary, link));

        var legendElement = LegendPrototype.Instantiate<LegendElement>();
        legendElement.Primary.color = primaryColor;
        legendElement.Secondary.color = secondaryColor;
        legendElement.Label.text = factionIndex < 0 ? "None" : $"F{factionIndex}";
    }

    private IEnumerable<int> ResolveFactionIndices()
    {
        return (_sectorMap?.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
            .SelectMany(zone => (zone.FactionIndices ?? Array.Empty<int>()).Append(zone.OwnerFactionIndex))
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index);
    }

    private Color ResolveFactionColor(int factionIndex, bool secondary)
    {
        var hue = frac((factionIndex + (secondary ? 0.37f : 0f)) * 0.173f);
        return Color.HSVToRGB(hue, secondary ? 0.48f : 0.62f, secondary ? 0.72f : 0.88f);
    }

    private IEnumerable<AetheriaRuntimeSectorMapLink> LinksForZone(int zoneIndex)
    {
        return (_sectorMap?.Links ?? Array.Empty<AetheriaRuntimeSectorMapLink>())
            .Where(link => link.FromZoneIndex == zoneIndex || link.ToZoneIndex == zoneIndex);
    }

    private float2 ResolveLabelDirection(AetheriaRuntimeSectorMapZone zone)
    {
        var direction = float2.zero;
        foreach (var adjacentIndex in zone.AdjacentZoneIndices ?? Array.Empty<int>())
        {
            if (_zonesByIndex.TryGetValue(adjacentIndex, out var adjacent))
                direction += normalizesafe(Position(adjacent) - Position(zone));
        }

        return lengthsq(direction) <= 0 ? new float2(0, 1) : normalize(direction);
    }

    private void AddZoneIcon(
        Transform zoneInstanceTransform,
        Texture2D icon,
        float2 linkDirection,
        int side)
    {
        var iconInstance = IconPrototype.Instantiate<MeshRenderer>();
        iconInstance.material.mainTexture = icon;
        var iconTransform = iconInstance.transform;
        iconTransform.SetParent(zoneInstanceTransform);
        iconTransform.localScale = Vector3.one;
        iconTransform.localPosition = new Vector3(
            side * -linkDirection.x * IconDistance,
            side * -linkDirection.y * IconDistance);
    }

    private void RenderInfluence()
    {
        foreach (var pair in _factionMaterials)
        {
            foreach (var zone in _sectorMap?.Zones ?? Array.Empty<AetheriaRuntimeSectorMapZone>())
            {
                if (!_zoneInstances.TryGetValue(zone.ZoneIndex, out var instance))
                    continue;

                var influence = 0f;
                if (zone.FactionIndices?.Count > 0)
                {
                    if (zone.FactionIndices.Contains(pair.Key))
                    {
                        influence = 10;
                        if (zone.OwnerFactionIndex != pair.Key)
                            influence *= .5f;
                    }
                    else influence = -10;
                }

                instance.Influence.material.SetFloat("_Depth", influence);
            }

            InfluenceCamera.targetTexture = pair.Value.influence;
            InfluenceCamera.Render();
        }
    }

    private AetheriaClient ResolveClient()
    {
        var gameDataDirectory = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "GameData"));
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(gameDataDirectory);
        if (_client != null && string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
            return _client;

        DisposeClient();
        _client = AetheriaClient
            .OpenLocalAsync(
                gameDataDirectory,
                "unity-sector-map",
                "local",
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        _clientStatePath = stateBoot.StateFilePath;
        return _client;
    }

    private void DisposeClient()
    {
        _client?.Dispose();
        _client = null;
        _clientStatePath = "";
        _sectorMapLoaded = false;
    }

    private static float2 Position(AetheriaRuntimeSectorMapZone zone)
    {
        return new float2((float)zone.X, (float)zone.Y);
    }
}
