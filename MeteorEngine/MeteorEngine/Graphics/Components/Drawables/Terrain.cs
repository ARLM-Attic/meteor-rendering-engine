using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Class that stores original terrain heightmap info and terrain patches
	/// to visually represent it.
	/// </summary>
	
	public class Terrain
	{
		/// Graphics device to set map data
		GraphicsDevice graphicsDevice;

		/// Content manager to load images
		ContentManager content;

		/// Heightmap dimensions
		int terrainWidth;
		int terrainHeight;

		/// Array to store the data of each map pixel
		short[,] heightData;

		/// Terrain patch grid dimensions
		Vector2 gridSize;

		/// Amount to scale heightmap by
		public float scale = 1f;

		/// Amount to scale heightmap textures by
		public float textureScale = 10f;

		/// Additional texture features
		public float specularity { set; get; }
		public float specularPower { set; get; }
		public float bumpIntensity { set; get; }

		/// Basic texture names for terrain
		public string heightMapTexture { set; get; }
		public string baseTexture { set; get; }
		public string steepTexture { set; get; }

		/// Blend texture names for terrain
		public string blendTexture1 { set; get; }
		public string blendTexture2 { set; get; }

		/// Normal map texture names for terrain
		public string baseNormalMap { set; get; }
		public string steepNormalMap { set; get; }

		/// Primary textures to apply to the terrain mesh
		private Texture2D baseTextureSource;
		private Texture2D steepTextureSource;

		/// Blend textures to apply to the terrain mesh
		private Texture2D blendTexture1Source;
		private Texture2D blendTexture2Source;

		/// Normal map textures to apply to the terrain mesh
		private Texture2D baseNormalMapSource;
		private Texture2D steepNormalMapSource;

		/// Grid of terrain patches
		private TerrainPatch[,] terrainPatches;
		public TerrainPatch[,] TerrainPatches 
		{ 
			get { return terrainPatches; }
		}

		/// List of visible patches
		public List<TerrainPatch> visiblePatches;
		public int totalVisiblePatches = 0;

		/// Location to offset the position of the terrain
		private Vector3 heightmapPosition;
		
		/// Rasterizer state for debugging
		private RasterizerState rWireframeState;

		/// <summary>
		/// Constructor to set up content and default texture
		/// </summary>
		/// <param name="content"></param>
		/// <param name="device"></param>

		public Terrain(ContentManager content, GraphicsDevice device)
		{
			this.content = content;
			this.graphicsDevice = device;

			rWireframeState = new RasterizerState();
			rWireframeState.FillMode = FillMode.WireFrame;
		}

		/// <summary>
		/// Create a heightmap from a grayscale image
		/// </summary>
		
		public void GenerateFromImage()
		{
			Texture2D heightMapTextureSource = content.Load<Texture2D>(heightMapTexture);
			baseTextureSource = content.Load<Texture2D>(baseTexture);
			steepTextureSource = content.Load<Texture2D>(steepTexture);
			steepNormalMapSource = content.Load<Texture2D>(steepNormalMap);

			terrainWidth = heightMapTextureSource.Width;
			terrainHeight = heightMapTextureSource.Height;

			// Calculate heightmap position
			heightmapPosition.X = -(terrainWidth * scale) / 2;
			heightmapPosition.Y = -40f * scale;
			heightmapPosition.Z = (terrainHeight * scale) / 2;

			Color[] heightMapColors = new Color[terrainWidth * terrainHeight];
			heightMapTextureSource.GetData(heightMapColors);

			// Initialize height data values
			heightData = new short[terrainWidth, terrainHeight];

			// Add the height values from the map
			for (int x = 0; x < terrainWidth; x++)
			{
				for (int y = 0; y < terrainHeight; y++)
				{
					heightData[x, y] = (short)(heightMapColors[x + y * terrainWidth].R);
				}
			}

			// Setup the clip maps, and determine how many should be needed to
			// fit within the bounds of the terrain
			gridSize.X = terrainWidth / TerrainPatch.patchSize;
			gridSize.Y = terrainHeight / TerrainPatch.patchSize;

			// Create terrain patches
			terrainPatches = new TerrainPatch[(int)gridSize.X, (int)gridSize.Y];
			visiblePatches = new List<TerrainPatch>((int)gridSize.X * (int)gridSize.Y);

			for (int i = 0; i < gridSize.Y; i++)
			{
				for (int j = 0; j < gridSize.X; j++)
				{
					Vector2 offset = new Vector2(j * TerrainPatch.patchSize, i * TerrainPatch.patchSize);

					terrainPatches[j, i] = new TerrainPatch(graphicsDevice, offset);
					terrainPatches[j, i].UpdateMap(heightData, scale, heightmapPosition);
					visiblePatches.Add(terrainPatches[j, i]);
				}
			}
		}

		/// <summary>
		/// Re-create the terrain mesh with the current parameters.
		/// </summary>
		public void Update(Vector3 centerPosition)
		{
			Vector2 mapCenter = getMapPosition(centerPosition);
		}

		/// <summary>
		/// Force an update in case the mesh data needs to be recovered.
		/// </summary>
		public void ForceUpdate(Vector3 centerPosition)
		{
			Vector2 mapCenter = getMapPosition(centerPosition);
		}

		/// <summary>
		/// Get the position in map coordinates according to the world location
		/// </summary>

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

			// normalize the height positions and interpolate them.
			float topHeight = MathHelper.Lerp(
				heightData[left, top] / 4.0f, 
				heightData[left + 1, top] / 4.0f, xNormalized);

			float bottomHeight = MathHelper.Lerp(
				heightData[left, top + 1] / 4.0f, 
				heightData[left + 1, top + 1] / 4.0f, xNormalized);

			float height = MathHelper.Lerp(topHeight, bottomHeight, zNormalized);

			height *= scale;
			return height + heightmapPosition.Y;
		}

		public bool textureToggle = false;

		/// <summary>
		/// Draw in wireframe (activated by debug mode only)
		/// </summary>
		[Conditional("DEBUG")]
		private void DrawDebug(Camera camera, Effect effect)
		{
			// Draw in wireframe mode
			graphicsDevice.RasterizerState = rWireframeState;
			effect.CurrentTechnique = effect.Techniques["DebugTerrain"];
			/*
			// Set buffers for mipmap terrain patches and draw them
			for (int i = totalVisiblePatches - 1; i >= 0; i--)
			{
				// Color code the clip maps
				effect.Parameters["clipLevel"].SetValue(0);

				int currentMipLevel = visiblePatches[i].currentMipLevel;

				graphicsDevice.Indices = visiblePatches[i].Meshes[currentMipLevel].Indices;
				graphicsDevice.SetVertexBuffer(visiblePatches[i].Meshes[currentMipLevel].Vertices);

				foreach (EffectPass pass in effect.CurrentTechnique.Passes)
				{
					pass.Apply();
					graphicsDevice.DrawIndexedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						visiblePatches[i].Meshes[currentMipLevel].UpdatedVertices, 0,
						visiblePatches[i].Meshes[currentMipLevel].UpdatedIndices / 3);
				}
			}
			*/
			// Draw bounding boxes
			for (int i = 0; i < totalVisiblePatches; i++)
				ShapeRenderer.AddBoundingBox(visiblePatches[i].boundingBox, Color.Red);

			ShapeRenderer.Draw(camera.view, camera.projection);
		}
		
		/// <summary>
		/// Draw the terrain mesh with the desired effect
		/// </summary>

		public int Draw(Camera camera, Effect effect)
		{
			Matrix worldMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(heightmapPosition);

			effect.Parameters["Texture"].SetValue(baseTextureSource);

			if (steepNormalMapSource != null && effect.CurrentTechnique != effect.Techniques["Default"])
				effect.Parameters["steepNormalMap"].SetValue(steepNormalMapSource);

			// Special texture effects
			effect.Parameters["textureScale"].SetValue(textureScale);
			effect.Parameters["mapScale"].SetValue(scale);
			effect.Parameters["clipLevel"].SetValue(0);

			effect.Parameters["specIntensity"].SetValue(specularity);
			effect.Parameters["specPower"].SetValue(specularPower);
			effect.Parameters["bumpIntensity"].SetValue(bumpIntensity);

			// Set world transformation for the map
			effect.Parameters["World"].SetValue(worldMatrix);

			if (effect.CurrentTechnique != effect.Techniques["Default"])
			{
				// Set camera transformation matrices
				effect.Parameters["View"].SetValue(camera.view);
				effect.Parameters["Projection"].SetValue(camera.projection);
				effect.Parameters["inverseView"].SetValue(Matrix.Invert(camera.view));

				// Additional textures for terrain features
				effect.Parameters["steepTexture"].SetValue(steepTextureSource);
			}

			int polycount = 0;
			
			// Set buffers for mipmap terrain patches and draw them
			for (int i = 0; i < totalVisiblePatches; i++)
			{
				// Color code the patches
				effect.Parameters["clipLevel"].SetValue(0);

				int currentMipLevel = visiblePatches[i].currentMipLevel;

				graphicsDevice.Indices = visiblePatches[i].Meshes[currentMipLevel].Indices;
				graphicsDevice.SetVertexBuffer(visiblePatches[i].Meshes[currentMipLevel].Vertices);

				ShapeRenderer.AddBoundingBox(visiblePatches[i].boundingBox, Color.Red);

				foreach (EffectPass pass in effect.CurrentTechnique.Passes)
				{
					pass.Apply();
					graphicsDevice.DrawIndexedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						visiblePatches[i].Meshes[currentMipLevel].UpdatedVertices, 0,
						visiblePatches[i].Meshes[currentMipLevel].UpdatedIndices / 3);
				}

				// Add to the total number of polygons drawn
				polycount += (visiblePatches[i].Meshes[currentMipLevel].UpdatedIndices / 3);
			}

			ShapeRenderer.Draw(camera.view, camera.projection);

			// Draw in wireframe mode
			DrawDebug(camera, effect);
			
			// End rendering clipmaps
			return polycount;
		}
	}
}
