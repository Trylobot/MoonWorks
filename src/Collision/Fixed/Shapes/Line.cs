using System.Collections.Generic;
using MoonWorks.Math.Fixed;

namespace MoonWorks.Collision.Fixed
{
	/// <summary>
    /// A line is a shape defined by exactly two points in space.
    /// </summary>
    public struct Line : IShape2D, System.IEquatable<Line>
    {
        public Vector2 Start { get; }
        public Vector2 End { get; }

        public AABB2D AABB { get; }

		public IEnumerable<IShape2D> Shapes
        {
            get
            {
				yield return this;
			}
        }

		public Line(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;

            AABB = new AABB2D(
				Fix64.Min(Start.X, End.X),
				Fix64.Min(Start.Y, End.Y),
				Fix64.Max(Start.X, End.X),
				Fix64.Max(Start.Y, End.Y)
			);
        }

        public Vector2 Support(Vector2 direction, Transform2D transform)
        {
            var transformedStart = Vector2.Transform(Start, transform.TransformMatrix);
            var transformedEnd = Vector2.Transform(End, transform.TransformMatrix);
            return Vector2.Dot(transformedStart, direction) > Vector2.Dot(transformedEnd, direction) ?
                transformedStart :
                transformedEnd;
        }

        public AABB2D TransformedAABB(Transform2D transform)
        {
            return AABB2D.Transformed(AABB, transform);
        }

        public override bool Equals(object obj)
        {
            return obj is IShape2D other && Equals(other);
        }

        public bool Equals(IShape2D other)
        {
            return other is Line otherLine && Equals(otherLine);
        }

        public bool Equals(Line other)
        {
            return
				(Start == other.Start && End == other.End) ||
				(End == other.Start && Start == other.End);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Start, End);
        }

        public static bool operator ==(Line a, Line b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Line a, Line b)
        {
            return !(a == b);
        }
    }
}
