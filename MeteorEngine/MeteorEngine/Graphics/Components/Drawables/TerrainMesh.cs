using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Mesh data for a particular terrain segment
	/// </summary>	

	public class TerrainMesh
	{
		/// Vertex buffer for mipmap
		private DynamicVertexBuffer patchVertexBuffer;
		public DynamicVertexBuffer Vertices
		{
			get { return patchVertexBuffer; }
		}

		/// <summary>
		/// Array for mipmap vertices. UpdatedVertices is used to count
		/// the vertices that have changed since the last frame, for quicker
		/// vertex buffer insertion.
		/// </summary>
		public VertexPositionTangentToWorld[] vertices { private set; get; }
		private ushort updatedVertices;
		public ushort UpdatedVertices
		{
			get { return updatedVertices; }
		}

		// Extents of this mesh
		short meshSize;

		/// <summary>
		/// Constructor to set up mesh
		/// </summary>

		public TerrainMesh(GraphicsDevice graphicsDevice, int mipLevel)
		{
			int scale = 1;
			for (int i = mipLevel; i > 0; i--)
				scale *= 2;

			meshSize = (short)(TerrainPatch.patchSize / scale + 1);

			// Create vertex buffer for this mipmap
			vertices = new VertexPositionTangentToWorld[meshSize * meshSize];
			patchVertexBuffer = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionTangentToWorld.vertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Update vertex data for this mesh.
		/// </summary>

		public void UpdateMesh(short[,] heightData, Vector2 offset, int mipLevel, ushort[] indices)
		{
			// Create vertex data
			SetUpVertices(heightData, offset, mipLevel);
			CalculateNormals(indices);

			// Set vertex and index buffers
			if (updatedVertices > 0)
				patchVertexBuffer.SetData(vertices, 0, updatedVertices);
		}

		/// <summary>
		/// Calculate the vertex positions that need to be updated.
		/// </summary>

		private void SetUpVertices(short[,] heightData, Vector2 mapOffset, int mipLevel)
		{
			int left = (int)mapOffset.X;
			int right = left + TerrainPatch.patchSize + 1;
			int top = (int)mapOffset.Y;
			int bottom = top + TerrainPatch.patchSize + 1;

			int index = 0;

			// Determine what vertices to skip (if any) for creating this mesh
			int next = 1;
			for (int i = mipLevel; i > 0; i--)
				next *= 2;

			for (int y = top; y < bottom; y += next)
			{
				for (int x = left; x < right; x += next)
				{
					float height = heightData[x, y] / 4.0f;
					vertices[index].Position = new Vector3(x, height, -y);

					updatedVertices++;
					index++;
				}
			}
			// Done creating vertices
		}

		/// <summary>
		/// Find the normals for each mesh vertex
		/// </summary>

		private void CalculateNormals(ushort[] indices)
		{
			for (int i = 0; i < vertices.Length; i++)
				vertices[i].Normal = new Vector3(0, 0, 0);

			for (int i = 0; i < indices.Length / 3; i++)
			{
				int index0 = indices[i * 3];
				int index1 = indices[i * 3 + 1];
				int index2 = indices[i * 3 + 2];

				Vector3 side1 = vertices[index0].Position - vertices[index2].Position;
				Vector3 side2 = vertices[index0].Position - vertices[index1].Position;
				Vector3 normal = Vector3.Cross(side1, side2);

				vertices[index0].Normal += normal;
				vertices[index1].Normal += normal;
				vertices[index2].Normal += normal;
			}

			// Correct normalization
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i].Normal.Normalize();

				Vector3 c1 = Vector3.Cross(vertices[i].Normal, Vector3.UnitZ);
				Vector3 c2 = Vector3.Cross(vertices[i].Normal, Vector3.UnitY);
				Vector3 tangent = Vector3.Zero;

				// Calculate tangent
				tangent = (Vector3.Distance(c1, Vector3.Zero) > Vector3.Distance(c2, Vector3.Zero)) ? c1 : c2;
				vertices[i].Tangent = tangent;
			}
		}
	}
}