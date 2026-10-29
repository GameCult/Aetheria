using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;


public class Stardust : MonoBehaviour
{
    #region variables
    //public bool debugLog = false;
    //public float proxyPersistTime = 2;
    public GameSettings Settings;
    public Camera TargetCamera;
    public ComputeShader ParticleCalculation;
    public Material ParticleMaterial;
    public int Span = 512;
    public float Period = .25f;

    public float MinimumSize = .1f;
    public float MaximumSize = 2;

    public float Spacing = 4.0f;
    public float Ceiling = -250.0f;
    public float Floor = -25.0f;
    public float MinHeadroom = 25;
    public float MaxHeadroom = 100;
    public float HeightExponent = 4;

    public RenderTexture NebulaSurfaceHeight;
    public RenderTexture NebulaPatchHeight;
    public RenderTexture NebulaPatch;
    public RenderTexture NebulaTint;
    public Texture Heightmap;
    public Texture2D ParticleColors;
    //public Transform TargetTransform;

    private const int GROUP_SIZE = 128;
    private int _updateParticlesKernel;
    #endregion

    #region Structs
    //Notice that this struct has to match the one in the compute shader exactly.
    struct Particle
    {
        public Vector3 Position;
        public Vector3 Color; //Color is a float3 in the compute shader, we need a Vector3 to match that layout, not a Color!
        public float Size;
    };
    #endregion

    #region buffers
    private ComputeBuffer _particlesBuffer;
    private const int PARTICLE_STRIDE = 28;

    private ComputeBuffer _quadPoints;
    private const int QUAD_STRIDE = 12;

    // The dust is drawn by a command buffer on TargetCamera: a camera replays its buffers at their event and never culls them,
    // so the draw needs no bounds and no extent can fall short of how deep the field is.
    private CommandBuffer _draw;
    // Into the G-buffer, so the dust's depth is in the depth texture the [ImageEffectOpaque] nebula marches against and the fog
    // attenuates the dust by distance like any opaque surface. Drawn later (BeforeImageEffectsOpaque) it missed that depth and
    // was fogged as if it were the far background.
    private const CameraEvent DrawEvent = CameraEvent.AfterGBuffer;

    #endregion

    #region setup
    // Use this for initialization
    void Start()
    {
        //Find compute kernel
        _updateParticlesKernel = ParticleCalculation.FindKernel("UpdateParticles");

        //Create particle buffer
        _particlesBuffer = new ComputeBuffer(Span * Span, PARTICLE_STRIDE);

        Particle[] particles = new Particle[Span * Span];

        for (int i = 0; i < Span * Span; ++i)
        {
            particles[i].Position = Random.insideUnitSphere * 100;
            particles[i].Color = Vector3.one; //white
            particles[i].Size = Random.value;
        }

        _particlesBuffer.SetData(particles);

        //Create quad buffer
        _quadPoints = new ComputeBuffer(6, QUAD_STRIDE);

        _quadPoints.SetData(new[] {
			new Vector3(-.5f, .5f),
			new Vector3(.5f, .5f),
			new Vector3(.5f, -.5f),
			new Vector3(.5f, -.5f),
			new Vector3(-.5f, -.5f),
			new Vector3(-.5f, .5f)
		});

        //The buffers never change after this, so the material is bound once
        ParticleMaterial.SetBuffer("particles", _particlesBuffer);
        ParticleMaterial.SetBuffer("quadPoints", _quadPoints);

        //Start may run again (the scene harness calls it by hand): drop the previous draw before building the next
        DetachDraw();
        _draw?.Release();
        _draw = new CommandBuffer { name = "Stardust" };
        _draw.SetRenderTarget(new RenderTargetIdentifier[]
        {
            BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.GBuffer1, BuiltinRenderTextureType.GBuffer2,
            BuiltinRenderTextureType.CameraTarget
        }, BuiltinRenderTextureType.CameraTarget);
        _draw.DrawProcedural(Matrix4x4.identity, ParticleMaterial, ParticleMaterial.FindPass("DEFERRED"), MeshTopology.Triangles, 6, Span * Span);
        AttachDraw();
    }

    void OnEnable() => AttachDraw();

    void OnDisable() => DetachDraw();

    void AttachDraw()
    {
        if (_draw != null && TargetCamera != null)
            TargetCamera.AddCommandBuffer(DrawEvent, _draw);
    }

    void DetachDraw()
    {
        if (_draw != null && TargetCamera != null)
            TargetCamera.RemoveCommandBuffer(DrawEvent, _draw);
    }
    #endregion

    #region Compute update
    // Update is called once per frame
    void Update()
    {
        //Bind resources to compute shader
        ParticleCalculation.SetBuffer(_updateParticlesKernel, "particles", _particlesBuffer);

        // Particle lifetimes cycle on the flow clock (zone time), so the dust stops with the sim like everything the flow moves.
        ParticleCalculation.SetFloat("phase", FlowField.Cycle(VolumeSampling.FlowClock, Period));

        ParticleCalculation.SetFloat("period", Period);
        ParticleCalculation.SetFloat("spacing", Spacing);
        ParticleCalculation.SetFloat("ceilingHeight", Ceiling);
        ParticleCalculation.SetFloat("floorHeight", Floor);
        ParticleCalculation.SetFloat("heightExponent", HeightExponent);
        ParticleCalculation.SetFloat("maximumSize", MaximumSize);
        ParticleCalculation.SetFloat("minimumSize", MinimumSize);
        ParticleCalculation.SetFloat("minHeadroom", MinHeadroom);
        ParticleCalculation.SetFloat("maxHeadroom", MaxHeadroom);
        ParticleCalculation.SetInt("span", Span);
        
        ParticleCalculation.SetTexture(_updateParticlesKernel, "HueTexture", ParticleColors);

        //Dispatch, launch threads on GPU
        int numberOfGroups = Mathf.CeilToInt((float)Span * Span / GROUP_SIZE);
        ParticleCalculation.Dispatch(_updateParticlesKernel, numberOfGroups, 1, 1);
    }
    #endregion

    #region cleanup
    void OnDestroy()
    {
//        m_emitterProxies.Release();
//        _temperature.Release();
        DetachDraw();
        _draw?.Release();
        _draw = null;
        if(_particlesBuffer!=null)
        {
            _particlesBuffer.Release();
            _quadPoints.Release();
        }
//        m_livingProxyIndices.Release();
    }
    #endregion
}
