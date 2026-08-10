using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using Random = UnityEngine.Random;

public class LightningCompute : MonoBehaviour
{
    public Camera Camera;
    
    [Header("Bolt Properties")]
    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public float StartWidth;
    public float EndWidth;
    public float BranchLength;
    public float MorphSpeed;
    public float NoiseAmplitude;
    public float NoiseFrequency;
    public float NoiseGain;
    public float NoiseLacunarity;
    public bool FixedEndpoint;
    public bool Animate;

    [Header("Animation Properties")]
    public float LeaderWidth;
    public float LeaderDuration;
    public float LeaderIntensity;
    public float PulseDuration;
    public float PulseIntensity;
    public float PulseWidth;
    public AnimationCurve PulseTrunkIntensityCurve;
    public AnimationCurve PulseBranchIntensityCurve;
    public AnimationCurve PulseWidthCurve;
    
    [Header("Buffer Sizes")]
    public int TrunkNodeCount;
    public int BranchCount;
    public int BranchNodeCount;

    [Header("Shaders")]
    public Material RenderMaterial;
    public ComputeShader ComputeShader;

    public bool DebugDrawNodes;
    public bool DebugDrawVertices;

    public Action OnLeaderComplete;
    public Action OnPulseComplete;

    private Transform _cameraTransform;

    private ComputeBuffer _nodeBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;
    private ComputeBuffer _boltBuffer;
    private Bolt[] _bolts;
    private Vector3[] _branchEndpoints;
    
    private const int GROUP_SIZE = 64;
    private int _updateKernel;
    private int _createWidthKernel;
    private float _animationStartTime;
    private float _time;
    private bool _leaderComplete;
    private bool _pulseComplete;
    private bool _initialized;
    private Material _materialInstance;

    private int NodeBufferSize => TrunkNodeCount + BranchCount * BranchNodeCount;
    private int VertexBufferSize => NodeBufferSize * 2;
    private int IndexBufferSize => (NodeBufferSize - (BranchCount + 1)) * 6;
    
    public struct Node
    {
        public Vector3 pos;
        public int bolt;
        public float time;
    }

    public struct Vertex
    {
        public Vector3 pos;
        public Vector2 uv;
    }

    public struct Bolt
    {
        public Vector3 startPos;
        public Vector3 endPos;
        public float startTime;
        public int startIndex;
        public Vector3 startOffset;
        public Vector3 endOffset;
    }
    
    void Start()
    {
        Initialize();
        _materialInstance = Instantiate(RenderMaterial);
    }

    void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        
        if (!Camera)
            Camera = Camera.main;
        _cameraTransform = Camera.transform;
        _updateKernel = ComputeShader.FindKernel("UpdateNodes");
        _createWidthKernel = ComputeShader.FindKernel("CreateWidth");
        
        _nodeBuffer = new ComputeBuffer(NodeBufferSize, Marshal.SizeOf(typeof(Node)));
        var nodeList = new List<Node>();
        for (int i = 0; i < TrunkNodeCount; i++)
        {
            nodeList.Add(new Node{bolt = 0, pos = Vector3.zero, time = (float)i/(TrunkNodeCount-1)});
        }

        for (int branch = 0; branch < BranchCount; branch++)
        {
            for (int branchNode = 0; branchNode < BranchNodeCount; branchNode++)
            {
                nodeList.Add(new Node{bolt = branch+1, pos = Vector3.zero, time = (float)branchNode/(BranchNodeCount-1)});
            }
        }
        
        _nodeBuffer.SetData(nodeList.ToArray());
        
        
        _boltBuffer = new ComputeBuffer(BranchCount + 1, Marshal.SizeOf(typeof(Bolt)));
        _branchEndpoints = new Vector3[BranchCount];
        _bolts = new Bolt[BranchCount + 1];
        _bolts[0] = new Bolt
        {
            startPos = StartPosition,
            endPos = EndPosition, 
            startTime = 0
        };
        
        _boltBuffer.SetData(_bolts);

        _vertexBuffer = new ComputeBuffer(VertexBufferSize, Marshal.SizeOf(typeof(Vertex))); // 1 node to 2 vtx(left,right)
        _vertexBuffer.SetData(Enumerable.Repeat(default(Vertex), _vertexBuffer.count).ToArray());

        var indexData = new int[IndexBufferSize];
        var index = 0;
        for (var iNode = 0; iNode < TrunkNodeCount - 1; iNode++)
        {
            var offset = iNode * 2;
            indexData[index++] = 0 + offset;
            indexData[index++] = 1 + offset;
            indexData[index++] = 2 + offset;
            indexData[index++] = 2 + offset;
            indexData[index++] = 1 + offset;
            indexData[index++] = 3 + offset;
        }
        for (int iBranch = 0; iBranch < BranchCount; iBranch++)
        {
            for (int iBranchNode = 0; iBranchNode < BranchNodeCount - 1; iBranchNode++)
            {
                var offset = iBranchNode * 2 + (TrunkNodeCount) * 2 + iBranch * (BranchNodeCount) * 2;
                indexData[index++] = 0 + offset;
                indexData[index++] = 1 + offset;
                indexData[index++] = 2 + offset;
                indexData[index++] = 2 + offset;
                indexData[index++] = 1 + offset;
                indexData[index++] = 3 + offset;
            }
        }

        _indexBuffer = new ComputeBuffer(indexData.Length, Marshal.SizeOf(typeof(uint))); // 1 node to 2 triangles(6vertexs)
        _indexBuffer.SetData(indexData);
    }

    public void StartAnimation()
    {
        Initialize();
        _animationStartTime = Time.time;
        _time = Random.value * 1000;
        _leaderComplete = false;
        _pulseComplete = false;
        
        for (int i = 0; i < BranchCount; i++)
        {
            _branchEndpoints[i] = Random.insideUnitSphere * BranchLength;
            _bolts[i+1] = new Bolt
            {
                startTime = Random.value * .8f
            };
        }
    }
    
    float fbm(float2 p)
    {
        float freq = NoiseFrequency, amp = NoiseAmplitude;
        float sum = 0;	
        for(int i = 0; i < 4; i++) 
        {
            sum += snoise(p * freq) * amp;
            freq *= NoiseLacunarity;
            amp *= NoiseGain;
        }
        return sum;
    }

    float3 fbm3(float2 p)
    {
        return float3(
            fbm(p),
            fbm(p+10),
            fbm(p+20));
    }

    void Update()
    {
        _time += Time.deltaTime * MorphSpeed;
        
        if(Animate)
        {
            var leaderLerp = (Time.time - _animationStartTime) / LeaderDuration;
            if (leaderLerp > 1 && !_leaderComplete)
            {
                OnLeaderComplete?.Invoke();
                _leaderComplete = true;
            }

            var pulseLerp = (Time.time - _animationStartTime - LeaderDuration) / PulseDuration;
            if (pulseLerp > 1 && !_pulseComplete)
            {
                OnPulseComplete?.Invoke();
                _pulseComplete = true;
            }
            ComputeShader.SetFloat("_StartWidth", leaderLerp < 1 ? LeaderWidth : StartWidth * PulseWidthCurve.Evaluate(pulseLerp)*PulseWidth);
            ComputeShader.SetFloat("_EndWidth", leaderLerp < 1 ? LeaderWidth : EndWidth * PulseWidthCurve.Evaluate(pulseLerp)*PulseWidth);
            
            _materialInstance.SetFloat("_LeaderProgress", leaderLerp);
            _materialInstance.SetFloat("_BranchIntensity", leaderLerp < 1 ? LeaderIntensity : PulseBranchIntensityCurve.Evaluate(pulseLerp) * PulseIntensity);
            _materialInstance.SetFloat("_Intensity", leaderLerp < 1 ? LeaderIntensity : PulseTrunkIntensityCurve.Evaluate(pulseLerp) * PulseIntensity);
        }
        else
        {
            ComputeShader.SetFloat("_StartWidth", StartWidth);
            ComputeShader.SetFloat("_EndWidth", EndWidth);
        }
        var diff = EndPosition - StartPosition;
        var dist = diff.magnitude;
        _bolts[0].startPos = StartPosition;
        _bolts[0].startOffset = -(Vector3) fbm3(float2(0, _time));
        _bolts[0].endPos = EndPosition;
        _bolts[0].endOffset = FixedEndpoint ? -(Vector3) fbm3(float2(dist, _time)) : Vector3.zero;

        for (int i = 0; i < BranchCount; i++)
        {
            var root = Vector3.Lerp(StartPosition, EndPosition, _bolts[i + 1].startTime);
            var rootDist = _bolts[i + 1].startTime * dist;
            _bolts[i + 1].startPos =
                root + (Vector3) fbm3(float2(rootDist, _time)) +
                _bolts[0].startOffset * (max(.5f - _bolts[i + 1].startTime, 0) * 2) +
                _bolts[0].endOffset * (max(_bolts[i + 1].startTime - .5f, 0) * 2);
            _bolts[i + 1].startOffset = -(Vector3) fbm3(float2(0, _time + (i + 1) * 10));
            _bolts[i + 1].endPos = root + _branchEndpoints[i];
        }
        
        _boltBuffer.SetData(_bolts);
        
        ComputeShader.SetBuffer(_updateKernel, "_NodeBuffer", _nodeBuffer);
        ComputeShader.SetBuffer(_updateKernel, "_BoltBuffer", _boltBuffer);
        
        ComputeShader.SetFloat("_Time", _time);
        ComputeShader.SetFloat("_NoiseAmplitude", NoiseAmplitude);
        ComputeShader.SetFloat("_NoiseFrequency", NoiseFrequency);
        ComputeShader.SetFloat("_NoiseGain", NoiseGain);
        ComputeShader.SetFloat("_NoiseLacunarity", NoiseLacunarity);
        
        ComputeShader.Dispatch(_updateKernel, NodeBufferSize/GROUP_SIZE, 1, 1);
        
        ComputeShader.SetInt("_TrunkNodeCount", TrunkNodeCount);
        ComputeShader.SetInt("_BranchNodeCount", BranchNodeCount);
        ComputeShader.SetVector("_CameraPos", _cameraTransform.position);
        
        ComputeShader.SetBuffer(_createWidthKernel, "_NodeBuffer", _nodeBuffer);
        ComputeShader.SetBuffer(_createWidthKernel, "_BoltBuffer", _boltBuffer);
        ComputeShader.SetBuffer(_createWidthKernel, "_VertexBuffer", _vertexBuffer);
        
        ComputeShader.Dispatch(_createWidthKernel, NodeBufferSize/GROUP_SIZE, 1, 1);
        
        _materialInstance.SetBuffer("_IndexBuffer", _indexBuffer);
        _materialInstance.SetBuffer("_VertexBuffer", _vertexBuffer);
        Graphics.DrawProcedural(
            _materialInstance, 
            new Bounds((EndPosition + StartPosition) / 2, (EndPosition - StartPosition).magnitude * Vector3.one), 
            MeshTopology.Triangles, 
            IndexBufferSize, 
            1, 
            Camera, 
            null, 
            ShadowCastingMode.Off, 
            false, 
            0);
        //
        // if(pulseLerp > 2)
        //     StartAnimation();
    }
    
    #region cleanup
    void OnDestroy()
    {
        if(_indexBuffer!=null)
        {
            _indexBuffer.Release();
            _boltBuffer.Release();
            _nodeBuffer.Release();
            _vertexBuffer.Release();
        }
    }
    #endregion
    
    public void OnDrawGizmosSelected()
    {
        if (DebugDrawNodes && _nodeBuffer != null)
        {
            Gizmos.color = Color.magenta;
            var data = new Node[_nodeBuffer.count];
            _nodeBuffer.GetData(data);
            foreach(var node in data)
            {
                Gizmos.DrawWireSphere(node.pos, StartWidth);
            };
        }

        if (DebugDrawVertices && _vertexBuffer != null)
        {
            Gizmos.color = Color.yellow;
            var data = new Vertex[_vertexBuffer.count];
            _vertexBuffer.GetData(data);

            var num = _vertexBuffer.count / 2;
            for(var i=0; i< num; ++i)
            {
                var v0 = data[2*i];
                var v1 = data[2*i +1];

                Gizmos.DrawLine(v0.pos, v1.pos);
            }
        }
    }
}
