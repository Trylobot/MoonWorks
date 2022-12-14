using System.Collections.Generic;
using MoonWorks.Math.Fixed;

namespace MoonWorks.Collision.Fixed
{
	/// <summary>
	/// A Circle is a shape defined by a radius.
	/// </summary>
	public struct Circle : IShape2D, System.IEquatable<Circle>
	{
		public Fix64 Radius { get; }
		public AABB2D AABB { get; }
		public IEnumerable<IShape2D> Shapes
		{
			get
			{
				yield return this;
			}
		}

		public Circle(Fix64 radius)
		{
			Radius = radius;
			AABB = new AABB2D(-Radius, -Radius, Radius, Radius);
		}

		public Circle(int radius)
		{
			Radius = (Fix64) radius;
			AABB = new AABB2D(-Radius, -Radius, Radius, Radius);
		}

		public Vector2 Support(Vector2 direction, Transform2D transform)
		{
			return Vector2.Transform(Vector2.Normalize(direction) * Radius, transform.TransformMatrix);
		}

		public AABB2D TransformedAABB(Transform2D transform2D)
		{
			return AABB2D.Transformed(AABB, transform2D);
		}

		public override bool Equals(object obj)
		{
			return obj is IShape2D other && Equals(other);
		}

		public bool Equals(IShape2D other)
		{
			return other is Circle circle && Equals(circle);
		}

		public bool Equals(Circle other)
		{
			return Radius == other.Radius;
		}

		public override int GetHashCode()
		{
			return System.HashCode.Combine(Radius);
		}

		public static bool operator ==(Circle a, Circle b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Circle a, Circle b)
		{
			return !(a == b);
		}
	}
}
