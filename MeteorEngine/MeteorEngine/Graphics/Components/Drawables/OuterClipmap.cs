using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Creates and updates the outer level geo clipmaps
	/// </summary>
	class OuterClipmap
	{
		/// Last recorded center position of the clipmap, in map coordinates
		private Vector2 lastMapCenter;

		/// Height and width (in tiles) for each clip map
		private int clipLevelSize = 64;

		/// Size of heightmap units to scale and snap the grid to
		private int gridUnitSize;

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

		public OuterClipmap(int level, int levelSize, GraphicsDevice graphicsDevice)
		{
			gridUnitSize = (int)Math.Pow(2, level);
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
			int gridSnap = gridUnitSize * 2;

			Vector2 originalMapCenter = mapCenter;
			Vector2 snappedDistance = mapCenter;

			// Outer boundary shifts by (gridUnitSize * 2) units
			snappedDistance.X += gridSnap - (mapCenter.X % gridSnap);
			snappedDistance.Y += gridSnap - (mapCenter.Y % gridSnap);

			// Inner boundary snaps to the grid units
			mapCenter.X += gridUnitSize - (mapCenter.X % gridUnitSize);
			mapCenter.Y += gridUnitSize - (mapCenter.Y % gridUnitSize);

			// If the change in position is large enough compared to the last change,
			// then the grid should be shifted and the vertices updated.

			if (Math.Abs(mapCenter.X - lastMapCenter.X) >= gridUnitSize ||
				Math.Abs(mapCenter.Y - lastMapCenter.Y) >= gridUnitSize)
			{
				int left = (int)snappedDistance.X - (clipLevelSize * gridUnitSize / 2);
				int top = (int)snappedDistance.Y - (clipLevelSize * gridUnitSize / 2);
				int right = (int)snappedDistance.X + (clipLevelSize * gridUnitSize / 2);
				int bottom = (int)snappedDistance.Y + (clipLevelSize * gridUnitSize / 2);

				// Clamp coordinates
				left = Math.Max(0, left);
				right = Math.Min((int)terrainSize.X - 1, right);
				top = Math.Max(0, top);
				bottom = Math.Min((int)terrainSize.Y - 1, bottom);

				// Get total span of vertices to be drawn, since it won't always be the full 
				// dimensions of the clipmap. Span is in grid units (not heightmap units)

				int verticesPerRow = ((right - left) / gridUnitSize) + 1;
				int verticesPerColumn = ((bottom - top) / gridUnitSize) + 1;

				// Go through all coordinates in the clipmap and update the vertices
				// For vertices that were already set last time, just ignore them

				updatedVertices = 0;

				for (int y = top, i = 0; y <= bottom; y += gridUnitSize, i++)
				{
					for (int x = left, j = 0; x <= right; x += gridUnitSize, j++)
					{
						vertices[i * clipLevelSize + j].Position = new Vector3(x, heightData[x, y] / 4.0f, -y);
						vertices[i * clipLevelSize + j].TextureCoordinate = new Vector2(x, y) / 20.0f;
						updatedVertices++;
					}
				}

				SetUpIndices(mapCenter, clipLevelSize, clipLevelSize);
				CalculateNormals();

				// Set vertex and index buffers
				if (updatedVertices > 0 && UpdatedIndices > 0)
				{
					clipmapVB.SetData(vertices, 0, updatedVertices);
					clipmapIndices.SetData(indices, 0, updatedIndices);
				}

				lastMapCenter = mapCenter;
			}
		}

		/// <summary>
		/// Force an update on this clipmap in case data gets lost.
		/// </summary>

		public void ForceUpdate(Vector2 terrainSize, Vector2 mapCenter, short[,] heightData)
		{
			// Force a change in the last map center registered
			lastMapCenter += new Vector2(0.5f) * gridUnitSize;

			// Repopulate the map data
			UpdateMap(terrainSize, mapCenter, heightData);
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices(Vector2 mapCenter, int vertsPerRow, int vertsPerColumn)
		{
			updatedIndices = 0;

			// Determine the iterior region bounds. The region shifts according to the position 
			// of the clip map contained in it. Inner vertices can be skipped for rendering and
			// won't be added to the indices list.

			// First, define default interior borders at 1/4th and 3/4ths the span distance.

			int interiorLeft = clipLevelSize / 4;
			int interiorRight = clipLevelSize * 3 / 4;
			int interiorTop = clipLevelSize / 4;
			int interiorBottom = clipLevelSize * 3 / 4;

			// Use the original center position of the map. It must be off by at least
			// mod GridUnitSize to shift the left and top interiors in those respective axes.

			if ((int)(mapCenter.X / gridUnitSize) % 2 == 1)
			{
				interiorLeft -= 1;
				interiorRight -= 1;
			}

			if ((int)(mapCenter.Y / gridUnitSize) % 2 == 1)
			{
				interiorTop -= 1;
				interiorBottom -= 1;
			}

			// Add additional interior vertices when the rows or columns become shorter
			interiorRight += (vertsPerRow - clipLevelSize) - 1;
			interiorBottom += (vertsPerColumn - clipLevelSize) - 1;

			for (int y = 0; y < vertsPerColumn - 1; y++)
			{
				for (int x = 0; x < vertsPerRow - 1; x++)
				{
					int lowerLeft = x + y * vertsPerRow;
					int lowerRight = (x + 1) + y * vertsPerRow;
					int topLeft = x + (y + 1) * vertsPerRow;
					int topRight = (x + 1) + (y + 1) * vertsPerRow;

					if ((x < interiorLeft || x >= interiorRight) ||
						(y < interiorTop || y >= interiorBottom))
					{
						// Find the diagonal with the smallest height difference (the less steep one).
						// This is where the split between two vertices will occur.

						float diff1 = Math.Abs(vertices[lowerLeft].Position.Y - vertices[topRight].Position.Y); 
						float diff2 = Math.Abs(vertices[lowerRight].Position.Y - vertices[topLeft].Position.Y);
					
						// Determine the vertex order according to the split diagonal

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
