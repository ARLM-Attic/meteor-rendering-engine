using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Creates and updates the innermost geo clipmap
	/// </summary>
	class InnerClipmap
	{
		/// Last recorded center position of the clipmap, in map coordinates
		private Vector2 lastMapCenter;

		/// Height and width (in tiles) for each clip map
		private int clipLevelSize = 64;

		/// Vertex buffer for geo clipmap
		private DynamicVertexBuffer clipmapVB;
		public DynamicVertexBuffer Vertices
		{
			get { return clipmapVB; }
		}

		/// Index buffer for geo clipmap
		private IndexBuffer clipmapIndices;
		public IndexBuffer Indices
		{
			get { return clipmapIndices; }
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
		/// Create fixed sized buffers and arrays for the clipmap.
		/// </summary>
		/// <param name="graphicsDevice"></param>

		public InnerClipmap(int levelSize, GraphicsDevice graphicsDevice)
		{
			clipLevelSize = levelSize;

			// Create vertex buffer for clip map root
			vertices = new VertexPositionTangentToWorld[(clipLevelSize + 1) * (clipLevelSize + 1)];
			clipmapVB = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionTangentToWorld.terrainVertexDeclaration, vertices.Length, BufferUsage.WriteOnly);

			// Create index buffer for clip map root
			indices = new int[clipLevelSize * clipLevelSize * 6];
			clipmapIndices = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Update the geo clipmaps according to the center position.
		/// </summary>

		public void UpdateMap(Vector2 terrainSize, Vector2 mapCenter, short[,] heightData)
		{
			// Snap to units of 2
			mapCenter.X += 2 - (mapCenter.X % 2);
			mapCenter.Y += 2 - (mapCenter.Y % 2);

			if (mapCenter != lastMapCenter)
			{
				Vector2 spans = SetUpVertices(mapCenter, terrainSize, heightData);
				SetUpIndices((int)spans.X, (int)spans.Y);
				CalculateNormals();

				// Set vertex and index buffers
				if (updatedVertices > 0 && UpdatedIndices > 0)
				{
					clipmapVB.SetData(vertices, 0, updatedVertices);
					clipmapIndices.SetData(indices, 0, updatedIndices);
				}
			}

			lastMapCenter = mapCenter;
		}

		/// <summary>
		/// Force an update on this clipmap in case data gets lost.
		/// </summary>

		public void ForceUpdate(Vector2 terrainSize, Vector2 mapCenter, short[,] heightData)
		{
			// Force a change in the last map center registered
			lastMapCenter += new Vector2(0.5f);

			// Repopulate the map data
			UpdateMap(terrainSize, mapCenter, heightData);
		}

		/// <summary>
		/// Calculate the vertex positions that need to be updated.
		/// </summary>

		private Vector2 SetUpVertices(Vector2 mapCenter, Vector2 terrainSize, short[,] heightData)
		{
			int left = (int)mapCenter.X - (clipLevelSize / 2);
			int top = (int)mapCenter.Y - (clipLevelSize / 2);
			int right = (int)mapCenter.X + (clipLevelSize / 2);
			int bottom = (int)mapCenter.Y + (clipLevelSize / 2);

			// Clamp coordinates
			left = Math.Max(0, left);
			right = Math.Min((int)terrainSize.X - 1, right);
			top = Math.Max(0, top);
			bottom = Math.Min((int)terrainSize.Y - 1, bottom);

			// Get total span of vertices to be drawn, since it won't always be
			// the full dimensions of the clipmap
			int spanWidth = right - left + 1;
			int spanHeight = bottom - top + 1;

			// Go through all coordinates in the clipmap and update the vertices
			// For vertices that were already set last time, just ignore them

			updatedVertices = 0;

			for (int y = top, i = 0; y <= bottom; y++, i++)
			{
				for (int x = left, j = 0; x <= right; x++, j++)
				{
					int index = i * spanWidth + j;

					vertices[index].Position =
						new Vector3(x, heightData[x, y] / 4.0f, -y);

					vertices[index].TextureCoordinate.X = (float)x / 20.0f;
					vertices[index].TextureCoordinate.Y = (float)y / 20.0f;

					updatedVertices++;
				}
			}

			// Patch up the gaps left at the seams/edges of the clipmap by pushing together
			// some edge vertices. Remove the gaps at the top and bottom, then the sides.

			for (int y = top, i = 0; y <= bottom; y++, i++)
			{
				if (i == 0 || i == spanHeight - 1)
				{
					for (int x = left + 1, j = 1; x <= right; x += 2, j += 2)
					{
						int index = i * spanWidth + j;

						vertices[index].Position.Y =
							(vertices[index - 1].Position.Y + vertices[index + 1].Position.Y) / 2f;
					}
				}
			}

			for (int y = top + 1, i = 1; y <= bottom; y += 2, i += 2)
			{
				for (int x = left, j = 0; x <= right; x++, j++)
				{
					if (j == 0 || j == spanWidth - 1)
					{
						int index = i * spanWidth + j;
						int prev = ((i - 1) * spanWidth) + j;
						int next = ((i + 1) * spanWidth) + j;

						vertices[index].Position.Y = 
							(vertices[prev].Position.Y + vertices[next].Position.Y) / 2f;
					}
				}
			}

			Vector2 span = new Vector2((int)spanWidth, (int)spanHeight);
			return span;
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices(int spanWidth, int spanHeight)
		{
			updatedIndices = 0;

			for (int y = 0; y < spanHeight - 1; y++)
			{
				for (int x = 0; x < spanWidth - 1; x++)
				{
					int lowerLeft = x + y * spanWidth;
					int lowerRight = (x + 1) + y * spanWidth;
					int topLeft = x + (y + 1) * spanWidth;
					int topRight = (x + 1) + (y + 1) * spanWidth;

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

			for (int i = 0; i < vertices.Length; i++)
				vertices[i].Normal.Normalize();
		}
	}
}
