using GameCult.Aetheria.State.Verse;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct AetheriaDaemonRenderNativeView
{
    public long FrameId;
    public long Generation;
    public int Count;
    public NativeArray<int> EntityIndex;
    public NativeArray<float3> Position;
    public NativeArray<float> RotationRadians;
    public NativeArray<float3> Velocity;
    public NativeArray<float> PhysicsBodyRadius;
    public NativeArray<float> PhysicsBodyMass;
    public NativeArray<float> PhysicsBodyInverseMass;
    public NativeArray<float> RenderScale;
    public NativeArray<byte> RenderVisibility;
    public NativeArray<int> RenderLod;
    public NativeArray<uint> RenderGroupId;

    public bool IsCreated =>
        Count > 0 &&
        Position.IsCreated;

    public bool HasRotation => RotationRadians.IsCreated;
    public bool HasEntityIndex => EntityIndex.IsCreated;
    public bool HasVelocity => Velocity.IsCreated;
    public bool HasPhysicsRadius => PhysicsBodyRadius.IsCreated;
    public bool HasPhysicsMass => PhysicsBodyMass.IsCreated;
    public bool HasPhysicsInverseMass => PhysicsBodyInverseMass.IsCreated;
    public bool HasRenderScale => RenderScale.IsCreated;
    public bool HasRenderVisibility => RenderVisibility.IsCreated;
    public bool HasRenderLod => RenderLod.IsCreated;
    public bool HasRenderGroupId => RenderGroupId.IsCreated;

    public JobHandle ScheduleBuildRenderMatrices(
        NativeArray<float4x4> matrices,
        float uniformScale = 1.0f,
        int innerLoopBatchCount = 64,
        JobHandle dependsOn = default)
    {
        if (!IsCreated)
        {
            return dependsOn;
        }

        var count = math.min(Count, matrices.IsCreated ? matrices.Length : 0);
        if (count <= 0)
        {
            return dependsOn;
        }

        var job = new AetheriaDaemonRenderMatrixJob
        {
            Position = Position,
            RotationRadians = RotationRadians,
            PhysicsBodyRadius = PhysicsBodyRadius,
            RenderScale = RenderScale,
            Matrices = matrices,
            HasRotation = HasRotation,
            HasPhysicsRadius = HasPhysicsRadius,
            HasRenderScale = HasRenderScale,
            UniformScale = uniformScale
        };

        return job.Schedule(count, math.max(1, innerLoopBatchCount), dependsOn);
    }

    public static bool TryCreate(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        out AetheriaDaemonRenderNativeView view)
    {
        view = default;
        if (index == null || map == null)
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(
                index,
                AetheriaRuntimeDaemonSoaColumnKinds.Position,
                out view.Position))
        {
            view = default;
            return false;
        }

        view.Count = view.Position.Length;

        TryAssignOptionalIntColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.EntityIndex, view.Count, out view.EntityIndex);
        TryAssignOptionalColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.RotationRadians, view.Count, out view.RotationRadians);
        TryAssignOptionalFloat3Column(index, map, AetheriaRuntimeDaemonSoaColumnKinds.Velocity, view.Count, out view.Velocity);
        TryAssignOptionalColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyRadius, view.Count, out view.PhysicsBodyRadius);
        TryAssignOptionalColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyMass, view.Count, out view.PhysicsBodyMass);
        TryAssignOptionalColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyInverseMass, view.Count, out view.PhysicsBodyInverseMass);
        TryAssignOptionalColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.RenderScale, view.Count, out view.RenderScale);
        TryAssignOptionalByteColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility, view.Count, out view.RenderVisibility);
        TryAssignOptionalIntColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.RenderLod, view.Count, out view.RenderLod);
        TryAssignOptionalUIntColumn(index, map, AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId, view.Count, out view.RenderGroupId);

        view.FrameId = index.View.FrameId;
        view.Generation = index.View.Generation;
        return true;
    }

    private static bool TryAssignOptionalFloat3Column(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        string kind,
        int expectedCount,
        out NativeArray<float3> array)
    {
        array = default;
        if (!index.TryGetFirstColumnOfKind(kind, out _))
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(index, kind, out array))
        {
            array = default;
            return false;
        }

        if (array.Length == expectedCount)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryAssignOptionalColumn(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        string kind,
        int expectedCount,
        out NativeArray<float> array)
    {
        array = default;
        if (!index.TryGetFirstColumnOfKind(kind, out _))
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(index, kind, out array))
        {
            array = default;
            return false;
        }

        if (array.Length == expectedCount)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryAssignOptionalUIntColumn(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        string kind,
        int expectedCount,
        out NativeArray<uint> array)
    {
        array = default;
        if (!index.TryGetFirstColumnOfKind(kind, out _))
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(index, kind, out array))
        {
            array = default;
            return false;
        }

        if (array.Length == expectedCount)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryAssignOptionalIntColumn(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        string kind,
        int expectedCount,
        out NativeArray<int> array)
    {
        array = default;
        if (!index.TryGetFirstColumnOfKind(kind, out _))
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(index, kind, out array))
        {
            array = default;
            return false;
        }

        if (array.Length == expectedCount)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryAssignOptionalByteColumn(
        AetheriaRuntimeDaemonSoaViewIndex index,
        AetheriaDaemonSoaMemoryMap map,
        string kind,
        int expectedCount,
        out NativeArray<byte> array)
    {
        array = default;
        if (!index.TryGetFirstColumnOfKind(kind, out _))
        {
            return false;
        }

        if (!map.TryCreateFirstNativeArrayOfKind(index, kind, out array))
        {
            array = default;
            return false;
        }

        if (array.Length == expectedCount)
        {
            return true;
        }

        array = default;
        return false;
    }
}
