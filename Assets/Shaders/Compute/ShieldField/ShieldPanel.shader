Shader "ShieldField/Panel"
{
    Properties
    {
        _FillColorA     ("Fill Tint A",    Color) = (0.15, 0.45, 0.9, 0.28)
        _FillColorB     ("Fill Tint B",    Color) = (0.30, 0.20, 0.8, 0.28)
        _LineColor      ("Outline",        Color) = (0.6, 0.9, 1.0, 1)
        _CrackColor     ("Crack Emission", Color) = (1.0, 0.55, 0.25, 1)
        _CompressColor  ("Compression",    Color) = (0.1, 0.3, 1.0, 1)
        _TensionColor   ("Tension",        Color) = (1.0, 0.35, 0.15, 1)
        _ConduitColor   ("Conduit",        Color) = (0.35, 0.8, 1.0, 1)

        _LineWidthPx    ("Outline px",     Range(0.5, 8)) = 1.5
        _CrackWidthPx   ("Crack px",       Range(0.5, 8)) = 1.0
        _ConduitWidthPx ("Conduit px",     Range(0.5, 8)) = 1.0
        _FeatherPx      ("Feather px",     Range(0.5, 4)) = 1.2

        _StrainScale    ("In-plane strain",  Float) = 0.35
        _RippleScale    ("Out-of-plane",     Float) = 0.30
        _StressColorGain("Stress emission",  Float) = 1.6
        _CrackGap       ("Pre-break gap",    Float) = 0.10
        _ShardSpread    ("Shard divergence", Float) = 1.40
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One One            // additive; fragments output premultiplied colour
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "ShieldCommon.hlsl"

        StructuredBuffer<CellStatic> _Cells;
        StructuredBuffer<float2>     _Verts;
        StructuredBuffer<DualEdge>   _Edges;
        StructuredBuffer<CellState>  _State;
        StructuredBuffer<float>      _U;
        StructuredBuffer<float>      _EdgeFlow;
        StructuredBuffer<uint>       _Visible;

        float4x4 _PanelToWorld;
        float    _Time01;          // absolute sim time; _Time.y is unreliable for indirect draws
        float    _CrackStart;
        float    _ShardLife;
        uint     _MaxShards;

        float4 _FillColorA, _FillColorB, _LineColor, _CrackColor;
        float4 _CompressColor, _TensionColor, _ConduitColor;
        float  _LineWidthPx, _CrackWidthPx, _ConduitWidthPx, _FeatherPx;
        float  _StrainScale, _RippleScale, _StressColorGain, _CrackGap, _ShardSpread;

        struct Ctx
        {
            CellStatic c;
            CellState  s;
            float  u;
            float  age;        // seconds since break, 0 while intact
            float  fade;
            uint   nSplits;
            float  fillT, lineT;
        };

        Ctx GetCtx(uint cell)
        {
            Ctx x;
            x.c = _Cells[cell];
            x.s = _State[cell];
            x.u = _U[cell];

            x.lineT = saturate(x.s.growth / 0.55);
            x.fillT = saturate((x.s.growth - 0.30) / 0.70);

            bool broken = x.s.breakTime >= 0.0;
            x.age  = broken ? max(_Time01 - x.s.breakTime, 0.0) : 0.0;
            x.fade = broken ? saturate(1.0 - x.age / max(_ShardLife, 1e-3)) : 1.0;

            x.nSplits = ShardCount(x.c.splitSeed, x.s.damage, x.s.released,
                                   x.c.vertCount, _CrackStart, _MaxShards);
            return x;
        }

        // Panel-space position of one corner of one fan triangle.
        // corner 0 = centroid, 1 = vert[tri], 2 = vert[tri+1].
        float3 CellPoint(Ctx x, uint tri, uint corner)
        {
            uint n = max(x.c.vertCount, 1u);
            float2 p = (corner == 0)
                ? x.c.center
                : _Verts[x.c.vertStart + ((tri + corner - 1) % n)];

            float2 local = p - x.c.center;

            // materialisation: the cell inflates from its own centroid
            local *= EaseOutBack(x.fillT);

            // the stress wave is visible in the geometry itself: tension dilates
            // the cell, compression pinches it
            local *= (1.0 + x.u * _StrainScale);

            float z = x.u * _RippleScale;

            if (x.nSplits > 0)
            {
                uint startTri;
                uint sid = ShardOf(x.c.splitSeed, x.nSplits, n, tri, startTri);

                float2 outward = _Verts[x.c.vertStart + startTri] - x.c.center;
                outward = normalize(outward + float2(1e-5, 1e-5));

                uint  sSeed = x.c.splitSeed ^ (sid * 0x9E3779B9u);
                float2 jit  = Hash2(sSeed) - 0.5;

                if (x.s.breakTime < 0.0)
                {
                    // cracked but holding: shards ease apart just enough to open
                    // the chord lines
                    float gap = saturate((x.s.damage - _CrackStart) / 0.35);
                    local += outward * gap * _CrackGap * x.c.radius;
                }
                else
                {
                    float2 drift = (outward + jit * 0.9) * x.age * _ShardSpread;
                    local  = Rot2(local, x.s.rot * (0.5 + jit.y));
                    local += drift * x.c.radius;
                    z     += jit.x * x.age * 0.4;
                }
            }

            float3 pos = float3(x.c.center + local, z) + x.s.pos;
            return mul(_PanelToWorld, float4(pos, 1.0)).xyz;
        }

        float3 StressEmission(float u, float glow)
        {
            float compress = saturate(-u * _StressColorGain);
            float tension  = saturate( u * _StressColorGain);
            return _CompressColor.rgb * compress
                 + _TensionColor.rgb  * tension
                 + _CrackColor.rgb    * glow;
        }

        // ----------------------------------------------------------
        // Screen-space line expansion. Both endpoints are projected, the
        // perpendicular is taken in pixels, and the offset is scaled by w so it
        // survives the perspective divide. Width is therefore exact in pixels at
        // any distance or field of view.
        // ----------------------------------------------------------
        struct LineVary
        {
            float4 pos : SV_POSITION;
            noperspective float dist : TEXCOORD0;   // signed pixels from the line centre
            float  halfPx : TEXCOORD1;
            float4 col    : TEXCOORD2;
        };

        LineVary BuildLine(float3 wa, float3 wb, uint k, float widthPx, float4 col)
        {
            LineVary o;
            float4 ca = UnityWorldToClipPos(wa);
            float4 cb = UnityWorldToClipPos(wb);

            // behind the near plane on either end: collapse to a zero-area tri
            if (ca.w <= 1e-4 || cb.w <= 1e-4 || col.a <= 0.001)
            {
                o.pos = float4(0, 0, 0, 1);
                o.dist = 0; o.halfPx = 1; o.col = 0;
                return o;
            }

            float2 halfRes = _ScreenParams.xy * 0.5;
            float2 sa = ca.xy / ca.w * halfRes;
            float2 sb = cb.xy / cb.w * halfRes;

            float2 d = sb - sa;
            float  L = length(d);
            d = (L > 1e-5) ? d / L : float2(1, 0);
            float2 nrm = float2(-d.y, d.x);

            float hw  = widthPx * 0.5 + _FeatherPx;
            float ext = hw;                       // overshoot the ends so joins close

            // 6 vertices: (end, side) pairs forming two triangles
            static const uint2 QUAD[6] = { uint2(0,0), uint2(1,0), uint2(0,1),
                                           uint2(0,1), uint2(1,0), uint2(1,1) };
            uint2 q = QUAD[k];
            float side = q.y * 2.0 - 1.0;
            float along = q.x * 2.0 - 1.0;

            float4 cp = q.x ? cb : ca;
            float2 offPx = nrm * side * hw + d * along * ext;

            cp.xy += offPx / halfRes * cp.w;

            o.pos    = cp;
            o.dist   = side * hw;
            o.halfPx = widthPx * 0.5;
            o.col    = col;
            return o;
        }

        float4 ShadeLine(LineVary i) : SV_Target
        {
            float a = 1.0 - smoothstep(i.halfPx - _FeatherPx * 0.5,
                                       i.halfPx + _FeatherPx * 0.5, abs(i.dist));
            a *= i.col.a;
            clip(a - 0.002);
            return float4(i.col.rgb * a, a);
        }
        ENDHLSL

        // ==========================================================
        // Pass 0 — fill
        // ==========================================================
        Pass
        {
            Name "Fill"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            struct V2F
            {
                float4 pos : SV_POSITION;
                float4 col : TEXCOORD0;
            };

            V2F vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                V2F o;
                uint cell = _Visible[iid];
                Ctx  x    = GetCtx(cell);

                uint tri = vid / 3, corner = vid % 3;
                if (tri >= x.c.vertCount || x.fillT <= 0.0 || x.fade <= 0.0)
                {
                    o.pos = float4(0, 0, 0, 1); o.col = 0; return o;
                }

                float3 w = CellPoint(x, tri, corner);
                o.pos = UnityWorldToClipPos(w);

                float4 base = lerp(_FillColorA, _FillColorB, (x.c.tileType & 1) ? 1.0 : 0.0);
                float  a    = base.a * saturate(x.fillT) * x.fade;

                // a depleted panel reads dimmer; temper is the visible charge
                a *= lerp(0.45, 1.0, saturate(x.s.temper));

                float3 rgb = base.rgb + StressEmission(x.u, x.s.glow);
                o.col = float4(rgb, a);
                return o;
            }

            float4 frag(V2F i) : SV_Target
            {
                clip(i.col.a - 0.002);
                return float4(i.col.rgb * i.col.a, i.col.a);
            }
            ENDHLSL
        }

        // ==========================================================
        // Pass 1 — outline. Perimeter edges plus the radial chords that
        // separate shard groups. Chords fade in with damage well before the
        // shards actually part, which is what produces the spidered-but-holding
        // state between weak hits.
        // ==========================================================
        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            LineVary vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                uint cell = _Visible[iid];
                Ctx  x    = GetCtx(cell);

                uint seg = vid / 6, k = vid % 6;
                uint n   = x.c.vertCount;

                if (seg >= n * 2 || x.lineT <= 0.0 || x.fade <= 0.0)
                    return BuildLine(0, 0, k, 1, 0);

                bool  radial = seg >= n;
                uint  ei     = radial ? seg - n : seg;
                float3 wa, wb;
                float4 col;
                float  width;

                if (radial)
                {
                    bool chord = (x.nSplits > 0) && IsChord(x.c.splitSeed, x.nSplits, n, ei);
                    float a = chord ? saturate((x.s.damage - _CrackStart) / 0.25) : 0.0;
                    a *= x.fade;

                    wa = CellPoint(x, ei, 0);
                    wb = CellPoint(x, ei, 1);
                    col = float4(_CrackColor.rgb * (1.0 + x.s.glow * 2.0), _CrackColor.a * a);
                    width = _CrackWidthPx;
                }
                else
                {
                    // during materialisation the ring draws itself on edge by edge
                    float a = saturate(x.lineT * (float)n - (float)ei) * x.fade;

                    wa = CellPoint(x, ei, 1);
                    wb = CellPoint(x, ei, 2);
                    col = float4(_LineColor.rgb + StressEmission(x.u, x.s.glow) * 0.7,
                                 _LineColor.a * a);
                    width = _LineWidthPx;
                }

                return BuildLine(wa, wb, k, width, col);
            }

            float4 frag(LineVary i) : SV_Target { return ShadeLine(i); }
            ENDHLSL
        }

        // ==========================================================
        // Pass 2 — dual-graph conduits. Centroid-to-centroid arcs lit by the
        // stress gradient flowing across them, so you can watch the wave route
        // through the lattice and reflect off broken regions.
        // ==========================================================
        Pass
        {
            Name "Conduit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            float _ConduitGain;

            LineVary vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                uint cell = _Visible[iid];
                Ctx  x    = GetCtx(cell);

                uint seg = vid / 6, k = vid % 6;
                if (seg >= x.c.nbrCount || x.s.breakTime >= 0.0 || x.s.growth < 0.6)
                    return BuildLine(0, 0, k, 1, 0);

                uint ei = x.c.nbrStart + seg;
                DualEdge e = _Edges[ei];
                if (e.nbr <= cell) return BuildLine(0, 0, k, 1, 0);   // draw each arc once

                CellState ns = _State[e.nbr];
                if (ns.breakTime >= 0.0 || ns.growth < 0.6) return BuildLine(0, 0, k, 1, 0);

                float flow = saturate(_EdgeFlow[ei] * _ConduitGain);
                float a = flow * min(x.s.growth, ns.growth);
                if (a <= 0.003) return BuildLine(0, 0, k, 1, 0);

                CellStatic nc = _Cells[e.nbr];
                float3 wa = mul(_PanelToWorld,
                                float4(x.c.center, x.u * _RippleScale, 1)).xyz + x.s.pos;
                float3 wb = mul(_PanelToWorld,
                                float4(nc.center, _U[e.nbr] * _RippleScale, 1)).xyz + ns.pos;

                float4 col = float4(_ConduitColor.rgb * (0.4 + flow * 2.0), _ConduitColor.a * a);
                return BuildLine(wa, wb, k, _ConduitWidthPx, col);
            }

            float4 frag(LineVary i) : SV_Target { return ShadeLine(i); }
            ENDHLSL
        }
    }
    Fallback Off
}
