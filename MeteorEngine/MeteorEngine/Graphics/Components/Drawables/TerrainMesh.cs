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
		public ushort updatedVertices { private set; get; }

		// Terrain patch that this mesh belongs to.
		TerrainPatch terrainPatch;

		// Extents of this mesh
		public short meshSize { private set; get; }

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

		public void UpdateMesh(
			ushort[,] heightData, float heightScale, Vector2 offset, int mipLevel, ushort[] indices)
		{
			// Create vertex data
			SetUpVertices(heightData, offset, mipLevel);
			CalculateNormals(indices, heightScale);

			// Set vertex and index buffers
			if (updatedVertices > 0)
				patchVertexBuffer.SetData(vertices, 0, updatedVertices);
		}

		/// <summary>
		/// Calculate the vertex positions that need to be updated.
		/// </summary>

		private void SetUpVertices(ushort[,] heightData, Vector2 mapOffset, int mipLevel)
		{
			int fullMeshSize = TerrainPatch.patchSize + 1;

			int left = (int)mapOffset.X * TerrainPatch.patchSize;
			int right = left + fullMeshSize;
			int top = (int)mapOffset.Y * TerrainPatch.patchSize;
			int bottom = top + fullMeshSize;

			int index = 0;

			// Determine what vertices to skip (if any) for creating this mesh
			int next = 1;
			for (int m = mipLevel; m > 0; m--)
				next *= 2;

			for (int y = top, i = 0; y < bottom; y += next, i += next)
			{
				for (int x = left, j = 0; x < right; x += next, j += next)
				{
					ushort height = (ushort)(heightData[x, y] >> 8);
					ushort vertexID = (ushort)(i * fullMeshSize + j);

					vertices[index].VertexID = vertexID;
					vertices[index].VertexHeight = height;

					updatedVertices++;
					index++;
				}
			}
			// Done creating vertices
		}

		/// <summary>
		/// Find the normals for each mesh vertex
		/// </summary>

		private void CalculateNormals(ushort[] indices, float heightScale)
		{
			int fullMeshSize = TerrainPatch.patchSize + 1;

			// store temporary normals
			Vector3[] vertexNormals = new Vector3[vertices.Length];

			for (int i = 0; i < vertices.Length; i++)
				vertexNormals[i] = new Vector3(0.5f, 0.5f, 1f);

			for (int i = 0; i < indices.Length / 3; i++)
			{
				int index0 = indices[i * 3];
				int index1 = indices[i * 3 + 1];
				int index2 = indices[i * 3 + 2];

				vertexNormals[index0] += Vector3.Up;
				vertexNormals[index1] += Vector3.Up;
				vertexNormals[index2] += Vector3.Up;

				//continue;

				Vector3 vertexPos0 = new Vector3
				(
					vertices[index0].VertexID % fullMeshSize,
					vertices[index0].VertexHeight * heightScale,
					-(int)(vertices[index0].VertexID / fullMeshSize)
				);
				Vector3 vertexPos1 = new Vector3
				(
					vertices[index1].VertexID % fullMeshSize,
					vertices[index1].VertexHeight * heightScale,
					-(int)(vertices[index1].VertexID / fullMeshSize)
				);
				Vector3 vertexPos2 = new Vector3
				(
					vertices[index2].VertexID % fullMeshSize,
					vertices[index2].VertexHeight * heightScale,
					-(int)(vertices[index2].VertexID / fullMeshSize)
				);

				Vector3 side1 = vertexPos0 - vertexPos2;
				Vector3 side2 = vertexPos0 - vertexPos1;
				Vector3 normal = Vector3.Cross(side1, side2);

				vertexNormals[index0] += normal;
				vertexNormals[index1] += normal;
				vertexNormals[index2] += normal;
			}

			float epsilon = 0.0000001f;

			// Correct normalization
			for (int i = 0; i < vertices.Length; i++)
			{
				vertexNormals[i].Normalize();
				//continue;

				// Fix side edges and we're done for this vertex
				if (i % meshSize == meshSize - 1 && terrainPatch.mapOffset.X < Terrain.gridSize.X - 1)
				{
					int adjacent = i - (meshSize - 1);
					TerrainMesh adjacentMesh = terrainPatch.neighbors[3].Meshes[mipLevel];
					vertices[i].NormalX = adjacentMesh.vertices[adjacent].NormalX;
					vertices[i].NormalY = adjacentMesh.vertices[adjacent].NormalY;

					continue;
				}

				// Fix top and bottom edges and we're done for this vertex
				if (i / meshSize == meshSize - 1 && terrainPatch.mapOffset.Y < Terrain.gridSize.Y - 1)
				{
					int adjacent = i - (meshSize * (meshSize - 1));
					TerrainMesh adjacentMesh = terrainPatch.neighbors[1].Meshes[mipLevel];
					vertices[i].NormalX = adjacentMesh.vertices[adjacent].NormalX;
					vertices[i].NormalY = adjacentMesh.vertices[adjacent].NormalY;

					continue;
				}

				// Encode normal into 2 components

				Vector2 projectedNormal;
				float absZ = Math.Abs(vertexNormals[i].Z) + 1.0f;
				projectedNormal.X = vertexNormals[i].X / absZ;
				projectedNormal.Y = vertexNormals[i].Y / absZ;

				// Convert unit circle to square
				// We add epsilon to avoid division by zero

				float d = Math.Abs(projectedNormal.X) + Math.Abs(projectedNormal.Y) + epsilon;
				float r = Vector2.Distance(projectedNormal, Vector2.Zero);
				Vector2 q = projectedNormal * r / d;

				// Mirror triangles to outer edge if z is negative

				float negativeZ = Math.Max(-Math.Sign(vertexNormals[i].Z), 0f);
				Vector2 qSign = new Vector2(Math.Sign(q.X), Math.Sign(q.Y));
				qSign.X = Math.Sign(qSign.X + 0.5f);
				qSign.Y = Math.Sign(qSign.Y + 0.5f);

				// Reflection: qr = q - 2 * n * (dot (q, n) - d) / dot (n, n)

				q -= negativeZ * (float)(Vector2.Dot(q, qSign) - 1.0) * qSign;
				vertices[i].NormalX = (short)(q.X * 32767);
				vertices[i].NormalY = (short)(q.Y * 32767);
			}
			// Finish reading normals
		}
	}
}