﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SkinnedModel;

namespace Meteor.Resources
{
	public class InstancedModel
	{
		/// Model representing the object
		public Model model;

		/// Model's main dffuse texture
		public List<Texture2D> modelTextures;

		public List<Texture2D> Textures
		{
			get
			{
				return modelTextures;
			}
		}

		/// Transformation matrix for the model
		Matrix modelMatrix;

		/// Translation vector
		public Vector3 position;

		/// Scaling vector
		public Vector3 scaling;

		/// Rotation and quaternion components
		public Vector3 rotation;
		public Quaternion quaternion;

		/// Number of meshes for this model
		int totalMeshes;

		public int TotalMeshes
		{
			get { return totalMeshes; }
		}

		/// List of visible meshes for this model
		Dictionary<int, ModelMesh> visibleMeshes;
		public Dictionary<int, ModelMesh> VisibleMeshes
		{
			get { return visibleMeshes; }
		}

		/// List to keep bounding boxes for all model meshes
		BoundingBox[] boundingBoxes;
		public BoundingBox[] BoundingBoxes
		{
			get { return boundingBoxes; }
		}

		public BoundingBox[] tempBoxes;
		public Vector3[] tempBoxPos;

		/// List to keep position of meshes
		Vector3[] meshPos;
		public Vector3[] MeshPos
		{
			get { return meshPos; }
		}

		/// List to keep screen space locations of meshes
		Vector2[] screenPos;
		public Vector2[] ScreenPos
		{
			get { return screenPos; }
		}

		// Initialize an array of indices of type short.
		public static readonly short[] bBoxIndices = {
			0, 1, 1, 2, 2, 3, 3, 0,
			4, 5, 5, 6, 6, 7, 7, 4,
			0, 4, 1, 5, 2, 6, 3, 7
		};

		/// <summary>
		/// Vertex declaration for mesh instancing, storing a 4x4 world transformation matrix
		/// </summary>

		public static VertexDeclaration instanceVertexDec = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
			new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
			new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3)
		);

		/// Vertex structure for the colored bounding boxes
		public VertexPositionColor[] boxVertices;

		/// Number of visible instances for current frame
		int totalVisible;

		public int TotalVisible
		{
			get { return totalVisible; }
		}

		/// Animator to link with a skinned mesh
		public AnimationPlayer animationPlayer;
		public Matrix[] boneMatrices;

		/// Model's contentManager
		ContentManager content;

		/// <summary>
		/// Load a model from the ContentManager from a file
		/// </summary>
		/// <param name="modelName">Model's file name</param>
		/// <param name="content">The program's ContentManger</param>

		public InstancedModel(string modelName, string directory, ContentManager content)
		{
			this.content = content;

			try
			{
				String path = "Models\\" + directory + "\\" + modelName;
				model = content.Load<Model>(path);
			}
			catch (Exception e)
			{
				String message = e.Message;
				String path = "Models\\" + modelName;
				model = content.Load<Model>(path);
			}

			totalMeshes = 0;
			totalVisible = 0;
			scaling = Vector3.One;
			animationPlayer = null;

			// Set up model data
			modelTextures = new List<Texture2D>();
			visibleMeshes = new Dictionary<int, ModelMesh>(model.Meshes.Count);

			boundingBoxes = new BoundingBox[model.Meshes.Count];
			tempBoxes = new BoundingBox[model.Meshes.Count];
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Set up positions
			meshPos = new Vector3[model.Meshes.Count];
			screenPos = new Vector2[model.Meshes.Count];
			tempBoxPos = new Vector3[model.Meshes.Count];

			boneMatrices = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneMatrices);

			// Set up matrix and instancing data
			position = Vector3.Zero;
			rotation = Vector3.Zero;
			modelMatrix = Matrix.Identity;
			quaternion = Quaternion.Identity;

			// Extract textures and create bounding boxes

			foreach (ModelMesh mesh in model.Meshes)
			{
				Matrix meshTransform = boneMatrices[mesh.ParentBone.Index];
				boundingBoxes[totalMeshes] = BuildBoundingBox(mesh, meshTransform);

				foreach (ModelMeshPart part in mesh.MeshParts)
				{
					modelTextures.Add(part.Effect.Parameters["Texture"].GetValueTexture2D());
				}
				totalMeshes++;
			}
		}

		/// <summary>
		/// Create a bounding box for each model mesh
		/// </summary>

		private static BoundingBox BuildBoundingBox(ModelMesh mesh, Matrix meshTransform)
		{
			// Create initial variables to hold min and max xyz values for the mesh
			Vector3 meshMax = new Vector3(float.MinValue);
			Vector3 meshMin = new Vector3(float.MaxValue);

			foreach (ModelMeshPart part in mesh.MeshParts)
			{
				// The stride is how big, in bytes, one vertex is in the vertex buffer
				// We have to use this as we do not know the make up of the vertex
				int stride = part.VertexBuffer.VertexDeclaration.VertexStride;

				VertexPositionNormalTexture[] vertexData = new VertexPositionNormalTexture[part.NumVertices];
				part.VertexBuffer.GetData(part.VertexOffset * stride, vertexData, 0, part.NumVertices, stride);

				// Find minimum and maximum xyz values for this mesh part
				Vector3 vertPosition = new Vector3();

				for (int i = 0; i < vertexData.Length; i++)
				{
					vertPosition = vertexData[i].Position;

					// update our values from this vertex
					meshMin = Vector3.Min(meshMin, vertPosition);
					meshMax = Vector3.Max(meshMax, vertPosition);
				}
			}

			// transform by mesh bone transforms
			meshMin = Vector3.Transform(meshMin, meshTransform);
			meshMax = Vector3.Transform(meshMax, meshTransform);

			// Create the bounding box
			BoundingBox box = new BoundingBox(meshMin, meshMax);
			return box;
		}

		/// <summary>
		/// Helpers to translate model and chain to another method
		/// </summary>

		public InstancedModel Translate(float x, float y, float z)
		{
			position = new Vector3(x, y, z);
			return this;
		}

		public InstancedModel Translate(Vector3 translate)
		{
			position = translate;
			return this;
		}

		/// <summary>
		/// Helpers to scale model and chain to another method
		/// </summary>

		public InstancedModel Scale(float x, float y, float z)
		{
			scaling = new Vector3(x, y, z);
			return this;
		}

		public InstancedModel Scale(float scale)
		{
			scaling = new Vector3(scale);
			return this;
		}

		public Matrix Transform
		{
			get { return modelMatrix; }
		}

		/// <summary>
		/// Helper to rotate model and chain to another method
		/// </summary>

		public InstancedModel Rotate(float x, float y, float z)
		{
			x = MathHelper.ToRadians(x);
			y = MathHelper.ToRadians(y);
			z = MathHelper.ToRadians(z);

			rotation = new Vector3(x, y, z);
			quaternion = Quaternion.CreateFromYawPitchRoll(x, y, z);
			return this;
		}

		/// <summary>
		/// Update model's world matrix based on scale, rotation, and translation
		/// </summary>

		public Matrix UpdateMatrix()
		{
			modelMatrix = Matrix.CreateScale(scaling) *
				Matrix.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) *
				Matrix.CreateTranslation(position);

			// Recalculate the screen space position of this mesh
			for (int i = 0; i < totalMeshes; i++)
			{
				tempBoxes[i] = boundingBoxes[i];
				tempBoxes[i].Min = Vector3.Transform(boundingBoxes[i].Min, Transform);
				tempBoxes[i].Max = Vector3.Transform(boundingBoxes[i].Max, Transform);

				// Calculate center of this mesh
				MeshPos[i] = (tempBoxes[i].Max + tempBoxes[i].Min) / 2f;
			}

			return modelMatrix;
		}
	}
}