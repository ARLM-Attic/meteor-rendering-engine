using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Class that stores original terrain heightmap info and vertex patches
	/// to visually represent it.
	/// </summary>
	
	public class TerrainPatch
	{
		/// Center of the patch
		private Vector2 center;

		/// Absolute location in the heightmap
		private Vector2 mapOffset;

		/// Bounding box extents
		public BoundingBox boundingBox { private set; get; }
		public BoundingSphere boundingSphere { private set; get; }

		private Vector3 bboxMin;
		private Vector3 bboxMax;

		/// Height and width in segments
		public readonly static int patchSize = 32;

		/// Vertex buffer for geo clipmap
		private DynamicVertexBuffer patchVertexBuffer;
		public DynamicVertexBuffer Vertices
		{
			get { return patchVertexBuffer; }
		}

		/// Index buffer for geo clipmap
		private IndexBuffer patchIndexBuffer;
		public IndexBuffer Indices
		{
			get { return patchIndexBuffer; }
		}

		/// <summary>
		/// Array for clipmap vertices. UpdatedVertices is used to count
		/// the vertices that have changed since the last frame, for quicker
		/// vertex buffer insertion.
		/// </summary>
		private VertexPositionTangentToWorld[] vertices;
		private int updatedVertices;
		public int UpdatedVertices
		{
			get { return updatedVertices; }
		}

		/// Array for clipmap indices
		int[] indices;
		private int updatedIndices;
		public int UpdatedIndices
		{
			get { return updatedIndices; }
		}

		/// <summary>
		/// Constructor to set up terrain patch vertex buffer
		/// </summary>

		public TerrainPatch(GraphicsDevice graphicsDevice, Vector2 offset)
		{
			// Create vertex buffer for clip map root
			vertices = new VertexPositionTangentToWorld[(patchSize + 1) * (patchSize + 1)];
			patchVertexBuffer = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionTangentToWorld.vertexDeclaration, vertices.Length, BufferUsage.WriteOnly);

			// Create index buffer for clip map root
			indices = new int[patchSize * patchSize * 6];
			patchIndexBuffer = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);

			mapOffset = offset;
		}

		/// <summary>
		/// Update vertex data for this patch.
		/// </summary>

		public void UpdateMap(short[,] heightData, float scale, Vector3 position)
		{
			// Create vertex data
			SetUpVertices(heightData, scale, position);
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

		private void SetUpVertices(short[,] heightData, float terrainScale, Vector3 terrainPosition)
		{
			int left = (int)mapOffset.X;
			int right = left + TerrainPatch.patchSize;
			int top = (int)mapOffset.Y;
			int bottom = top + TerrainPatch.patchSize;

			int index = 0;
			float minY = 1000000;
			float maxY = -1000000;

			for (int y = top; y < bottom; y++)
			{
				for (int x = left; x < right; x++)
				{
					float height = heightData[x, y] / 4.0f;
					vertices[index].Position = new Vector3(x, height, -y);

					vertices[index].TextureCoordinate.X = (float)x / 20.0f;
					vertices[index].TextureCoordinate.Y = (float)y / 20.0f;

					minY = (minY > height) ? height : minY;
					maxY = (maxY < height) ? height : maxY;

					updatedVertices++;
					index++;
				}
			}

			// Adjust bounding box extents
			bboxMin = new Vector3(left, minY, top);
			bboxMax = new Vector3(right, maxY, bottom);

			Vector3 scale = new Vector3(terrainScale);
			scale.Z = -scale.Z;

			// Transform bounding box to fit the actual terrain
			Matrix bboxTransform = Matrix.CreateScale(scale) *
				Matrix.CreateTranslation(terrainPosition);

			bboxMin = Vector3.Transform(bboxMin, bboxTransform);
			bboxMax = Vector3.Transform(bboxMax, bboxTransform);

			boundingBox = new BoundingBox(bboxMin, bboxMax);
			boundingSphere = BoundingSphere.CreateFromBoundingBox(boundingBox);
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices()
		{
			updatedIndices = 0;

			for (int y = 0; y < patchSize - 1; y++)
			{
				for (int x = 0; x < patchSize - 1; x++)
				{
					int lowerLeft = x + y * patchSize;
					int lowerRight = (x + 1) + y * patchSize;
					int topLeft = x + (y + 1) * patchSize;
					int topRight = (x + 1) + (y + 1) * patchSize;

					// First, find the diagonal with the smallest height difference.
					// This is where the split between two vertices will occur.

					float diff1 = Math.Abs(vertices[lowerLeft].Position.Y - vertices[topRight].Position.Y);
					float diff2 = Math.Abs(vertices[lowerRight].Position.Y - vertices[topLeft].Position.Y);

					if (diff2 < diff1)
					{
						indices[updatedIndices++] = topLeft;
						indices[updatedIndices++] = lowerRight;
						indices[updatedIndices++] = lowerLeft;

						indices[updatedIndices++] = topLeft;
						indices[updatedIndices++] = topRight;
						indices[updatedIndices++] = lowerRight;
					}
					else
					{
						indices[updatedIndices++] = topLeft;
						indices[updatedIndices++] = topRight;
						indices[updatedIndices++] = lowerLeft;

						indices[updatedIndices++] = topRight;
						indices[updatedIndices++] = lowerRight;
						indices[updatedIndices++] = lowerLeft;
					}
				}
			}
			// Done creating indices
		}

		/// <summary>
		/// Set the normals for each terrain mesh vertex
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

				vertices[index0].Tangent = tangent;
				vertices[index1].Tangent = tangent;
				vertices[index2].Tangent = tangent;

				vertices[index0].Binormal = bitangent;
				vertices[index1].Binormal = bitangent;
				vertices[index2].Binormal = bitangent;
			}

			// Correct normalization
			for (int i = 0; i < vertices.Length; i++)
				vertices[i].Normal.Normalize();
		}
	}
}
