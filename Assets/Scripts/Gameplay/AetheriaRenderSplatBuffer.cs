using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;
using UnityEngine;

public sealed class AetheriaRenderSplatBuffer : System.IDisposable
{
    public const string SplatBufferPropertyName = "_AetheriaSplats";
    public const string SplatCountPropertyName = "_AetheriaSplatCount";
    public const string ViewportToClipPropertyName = "_AetheriaViewportToClip";
    public const string ChannelFilterPropertyName = "_AetheriaChannelFilter";

    private const int SplatStrideBytes = 80;

    private AetheriaRenderSplat[] _splats = System.Array.Empty<AetheriaRenderSplat>();
    private GraphicsBuffer _buffer;
    private int _capacity;

    public int Count { get; private set; }
    public GraphicsBuffer Buffer => _buffer;
    public bool HasGpuBuffer => _buffer != null && _buffer.IsValid() && Count > 0;

    public bool Upload(AetheriaRuntimeRenderSplatSoa splats, int channelFilter = -1, int layerFilter = -1)
    {
        Count = Pack(splats, channelFilter, layerFilter);
        EnsureCapacity(Count);
        if (Count <= 0)
            return false;

        _buffer.SetData(_splats, 0, 0, Count);
        return true;
    }

    private int Pack(AetheriaRuntimeRenderSplatSoa splats, int channelFilter, int layerFilter)
    {
        if (splats == null || splats.Count <= 0)
            return 0;

        EnsureCpuCapacity(splats.Count);
        var count = 0;
        for (var i = 0; i < splats.Count; i++)
        {
            var channel = Read(splats.Channel, i, 0);
            if (channelFilter >= 0 && channel != channelFilter)
                continue;

            var layerIndex = Read(splats.LayerIndex, i, -1);
            if (layerFilter >= 0 && layerIndex != layerFilter)
                continue;

            var halfExtentX = (float)Read(splats.HalfExtentX, i, 0);
            var halfExtentY = (float)Read(splats.HalfExtentY, i, 0);
            if (halfExtentX <= 0 || halfExtentY <= 0)
                continue;

            _splats[count++] = new AetheriaRenderSplat
            {
                CenterHalfExtent = new float4(
                    (float)Read(splats.CenterX, i, 0),
                    (float)Read(splats.CenterY, i, 0),
                    halfExtentX,
                    halfExtentY),
                RotationChannelFalloff = new float4(
                    (float)Read(splats.RotationCos, i, 1),
                    (float)Read(splats.RotationSin, i, 0),
                    channel,
                    Read(splats.Falloff, i, AetheriaRuntimeRenderSplatFalloffs.Smooth)),
                LayerSource = new float4(
                    layerIndex,
                    Read(splats.SourceKind, i, AetheriaRuntimeRenderSplatSourceKinds.Constant),
                    (float)Read(splats.AnimationSpeed, i, 0),
                    (float)Read(splats.SourceFlags, i, 0)),
                SourceFrequencyPhase = new float4(
                    (float)Read(splats.FrequencyX, i, 1),
                    (float)Read(splats.FrequencyY, i, 1),
                    (float)Read(splats.PhaseX, i, 0),
                    (float)Read(splats.PhaseY, i, 0)),
                Value = new float4(
                    (float)Read(splats.ValueR, i, 0),
                    (float)Read(splats.ValueG, i, 0),
                    (float)Read(splats.ValueB, i, 0),
                    (float)Read(splats.ValueA, i, 1))
            };
        }

        return count;
    }

    private void EnsureCpuCapacity(int count)
    {
        if (_splats.Length >= count)
            return;

        _splats = new AetheriaRenderSplat[math.ceilpow2(math.max(1, count))];
    }

    private void EnsureCapacity(int count)
    {
        if (_buffer != null && _buffer.IsValid() && _capacity >= count)
            return;

        _buffer?.Release();
        _buffer = null;
        _capacity = 0;
        if (count <= 0)
            return;

        _capacity = math.ceilpow2(math.max(1, count));
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _capacity, SplatStrideBytes);
    }

    public void Dispose()
    {
        _buffer?.Release();
        _buffer = null;
        _capacity = 0;
        Count = 0;
    }

    private static double Read(System.Collections.Generic.IReadOnlyList<double> values, int index, double fallback)
    {
        return values != null && index >= 0 && index < values.Count ? values[index] : fallback;
    }

    private static int Read(System.Collections.Generic.IReadOnlyList<int> values, int index, int fallback)
    {
        return values != null && index >= 0 && index < values.Count ? values[index] : fallback;
    }

    private struct AetheriaRenderSplat
    {
        public float4 CenterHalfExtent;
        public float4 RotationChannelFalloff;
        public float4 LayerSource;
        public float4 SourceFrequencyPhase;
        public float4 Value;
    }
}
