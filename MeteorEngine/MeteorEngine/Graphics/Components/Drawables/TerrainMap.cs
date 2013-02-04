using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	public class TerrainMap
	{
		/// <summary>
		/// Number of times to split up the map and generate it
		/// </summary>
		int totalIterations;

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
		private int terrainWidth = 128;
		private int terrainHeight = 128;

		/// <summary>
		/// Array for terrain mesh vertices
		/// </summary>
		VertexPositionNormalTexture[] vertices;

		/// <summary>
		/// Array for terrain mesh indices
		/// </summary>
		int[] indices;

		/// <summary>
		/// Array to store the data of each map pixel
		/// </summary>
		private float[,] heightData;

		/// <summary>
		/// Amount to scale heightmap by
		/// </summary>
		private float scale;

		/// <summary>
		/// Primary texture to apply to the terrain mesh
		/// </summary>
		private Texture2D mainTexture;

		/// <summary>
		/// Location to offset the position of the terrain
		/// </summary>
		private Vector3 heightmapPosition;

		/// <summary>
		/// Reduction factor for LOD maps
		/// </summary>
		private int reductionFactor = 2;

		/// <summary>
		/// Constructor to set up content and default texture
		/// </summary>
		/// <param name="content"></param>
		/// <param name="device"></param>

		public TerrainMap(ContentManager content, GraphicsDevice device)
		{
			this.content = content;
			this.graphicsDevice = device;

			scale = 2.5f;
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
			heightData = new float[terrainWidth, terrainHeight];

			// Add the height values from the map
			for (int x = 0; x < terrainWidth; x++)
				for (int y = 0; y < terrainHeight; y++)
					heightData[x, y] = heightMapColors[x + y * terrainWidth].R / 5.0f;
					
			SetUpVerticesLOD();
			SetUpIndices();
			CalculateNormals();
		}

		/// <summary>
		/// Create terrain mesh based on the input data
		/// </summary>

		private void SetUpVertices()
		{
			vertices = new VertexPositionNormalTexture[terrainWidth * terrainHeight];
			for (int x = 0; x < terrainWidth; x++)
			{
				for (int y = 0; y < terrainHeight; y++)
				{
					vertices[x + y * terrainWidth].Position =
						new Vector3(x * scale, heightData[x, y] * scale, y * scale);

					vertices[x + y * terrainWidth].TextureCoordinate.X = (float)x / 50.0f;
					vertices[x + y * terrainWidth].TextureCoordinate.Y = (float)y / 50.0f;
				}
			}
		}

		/// <summary>
		/// Set the vertices for a reduced detailed version of the mesh.
		/// </summary>

		private void SetUpVerticesLOD()
		{
			int reducedWidth = terrainWidth / reductionFactor;
			int reducedHeight = terrainHeight / reductionFactor;

			vertices = new VertexPositionNormalTexture[reducedWidth * reducedHeight];

			for (int x = 0; x < reducedWidth; x++)
			{
				for (int y = 0; y < reducedHeight; y++)
				{
					vertices[x + y * reducedWidth].Position =
						new Vector3(x, heightData[x * reductionFactor, y * reductionFactor], -y);

					vertices[x + y * reducedWidth].TextureCoordinate.X = (float)x / 20.0f;
					vertices[x + y * reducedWidth].TextureCoordinate.Y = (float)y / 20.0f;
				}
			}
		}

		/// <summary>
		/// Get the vertex indices for the terrain mesh
		/// </summary>

		private void SetUpIndices()
		{
			int reducedWidth = terrainWidth / reductionFactor;
			int reducedHeight = terrainHeight / reductionFactor;

			indices = new int[(reducedWidth - 1) * (reducedHeight - 1) * 6];
			int counter = 0;

			for (int y = 0; y < reducedWidth - 1; y++)
			{
				for (int x = 0; x < reducedHeight - 1; x++)
				{
					int lowerLeft	= x + y * reducedWidth;
					int lowerRight	= (x + 1) + y * reducedWidth;
					int topLeft		= x + (y + 1) * reducedWidth;
					int topRight	= (x + 1) + (y + 1) * reducedWidth;

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

			// Use modulus to find out how far away we are from the upper
			// left corner of the cell, then normalize it with the scale.
			float xNormalized = (positionOnMap.X % scale) / scale;
			float zNormalized = (positionOnMap.Z % scale) / scale;

			// nrmalize the height positions and interpolate them.
			float topHeight = MathHelper.Lerp(
				heightData[left, top], heightData[left + 1, top], xNormalized);

			float bottomHeight = MathHelper.Lerp(
				heightData[left, top + 1], heightData[left + 1, top + 1], xNormalized);

			float height = MathHelper.Lerp(topHeight, bottomHeight, zNormalized);

			height *= scale;
			return height + heightmapPosition.Y;
		}
		
		/// <summary>
		/// Draw the terrain mesh with the desired effect
		/// </summary>

		public int Draw(Camera camera, Effect effect, Texture2D texture)
		{
			Vector3 reduction = new Vector3(reductionFactor, 1, reductionFactor);

			Matrix worldMatrix = Matrix.CreateScale(reduction * scale);
			worldMatrix *= Matrix.CreateTranslation(heightmapPosition);

			//RasterizerState rWireframeState = new RasterizerState();
			//rWireframeState.FillMode = FillMode.WireFrame;
			//graphicsDevice.RasterizerState = rWireframeState;

			effect.Parameters["Texture"].SetValue(mainTexture);

			// Set world transformation for the map
			effect.Parameters["World"].SetValue(worldMatrix);

			if (effect.CurrentTechnique != effect.Techniques["Default"])
			{
				// Set camera transformation matrices
				effect.Parameters["View"].SetValue(camera.View);
				effect.Parameters["Projection"].SetValue(camera.Projection);

				// Additional camera parameters
				effect.Parameters["WorldInverseTranspose"].SetValue(
					Matrix.Transpose(Matrix.Invert(worldMatrix)));
				effect.Parameters["CameraPosition"].SetValue(camera.Position);
			}

			foreach (EffectPass pass in effect.CurrentTechnique.Passes)
			{
				pass.Apply();

				graphicsDevice.DrawUserIndexedPrimitives(
					PrimitiveType.TriangleList, vertices, 0, 
					vertices.Length, indices, 0, 
					indices.Length / 3, VertexPositionNormalTexture.VertexDeclaration);
			}

			// Add to the total number of polygons drawn;
			return indices.Length / 3;
		}
	}
}
