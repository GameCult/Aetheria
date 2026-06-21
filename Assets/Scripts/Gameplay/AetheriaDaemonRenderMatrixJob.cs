using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AetheriaDaemonRenderMatrixJob : IJobParallelFor
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

    [WriteOnly]
    public NativeArray<float4x4> Matrices;

    public bool HasRotation;
    public bool HasPhysicsRadius;
    public bool HasRenderScale;
    public float UniformScale;

    public void Execute(int index)
    {
        var position = new float3(PositionX[index], PositionY[index], PositionZ[index]);
        var rotation = HasRotation
            ? quaternion.RotateY(RotationRadians[index])
            : quaternion.identity;
        var scaleValue = HasRenderScale
            ? math.max(RenderScale[index], 0.0001f)
            : HasPhysicsRadius
            ? math.max(PhysicsBodyRadius[index] * 2.0f, 0.0001f)
            : math.max(UniformScale, 0.0001f);

        Matrices[index] = float4x4.TRS(position, rotation, new float3(scaleValue));
    }
}
