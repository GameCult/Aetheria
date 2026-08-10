#pragma multi_compile __ FLOW_GLOBAL 
#pragma multi_compile __ FLOW_SLOPE
#pragma multi_compile __ NOISE_SLOPE

#include "UnityCG.cginc"

uniform sampler2D _NebulaSurfaceHeight;
float4 _NebulaSurfaceHeight_TexelSize;
uniform sampler2D _NebulaPatch;
uniform sampler2D _NebulaPatchHeight;
uniform sampler2D _NebulaTint;
float4 _NebulaTint_TexelSize;
uniform sampler2D _FluidVelocity;

uniform float3 _GridTransform;
uniform float3 _FluidTransform;

uniform half _NebulaFillDensity,
            _NebulaFillDistance,
            _NebulaFillExponent,
            _NebulaFillOffset,
            _NebulaPatchDensity,
            _NebulaFloorOffset,
            _NebulaFloorBlend,
            _NebulaPatchBlend,
            _NebulaLuminance,
            _TintExponent,
            _TintLodExponent,
            _NebulaNoiseScale,
            _NebulaNoiseExponent,
            _NebulaNoiseAmplitude,
            _NebulaNoiseSpeed,
            _NebulaNoiseSlopeExponent,
            _FlowScale,
            _FlowAmplitude,
            _FlowScroll,
            _FlowPeriod,
            _FlowSlopeAmplitude,
            _FlowSwirlAmplitude,
            _SafetyDistance,
            _DynamicLodLow,
            _DynamicLodHigh,
            _DynamicIntensity,
            _DynamicSkyBoost;

inline float tri(in float x){return abs(frac(x)-.5);}
inline float3 tri3(in float3 p){return float3( tri(p.z+tri(p.y*1.)), tri(p.z+tri(p.x*1.)), tri(p.y+tri(p.x*1.)));}

float triNoise3d(in float3 p)
{
    float z=1.4;
    float rz = 0.001;
    float3 bp = p;
    for (float i=0.; i<=1.; i++ )
    {
        float3 dg = tri3(bp * 2.);
        p += dg + _Time.y * _NebulaNoiseSpeed;

        bp *= 1.8;
        z *= 1.5;
        p *= 1.2;
	        
        rz+= tri(p.z+tri(p.x+tri(p.y)))/z;
        bp += 0.14;
    }
    return rz;
}

float3 Tri3D( float3 P )
{
    float3 t1 = normalize(tri3(P));
    float3 t2 = normalize(tri3(P * 1.61803398875));
    return cross(t1,t2);
}

float parabola( float x, float k )
{
    return pow( 4.0*x*(1.0-x), k );
}

float2 getUV(float2 pos)
{
    return -(pos-_GridTransform.xy)/_GridTransform.z + float2(.5,.5);
}

float2 getUVFluid(float2 pos)
{
    return (pos-_FluidTransform.xy)/_FluidTransform.z + float2(.5,.5);
}

float cloudDensity(float3 pos, float surfaceDisp)
{
    float2 uv = getUV(pos.xz);
    const float dist = pos.y + surfaceDisp - _NebulaFloorOffset;
    const float patch = tex2Dlod(_NebulaPatch, half4(uv, 0, 0)).r;
    const float patchDisp = tex2Dlod(_NebulaPatchHeight, half4(uv, 0, 0)).r;

    const float patchDensity = saturate((-abs(pos.y+patchDisp - _NebulaFloorOffset)+patch)/_NebulaPatchBlend)*_NebulaPatchDensity;
    const float floorDist = -dist;
    const float floorDensity = floorDist/_NebulaFloorBlend;
    return patchDensity + max(0,floorDensity);
}

// TODO: Get this working?
float2 texFlow(float3 pos)
{
    const float2 fluidUv = getUVFluid(pos.xz);
    if(any(fluidUv<0)||any(fluidUv>1)) return 0;
    const float2 fluidSample = tex2Dlod(_FluidVelocity, half4(fluidUv, 0, 0)).xy;
    // TODO: remove magic number for fluid texture dimensions
    return float3(fluidSample.x,0,fluidSample.y) * (512 / _FluidTransform.z);
}

float2 gravGradient (float2 uv)
{
    return float2(
        tex2Dlod(_NebulaSurfaceHeight, float4(uv.x + _NebulaSurfaceHeight_TexelSize.x, uv.y, 0, 0)).r -
        tex2Dlod(_NebulaSurfaceHeight, float4(uv.x - _NebulaSurfaceHeight_TexelSize.x, uv.y, 0, 0)).r,
        tex2Dlod(_NebulaSurfaceHeight, float4(uv.x, uv.y + _NebulaSurfaceHeight_TexelSize.y, 0, 0)).r -
        tex2Dlod(_NebulaSurfaceHeight, float4(uv.x, uv.y - _NebulaSurfaceHeight_TexelSize.y, 0, 0)).r
    );
}

float3 gravNormal (float2 grad)
{
    float3 normal = float3(-grad.x, _NebulaSurfaceHeight_TexelSize.x*2*_GridTransform.z, -grad.y);
    return normalize(normal);
}

float3 globalFlow(float3 pos)
{
    float3 flowSample = Tri3D( pos / _FlowScale - float3(0,_FlowScroll,0) ) * _FlowAmplitude;
    flowSample.y *= .5;
    return flowSample;
}

float3 slopeFlow(float2 grad)
{
    float2 ngrad = normalize(grad);
    return float3(ngrad.y, 0, -ngrad.x) * _FlowSwirlAmplitude +  float3(ngrad.x, 0, ngrad.y) * _FlowSlopeAmplitude;
}

float3 slopeFlow(float3 pos)
{
    return slopeFlow(gravGradient(getUV(pos.xz)));
}

float3 flow(float3 pos)
{
    float3 flow = 0;
    
    #if FLOW_GLOBAL
    flow += globalFlow(pos);
    #endif

    #if FLOW_SLOPE
    flow += slopeFlow(pos);
    #endif
    
    return flow;
}

float tri2(in float x){return 1-2*abs(frac(x)-.5);}

float density(float3 pos)
{
    const float2 uv = getUV(pos.xz);
    const float surfaceDisp = tex2Dlod(_NebulaSurfaceHeight, half4(uv, 0, 0)).r;
    const float dist = pos.y + surfaceDisp;
    float d = pow(smoothstep(0, _NebulaFillDistance, abs(dist+_NebulaFillOffset)),-_NebulaFillExponent) * _NebulaFillDensity;
    if(dist < _SafetyDistance)
    {
        float heightFade = smoothstep(_SafetyDistance,_SafetyDistance*.75,dist);
        float exponent = _NebulaNoiseExponent;
        
        #if NOISE_SLOPE
        const float2 grad = gravGradient(uv);
        const float3 normal = gravNormal(grad);
        float flatness = 1 - dot(normal,float3(0,1,0));
        flatness = pow(flatness,_NebulaNoiseSlopeExponent);
        heightFade *= flatness;
        exponent *= flatness;
        #endif

        #if FLOW_GLOBAL || FLOW_SLOPE
        float3 fl = 0;
        
        #if FLOW_GLOBAL
        fl += globalFlow(pos);
        #endif
        
        #if FLOW_SLOPE
        #if NOISE_SLOPE
        fl += slopeFlow(grad);
        #else
        fl += slopeFlow(pos);
        #endif
        #endif
        
        const float lerp1 = frac(_Time.y / _FlowPeriod);
        const float lerp2 = frac(_Time.y / _FlowPeriod + .5);
        const float noise1 = pow(triNoise3d((pos+fl.xyz * (lerp1 - .5) * _FlowPeriod) / _NebulaNoiseScale),exponent) * tri2(lerp1);
        const float noise2 = pow(triNoise3d((pos+fl.xyz * (lerp2 - .5) * _FlowPeriod) / _NebulaNoiseScale),exponent) * tri2(lerp2);
        pos.y += (noise1 + noise2) * heightFade * _NebulaNoiseAmplitude;
        const float lerp3 = frac(_Time.y / _FlowPeriod * 2 + .25);
        const float lerp4 = frac(_Time.y / _FlowPeriod * 2 + .75);
        const float noise3 = pow(triNoise3d((pos+fl.xyz * (lerp3 - .5) * _FlowPeriod / 2) / _NebulaNoiseScale * 8), exponent) * tri2(lerp3);
        const float noise4 = pow(triNoise3d((pos+fl.xyz * (lerp4 - .5) * _FlowPeriod / 2) / _NebulaNoiseScale * 8), exponent) * tri2(lerp4);
        pos.y -= (noise3 + noise4) * heightFade * _NebulaNoiseAmplitude / 2;
        
        #else
        
        const float noise1 = pow(triNoise3d(pos / _NebulaNoiseScale), exponent) * _NebulaNoiseAmplitude;
        const float noise2 = pow(triNoise3d(pos / _NebulaNoiseScale * 8), exponent) * _NebulaNoiseAmplitude / 2;
        pos.y += (noise1 - noise2) * heightFade;
        
        #endif
        d += cloudDensity(pos, surfaceDisp);
    }
    return d;
}

float4 VolumeSampleColor(float3 pos)
{
	float d = density(pos);
    float2 uv = getUV(pos.xz);
    float3 tint = tex2Dlod(_NebulaTint, float4(uv.x, uv.y, 0, pow(max(.01,d), _TintLodExponent)));
    return float4(tint, d);
}

float2 tintGradient (float2 uv)
{
    return float2(
        length(tex2Dlod(_NebulaTint, float4(uv.x + _NebulaTint_TexelSize.x, uv.y, 0, 0))) - length(tex2Dlod(_NebulaTint, float4(uv.x - _NebulaTint_TexelSize.x, uv.y, 0, 0))),
        length(tex2Dlod(_NebulaTint, float4(uv.x, uv.y + _NebulaTint_TexelSize.y, 0, 0))) - length(tex2Dlod(_NebulaTint, float4(uv.x, uv.y - _NebulaTint_TexelSize.y, 0, 0)))
    );
}
        
float3 VolumeSampleColorSimple(float3 pos, float3 normal)
{
    float2 uv = getUV(pos.xz);
    float3 low = tex2Dlod(_NebulaTint, float4(uv.x, uv.y, 0, _DynamicLodLow)).rgb;
    float3 high = tex2Dlod(_NebulaTint, float4(uv.x, uv.y, 0, _DynamicLodHigh)).rgb * _DynamicSkyBoost;
    float upness = dot(normal, float3(0,1,0));
    //float lambert = 2 - dot(normalize(normal.xz), normalize(tintGradient(uv)));
    return lerp(low,high,sqrt((upness+1)/2)) * _DynamicIntensity / (density(pos)+1);
}
