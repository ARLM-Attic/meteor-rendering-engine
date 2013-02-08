﻿using System;
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
		/// The innermost (level 0) geo clipmap to render the terrain with
		/// </summary>
		private InnerClipmap innerClipMap;

		/// <summary>
		/// Location to offset the position of the terrain
		/// </summary>
		private Vector3 heightmapPosition;

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

			innerClipMap = new InnerClipmap(graphicsDevice);
		}

		/// <summary>
		/// Re-create the terrain mesh with the current parameters.
		/// </summary>
		public void Update(Vector3 centerPosition)
		{
			Vector2 mapCenter = getMapPosition(centerPosition);
			innerClipMap.UpdateMap(new Vector2(terrainWidth, terrainHeight), mapCenter, heightData);
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
			// Do not draw if this buffer isn't currently filled
			if (innerClipMap.UpdatedIndices == 0)
				return 0;

			Matrix worldMatrix = Matrix.CreateScale(scale);
			worldMatrix *= Matrix.CreateTranslation(heightmapPosition);

			RasterizerState rWireframeState = new RasterizerState();
			rWireframeState.FillMode = FillMode.WireFrame;
			//graphicsDevice.RasterizerState = rWireframeState;

			// Set vertex and index buffers
			graphicsDevice.Indices = innerClipMap.Indices;
			graphicsDevice.SetVertexBuffer(innerClipMap.Vertices);

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
					innerClipMap.UpdatedVertices, 0, innerClipMap.UpdatedIndices / 3);
			}

			// Add to the total number of polygons drawn;
			return innerClipMap.UpdatedIndices / 3;
		}
	}
}
