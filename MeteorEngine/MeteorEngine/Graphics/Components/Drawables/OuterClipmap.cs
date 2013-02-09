﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	class OuterClipmap
	{
		/// Last recorded center position of the clipmap, in map coordinates
		private Vector2 lastMapCenter;

		/// Height and width (in tiles) for each clip map
		private int clipLevelSize = 48;

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
		private VertexPositionNormalTexture[] vertices;
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

		public OuterClipmap(int level, GraphicsDevice graphicsDevice)
		{
			gridUnitSize = (int)Math.Pow(2, level);

			// Create vertex buffer for clip map root
			vertices = new VertexPositionNormalTexture[(clipLevelSize + 1) * (clipLevelSize + 1)];
			clipmapVB = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);

			// Create index buffer for clip map root
			indices = new int[clipLevelSize * clipLevelSize * 6];
			clipmapIndices = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Update the geo clipmaps according to the center position.
		/// </summary>

		public void UpdateMap(Vector2 terrainSize, Vector2 mapCenter, char[,] heightData)
		{
			int gridSnap = gridUnitSize * 2;

			// Snap to units according to the grid unit size
			mapCenter.X += gridSnap - (mapCenter.X % gridSnap);
			mapCenter.Y += gridSnap - (mapCenter.Y % gridSnap);

			// If the change in position is large enough compared to the last change,
			// then the grid should be shifted and the vertices updated.

			if (Math.Abs(mapCenter.X - lastMapCenter.X) >= gridSnap ||
				Math.Abs(mapCenter.Y - lastMapCenter.Y) >= gridSnap)
			{
				int left = (int)mapCenter.X - (clipLevelSize * gridUnitSize / 2);
				int top = (int)mapCenter.Y - (clipLevelSize * gridUnitSize / 2);
				int right = (int)mapCenter.X + (clipLevelSize * gridUnitSize / 2);
				int bottom = (int)mapCenter.Y + (clipLevelSize * gridUnitSize / 2);

				// Clamp coordinates
				left = Math.Max(0, left);
				right = Math.Min((int)terrainSize.X - 1, right);
				top = Math.Max(0, top);
				bottom = Math.Min((int)terrainSize.Y - 1, bottom);

				// Get total span of vertices to be drawn, since it won't always be the full 
				// dimensions of the clipmap. Span is in grid units (not heightmap units)

				int verticesPerRow = (right - left) / gridUnitSize + 1;
				int verticesPerColumn = (bottom - top) / gridUnitSize + 1;

				// Go through all coordinates in the clipmap and update the vertices
				// For vertices that were already set last time, just ignore them

				updatedVertices = 0;

				for (int y = top, i = 0; y <= bottom; y += gridUnitSize, i++)
				{
					for (int x = left, j = 0; x <= right; x += gridUnitSize, j++)
					{
						vertices[i * verticesPerRow + j].Position =
							new Vector3(x, heightData[x, y] / 5.0f, -y);

						vertices[i * verticesPerRow + j].TextureCoordinate.X = (float)x / 20.0f;
						vertices[i * verticesPerRow + j].TextureCoordinate.Y = (float)y / 20.0f;
						updatedVertices++;
					}
				}

				SetUpIndices(verticesPerRow, verticesPerColumn);
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
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices(int vertsPerRow, int vertsPerColumn)
		{
			updatedIndices = 0;

			for (int y = 0; y < vertsPerColumn - 1; y++)
			{
				for (int x = 0; x < vertsPerRow - 1; x++)
				{
					int lowerLeft = x + y * vertsPerRow;
					int lowerRight = (x + 1) + y * vertsPerRow;
					int topLeft = x + (y + 1) * vertsPerRow;
					int topRight = (x + 1) + (y + 1) * vertsPerRow;

					// Determine the iterior region bounds. The region shifts according to the position 
					// of the clip map contained in it. Inner vertices can be skipped for rendering and
					// won't be added to the indices list.

					int interiorLeft = clipLevelSize / 4;
					int interiorRight = clipLevelSize * 3 / 4;
					int interiorTop = clipLevelSize / 4;
					int interiorBottom = clipLevelSize * 3 / 4;

					if ((y < interiorLeft || y >= interiorRight) ||
						(x < interiorTop || x >= interiorBottom))
					{
						// First, find the diagonal with the smallest height difference.
						// This is where the split between two vertices will occur.

						float diff1 = Math.Abs(vertices[lowerLeft].Position.Y - vertices[topRight].Position.Y); 
						float diff2 = Math.Abs(vertices[lowerRight].Position.Y - vertices[topLeft].Position.Y);
					
						// Determine the triangle order according to the height differences

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
				int index1 = indices[i * 3];
				int index2 = indices[i * 3 + 1];
				int index3 = indices[i * 3 + 2];

				Vector3 side1 = vertices[index1].Position - vertices[index3].Position;
				Vector3 side2 = vertices[index1].Position - vertices[index2].Position;
				Vector3 normal = Vector3.Cross(side1, side2);

				vertices[index1].Normal += normal;
				vertices[index2].Normal += normal;
				vertices[index3].Normal += normal;
			}

			for (int i = 0; i < vertices.Length; i++)
				vertices[i].Normal.Normalize();
		}
	}
}