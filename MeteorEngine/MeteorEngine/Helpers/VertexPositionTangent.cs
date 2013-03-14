using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Vertex structure for normal mapped meshes
	/// </summary>

	public struct VertexPositionColorTextureNormal : IVertexType
	{
		public Vector3 Position;
		public Color Color;
		public Vector2 TextureCoordinate;
		public Vector3 Normal;

		public readonly static VertexDeclaration vertexDeclaration = new VertexDeclaration 
		(
			new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
			new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Color, 0),
			new VertexElement(sizeof(float) * 7, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(sizeof(float) * 9, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0)
		);

		public VertexDeclaration VertexDeclaration 
		{ 
			get { return vertexDeclaration; }
		}

		public VertexPositionColorTextureNormal(Vector3 position, Color color, Vector2 textureCoordinate, Vector3 normal)
		{
			Position = position;
			Color = color;
			TextureCoordinate = textureCoordinate;
			Normal = normal;
		}

		public VertexPositionColorTextureNormal(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
		{
			Position = position;
			Color = Color.White;
			TextureCoordinate = textureCoordinate;
			Normal = normal;
		}

		public static int SizeInBytes { get { return sizeof(float) * 12; } }
	}

	/// <summary>
	/// Vertex structure for normal mapped meshes
	/// </summary>

	public struct VertexPositionTangentToWorld : IVertexType
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector3 Tangent;

		public static VertexDeclaration vertexDeclaration = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
			new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
			new VertexElement(sizeof(float) * 6, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0)
		);

		public VertexDeclaration VertexDeclaration
		{
			get { return vertexDeclaration; }
		}

		public VertexPositionTangentToWorld(Vector3 position, Vector3 normal, Vector3 tangent)
		{
			Position = position;
			Normal = normal;
			Tangent = tangent;
		}

		public VertexPositionTangentToWorld(Vector3 position, Vector3 normal)
		{
			Position = position;
			Normal = normal;
			Tangent = Vector3.Zero;
		}

		public static int SizeInBytes { get { return sizeof(float) * 9; } }
	}
}
