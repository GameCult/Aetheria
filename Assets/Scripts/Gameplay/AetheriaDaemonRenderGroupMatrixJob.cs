using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AetheriaDaemonRenderGroupMatrixJob : IJob
{
    [ReadOnly]
    public NativeArray<float> PositionX;

    [ReadOnly]
    public NativeArray<float> PositionY;

    [ReadOnly]
    public NativeArray<float> PositionZ;

    [ReadOnly]
    public NativeArray<float> RotationRadians;

    [ReadOnly]
    public NativeArray<float> PhysicsBodyRadius;

    [ReadOnly]
    public NativeArray<float> RenderScale;

    [ReadOnly]
    public NativeArray<byte> RenderVisibility;

    [ReadOnly]
    public NativeArray<int> RenderLod;

    [ReadOnly]
    public NativeArray<uint> RenderGroupId;

    [WriteOnly]
    public NativeArray<float4x4> Matrices;

    public NativeArray<int> WrittenCount;
    public uint TargetRenderGroupId;
    public int TargetRenderLod;
    public bool HasRotation;
    public bool HasPhysicsRadius;
    public bool HasRenderScale;
    public bool HasRenderVisibility;
    public bool HasRenderLod;
    public bool HasRenderGroupFilter;
    public bool HasRenderLodFilter;
    public float UniformScale;

    public void Execute()
    {
        var written = 0;
        for (var index = 0; index < PositionX.Length; index++)
        {
            if (HasRenderGroupFilter && RenderGroupId[index] != TargetRenderGroupId)
            {
                continue;
            }

            if (HasRenderVisibility && RenderVisibility[index] == 0)
            {
                continue;
            }

            if (HasRenderLodFilter && (!HasRenderLod || RenderLod[index] != TargetRenderLod))
            {
                continue;
            }

            if (written >= Matrices.Length)
            {
                break;
            }

            var position = new float3(PositionX[index], PositionY[index], PositionZ[index]);
            var rotation = HasRotation
                ? quaternion.RotateY(RotationRadians[index])
                : quaternion.identity;
            var scaleValue = HasRenderScale
                ? math.max(RenderScale[index], 0.0001f)
                : HasPhysicsRadius
                ? math.max(PhysicsBodyRadius[index] * 2.0f, 0.0001f)
                : math.max(UniformScale, 0.0001f);

            Matrices[written] = float4x4.TRS(position, rotation, new float3(scaleValue));
            written++;
        }

        WrittenCount[0] = written;
    }
}
