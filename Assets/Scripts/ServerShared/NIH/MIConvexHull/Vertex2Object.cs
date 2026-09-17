using CultMath;

public class Vertex2<T> : Vertex2
{
    public T StoredObject { get; }
    
    public Vertex2(float x, float y, T obj) : base(x, y)
    {
        StoredObject = obj;
    }

    public Vertex2(float2 pos, T obj) : base(pos)
    {
        StoredObject = obj;
    }
}