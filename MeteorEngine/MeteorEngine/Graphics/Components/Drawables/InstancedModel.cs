﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SkinnedModel;

namespace Meteor.Resources
{
	/// Data structure to contain all instance data
	/// and related vertex buffer for instancing
	public class MeshInstanceGroup
	{
		/// List of all instances present for this mesh
		public List<MeshInstance> instances;

		/// List of all visible instances after culling
		public List<MeshInstance> visibleInstances;

		/// Number of visible instances after culling
		public int totalVisible = 0;

		/// Temporary transforms for copying
		public Matrix[] tempTransforms;

		/// Tightest bounding box that fits this mesh
		public BoundingBox boundingBox;

		/// Vertex buffer for all mesh instances
		public DynamicVertexBuffer instanceVB;

		/// Name take from the mesh
		public string meshName;

		/// <summary>
		/// Default constructor
		/// </summary>
		public MeshInstanceGroup()
		{
			instances = new List<MeshInstance>();
			visibleInstances = new List<MeshInstance>();
			tempTransforms = new Matrix[1];

			meshName = "default";
		}
	}


	public class InstancedModel
	{
		/// Model representing the object
		public Model model;

		/// Pointer for the last updated instance
		private int lastInstance;

		/// Model's diffuse color textures
		public List<Texture2D> textures;

		/// Model's normal/bump map textures
		public List<Texture2D> normalMapTextures;

		/// Array for the instancing groups of each mesh
		/// Instances are grouped by mesh name
		Dictionary<string, MeshInstanceGroup> meshInstanceGroups;
		public Dictionary<string, MeshInstanceGroup> MeshInstanceGroups
		{
			get { return meshInstanceGroups; }
		}

		/// Initialize an array of bounding box indices
		public static readonly short[] bBoxIndices = {
			0, 1, 1, 2, 2, 3, 3, 0,
			4, 5, 5, 6, 6, 7, 7, 4,
			0, 4, 1, 5, 2, 6, 3, 7
		};

		/// Vertex declaration for mesh instancing, storing a 4x4 world transformation matrix
		public static VertexDeclaration instanceVertexDec = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
			new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
			new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3)
		);

		/// Vertex structure for the colored bounding boxes
		public VertexPositionColor[] boxVertices;

		/// Animator to link with a skinned mesh
		public AnimationPlayer animationPlayer;
		public Matrix[] boneMatrices;

		/// Indicates whether or not to use imposters for faraway objects
		public bool useImposters;

		/// <summary>
		/// Load a model from the ContentManager from a file
		/// </summary>
		/// <param name="modelName">Model's file name</param>
		/// <param name="content">The program's ContentManger</param>

		public InstancedModel(Model newModel, GraphicsDevice graphicsDevice)
		{
			model = newModel;
			animationPlayer = null;

			// Set up model data
			textures = new List<Texture2D>();
			normalMapTextures = new List<Texture2D>();
			meshInstanceGroups = new Dictionary<string, MeshInstanceGroup>();

			// Bounding box data
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Add model matrices
			boneMatrices = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneMatrices);
			int index = 0;

			// Extract textures and create bounding boxes
			foreach (ModelMesh mesh in model.Meshes)
			{
				string meshName = mesh.Name;

				if (mesh.Name == null || mesh.Name == "(null)")
					meshName = "DefaultName_" + index++;

				// Add mesh Instance Group
				meshInstanceGroups.Add(meshName, new MeshInstanceGroup());

				// Build bounding volumes
				Matrix meshTransform = boneMatrices[mesh.ParentBone.Index];
				BoundingSphere boundingSphere = new BoundingSphere();
				meshInstanceGroups[meshName].boundingBox = BuildBoundingBox(mesh, ref boundingSphere);

				// Add instance data
				meshInstanceGroups[meshName].instances.Add(new MeshInstance(boundingSphere));
				meshInstanceGroups[meshName].visibleInstances.Add(new MeshInstance(boundingSphere));
				meshInstanceGroups[meshName].tempTransforms[0] = new Matrix();
				lastInstance = 0; 

				// Add dynamic vertex buffers
				meshInstanceGroups[meshName].instanceVB =
					CreateInstanceVB(graphicsDevice, meshInstanceGroups[meshName].instances);

				// Extract textures from each mesh
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					textures.Add(meshPart.Effect.Parameters["Texture"].GetValueTexture2D());
					normalMapTextures.Add(meshPart.Effect.Parameters["NormalMap"].GetValueTexture2D());
				}
			}
		}

		/// <summary>
		/// Create a bounding box for each model mesh
		/// </summary>

		private static BoundingBox BuildBoundingBox(ModelMesh mesh, ref BoundingSphere sphere)
		{
			// Create initial variables to hold min and max xyz values for the mesh
			Vector3 meshMax = new Vector3(float.MinValue);
			Vector3 meshMin = new Vector3(float.MaxValue);

			Vector3[] vertexPositions = null;

			foreach (ModelMeshPart part in mesh.MeshParts)
			{
				// The stride is how big, in bytes, one vertex is in the vertex buffer
				// We have to use this as we do not know the make up of the vertex
				int stride = part.VertexBuffer.VertexDeclaration.VertexStride;

				VertexPositionNormalTexture[] vertexData = new VertexPositionNormalTexture[part.NumVertices];
				vertexPositions = new Vector3[part.NumVertices];

				part.VertexBuffer.GetData(part.VertexOffset * stride, vertexData, 0, part.NumVertices, stride);

				// Find minimum and maximum xyz values for this mesh part
				Vector3 vertPosition = new Vector3();

				for (int i = 0; i < vertexData.Length; i++)
				{
					vertPosition = vertexData[i].Position;
					vertexPositions[i] = vertexData[i].Position;

					// update our values from this vertex
					meshMin = Vector3.Min(meshMin, vertPosition);
					meshMax = Vector3.Max(meshMax, vertPosition);
				}
			}

			// Create the bounding box
			BoundingBox box = new BoundingBox(meshMin, meshMax);

			sphere = BoundingSphere.CreateFromBoundingBox(box);
			return box;
		}

		/// <summary>
		/// Create an instance vertex buffer from instancing data
		/// </summary>

		private DynamicVertexBuffer CreateInstanceVB(
			GraphicsDevice graphicsDevice, List<MeshInstance> meshInstances)
		{
			int totalInstances = meshInstances.Count;
			Matrix[] instances = new Matrix[totalInstances];

			// Copy transform data to InstanceData structure
			for (int i = 0; i < totalInstances; i++)
				instances[i] = meshInstances[i].Transform;

			// Initialize vertex buffer
			DynamicVertexBuffer instanceVB = new DynamicVertexBuffer(
				graphicsDevice, instanceVertexDec, totalInstances, BufferUsage.WriteOnly);
			instanceVB.SetData(instances);

			return instanceVB;
		}

		/// <summary>
		/// Update the instancing vertex buffer
		/// </summary>

		private DynamicVertexBuffer UpdateInstanceVB(MeshInstanceGroup instanceGroup)
		{			
			// Copy transform data to InstanceData structure
			for (int i = 0; i < instanceGroup.totalVisible; i++)
				instanceGroup.tempTransforms[i] = instanceGroup.visibleInstances[i].Transform;

			// Update vertex buffer
			if (instanceGroup.totalVisible > 0)
			{
				instanceGroup.instanceVB.SetData(instanceGroup.tempTransforms, 0, 
					instanceGroup.totalVisible, SetDataOptions.None);
			}

			return instanceGroup.instanceVB;
		}

		/// <summary>
		/// Resize mesh vertex buffer and/or get the bone matrices
		/// </summary>
		public void PrepareMeshData(GraphicsDevice graphicsDevice, MeshInstanceGroup instanceGroup)
		{
			int totalInstances = instanceGroup.instances.Count;
			graphicsDevice.SetVertexBuffers(null);

			/// Resize the vertex buffer for instances if needed
			if (totalInstances > instanceGroup.instanceVB.VertexCount)
			{
				instanceGroup.instanceVB =
					CreateInstanceVB(graphicsDevice, instanceGroup.instances);
			}
			else
			{
				UpdateInstanceVB(instanceGroup);
			}
		}

		/// <summary>
		/// Add a new instance of this model with default transformation.
		/// Returns the object, useful for adding transformations to this instance.
		/// </summary>

		public InstancedModel NewInstance(Matrix transform)
		{
			int i = 0;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				instanceGroup.instances.Add(new MeshInstance(model.Meshes[i++].BoundingSphere, transform));
				instanceGroup.visibleInstances.Add(null);

				Array.Resize(ref instanceGroup.tempTransforms, instanceGroup.tempTransforms.Length + 1);
				lastInstance = instanceGroup.instances.Count - 1; 
			}

			return this;
		}

		/// <summary>
		/// Add a new instance of this model with no transformation.
		/// </summary>

		public InstancedModel NewInstance()
		{
			int i = 0;
			bool foundExisting = false;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				for (int j = 0; j < instanceGroup.instances.Count; j++)
				{
					if (instanceGroup.instances[j] == null)
					{
						instanceGroup.instances[j] = new MeshInstance(model.Meshes[i++].BoundingSphere);
						lastInstance = j;
						foundExisting = true;

						break;
					}
				}
			}

			// We're done here, no need to add to the lists
			if (foundExisting) 
				return this;

			i = 0;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				// We haven't found any null/unused instances so let's just add one now
				instanceGroup.instances.Add(new MeshInstance(model.Meshes[i++].BoundingSphere));
				instanceGroup.visibleInstances.Add(null);

				Array.Resize(ref instanceGroup.tempTransforms, instanceGroup.tempTransforms.Length + 1);
				lastInstance = instanceGroup.instances.Count - 1; 
			}

			return this;
		}

		/// <summary>
		/// Allocates new instances for this model for later updating.
		/// Returns the object, useful for adding transformations to this instance.
		/// </summary>

		public InstancedModel NewInstances(int capacity)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				instanceGroup.instances.AddRange(new List<MeshInstance>(capacity));
				instanceGroup.visibleInstances.AddRange(new List<MeshInstance>(capacity));

				Array.Resize(ref instanceGroup.tempTransforms, instanceGroup.tempTransforms.Length + capacity);
			}

			return this;
		}

		/// <summary>
		/// Set a world transform for all meshes of an instance
		/// </summary>
		/*
		public MeshInstance UpdateInstance(int index, Matrix worldTransform)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
				instanceGroup.instances[instanceGroup.instances.Count - 1].UpdateTransform(worldTransform);

			return meshInstanceGroups[0].instances[0];
		} */

		/// <summary>
		/// Returns the last instance of this model.
		/// </summary>

		public Vector3 Position
		{
			get 
			{ 
				Vector3 position = new Vector3();
				foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
				{
					int last = instanceGroup.instances.Count - 1;
					position = instanceGroup.instances[last].Transform.Translation;
				}
				return position;
			}
		}

		/// <summary>
		/// Helper to transform model and chain to another method
		/// </summary>

		public InstancedModel Transform(Matrix transform)
		{
			Vector3 scale, translation;
			Quaternion rotation;
			transform.Decompose(out scale, out rotation, out translation);

			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].position = translation;
				instanceGroup.instances[last].rotation = rotation;
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		/// <summary>
		/// Helpers to translate model and chain to another method
		/// </summary>

		public InstancedModel Translate(float x, float y, float z)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].position = new Vector3(x, y, z);
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		public InstancedModel Translate(Vector3 translate)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].position = translate;
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		/// <summary>
		/// Helpers to scale model and chain to another method
		/// </summary>

		public InstancedModel Scale(float x, float y, float z)
		{
			return this;
		}

		public InstancedModel Scale(float scale)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].scaling = new Vector3(scale);
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		/// <summary>
		/// Helper to rotate model and chain to another method
		/// </summary>

		public InstancedModel Rotate(float x, float y, float z)
		{
			x = MathHelper.ToRadians(x);
			y = MathHelper.ToRadians(y);
			z = MathHelper.ToRadians(z);

			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].rotation = Quaternion.CreateFromYawPitchRoll(y, x, z);
				instanceGroup.instances[last].UpdateMatrix();
			}
			return this;
		}
	}
}