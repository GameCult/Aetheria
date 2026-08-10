using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MessagePack;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
using static Unity.Mathematics.math;

public class Slime : MonoBehaviour
{
    #region variables
    
    [Header("Scene Links")]
    public Camera TargetCamera;
    
    [Header("Materials")]
    public ComputeShader SlimeCompute;
    public Material AccumulationMaterial;
    public Material ParticleMaterial;
    public RenderTexture GravityTexture;
    
    [Header("Performance")]
    public int TextureSize = 1024;
    public float ZoneRadius = 512;
    public int AgentCount = 524288;
    public int ParticleCount = 65536;
    public int TrailPoints = 8;

    [Header("Parameters")]
    public SlimeSettings SlimeSettings;
    public float ZoneCeiling;
    public float ZoneFloor;
    public float NoiseFrequency = 1;
    public float NoiseAmplitude = 5;
    public float ParticleHeightRange;
    public float ParticleHeightOffset;
    public float TrailStartWidth = .5f;
    public float TrailEndWidth = .1f;
    public float TrailDamping = .05f;
    public Vector2[] SpawnPositions;

    private const int GROUP_SIZE = 64;
    private int _updateAgentsKernel;
    private int _diffuseKernel;
    private int _particleKernel;
    private int _trailKernel;
    private int _geometryKernel;
    private string _settingsHash;
    private int _spawnBufferSize = 1;

    private int IndexBufferSize => ParticleCount * 6 * (TrailPoints - 1);
    
    #endregion

    #region buffers
    
    private ComputeBuffer _agentsBuffer;
    private RenderTexture _accumulationTexture;
    private RenderTexture _previousAccumulationTexture;

    private ComputeBuffer _spawnBuffer;
    private ComputeBuffer _parameterBuffer;
    private ComputeBuffer _particlesBuffer;

    private ComputeBuffer _trailBuffer;
    private ComputeBuffer _trailPreviousBuffer;
    
    private ComputeBuffer _indexBuffer;
    private ComputeBuffer _vertexBuffer;
    
    #endregion

    #region Structs
    
    struct Agent
    {
        public Vector2 Position;
        public float Angle;
    }
    
    struct Particle
    {
        public Vector3 Start;
        public float Intensity;
    }
    
    struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public float Intensity;
    }
    
    #endregion
    
    void Start()
    {
        _updateAgentsKernel = SlimeCompute.FindKernel("UpdateAgents");
        _diffuseKernel = SlimeCompute.FindKernel("Diffuse");
        _particleKernel = SlimeCompute.FindKernel("CreateAgentParticles");
        _trailKernel = SlimeCompute.FindKernel("UpdateTrailPoints");
        _geometryKernel = SlimeCompute.FindKernel("CreateTrailGeometry");

        _accumulationTexture = new RenderTexture(TextureSize, TextureSize, 1, RenderTextureFormat.RFloat);
        _accumulationTexture.enableRandomWrite = true;
        _previousAccumulationTexture = new RenderTexture(TextureSize, TextureSize, 1, RenderTextureFormat.RFloat);
        _previousAccumulationTexture.enableRandomWrite = true;
        AccumulationMaterial.SetTexture("_MainTex", _accumulationTexture);

        _agentsBuffer = new ComputeBuffer(AgentCount, Marshal.SizeOf(typeof(Agent)));
        var agents = new Agent[AgentCount];
        for (var i = 0; i < agents.Length; i++)
        {
            agents[i] = new Agent
            {
                Position = Random.insideUnitCircle * ZoneRadius,
                Angle = Random.value * Mathf.PI * 2
            };
        }
        _agentsBuffer.SetData(agents);

        _parameterBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(SlimeSettings)));
        _spawnBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(Vector2)));
        _spawnBuffer.SetData(new [] {Vector2.zero});
        
        _trailBuffer = new ComputeBuffer(ParticleCount * TrailPoints, Marshal.SizeOf(typeof(Vector3)));
        _trailPreviousBuffer = new ComputeBuffer(ParticleCount * TrailPoints, Marshal.SizeOf(typeof(Vector3)));
        var trailData = Enumerable.Repeat(Vector3.zero, _trailBuffer.count).ToArray();
        _trailBuffer.SetData(trailData);
        _trailPreviousBuffer.SetData(trailData);

        _particlesBuffer = new ComputeBuffer(ParticleCount, Marshal.SizeOf(typeof(Particle)));

        _vertexBuffer = new ComputeBuffer(ParticleCount * 2 * TrailPoints, Marshal.SizeOf(typeof(Vertex)));
        
        var indexData = new int[IndexBufferSize];
        var index = 0;
        for (int iTrail = 0; iTrail < ParticleCount; iTrail++)
        {
            var trailOffset = iTrail * TrailPoints * 2;
            for (int iTrailPoint = 0; iTrailPoint < TrailPoints - 1; iTrailPoint++)
            {
                var offset = trailOffset + iTrailPoint * 2;
                indexData[index++] = 0 + offset;
                indexData[index++] = 1 + offset;
                indexData[index++] = 2 + offset;
                indexData[index++] = 2 + offset;
                indexData[index++] = 1 + offset;
                indexData[index++] = 3 + offset;
            }
        }

        _indexBuffer = new ComputeBuffer(indexData.Length, Marshal.SizeOf(typeof(uint))); // 1 trail point to 2 triangles (6 vertices)
        _indexBuffer.SetData(indexData);
    }

    void UpdateSettings()
    {
        RegisterResolver.Register();
        var hash = MessagePackSerializer.Serialize(SlimeSettings).GetHashSHA1();
        if(hash != _settingsHash)
        {
            _settingsHash = hash;
            _parameterBuffer.SetData(new[] {SlimeSettings});
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateSettings();

        if (SpawnPositions != null && SpawnPositions.Length > 0)
        {
            if (SpawnPositions.Length != _spawnBufferSize)
            {
                _spawnBuffer.Release();
                _spawnBuffer = new ComputeBuffer(SpawnPositions.Length, Marshal.SizeOf(typeof(Vector2)));
                _spawnBufferSize = SpawnPositions.Length;
            }
            _spawnBuffer.SetData(SpawnPositions);
        }
        
        SlimeCompute.SetInt("randomOffset", Random.Range(0,65536));
        SlimeCompute.SetInt("textureSize", TextureSize);
        SlimeCompute.SetInt("gravityTextureSize", GravityTexture.width);
        SlimeCompute.SetInt("numAgents", AgentCount);
        SlimeCompute.SetFloat("zoneRadius", ZoneRadius);
        SlimeCompute.SetFloat("deltaTime", Time.deltaTime);
        SlimeCompute.SetInt("spawnPositionCount", _spawnBufferSize);

        SlimeCompute.SetBuffer(_updateAgentsKernel, "agents", _agentsBuffer);
        SlimeCompute.SetBuffer(_updateAgentsKernel, "spawnPositions", _spawnBuffer);
        SlimeCompute.SetBuffer(_updateAgentsKernel, "parameters", _parameterBuffer);
        SlimeCompute.SetTexture(_updateAgentsKernel, "accumulation", _accumulationTexture);
        SlimeCompute.SetTexture(_updateAgentsKernel, "Heightmap", GravityTexture);
        
        var numberOfGroups = Mathf.CeilToInt((float)AgentCount / GROUP_SIZE);
        SlimeCompute.Dispatch(_updateAgentsKernel, numberOfGroups, 1, 1);

        Graphics.Blit(_accumulationTexture, _previousAccumulationTexture);
        SlimeCompute.SetBuffer(_diffuseKernel, "parameters", _parameterBuffer);
        SlimeCompute.SetTexture(_diffuseKernel, "Heightmap", GravityTexture);
        SlimeCompute.SetTexture(_diffuseKernel, "accumulation", _previousAccumulationTexture);
        SlimeCompute.SetTexture(_diffuseKernel, "diffusedAccumulation", _accumulationTexture);
        numberOfGroups = Mathf.CeilToInt((float)TextureSize / 8);
        SlimeCompute.Dispatch(_diffuseKernel, numberOfGroups, numberOfGroups, 1);
        
        SlimeCompute.SetBuffer(_particleKernel, "parameters", _parameterBuffer);
        SlimeCompute.SetTexture(_particleKernel, "Heightmap", GravityTexture);
        SlimeCompute.SetBuffer(_particleKernel, "agents", _agentsBuffer);
        SlimeCompute.SetBuffer(_particleKernel, "particles", _particlesBuffer);
        SlimeCompute.SetFloat("time", Time.time * NoiseFrequency);
        SlimeCompute.SetFloat("noiseAmplitude", NoiseAmplitude);
        SlimeCompute.SetFloat("heightRange", ParticleHeightRange);
        SlimeCompute.SetFloat("heightOffset", ParticleHeightOffset);
        SlimeCompute.SetFloat("trailDamping", TrailDamping);
        SlimeCompute.SetInt("particleCount", ParticleCount);

        numberOfGroups = ParticleCount / GROUP_SIZE;
        SlimeCompute.Dispatch(_particleKernel, numberOfGroups, 1, 1);
        
        SlimeCompute.SetInt("trailPoints", TrailPoints);
        
        numberOfGroups = ParticleCount * TrailPoints / GROUP_SIZE;
        SlimeCompute.SetBuffer(_trailKernel, "previousTrailBuffer", _trailPreviousBuffer);
        SlimeCompute.SetBuffer(_trailKernel, "particles", _particlesBuffer);
        SlimeCompute.SetBuffer(_trailKernel, "trailBuffer", _trailBuffer);
        SlimeCompute.Dispatch(_trailKernel, numberOfGroups, 1, 1);
        var tempTrailBuffer = _trailBuffer;
        _trailBuffer = _trailPreviousBuffer;
        _trailPreviousBuffer = tempTrailBuffer;
        
        SlimeCompute.SetFloat("trailStartWidth", TrailStartWidth);
        SlimeCompute.SetFloat("trailEndWidth", TrailEndWidth);
        SlimeCompute.SetVector("cameraPos", TargetCamera.transform.position);
        SlimeCompute.SetBuffer(_geometryKernel, "particles", _particlesBuffer);
        SlimeCompute.SetBuffer(_geometryKernel, "trailBuffer", _trailBuffer);
        SlimeCompute.SetBuffer(_geometryKernel, "vertexBuffer", _vertexBuffer);

        SlimeCompute.Dispatch(_geometryKernel, numberOfGroups, 1, 1);
        
        ParticleMaterial.SetBuffer("_IndexBuffer", _indexBuffer);
        ParticleMaterial.SetBuffer("_VertexBuffer", _vertexBuffer);
        Graphics.DrawProcedural(
            ParticleMaterial, 
            new Bounds(Vector3.zero, ZoneRadius * 2 * Vector3.one), 
            MeshTopology.Triangles, 
            IndexBufferSize, 
            1, 
            TargetCamera, 
            null, 
            ShadowCastingMode.Off, 
            false, 
            0);
    }

    private void OnDisable()
    {
        _agentsBuffer.Release();
        _accumulationTexture.Release();
        _previousAccumulationTexture.Release();
        
        _spawnBuffer.Release();
        _parameterBuffer.Release();
        _particlesBuffer.Release();
        
        _trailBuffer.Release();
        _trailPreviousBuffer.Release();
        
        _indexBuffer.Release();
        _vertexBuffer.Release();
    }
}

[Serializable, MessagePackObject(keyAsPropertyName:true)]
public struct SlimeSettings
{
    public SlimeParameter Deposition;
    public SlimeParameter Diffusion;
    public SlimeParameter Decay;
    public SlimeParameter TurnSpeed;
    public SlimeParameter Speed;
    public SlimeParameter Drive;
    public SlimeParameter SensorDistance;
    public SlimeParameter SensorSpread;
    public SlimeParameter TrailDamping;
    public SlimeParameter Intensity;
}

[Serializable, MessagePackObject(keyAsPropertyName:true)]
public struct SlimeParameter
{
    public float SlopeThreshold;
    public float SlopeBlend;
    public float HighValue;
    public float LowValue;
    public float Exponent;

    public float Evaluate(float slope)
    {
        return lerp(LowValue, HighValue, pow(smoothstep(SlopeThreshold - SlopeBlend, SlopeThreshold + SlopeBlend, slope), Exponent));
    }
}
