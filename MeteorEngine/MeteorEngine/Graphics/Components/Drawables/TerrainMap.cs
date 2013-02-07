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
	
	public class TerrainMap
	{
		/// <summary>
		/// Graphics device to set map data
		/// </summary>
		GraphicsDevice graphicsDevice;

		/// <summary>
		/// Content manager to load images
		/// </summary>
		ContentManager content;

		/// <summary>
		/// Heightmap dimensions
		/// </summary>
		private int terrainWidth;
		private int terrainHeight;

		/// <summary>
		/// Arrays for terrain mesh vertices. VerticesToUpdate are used just
		/// for the vertices that have changed since the last frame, for quicker
		/// vertex buffer insertion.
		/// </summary>
		VertexPositionNormalTexture[] vertices;

		/// <summary>
		/// Array for terrain mesh indices
		/// </summary>
		int[] indices;

		/// <summary>
		/// Array to store the data of each map pixel
		/// </summary>
		private char[,] heightData;

		/// <summary>
		/// Amount to scale heightmap by
		/// </summary>
		private float scale;

		/// <summary>
		/// Amount to scale heightmap textures by
		/// </summary>
		private float textureScale = 10f;

		/// <summary>
		/// Primary texture to apply to the terrain mesh
		/// </summary>
		private Texture2D mainTexture;

		/// <summary>
		/// Last recorded center position of the clipmap, in map coordinates
		/// </summary>
		private Vector2 lastMapCenter;

		/// <summary>
		/// Location to offset the position of the terrain
		/// </summary>
		private Vector3 heightmapPosition;

		/// <summary>
		/// Height and width (in tiles) for each clip map
		/// </summary>
		private int clipLevelSize = 32;

		/// <summary>
		/// Vertex buffer for terain mesh
		/// </summary>
		private DynamicVertexBuffer terrainVB;

		/// <summary>
		/// Index buffer for terrain mesh
		/// </summary>
		private IndexBuffer terrainIndices;

		/// <summary>
		/// Constructor to set up content and default texture
		/// </summary>
		/// <param name="content"></param>
		/// <param name="device"></param>

		public TerrainMap(ContentManager content, GraphicsDevice device)
		{
			this.content = content;
			this.graphicsDevice = device;

			scale = 10f;
		}

		/// <summary>
		/// Create a heightmap from a grayscale image
		/// </summary>
		
		public void GenerateFromImage(string image, string texture)
		{
			Texture2D heightMap = content.Load<Texture2D>(image);
			mainTexture = content.Load<Texture2D>(texture);

			terrainWidth = heightMap.Width;
			terrainHeight = heightMap.Height;

			// Calculate heightmap position
			heightmapPosition.X = -(terrainWidth * scale) / 2;
			heightmapPosition.Y = -40f * scale;
			heightmapPosition.Z = (terrainHeight * scale) / 2;

			Color[] heightMapColors = new Color[terrainWidth * terrainHeight];
			heightMap.GetData(heightMapColors);

			// Initialize height data values
			heightData = new char[terrainWidth, terrainHeight];

			// Add the height values from the map
			for (int x = 0; x < terrainWidth; x++)
				for (int y = 0; y < terrainHeight; y++)
					heightData[x, y] = (char)heightMapColors[x + y * terrainWidth].R;

			// Create vertex buffer for clip map root
			vertices = new VertexPositionNormalTexture[clipLevelSize * clipLevelSize];
			terrainVB = new DynamicVertexBuffer(graphicsDevice,
				VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);

			// Create index buffer for clip map root
			indices = new int[(clipLevelSize - 1) * (clipLevelSize - 1) * 6];
			terrainIndices = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);
		}

		/// <summary>
		/// Re-create the terrain mesh with the current parameters.
		/// </summary>
		public void Regenerate(Vector3 centerPosition)
		{
			UpdateMap(centerPosition);
		}

		/// <summary>
		/// Update the geo clipmaps according to the center position
		/// </summary>

		private void UpdateMap(Vector3 position)
		{
			Vector2 mapCenter = getMapPosition(position);
			// Snap to units of 2
			mapCenter.X -= (mapCenter.X % 2);
			mapCenter.Y -= (mapCenter.Y % 2);

			// Clamp coordinates
			if (mapCenter.X - (clipLevelSize / 2) < 0) 
				mapCenter.X = (clipLevelSize / 2);

			if (mapCenter.X + (clipLevelSize / 2) > terrainWidth) 
				mapCenter.X = terrainWidth - (clipLevelSize / 2);

			if (mapCenter.Y - (clipLevelSize / 2) < 0)
				mapCenter.Y = (clipLevelSize / 2);

			if (mapCenter.Y + (clipLevelSize / 2) > terrainHeight)
				mapCenter.Y = terrainHeight - (clipLevelSize / 2);

			if (mapCenter != lastMapCenter)
			{
				int left = (int)mapCenter.X - (clipLevelSize / 2);
				int top = (int)mapCenter.Y - (clipLevelSize / 2);
				int right = (int)mapCenter.X + (clipLevelSize / 2);
				int bottom = (int)mapCenter.Y + (clipLevelSize / 2);

				// Go through all coordinates in the clipmap and update the vertices
				// For vertices that were already set last time, just ignore them

				for (int y = top, i = 0; y < bottom; y++, i++)
				{
					for (int x = left, j = 0; x < right; x++, j++)
					{
						vertices[i * clipLevelSize + j].Position =
							new Vector3(x, heightData[x, y] / 5.0f, -y);

						vertices[i * clipLevelSize + j].TextureCoordinate.X = (float)x / 20.0f;
						vertices[i * clipLevelSize + j].TextureCoordinate.Y = (float)y / 20.0f;
					}
				}

				SetUpIndices();
				CalculateNormals();

				// Set vertex and index buffers
				terrainVB.SetData(vertices);
				terrainIndices.SetData(indices);
			}

			lastMapCenter = mapCenter;
		}

		/// <summary>
		/// Update a higher level geo clipmap
		/// </summary>

		private void UpdateOuterClipMap(Vector3 position)
		{
			Vector2 mapCenter = getMapPosition(position);

			// Snap to units of 4
			mapCenter.X -= (mapCenter.X % 4);
			mapCenter.Y -= (mapCenter.Y % 4);

			int left = (int)mapCenter.X - clipLevelSize;
			int top = (int)mapCenter.Y - clipLevelSize;
			int right = (int)mapCenter.X + clipLevelSize;
			int bottom = (int)mapCenter.Y + clipLevelSize;

			// Clamp coordinates
			left = (left <= 0) ? 0 : left;
			top = (top <= 0) ? 0 : top;

			left = (left > terrainWidth) ? terrainWidth : left;
			top = (top > terrainHeight) ? terrainHeight : top;

			for (int y = top, i = 0; y < bottom; y++, i++)
			{
				for (int x = left, j = 0; x < right; x++, j++)
				{
					vertices[i * clipLevelSize + j].Position =
						new Vector3(x, heightData[x, y], -y);

					vertices[i * clipLevelSize + j].TextureCoordinate.X = (float)x / 20.0f;
					vertices[i * clipLevelSize + j].TextureCoordinate.Y = (float)y / 20.0f;
				}
			}
			// Finish updating vertices
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices()
		{
			int counter = 0;

			for (int y = 0; y < clipLevelSize - 1; y++)
			{
				for (int x = 0; x < clipLevelSize - 1; x++)
				{
					int lowerLeft =	x + y * clipLevelSize;
					int lowerRight = (x + 1) + y * clipLevelSize;
					int topLeft = x + (y + 1) * clipLevelSize;
					int topRight = (x + 1) + (y + 1) * clipLevelSize;

					indices[counter++] = topLeft;
					indices[counter++] = lowerRight;
					indices[counter++] = lowerLeft;

					indices[counter++] = topLeft;
					indices[counter++] = topRight;
					indices[counter++] = lowerRight;
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

		/// <summary>
		/// Get the position in map coordinates according to the world location
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>

		public Vector2 getMapPosition(Vector3 position)
		{
			Vector3 positionOnMap = position - heightmapPosition;
			positionOnMap.Z = -positionOnMap.Z;

			int left, top;
			left = (int)(positionOnMap.X / scale);
			top = (int)(positionOnMap.Z / scale);

			// Clamp coordinates
			left = (left <= 0) ? 0 : left;
			top = (top <= 0) ? 0 : top;

			return new Vector2(left, top);
		}

		/// <summary>
		/// Get the interpolated height at a particular location in the map
		/// </summary>

		public float GetHeight(Vector3 position)
		{
			Vector3 positionOnMap = position - heightmapPosition;
			positionOnMap.Z = -positionOnMap.Z;

			int left, top;
			left = (int)(positionOnMap.X / scale);
			top = (int)(positionOnMap.Z / scale);

			// Clamp coordinates
			left = (left <= 0) ? 0 : left;
			top = (top <= 0) ? 0 : top;
			left = (left > terrainWidth - 2) ? terrainWidth - 2 : left;
			top = (top > terrainHeight - 2) ? terrainHeight - 2 : top;

			// Use modulus to find out how far away we are from the upper
			// left corner of the cell, then normalize it with the scale.
			float xNormalized = (positionOnMap.X % scale) / scale;
			float zNormalized = (positionOnMap.Z % scale) / scale;

			// nrmalize the height positions and interpolate them.
			float topHeight = MathHelper.Lerp(
				heightData[left, top] / 5.0f, heightData[left + 1, top] / 5.0f, xNormalized);

			float bottomHeight = MathHelper.Lerp(
				heightData[left, top + 1] / 5.0f, heightData[left + 1, top + 1] / 5.0f, xNormalized);

			float height = MathHelper.Lerp(topHeight, bottomHeight, zNormalized);

			height *= scale;
			return height + heightmapPosition.Y;
		}
		
		/// <summary>
		/// Draw the terrain mesh with the desired effect
		/// </summary>

		public int Draw(Camera camera, Effect effect, Texture2D texture)
		{
			Matrix worldMatrix = Matrix.CreateScale(scale);
			worldMatrix *= Matrix.CreateTranslation(heightmapPosition);

			RasterizerState rWireframeState = new RasterizerState();
			rWireframeState.FillMode = FillMode.WireFrame;
			//graphicsDevice.RasterizerState = rWireframeState;

			// Set vertex and index buffers
			graphicsDevice.Indices = terrainIndices;
			graphicsDevice.SetVertexBuffer(terrainVB);

			effect.Parameters["Texture"].SetValue(mainTexture);
			effect.Parameters["TextureScale"].SetValue(textureScale);

			// Set world transformation for the map
			effect.Parameters["World"].SetValue(worldMatrix);

			if (effect.CurrentTechnique != effect.Techniques["Default"])
			{
				// Set camera transformation matrices
				effect.Parameters["View"].SetValue(camera.View);
				effect.Parameters["Projection"].SetValue(camera.Projection);
			}

			foreach (EffectPass pass in effect.CurrentTechnique.Passes)
			{
				pass.Apply();

				graphicsDevice.DrawIndexedPrimitives(
					PrimitiveType.TriangleList, 0, 0, 
					vertices.Length, 0, indices.Length / 3);
			}

			// Add to the total number of polygons drawn;
			return indices.Length / 3;
		}
	}
}
