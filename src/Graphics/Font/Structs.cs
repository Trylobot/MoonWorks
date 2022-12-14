using System.Runtime.InteropServices;
using MoonWorks.Math.Float;

namespace MoonWorks.Graphics.Font
{
	[StructLayout(LayoutKind.Sequential)]
	public struct FontRange
	{
		public uint FirstCodepoint;
		public uint NumChars;
		public byte OversampleH;
		public byte OversampleV;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vertex
	{
		public Vector3 Position;
		public Vector2 TexCoord;
		public Color Color;
	}
}
