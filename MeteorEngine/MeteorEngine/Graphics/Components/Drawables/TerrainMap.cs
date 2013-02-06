using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	class TriangleTreeNode 
	{ 
		/// <summary>
		/// The corners (in map coordinates) of the node
		/// </summary>
		public Vector2[] corners;

		/// <summary>
		/// The children for this tree node
		/// </summary>
		public TriangleTreeNode leftChild, rightChild;

		/// <summary>
		/// The location that divides the longest edge in half
		/// </summary>
		private Vector2 edgeSplit;

		/// <summary>
		/// minimum length for each node
		/// </summary>
		public static float edgeMinimum = 8;

		/// <summary>
		/// minimum length for each node
		/// </summary>
		public static float heightTolerance = 5f;

		/// <summary>
		/// Create a node with corners and edge coordinates
		/// </summary>
		public TriangleTreeNode(float[,] heights, params Vector2[] triangleCorners)
		{
			// Set this node's corners
			corners = triangleCorners;

			// Split the longest edge
			edgeSplit = (corners[0] + corners[2]) / 2;

			// Calculate the averaged height value from the two corners
			float edgeHeight =
				(heights[(int)corners[0].X, (int)corners[0].Y] +
				heights[(int)corners[2].X, (int)corners[2].Y]) / 2;

			// If the triangle split coordinates are coarser than the threshold,
			// add two new children. Otherwise, we have reached the final node
			// for this tree.
			if (edgeSplit.X % edgeMinimum == 0 || edgeSplit.Y % edgeMinimum == 0)
			{
				leftChild = new TriangleTreeNode(heights, corners[1], edgeSplit, corners[2]);
				rightChild = new TriangleTreeNode(heights, corners[0], edgeSplit, corners[1]);
			}

			// Finish building this node
		}
	};

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
		/// Amount to scale heightmap textures by
		/// </summary>
		private float textureScale = 10f;

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
		private int reductionFactor = 1;

		/// <summary>
		/// Vertex buffer for terain mesh
		/// </summary>
		private VertexBuffer terrainVB;

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
			heightData = new float[terrainWidth, terrainHeight];

			// Add the height values from the map
			for (int x = 0; x < terrainWidth; x++)
				for (int y = 0; y < terrainHeight; y++)
					heightData[x, y] = heightMapColors[x + y * terrainWidth].R / 5.0f;
					
			// Create the terrain tree data.
			Regenerate(TriangleTreeNode.edgeMinimum);
		}

		/// <summary>
		/// Re-create the terrain mesh with the current parameters.
		/// </summary>
		public void Regenerate(float terrainDetail)
		{
			TriangleTreeNode.edgeMinimum = terrainDetail;
		
			GenerateVertexPatches();

			//SetUpVerticesLOD();
			//SetUpIndices();

			CalculateNormals();

			// Set vertex and index buffers
			terrainVB = new VertexBuffer(graphicsDevice,
				VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
			terrainVB.SetData(vertices);

			terrainIndices = new IndexBuffer(graphicsDevice, typeof(int), indices.Length, BufferUsage.WriteOnly);
			terrainIndices.SetData(indices);
		}

		/// <summary>
		/// Set the vertices for a reduced detail version of the mesh.
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
		/// Automatically generate vertex patches based on the ROAM method
		/// </summary>

		private void GenerateVertexPatches()
		{
			// First generate two root (level 0) tree nodes with map coordinates
			Vector2[] bottomLeftCoords = {
				new Vector2(0, 0),
				new Vector2(0, terrainHeight - 1),
				new Vector2(terrainWidth - 1, terrainHeight - 1)
			};

			Vector2[] topRightCoords = {
				new Vector2(0, 0),
				new Vector2(terrainWidth - 1, 0),
				new Vector2(terrainWidth - 1, terrainHeight - 1)
			};

			TriangleTreeNode bottomLeft = new TriangleTreeNode(heightData, bottomLeftCoords);
			TriangleTreeNode topRight = new TriangleTreeNode(heightData, topRightCoords);

			// Get the vertex locations for all tree node patches.
			List<Vector2> patchVertices = new List<Vector2>();
			
			patchVertices.AddRange(GetVerticesFromPatches(bottomLeft));
			patchVertices.AddRange(GetVerticesFromPatches(topRight));

			// Create an array of vertices from the tree patch structure,
			// and set the data.
			vertices = new VertexPositionNormalTexture[patchVertices.Count];

			for (int i = 0; i < patchVertices.Count; i++)
			{
				vertices[i].Position = new Vector3(
					patchVertices[i].X,
					heightData[(int)patchVertices[i].X, (int)patchVertices[i].Y], 
					-patchVertices[i].Y);

				vertices[i].TextureCoordinate.X = (float)patchVertices[i].X / 20.0f;
				vertices[i].TextureCoordinate.Y = (float)patchVertices[i].Y / 20.0f;

				vertices[i].Normal = Vector3.Up;
			}

			// Create the indices
			indices = new int[patchVertices.Count];
			for (int i = 0; i < patchVertices.Count; i++)
				indices[i] = i;

			patchVertices.Clear();
		}

		/// <summary>
		/// Get the vertex locations for all the patches recursively down the tree.
		/// </summary>

		List<Vector2> GetVerticesFromPatches(TriangleTreeNode node)
		{
			List<Vector2> vertices = new List<Vector2>();

			// First traverse through child nodes if they are valid.
			if (node.leftChild != null)
				vertices.AddRange(GetVerticesFromPatches(node.leftChild));

			if (node.rightChild != null)
				vertices.AddRange(GetVerticesFromPatches(node.rightChild));

			// Finally, if its child nodes aren't valid, we have reached the lowest
			// level node, so add its vertices instead.
			if (node.leftChild == null && node.rightChild == null)
				vertices.AddRange(node.corners);

			return vertices;
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

			RasterizerState rWireframeState = new RasterizerState();
			rWireframeState.FillMode = FillMode.WireFrame;
			graphicsDevice.RasterizerState = rWireframeState;

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
