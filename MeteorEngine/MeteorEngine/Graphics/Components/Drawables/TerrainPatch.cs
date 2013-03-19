using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// Patch of terrain that contains geo mipmapped meshes
	/// </summary>
	
	public class TerrainPatch
	{
		/// Center of the patch
		public Vector3 center { private set; get; }

		/// Absolute and relative locations in the heightmap
		public Vector2 mapOffset { private set; get; }
		public Vector3 worldOffset { private set; get; }

		/// Bounding box extents
		public BoundingBox boundingBox { private set; get; }
		public BoundingSphere boundingSphere { private set; get; }

		private Vector3 bboxMin;
		private Vector3 bboxMax;

		/// Height and width in segments
		public readonly static int patchSize = 64;

		/// Number of terrain LODs
		public readonly static int mipLevels = 4;

		/// Direct neighbors for this patch
		/// 0 - north, 1 - south, 2 - west, 3 - east
		public TerrainPatch[] neighbors;

		/// LOD meshes for this patch
		private TerrainMesh[] meshes;
		public TerrainMesh[] Meshes
		{
			get { return meshes; }
		}

		/// Current mipmap level being used to render
		public int currentMipLevel = 0;

		/// <summary>
		/// Constructor to set up terrain patch
		/// </summary>

		public TerrainPatch(GraphicsDevice graphicsDevice, Vector2 offset)
		{
			neighbors = new TerrainPatch[4];

			meshes = new TerrainMesh[mipLevels];
			mapOffset = offset;
			worldOffset = new Vector3(mapOffset.X * patchSize, 0f, -mapOffset.Y * patchSize);

			for (int i = 0; i < mipLevels; i++)
				meshes[i] = new TerrainMesh(graphicsDevice, this, i);
		}

		/// <summary>
		/// Update vertex data for this patch.
		/// </summary>

		public void UpdateMap(ushort[,] heightData, float scale, Vector3 position, ushort[][] indices)
		{
			// Create the meshes and bounding volumes
			for (int i = 0; i < mipLevels; i++)
				meshes[i].UpdateMesh(heightData, mapOffset, i, indices[i]);

			SetBoundingVolumes(scale, position);
		}

		/// <summary>
		/// Create the BoundingBox and BoundingSphere for this patch.
		/// </summary>

		private void SetBoundingVolumes(float terrainScale, Vector3 terrainPosition)
		{
			int left = (int)mapOffset.X * TerrainPatch.patchSize;
			int right = left + TerrainPatch.patchSize;
			int top = (int)mapOffset.Y * TerrainPatch.patchSize;
			int bottom = top + TerrainPatch.patchSize;

			float minY = 1000000;
			float maxY = -1000000;

			for (int index = 0; index < meshes[0].vertices.Length; index++)
			{
				float height = meshes[0].vertices[index].VertexHeight;

				minY = (minY > height) ? height : minY;
				maxY = (maxY < height) ? height : maxY;
			}

			// Adjust bounding box extents
			bboxMin = new Vector3(left, minY, top);
			bboxMax = new Vector3(right, maxY, bottom);

			Vector3 scale = new Vector3(terrainScale);
			scale.Z = -scale.Z;

			// Transform bounding box to fit the actual terrain
			Matrix bboxTransform = Matrix.CreateScale(scale) *
				Matrix.CreateTranslation(terrainPosition);

			bboxMin = Vector3.Transform(bboxMin, bboxTransform);
			bboxMax = Vector3.Transform(bboxMax, bboxTransform);

			// Calculate the center point
			center = (bboxMin + bboxMax) / 2;

			boundingBox = new BoundingBox(bboxMin, bboxMax);
			boundingSphere = BoundingSphere.CreateFromBoundingBox(boundingBox);
		}
	}
}
