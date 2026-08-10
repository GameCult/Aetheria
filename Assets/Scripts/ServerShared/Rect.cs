using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

namespace Unity.Tiny
{
    /// <summary>
    /// 2D rectangle
    /// </summary>
    public struct Rect
    {
        /// <summary>
        /// Default unit rectangle.
        /// </summary>
        public static Rect Default { get; } = new Rect()
        {
            position = float2.zero,
            size = float2(1,1)
        };

        public Rect(float x, float y, float width, float height)
        {
            position = float2(x, y);
            size = float2(width, height);
        }

        public Rect(float2 position, float2 size)
        {
            this.position = position;
            this.size = size;
        }

        public bool IsEmpty()
        {
            return !(size.x > 0.0f && size.y > 0.0f);
        }

        /// <summary>
        /// Returns true if <paramref name="pos"/> is inside this rectangle.
        /// </summary>
        /// <remarks>
        /// The left and bottom edges are inclusive, while the right and top edges
        /// are exclusive. Also, while this class doesn't forbid 0 or negative
        /// width / heights, Contains() will be always be false in that case.
        /// </remarks>
        public bool Contains(float2 pos)
        {
            return pos.x >= position.x && pos.y >= position.y && pos.x < position.x + size.x && pos.y < position.y + size.y;
        }

        /// <summary>
        /// Returns a new rectangle translated and scaled by <paramref name="relative"/>.
        /// </summary>
        public Rect Region(in Rect relative)
        {
            return new Rect
            {
                position = float2(position.x + relative.position.x * size.x,position.y + relative.position.y * size.y) ,
                size = float2(size.x * relative.size.x, size.y * relative.size.y)
            };
        }
        
        public bool IntersectsWith(Rect r2)
        {
            return r2.position.x + r2.size.x >= position.x && r2.position.x <= position.x + size.x && 
                   r2.position.y + r2.size.y >= position.y && r2.position.y <= position.y + size.y;
        }

        public void Clamp(Rect r)
        {
            float x2 = position.x + size.x;
            float y2 = position.y + size.y;
            float rx2 = r.position.x + r.size.x;
            float ry2 = r.position.y + r.size.y;
            if (position.x < r.position.x) position.x = r.position.x;
            if (x2 > rx2) x2 = rx2;
            if (position.y < r.position.y) position.y = r.position.y;
            if (y2 > ry2) y2 = ry2;
            size.x = x2 - position.x;
            if (size.x < 0.0f) size.x = 0.0f;
            size.y = y2 - position.y;
            if (size.y < 0.0f) size.y = 0.0f;
        }

        public float2 position;
        public float2 size;

        /// <summary>
        /// The position of the center of the rectangle.
        /// </summary>
        public float2 Center
        {
            get => new float2(position.x + size.x / 2f, position.y + size.y/ 2f);
            set
            {
                position.x = value.x - size.x / 2f;
                position.y = value.y - size.y / 2f;
            }
        }

    }
}
