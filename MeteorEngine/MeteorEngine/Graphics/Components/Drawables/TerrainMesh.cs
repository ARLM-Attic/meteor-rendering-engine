using System;
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

		/// Index buffer for mipmap
		private IndexBuffer patchIndexBuffer;
		public IndexBuffer Indices
		{
			get { return patchIndexBuffer; }
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

		/// Array for clipmap indices
		ushort[] indices;
		private ushort updatedIndices;
		public ushort UpdatedIndices
		{
			get { return updatedIndices; }
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

			// Create index buffer for this mipmap
			indices = new ushort[meshSize * meshSize * 6];
			patchIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Update vertex data for this mesh.
		/// </summary>

		public void UpdateMesh(short[,] heightData, Vector2 offset, int mipLevel)
		{
			// Create vertex data
			SetUpVertices(heightData, offset, mipLevel);
			SetUpIndices();
			CalculateNormals();

			// Set vertex and index buffers
			if (updatedVertices > 0 && UpdatedIndices > 0)
			{
				patchVertexBuffer.SetData(vertices, 0, updatedVertices);
				patchIndexBuffer.SetData(indices, 0, updatedIndices);
			}
		}

		/// <summary>
		/// Calculate the vertex positions that need to be updated.
		/// </summary>

		private void SetUpVertices(short[,] heightData, Vector2 mapOffset, int mipLevel)
		{
			int left = (int)mapOffset.X;
			int right = left + meshSize - 1;
			int top = (int)mapOffset.Y;
			int bottom = top + meshSize - 1;

			int index = 0;

			// Determine what vertices to skip (if any) for creating this mesh
			int next = 1;
			for (int i = mipLevel; i > 0; i--)
				next *= 2;

			for (int y = top; y < bottom + 1; y += next)
			{
				for (int x = left; x < right + 1; x += next)
				{
					float height = heightData[x, y] / 4.0f;
					vertices[index].Position = new Vector3(x, height, -y);

					vertices[index].TextureCoordinate.X = (float)x / 20.0f;
					vertices[index].TextureCoordinate.Y = (float)y / 20.0f;

					updatedVertices++;
					index++;
				}
			}
			// Done creating vertices
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices()
		{
			updatedIndices = 0;
			short rowSpan = meshSize;

			for (short y = 0; y < meshSize - 1; y++)
			{
				for (short x = 0; x < meshSize - 1; x++)
				{
					int lowerLeft = x + y * rowSpan;
					int lowerRight = (x + 1) + y * rowSpan;
					int topLeft = x + (y + 1) * rowSpan;
					int topRight = (x + 1) + (y + 1) * rowSpan;

					// First, find the diagonal with the smallest height difference.
					// This is where the split between two vertices will occur.

					float diff1 = Math.Abs(vertices[lowerLeft].Position.Y - vertices[topRight].Position.Y);
					float diff2 = Math.Abs(vertices[lowerRight].Position.Y - vertices[topLeft].Position.Y);

					if (diff2 < diff1)
					{
						indices[updatedIndices++] = (ushort)topLeft;
						indices[updatedIndices++] = (ushort)lowerRight;
						indices[updatedIndices++] = (ushort)lowerLeft;

						indices[updatedIndices++] = (ushort)topLeft;
						indices[updatedIndices++] = (ushort)topRight;
						indices[updatedIndices++] = (ushort)lowerRight;
					}
					else
					{
						indices[updatedIndices++] = (ushort)topLeft;
						indices[updatedIndices++] = (ushort)topRight;
						indices[updatedIndices++] = (ushort)lowerLeft;

						indices[updatedIndices++] = (ushort)topRight;
						indices[updatedIndices++] = (ushort)lowerRight;
						indices[updatedIndices++] = (ushort)lowerLeft;
					}
				}
			}
			// Done creating indices
		}

		/// <summary>
		/// Find the normals for each mesh vertex
		/// </summary>

		private void CalculateNormals()
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

				// Edges of the triangle : postion delta
				Vector3 deltaPos1 = vertices[index1].Position - vertices[index0].Position;
				Vector3 deltaPos2 = vertices[index2].Position - vertices[index0].Position;

				// UV delta
				Vector2 deltaUV1 = vertices[index1].TextureCoordinate - vertices[index0].TextureCoordinate;
				Vector2 deltaUV2 = vertices[index2].TextureCoordinate - vertices[index0].TextureCoordinate;

				// Calculate tangent and bitangent
				float r = 1.0f / (deltaUV1.X * deltaUV2.Y - deltaUV1.Y * deltaUV2.Y);
				Vector3 tangent = (deltaPos1 * deltaUV2.Y - deltaPos2 * deltaUV1.Y) * r;
				Vector3 bitangent = (deltaPos2 * deltaUV1.X - deltaPos1 * deltaUV2.X) * r;

				tangent.Normalize();
				bitangent.Normalize();

				vertices[index0].Tangent = tangent;
				vertices[index1].Tangent = tangent;
				vertices[index2].Tangent = tangent;

				vertices[index0].Binormal = bitangent;
				vertices[index1].Binormal = bitangent;
				vertices[index2].Binormal = bitangent;
			}

			// Correct normalization
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 cleared1, cleared2;
				cleared1 = (vertices[i].Binormal != vertices[i].Binormal) ? Vector3.Zero : vertices[i].Binormal;
				cleared2 = (vertices[i].Tangent != vertices[i].Tangent) ? Vector3.Zero : vertices[i].Tangent;

				vertices[i].Binormal = cleared1;
				vertices[i].Tangent = cleared2;
				vertices[i].Normal.Normalize();
			}
		}
	}
}