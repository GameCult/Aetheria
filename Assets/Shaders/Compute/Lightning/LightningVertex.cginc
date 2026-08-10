#ifndef LIGHTNING_VERTEX_INCLUDED
#define LIGHTNING_VERTEX_INCLUDED

struct Vertex
{
    float3 pos;
    float2 uv;
};

inline Vertex GetDefaultVertex() {

    Vertex ret;
    ret.pos = (0).xxx;
    ret.uv = (-1).xx;

    return ret;
}


#endif // LIGHTNING_VERTEX_INCLUDED
