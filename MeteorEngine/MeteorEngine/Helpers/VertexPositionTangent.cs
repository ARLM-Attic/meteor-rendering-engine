using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Vertex structure for normal mapped meshes
	/// </summary>

	public struct VertexPositionTangentToWorld
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector2 TextureCoordinate;
		public Vector3 Tangent;
		public Vector3 Binormal;

		public static VertexDeclaration terrainVertexDeclaration = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
			new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
			new VertexElement(sizeof(float) * 6, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(sizeof(float) * 8, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
			new VertexElement(sizeof(float) * 11, VertexElementFormat.Vector3, VertexElementUsage.Binormal, 0)
		);

		public VertexPositionTangentToWorld(Vector3 position, Vector3 normal, Vector2 textureCoordinate,
			Vector3 tangent, Vector3 binormal)
		{
			Position = position;
			Normal = normal;
			TextureCoordinate = textureCoordinate;
			Tangent = tangent;
			Binormal = binormal;
		}

		public VertexPositionTangentToWorld(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
		{
			Position = position;
			Normal = normal;
			TextureCoordinate = textureCoordinate;
			Tangent = Vector3.Zero;
			Binormal = Vector3.Zero;
		}

		public static int SizeInBytes { get { return sizeof(float) * 14; } }
	}
}
