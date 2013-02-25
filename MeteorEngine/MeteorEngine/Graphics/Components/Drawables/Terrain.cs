using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Vertex structure for normal mapped terrain
	/// </summary>

	public struct VertexPositionTangentToWorld
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Vector2 TextureCoordinate;
		public Vector3 Tangent;
		public Vector3 Binormal;

		public static VertexDeclaration terrainVertexDeclaration = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
			new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
			new VertexElement(sizeof(float) * 6, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(sizeof(float) * 8, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
			new VertexElement(sizeof(float) * 11, VertexElementFormat.Vector3, VertexElementUsage.Binormal, 0)
		);

		public VertexPositionTangentToWorld(Vector3 position, Vector3 normal, Vector2 textureCoordinate,
			Vector3 tangent, Vector3 binormal)
		{
			Position = position;
			Normal = normal;
			TextureCoordinate = textureCoordinate;
			Tangent = tangent;
			Binormal = binormal;
		}

		public static int SizeInBytes { get { return sizeof(float) * 14; } }
	}

	/// <summary>
	/// Class that stores original terrain heightmap info and clipmaps
	/// to visually represent it.
	/// </summary>
	
	public class Terrain
	{
		/// Graphics device to set map data
		GraphicsDevice graphicsDevice;

		/// Content manager to load images
		ContentManager content;

		/// Heightmap dimensions
		private int terrainWidth;
		private int terrainHeight;

		/// Array to store the data of each map pixel
		private short[,] heightData;

		/// Amount to scale heightmap by
		public float scale;

		/// Amount to scale heightmap textures by
		public float textureScale = 10f;

		/// Primary texture to apply to the terrain mesh
		private Texture2D mainTexture, normalTexture, heightMapTexture;
		
		/// Splate textures for additional terrain features
		private Texture2D blendTexture1;

		/// The innermost (level 0) geo clipmap to render the terrain with
		private InnerClipmap innerClipMap;

		/// The outer geo clipmaps
		private OuterClipmap[] outerClipMaps;

		/// Location to offset the position of the terrain
		private Vector3 heightmapPosition;

		/// <summary>
		/// Constructor to set up content and default texture
		/// </summary>
		/// <param name="content"></param>
		/// <param name="device"></param>

		public Terrain(ContentManager content, GraphicsDevice device)
		{
			this.content = content;
			this.graphicsDevice = device;

			scale = 8f;
		}

		/// <summary>
		/// Create a heightmap from a grayscale image
		/// </summary>
		
		public void GenerateFromImage(String image, String texture, String normalTex)
		{
			heightMapTexture = content.Load<Texture2D>(image);
			mainTexture = content.Load<Texture2D>(texture);
			blendTexture1 = content.Load<Texture2D>("Textures/grass");
			normalTexture = content.Load<Texture2D>(normalTex);

			terrainWidth = heightMapTexture.Width;
			terrainHeight = heightMapTexture.Height;

			// Calculate heightmap position
			heightmapPosition.X = -(terrainWidth * scale) / 2;
			heightmapPosition.Y = -40f * scale;
			heightmapPosition.Z = (terrainHeight * scale) / 2;

			Color[] heightMapColors = new Color[terrainWidth * terrainHeight];
			heightMapTexture.GetData(heightMapColors);

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

			// Setup other data values
			//SetUpIndices();

			// Setup the clip maps
			int clipMapSize = 80;

			innerClipMap = new InnerClipmap(clipMapSize, graphicsDevice);
			outerClipMaps = new OuterClipmap[5];

			for (int i = 0; i < outerClipMaps.Length; i++)
				outerClipMaps[i] = new OuterClipmap(i + 1, clipMapSize, graphicsDevice);
		}

		/// <summary>
		/// Re-create the terrain mesh with the current parameters.
		/// </summary>
		public void Update(Vector3 centerPosition)
		{
			Vector2 mapCenter = getMapPosition(centerPosition);
			innerClipMap.UpdateMap(new Vector2(terrainWidth, terrainHeight), mapCenter, heightData);

			// Update the movements of the outer clip maps next
			for (int i = 0; i < outerClipMaps.Length; i++)
				outerClipMaps[i].UpdateMap(new Vector2(terrainWidth, terrainHeight), mapCenter, heightData);
		}

		/// <summary>
		/// Force an update in case the mesh data needs to be recovered.
		/// </summary>
		public void ForceUpdate(Vector3 centerPosition)
		{
			Vector2 mapCenter = getMapPosition(centerPosition);
			innerClipMap.ForceUpdate(new Vector2(terrainWidth, terrainHeight), mapCenter, heightData);
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
		/// Draw the terrain mesh with the desired effect
		/// </summary>

		public int Draw(Camera camera, Effect effect, Texture2D texture)
		{
			// Do not draw if this buffer isn't currently filled
			if (innerClipMap.UpdatedIndices == 0)
				return 0;

			foreach (OuterClipmap outerMap in outerClipMaps)
				if (outerMap.UpdatedIndices == 0)
					return 0;

			Matrix worldMatrix = Matrix.CreateScale(scale);
			worldMatrix *= Matrix.CreateTranslation(heightmapPosition);

			//RasterizerState rWireframeState = new RasterizerState();
			//rWireframeState.FillMode = FillMode.WireFrame;
			//graphicsDevice.RasterizerState = rWireframeState;

			effect.Parameters["Texture"].SetValue(mainTexture);

			if (normalTexture != null && effect.CurrentTechnique != effect.Techniques["Default"])
				effect.Parameters["NormalMap"].SetValue(normalTexture);

			// Special texture effects
			effect.Parameters["textureScale"].SetValue(textureScale);
			effect.Parameters["mapScale"].SetValue(scale);
			effect.Parameters["clipLevel"].SetValue(0);

			// Set world transformation for the map
			effect.Parameters["World"].SetValue(worldMatrix);

			if (effect.CurrentTechnique != effect.Techniques["Default"])
			{
				// Set camera transformation matrices
				effect.Parameters["View"].SetValue(camera.view);
				effect.Parameters["Projection"].SetValue(camera.projection);
				effect.Parameters["WorldInverseTranspose"].SetValue(camera.view);

				// Additional textures for terrain features
				effect.Parameters["blendTexture1"].SetValue(blendTexture1);
			}

			int polycount = 0;
			int clipLevel = 1;

			// Set buffers for outer clip maps and draw them
			for (int i = outerClipMaps.Length - 1; i >= 0; i-- )
			{
				// Color code the clip maps
				effect.Parameters["clipLevel"].SetValue(clipLevel++);

				graphicsDevice.Indices = outerClipMaps[i].Indices;
				graphicsDevice.SetVertexBuffer(outerClipMaps[i].Vertices);

				foreach (EffectPass pass in effect.CurrentTechnique.Passes)
				{
					pass.Apply();
					graphicsDevice.DrawIndexedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						outerClipMaps[i].UpdatedVertices, 0, outerClipMaps[i].UpdatedIndices / 3);
				}
				// Add to the total number of polygons drawn
				polycount += (outerClipMaps[i].UpdatedIndices / 3);
			}

			foreach (EffectPass pass in effect.CurrentTechnique.Passes)
			{
				pass.Apply();

				// Set vertex and index buffers for the inner clip map and draw it
				graphicsDevice.Indices = innerClipMap.Indices;
				graphicsDevice.SetVertexBuffer(innerClipMap.Vertices);

				graphicsDevice.DrawIndexedPrimitives(
					PrimitiveType.TriangleList, 0, 0,
					innerClipMap.UpdatedVertices, 0, innerClipMap.UpdatedIndices / 3);
			}

			// Add to the total number of polygons drawn;
			polycount += (innerClipMap.UpdatedIndices / 3);

			// End rendering clipmaps
			return polycount;
		}
	}
}
