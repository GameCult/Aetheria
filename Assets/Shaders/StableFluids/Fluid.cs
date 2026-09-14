// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// https://github.com/keijiro/StableFluids

using System.Collections.Generic;
using CultMath;
using UnityEngine;
using static CultMath.math;
using int2 = CultMath.int2;
using Random = UnityEngine.Random;

public class Fluid : MonoBehaviour
{
    #region Editable attributes

    [SerializeField] int _resolution = 512;
    [SerializeField] float _viscosity = 1e-6f;
    [SerializeField] float _damping = .05f;
    [SerializeField] float _range = 512;
    [SerializeField] Transform _camera;

    #endregion

    #region Internal resources

    [SerializeField, HideInInspector] ComputeShader _compute;
    [SerializeField, HideInInspector] Shader _shader;

    #endregion

    #region Private members

    Material _shaderSheet;
    int2 _previousCameraIntPosition = int2.zero;
    private List<Force> _forces = new List<Force>();

    struct Force
    {
        public float2 Position;
        public float2 Vector;
        public float Exponent;
    }

    static class Kernels
    {
        public const int Advect = 0;
        public const int Force = 1;
        public const int PSetup = 2;
        public const int PFinish = 3;
        public const int Jacobi1 = 4;
        public const int Jacobi2 = 5;
        public const int Adjust = 6;
    }

    int ThreadCount { get { return (_resolution + 7) / 8; } }

    int Resolution { get { return ThreadCount * 8; } }

    // Vector field buffers
    static class VFB
    {
        public static RenderTexture V1;
        public static RenderTexture V2;
        public static RenderTexture V3;
        public static RenderTexture P1;
        public static RenderTexture P2;
    }

    RenderTexture AllocateBuffer(int componentCount)
    {
        var format = RenderTextureFormat.ARGBHalf;
        if (componentCount == 1) format = RenderTextureFormat.RHalf;
        if (componentCount == 2) format = RenderTextureFormat.RGHalf;

        var rt = new RenderTexture(Resolution, Resolution, 0, format);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    #endregion

    #region MonoBehaviour implementation

    public void AddForce(float2 position, float2 vector, float exponent)
    {
        _forces.Add(new Force { Position = position, Vector = vector, Exponent = exponent });
    }

    void OnValidate()
    {
        _resolution = Mathf.Max(_resolution, 8);
    }

    void Start()
    {
        _shaderSheet = new Material(_shader);

        VFB.V1 = AllocateBuffer(2);
        VFB.V2 = AllocateBuffer(2);
        VFB.V3 = AllocateBuffer(2);
        VFB.P1 = AllocateBuffer(1);
        VFB.P2 = AllocateBuffer(1);
    }

    void OnDestroy()
    {
        Destroy(_shaderSheet);

        Destroy(VFB.V1);
        Destroy(VFB.V2);
        Destroy(VFB.V3);
        Destroy(VFB.P1);
        Destroy(VFB.P2);
    }

    void Update()
    {
        var dt = max(Time.deltaTime, .01f);
        var dx = 1.0f / Resolution;
        
        var snapLength = _range / _resolution;

        // Input point
        var cameraIntPosition = int2(
            (int)(_camera.position.x / snapLength), 
            (int)(_camera.position.z / snapLength)
            );
        var cameraPosition = float2(
            cameraIntPosition.x * snapLength, 
            cameraIntPosition.y * snapLength
        );
        Shader.SetGlobalVector("_FluidTransform", new Vector4(cameraPosition.x, cameraPosition.y, _range));
        Shader.SetGlobalTexture("_FluidVelocity", VFB.V1);

        var delta = (float2)(cameraIntPosition - _previousCameraIntPosition) / Resolution;
        _compute.SetVector("Delta", new Vector4(delta.x,delta.y));
        _compute.SetFloat("Damping", _damping);
        _compute.SetTexture(Kernels.Adjust, "U_in", VFB.V1);
        _compute.SetTexture(Kernels.Adjust, "W_out", VFB.V3);
        _compute.Dispatch(Kernels.Adjust, ThreadCount, ThreadCount, 1);

        // Common variables
        _compute.SetFloat("Time", Time.time);
        _compute.SetFloat("DeltaTime", dt);

        // Advection
        _compute.SetTexture(Kernels.Advect, "U_in", VFB.V3);
        _compute.SetTexture(Kernels.Advect, "W_out", VFB.V2);
        _compute.Dispatch(Kernels.Advect, ThreadCount, ThreadCount, 1);

        // Diffuse setup
        var dif_alpha = dx * dx / (_viscosity * dt);
        _compute.SetFloat("Alpha", dif_alpha);
        _compute.SetFloat("Beta", 4 + dif_alpha);
        Graphics.CopyTexture(VFB.V2, VFB.V1);
        _compute.SetTexture(Kernels.Jacobi2, "B2_in", VFB.V1);

        // Jacobi iteration
        for (var i = 0; i < 20; i++)
        {
            _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V2);
            _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V3);
            _compute.Dispatch(Kernels.Jacobi2, ThreadCount, ThreadCount, 1);

            _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V3);
            _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V2);
            _compute.Dispatch(Kernels.Jacobi2, ThreadCount, ThreadCount, 1);
        }
        
        float2 getUV(float2 pos, float2 camPos, float scale)
        {
            return -(pos-camPos)/scale + float2(.5f,.5f);
        }

        // Add external forces
        foreach(var force in _forces)
        {
            var uv = getUV(force.Position, cameraIntPosition, _range);
            _compute.SetVector("ForceOrigin", new Vector4(uv.x,uv.y));
            _compute.SetFloat("ForceExponent", force.Exponent);
            _compute.SetTexture(Kernels.Force, "W_in", VFB.V2);
            _compute.SetTexture(Kernels.Force, "W_out", VFB.V3);
            _compute.SetVector("ForceVector", new Vector4(force.Vector.x,force.Vector.y));

            _compute.Dispatch(Kernels.Force, ThreadCount, ThreadCount, 1);
        }

        // Projection setup
        _compute.SetTexture(Kernels.PSetup, "W_in", VFB.V3);
        _compute.SetTexture(Kernels.PSetup, "DivW_out", VFB.V2);
        _compute.SetTexture(Kernels.PSetup, "P_out", VFB.P1);
        _compute.Dispatch(Kernels.PSetup, ThreadCount, ThreadCount, 1);

        // Jacobi iteration
        _compute.SetFloat("Alpha", -dx * dx);
        _compute.SetFloat("Beta", 4);
        _compute.SetTexture(Kernels.Jacobi1, "B1_in", VFB.V2);

        for (var i = 0; i < 20; i++)
        {
            _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P1);
            _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P2);
            _compute.Dispatch(Kernels.Jacobi1, ThreadCount, ThreadCount, 1);

            _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P2);
            _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P1);
            _compute.Dispatch(Kernels.Jacobi1, ThreadCount, ThreadCount, 1);
        }

        // Projection finish
        _compute.SetTexture(Kernels.PFinish, "W_in", VFB.V3);
        _compute.SetTexture(Kernels.PFinish, "P_in", VFB.P1);
        _compute.SetTexture(Kernels.PFinish, "U_out", VFB.V1);
        _compute.Dispatch(Kernels.PFinish, ThreadCount, ThreadCount, 1);

        _previousCameraIntPosition = cameraIntPosition;
    }

    #endregion
}