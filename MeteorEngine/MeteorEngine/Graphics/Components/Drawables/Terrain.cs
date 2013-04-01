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
		/// Content manager to load images
		private ContentManager terrainContent;

		/// Heightmap dimensions
		private int terrainWidth;
		private int terrainHeight;

		/// Array to store the data of each map pixel
		private ushort[,] heightData;

		/// Terrain patch grid dimensions
		public static Vector2 gridSize;

		/// Number of terrain tiles for building/updating on each pass
		private int tilesToBuild = 4;

		/// Amount to scale terrain mesh
		public float scale = 1f;

		/// Amount to scale heightmap textures
		public float textureScale = 10f;

		/// Amount to scale height values
		public float heightScale = 1f;

		/// Set whether to render for shadow mapping
		public bool castsShadows = false;

		/// Set for debugging mesh features
		public int debug = 0;

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
		public Vector3 mapPosition { private set; get; }

		/// Number of terrain LODs
		public readonly static int mipLevels = 4;

		/// Index buffer for mipmaps
		private IndexBuffer[] patchIndexBuffers;
		public IndexBuffer[] Indices
		{
			get { return patchIndexBuffers; }
		}

		/// Total terrain patches built in the process
		public int patchesBuilt { private set; get; }
		public int nextPatch { private set; get; }

		/// Array for mesh indices
		private ushort[][] indices;

		/// Rasterizer state for debugging
		private RasterizerState rWireframeState;
		private GraphicsDeviceManager graphics;

		/// <summary>
		/// Constructor to set up content and default texture
		/// </summary>
		/// <param name="content"></param>
		/// <param name="device"></param>

		public Terrain(GraphicsDevice graphicsDevice)
		{
			rWireframeState = new RasterizerState();
			rWireframeState.FillMode = FillMode.WireFrame;

			// Create index buffers for each patch LOD
			indices = new ushort[mipLevels][];
			patchIndexBuffers = new IndexBuffer[mipLevels];

			for (int i = 0; i < mipLevels; i++)
				SetUpIndices(graphicsDevice, i);
		}

		/// <summary>
		/// Create a heightmap from a grayscale image
		/// </summary>
		
		public void GenerateFromImage(IServiceProvider services)
		{
			terrainContent = new ContentManager(services, "Content");

			// First load the height map so we can get rid of it soon afterwards.
			Texture2D heightMapTextureSource = terrainContent.Load<Texture2D>(heightMapTexture);

			terrainWidth = heightMapTextureSource.Width;
			terrainHeight = heightMapTextureSource.Height;

			Color[] heightMapColors = new Color[terrainWidth * terrainHeight];
			heightMapTextureSource.GetData(heightMapColors);

			// Initialize height data values
			heightData = new ushort[terrainWidth, terrainHeight];

			// Add the height values from the map
			for (int x = 0; x < terrainWidth; x++)
			{
				for (int y = 0; y < terrainHeight; y++)
				{
					heightData[x, y] = (ushort)(heightMapColors[x + y * terrainWidth].R);
					heightData[x, y] <<= 8;
				}
			}

			// Calculate heightmap position
			mapPosition = new Vector3(
				-(terrainWidth * scale) / 2, 0f, (terrainHeight * scale) / 2);

			// Heightmap source no longer needed. Now load the textures.
			terrainContent.Unload();

			baseTextureSource = terrainContent.Load<Texture2D>(baseTexture);
			steepTextureSource = terrainContent.Load<Texture2D>(steepTexture);
			steepNormalMapSource = terrainContent.Load<Texture2D>(steepNormalMap);

			// Determine how many tiles should be needed to fit within the bounds of the terrain
			gridSize.X = terrainWidth / TerrainPatch.patchSize;
			gridSize.Y = terrainHeight / TerrainPatch.patchSize;

			// Create terrain patches
			terrainPatches = new TerrainPatch[(int)gridSize.X, (int)gridSize.Y];
			visiblePatches = new List<TerrainPatch>((int)gridSize.X * (int)gridSize.Y);

			nextPatch = (int)(gridSize.X * gridSize.Y) - 1;
			patchesBuilt = 0;
		}

		/*
		public void BuildMeshData(GraphicsDevice graphicsDevice)
		{
			// Iterate backwards because we need to grab neighboring edges for normals
			for (int i = (int)(gridSize.X * gridSize.Y) - 1; i >= 0; i--)
			{
				int x = i % (int)gridSize.X;
				int y = i / (int)gridSize.Y;

				Vector2 offset = new Vector2(x, y);
				TerrainPatch currentPatch = new TerrainPatch(graphicsDevice, offset);

				// find south and east neighbors
				if (y < gridSize.Y - 1) currentPatch.neighbors[1] = terrainPatches[y + 1, x];
				if (x < gridSize.X - 1) currentPatch.neighbors[3] = terrainPatches[y, x + 1];

				currentPatch.UpdateMap(heightData, scale, heightScale, mapPosition, indices);

				terrainPatches[y, x] = currentPatch;
				visiblePatches.Add(terrainPatches[y, x]);
			}

			// Second pass, find its other neighbors
			/*
			for (int y = 0; y < gridSize.Y; y++)
			{
				for (int x = 0; x < gridSize.X; x++)
				{
					TerrainPatch currentPatch = terrainPatches[x, y];

					// Find north and west neighbors
					if (y > 0)	currentPatch.neighbors[0] = terrainPatches[y - 1, x];
					if (x > 0)	currentPatch.neighbors[2] = terrainPatches[y, x - 1];
				}
			}
		}
		*/
		/// <summary>
		/// Build the array of terrain patches to draw with
		/// </summary>

		public bool BuildMeshData(GraphicsDevice graphicsDevice)
		{
			// Iterate backwards because we need to grab neighboring edges for normals
			for (int i = 0; i < tilesToBuild; i++)
			{
				int x = nextPatch % (int)gridSize.X;
				int y = nextPatch / (int)gridSize.Y;

				Vector2 offset = new Vector2(x, y);
				TerrainPatch currentPatch = new TerrainPatch(graphicsDevice, offset);

				// find south and east neighbors
				if (y < gridSize.Y - 1) currentPatch.neighbors[1] = terrainPatches[y + 1, x];
				if (x < gridSize.X - 1) currentPatch.neighbors[3] = terrainPatches[y, x + 1];

				currentPatch.UpdateMap(heightData, scale, heightScale, mapPosition, indices);

				terrainPatches[y, x] = currentPatch;
				visiblePatches.Add(terrainPatches[y, x]);

				nextPatch--;
				patchesBuilt++;

				// Finish updating this frame
				if (patchesBuilt == (gridSize.X * gridSize.Y))
				{
					// All patches have been built
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Get heightmap data as an array of floats.
		/// </summary>

		public float[,] HeightDataFloats()
		{
			float[,] floatHeights = new float[terrainHeight, terrainWidth];

			for (int y = 0; y < terrainHeight; y++)
			{
				for (int x = 0; x < terrainWidth; x++)
				{
					// normalize the values to local space
					floatHeights[y, x] = heightData[y, x] >> 8;
				}
			}
			return floatHeights;
		}

		/// <summary>
		/// Get the position in map coordinates according to the world location
		/// </summary>

		public Vector2 getMapPosition(Vector3 position)
		{
			Vector3 positionOnMap = position - mapPosition;
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
			Vector3 positionOnMap = position - mapPosition;
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
				heightData[left, top] / 256f,
				heightData[left + 1, top] / 256f, xNormalized);

			float bottomHeight = MathHelper.Lerp(
				heightData[left, top + 1] / 256f,
				heightData[left + 1, top + 1] / 256f, xNormalized);

			topHeight *= heightScale;
			bottomHeight *= heightScale;

			float height = MathHelper.Lerp(topHeight, bottomHeight, zNormalized);

			height *= scale;
			return height + mapPosition.Y;
		}

		/// <summary>
		/// Get the interpolated position from an intersecting ray.
		/// </summary>

		public Vector3 GetPosition(Ray ray)
		{
			Vector3 rayOrigin = ray.Position;
			Vector3 rayDirection = ray.Direction;

			// Move ray to local space

			// readjust coordinate origin
			//rayOrigin.X += terrainWidth / 2;
			//rayOrigin.Z += terrainHeight / 2;

			// scale down to vertex level
			//rayOrigin /= scale;
			//rayDirection /= scale;
			//rayDirection.Normalize();

			Ray localRay = new Ray(rayOrigin, rayDirection);

			foreach(TerrainPatch patch in visiblePatches)
			{
				float? intersect = localRay.Intersects(patch.boundingBox);
				patch.active = (intersect != null);
			}
			return Vector3.Zero;
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices(GraphicsDevice graphicsDevice, int mipLevel)
		{
			short meshSize = (short)TerrainPatch.patchSize;

			for (int i = mipLevel; i > 0; i--)
				meshSize /= 2;

			meshSize += 1;
			ushort updatedIndices = 0;

			// Create and assign indices for this LOD
			indices[mipLevel] = new ushort[meshSize * meshSize * 6];
			patchIndexBuffers[mipLevel] = new IndexBuffer(graphicsDevice, typeof(short),
				indices[mipLevel].Length, BufferUsage.WriteOnly);

			for (short y = 0; y < meshSize - 1; y++)
			{
				for (short x = 0; x < meshSize - 1; x++)
				{
					int lowerLeft = (y * meshSize) + x;
					int lowerRight = (y * meshSize) + (x + 1);
					int topLeft = ((y + 1) * meshSize) + x;
					int topRight = ((y + 1) * meshSize) + (x + 1);

					indices[mipLevel][updatedIndices++] = (ushort)topLeft;
					indices[mipLevel][updatedIndices++] = (ushort)lowerRight;
					indices[mipLevel][updatedIndices++] = (ushort)lowerLeft;

					indices[mipLevel][updatedIndices++] = (ushort)topLeft;
					indices[mipLevel][updatedIndices++] = (ushort)topRight;
					indices[mipLevel][updatedIndices++] = (ushort)lowerRight;
				}
			}

			// Done creating indices, add them to the buffer
			patchIndexBuffers[mipLevel].SetData(indices[mipLevel], 0, updatedIndices);
		}

		/// <summary>
		/// Draw in wireframe (activated by debug mode only)
		/// </summary>
		[Conditional("DEBUG")]
		private void DrawDebug(GraphicsDevice graphicsDevice, Camera camera, Effect effect)
		{
			// Draw bounding boxes
			for (int i = 0; i < totalVisiblePatches; i++)
			{
				Color color = (visiblePatches[i].active) ? Color.Yellow : Color.Red;
				ShapeRenderer.AddBoundingBox(visiblePatches[i].boundingBox, color);
			}			

			ShapeRenderer.Draw(camera.view, camera.projection);

			if (debug == 1)
				return;

			// Draw in wireframe mode
			graphicsDevice.RasterizerState = rWireframeState;
			effect.CurrentTechnique = effect.Techniques["DebugTerrain"];
			effect.Parameters["meshSize"].SetValue(TerrainPatch.patchSize + 1);
			
			// Set buffers for mipmap terrain patches and draw them
			for (int i = totalVisiblePatches - 1; i >= 0; i--)
			{
				// Set world transformation for the map
				Vector3 location = mapPosition + (visiblePatches[i].worldOffset * scale);
				Matrix worldMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(location);
				effect.Parameters["World"].SetValue(worldMatrix);

				int currentMipLevel = visiblePatches[i].currentMipLevel;

				// Color code the clip maps
				effect.Parameters["clipLevel"].SetValue(0);

				graphicsDevice.Indices = patchIndexBuffers[currentMipLevel];
				graphicsDevice.SetVertexBuffer(visiblePatches[i].Meshes[currentMipLevel].Vertices);

				foreach (EffectPass pass in effect.CurrentTechnique.Passes)
				{
					pass.Apply();
					graphicsDevice.DrawIndexedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						visiblePatches[i].Meshes[currentMipLevel].updatedVertices, 0,
						patchIndexBuffers[currentMipLevel].IndexCount / 3);
				}
			}
		}
		
		/// <summary>
		/// Draw the terrain mesh with the desired effect
		/// </summary>

		public int Draw(GraphicsDevice graphicsDevice, Camera camera, Effect effect)
		{
			effect.Parameters["Texture"].SetValue(baseTextureSource);

			if (steepNormalMapSource != null && effect.CurrentTechnique != effect.Techniques["Default"])
				effect.Parameters["steepNormalMap"].SetValue(steepNormalMapSource);

			// Terrain transformation parameters
			effect.Parameters["textureScale"].SetValue(textureScale);
			effect.Parameters["mapScale"].SetValue(scale);
			effect.Parameters["heightScale"].SetValue(heightScale);
			effect.Parameters["meshSize"].SetValue(TerrainPatch.patchSize + 1);
			effect.Parameters["clipLevel"].SetValue(0);

			// Special texture effects
			effect.Parameters["specIntensity"].SetValue(specularity);
			effect.Parameters["specPower"].SetValue(specularPower);
			effect.Parameters["bumpIntensity"].SetValue(bumpIntensity);

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
				// Set world transformation for the map
				Vector3 location = mapPosition + (visiblePatches[i].worldOffset * scale);
				Matrix worldMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(location);
				effect.Parameters["World"].SetValue(worldMatrix);

				// Color the patches
				int currentMipLevel = visiblePatches[i].currentMipLevel;
				effect.Parameters["clipLevel"].SetValue(visiblePatches[i].active);

				graphicsDevice.Indices = patchIndexBuffers[currentMipLevel];
				graphicsDevice.SetVertexBuffer(visiblePatches[i].Meshes[currentMipLevel].Vertices);

				foreach (EffectPass pass in effect.CurrentTechnique.Passes)
				{
					pass.Apply();
					graphicsDevice.DrawIndexedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						visiblePatches[i].Meshes[currentMipLevel].updatedVertices, 0,
						patchIndexBuffers[currentMipLevel].IndexCount / 3);
				}

				// Add to the total number of polygons drawn
				polycount += (patchIndexBuffers[currentMipLevel].IndexCount / 3);
			}

			// Draw in wireframe mode
			if (debug > 2) debug = 0;
			if (debug > 0)
				DrawDebug(graphicsDevice, camera, effect);
			
			// End rendering clipmaps
			return polycount;
		}
	}
}
