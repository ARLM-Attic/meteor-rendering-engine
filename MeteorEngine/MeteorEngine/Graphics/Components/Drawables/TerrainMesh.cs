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
		public VertexPositionNormal[] vertices { private set; get; }
		private ushort updatedVertices;
		public ushort UpdatedVertices
		{
			get { return updatedVertices; }
		}

		// Terrain patch that this mesh belongs to.
		TerrainPatch terrainPatch;

		// Extents of this mesh
		short meshSize;

		// Level of detail for this mesh
		int mipLevel;

		/// <summary>
		/// Constructor to set up mesh
		/// </summary>

		public TerrainMesh(GraphicsDevice graphicsDevice, TerrainPatch patch, int level)
		{
			terrainPatch = patch;
			mipLevel = level;

			int scale = 1;
			for (int i = mipLevel; i > 0; i--)
				scale *= 2;

			meshSize = (short)(TerrainPatch.patchSize / scale + 1);

			// Create vertex buffer for this mipmap
			vertices = new VertexPositionNormal[meshSize * meshSize];
			patchVertexBuffer = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionNormal.vertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Update vertex data for this mesh.
		/// </summary>

		public void UpdateMesh(ushort[,] heightData, Vector2 offset, int mipLevel, ushort[] indices)
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

		private void SetUpVertices(ushort[,] heightData, Vector2 mapOffset, int mipLevel)
		{
			int left = (int)mapOffset.X * TerrainPatch.patchSize;
			int right = left + TerrainPatch.patchSize + 1;
			int top = (int)mapOffset.Y * TerrainPatch.patchSize;
			int bottom = top + TerrainPatch.patchSize + 1;

			int index = 0;

			// Determine what vertices to skip (if any) for creating this mesh
			int next = 1;
			for (int m = mipLevel; m > 0; m--)
				next *= 2;

			for (int y = top, i = 0; y < bottom; y += next, i += next)
			{
				for (int x = left, j = 0; x < right; x += next, j += next)
				{
					float height = heightData[x, y] / 256f;
					vertices[index].Position = new Vector3(j, height, -i);

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
				
				// Fix side edges
				if (i % meshSize == meshSize - 1 && terrainPatch.mapOffset.X < Terrain.gridSize.X - 1)
				{
					int adjacent = i - (meshSize - 1);
					TerrainMesh adjacentMesh = terrainPatch.neighbors[3].Meshes[mipLevel];
					vertices[i].Normal = adjacentMesh.vertices[adjacent].Normal;
				}

				// Fix top and bottom edges
				if (i / meshSize == meshSize - 1 && terrainPatch.mapOffset.Y < Terrain.gridSize.Y - 1)
				{
					int adjacent = i - (meshSize * (meshSize - 1));
					TerrainMesh adjacentMesh = terrainPatch.neighbors[1].Meshes[mipLevel];
					vertices[i].Normal = adjacentMesh.vertices[adjacent].Normal;
				}
			}
			// Finish reading normals
		}
	}
}