using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;


public class Stardust : MonoBehaviour
{
    private static readonly List<Stardust> Instances = new List<Stardust>();

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
    public Camera GravityCamera;

    private float _flowScroll;
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
    private bool _buffersReady;

    #endregion

    public static void RenderForCamera(UnsafeCommandBuffer cmd, Camera camera, TextureHandle colorTarget, TextureHandle depthTarget)
    {
        for (var i = 0; i < Instances.Count; i++)
        {
            var stardust = Instances[i];
            if (stardust == null || !stardust.isActiveAndEnabled || stardust.TargetCamera != camera)
                continue;

            stardust.RenderStardust(cmd, colorTarget, depthTarget);
        }
    }

    #region setup
    private void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    private void OnDisable()
    {
        Instances.Remove(this);
        ReleaseBuffers();
    }

    // Use this for initialization
    void Start()
    {
        EnsureBuffers();
    }

    private void EnsureBuffers()
    {
        if (_buffersReady && (_particlesBuffer == null || _quadPoints == null))
            _buffersReady = false;

        if (_buffersReady)
            return;

        if (ParticleCalculation == null || ParticleMaterial == null)
            return;

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

        _buffersReady = true;
    }
    #endregion

    #region Compute update
    // Update is called once per frame
    void Update()
    {
        EnsureBuffers();
        if (!_buffersReady || GravityCamera == null)
            return;

        if (_particlesBuffer == null)
        {
            _buffersReady = false;
            EnsureBuffers();
            if (!_buffersReady)
                return;
        }

        //Bind resources to compute shader
        ParticleCalculation.SetBuffer(_updateParticlesKernel, "particles", _particlesBuffer);

        ParticleCalculation.SetFloat("time", Time.time);

        var pos = GravityCamera.transform.position;
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

    private void RenderStardust(UnsafeCommandBuffer cmd, TextureHandle colorTarget, TextureHandle depthTarget)
    {
        EnsureBuffers();
        if (!_buffersReady || ParticleMaterial == null || _particlesBuffer == null || _quadPoints == null)
            return;

        ParticleMaterial.SetBuffer("particles", _particlesBuffer);
        ParticleMaterial.SetBuffer("quadPoints", _quadPoints);

        if (depthTarget.IsValid())
            cmd.SetRenderTarget(colorTarget, depthTarget);
        else
            cmd.SetRenderTarget(colorTarget);

        cmd.DrawProcedural(Matrix4x4.identity, ParticleMaterial, 0, MeshTopology.Triangles, 6, Span * Span);
    }
    #endregion

    #region rendering
    // void OnRenderObject()
    // {
    //     if (Camera.current == TargetCamera || Camera.current.name == "SceneCamera")
    //     {
    //         //Bind resources to material
    //         ParticleMaterial.SetBuffer("particles", _particlesBuffer);
    //         ParticleMaterial.SetBuffer("quadPoints", _quadPoints);
    //         // ParticleMaterial.SetFloat("velocityStretch", ParticleVelocityStretch);
    //         // ParticleMaterial.SetVector("velocity", PlayerVelocity);
    //         
    //         //Set the pass
    //         ParticleMaterial.SetPass(0);
    //
    //         //Draw
    //         Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, Span * Span);
    //     }
    // }
    #endregion

    #region cleanup
    void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        _particlesBuffer?.Release();
        _quadPoints?.Release();
        _particlesBuffer = null;
        _quadPoints = null;
        _buffersReady = false;
    }
    #endregion
}
