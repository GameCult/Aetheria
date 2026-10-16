// The minimap and map screen's belt asteroids: one instanced draw per belt, six vertices per asteroid expanded to a
// quad on the map plane. _Belt holds the belt's per-frame float4s (x, y planar, z spin in degrees, w size); the sprite
// cell is a hash of the instance id, so a belt keeps its sprites without any per-asteroid CPU state.
Shader "Aetheria/MapBelt"
{
    Properties { _MainTex ("Sprite Sheet", 2D) = "white" {} _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            StructuredBuffer<float4> _Belt;
            float4 _Sheet;
            float _PlaneY;
            float _AsteroidSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            uint Hash(uint x)
            {
                x = x * 747796405u + 2891336453u;
                x = ((x >> ((x >> 28u) + 4u)) ^ x) * 277803737u;
                return (x >> 22u) ^ x;
            }

            v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                // Triangles (0,1,3) and (3,1,2) over the corners bottom left, top left, top right, bottom right.
                static const uint corner[6] = { 0, 1, 3, 3, 1, 2 };
                static const float2 offsets[4] = { float2(-1, -1), float2(-1, 1), float2(1, 1), float2(1, -1) };
                float4 a = _Belt[iid];
                float2 o = offsets[corner[vid]];
                float theta = radians(a.z);
                float s = sin(theta), c = cos(theta);
                float2 q = o * a.w * _AsteroidSize;
                float3 world = float3(a.x + q.x * c + q.y * s, _PlaneY, a.y - q.x * s + q.y * c);
                uint cell = Hash(iid) % (uint)(_Sheet.x * _Sheet.y);
                float2 size = float2(1.0 / _Sheet.x, 1.0 / _Sheet.y);
                float2 origin = float2(cell % (uint)_Sheet.x, cell / (uint)_Sheet.x) * size;
                v2f v;
                v.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                v.uv = origin + (o * .5 + .5) * size;
                return v;
            }

            fixed4 frag(v2f v) : SV_Target { return tex2D(_MainTex, v.uv) * _Color; }
            ENDCG
        }
    }
}
