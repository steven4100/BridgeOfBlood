using UnityEngine;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>
	/// Shape type for spawn pattern fill and omission zones.
	/// </summary>
	public enum SpawnShapeType : byte
	{
		Circle = 0,
		Rectangle = 1,
		Triangle = 2  // equilateral
	}

	/// <summary>
	/// Serializable shape used for spawn pattern fill region and omission zones.
	/// Circle: center + size.x as radius (size.y unused).
	/// Rectangle: center + size (width/height) + rotationDegrees.
	/// Triangle: equilateral, center + size.x as circumradius + rotationDegrees.
	/// </summary>
	[System.Serializable]
	public struct SpawnShape
	{
		public SpawnShapeType type;
		public Vector2 center;
		/// <summary>Circle: x = radius. Rectangle: x = width, y = height. Triangle: x = circumradius (equilateral).</summary>
		public Vector2 size;
		/// <summary>Rotation in degrees (ignored for circle).</summary>
		public float rotationDegrees;

		/// <summary>Area of the shape in world units squared.</summary>
		public float GetArea()
		{
			switch (type)
			{
				case SpawnShapeType.Circle:
					float r = size.x;
					return r > 0f ? Mathf.PI * r * r : 0f;
				case SpawnShapeType.Rectangle:
					return size.x * size.y;
				case SpawnShapeType.Triangle:
					// Equilateral: side = circumradius * sqrt(3); area = (sqrt(3)/4) * side^2
					float circum = size.x;
					if (circum <= 0f) return 0f;
					float side = circum * Mathf.Sqrt(3f);
					return (Mathf.Sqrt(3f) / 4f) * side * side;
				default:
					return 0f;
			}
		}

		/// <summary>True if the point (in the same space as center) is inside the shape.</summary>
		public bool Contains(Vector2 point)
		{
			Vector2 local = point - center;
			switch (type)
			{
				case SpawnShapeType.Circle:
					return local.sqrMagnitude <= size.x * size.x;
				case SpawnShapeType.Rectangle:
					float rad = rotationDegrees * Mathf.Deg2Rad;
					float c = Mathf.Cos(-rad);
					float s = Mathf.Sin(-rad);
					Vector2 rotated = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
					float hw = size.x * 0.5f;
					float hh = size.y * 0.5f;
					return Mathf.Abs(rotated.x) <= hw && Mathf.Abs(rotated.y) <= hh;
				case SpawnShapeType.Triangle:
					return ContainsEquilateralTriangle(local, size.x, rotationDegrees);
				default:
					return false;
			}
		}

		/// <summary>Get a random point inside the shape. Caller provides RNG state (0-1).</summary>
		public Vector2 GetRandomPoint(float u, float v)
		{
			switch (type)
			{
				case SpawnShapeType.Circle:
					float r = size.x * Mathf.Sqrt(u);
					float theta = v * 2f * Mathf.PI;
					return center + new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
				case SpawnShapeType.Rectangle:
					float hw = size.x * 0.5f;
					float hh = size.y * 0.5f;
					Vector2 local = new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
					float rad = rotationDegrees * Mathf.Deg2Rad;
					float cos = Mathf.Cos(rad);
					float sin = Mathf.Sin(rad);
					return center + new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
				case SpawnShapeType.Triangle:
					// Equilateral: sample via barycentric then convert to world
					float s = Mathf.Sqrt(u);
					float t = v;
					float a = 1f - s;
					float b = s * (1f - t);
					float c2 = s * t;
					float circum = size.x;
					float angle = rotationDegrees * Mathf.Deg2Rad;
					Vector2 v0 = circum * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
					Vector2 v1 = circum * new Vector2(Mathf.Cos(angle + 2f * Mathf.PI / 3f), Mathf.Sin(angle + 2f * Mathf.PI / 3f));
					Vector2 v2 = circum * new Vector2(Mathf.Cos(angle + 4f * Mathf.PI / 3f), Mathf.Sin(angle + 4f * Mathf.PI / 3f));
					Vector2 p = a * v0 + b * v1 + c2 * v2;
					return center + p;
				default:
					return center;
			}
		}

		/// <summary>Get corners for rectangle (4 points) or triangle (3 points) for drawing. Circle returns center + radius for drawing as disc.</summary>
		public int GetCornerCount()
		{
			switch (type)
			{
				case SpawnShapeType.Circle: return 0;
				case SpawnShapeType.Rectangle: return 4;
				case SpawnShapeType.Triangle: return 3;
				default: return 0;
			}
		}

		public void GetCorners(Vector2[] buffer)
		{
			float rad = rotationDegrees * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);
			if (type == SpawnShapeType.Rectangle)
			{
				float hw = size.x * 0.5f;
				float hh = size.y * 0.5f;
				buffer[0] = center + Rotate(new Vector2(-hw, -hh), cos, sin);
				buffer[1] = center + Rotate(new Vector2(hw, -hh), cos, sin);
				buffer[2] = center + Rotate(new Vector2(hw, hh), cos, sin);
				buffer[3] = center + Rotate(new Vector2(-hw, hh), cos, sin);
			}
			else if (type == SpawnShapeType.Triangle)
			{
				float circum = size.x;
				for (int i = 0; i < 3; i++)
				{
					float angle = rad + i * 2f * Mathf.PI / 3f;
					buffer[i] = center + circum * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				}
			}
		}

		/// <summary>Returns the axis-aligned bounding box of this shape.</summary>
		public void GetBounds(out float minX, out float minY, out float maxX, out float maxY)
		{
			switch (type)
			{
				case SpawnShapeType.Circle:
					float r = size.x;
					minX = center.x - r; minY = center.y - r;
					maxX = center.x + r; maxY = center.y + r;
					break;
				case SpawnShapeType.Rectangle:
					float hw = size.x * 0.5f;
					float hh = size.y * 0.5f;
					float rad = rotationDegrees * Mathf.Deg2Rad;
					float ca = Mathf.Abs(Mathf.Cos(rad));
					float sa = Mathf.Abs(Mathf.Sin(rad));
					float ex = hw * ca + hh * sa;
					float ey = hw * sa + hh * ca;
					minX = center.x - ex; minY = center.y - ey;
					maxX = center.x + ex; maxY = center.y + ey;
					break;
				case SpawnShapeType.Triangle:
					float circum = size.x;
					minX = center.x - circum; minY = center.y - circum;
					maxX = center.x + circum; maxY = center.y + circum;
					break;
				default:
					minX = minY = maxX = maxY = 0f;
					break;
			}
		}

		static Vector2 Rotate(Vector2 v, float cos, float sin)
		{
			return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
		}

		static bool ContainsEquilateralTriangle(Vector2 local, float circumRadius, float rotationDegrees)
		{
			float angle = -rotationDegrees * Mathf.Deg2Rad;
			float cos = Mathf.Cos(angle);
			float sin = Mathf.Sin(angle);
			Vector2 r = new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
			// Vertices of equilateral centered at origin, first vertex at (circumRadius, 0)
			Vector2 a = circumRadius * new Vector2(1f, 0f);
			Vector2 b = circumRadius * new Vector2(-0.5f, 0.866025404f);  // sqrt(3)/2
			Vector2 c = circumRadius * new Vector2(-0.5f, -0.866025404f);
			return PointInTriangle(r, a, b, c);
		}

		static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
		{
			// Same-side test: p must be on the same side of each edge as the opposite vertex.
			return SameSide(p, a, b, c) && SameSide(p, b, c, a) && SameSide(p, c, a, b);
		}

		static bool SameSide(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float cp1 = ab.x * (p1.y - a.y) - ab.y * (p1.x - a.x);
			float cp2 = ab.x * (p2.y - a.y) - ab.y * (p2.x - a.x);
			return (cp1 >= 0f && cp2 >= 0f) || (cp1 <= 0f && cp2 <= 0f);
		}
	}
}
