using System;
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
		public MeshInstanceGroup(string name)
		{
			instances = new List<MeshInstance>();
			visibleInstances = new List<MeshInstance>();
			tempTransforms = new Matrix[1];

			meshName = name;
		}
	}


	public class InstancedModel
	{
		/// Model representing the object
		public Model model;

		/// Model's main dffuse texture
		public List<Texture2D> modelTextures;
		public List<Texture2D> Textures
		{
			get { return modelTextures; }
		}

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
			modelTextures = new List<Texture2D>();
			meshInstanceGroups = new Dictionary<string, MeshInstanceGroup>();

			// Bounding box data
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Add model matrices
			boneMatrices = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneMatrices);

			// Extract textures and create bounding boxes
			foreach (ModelMesh mesh in model.Meshes)
			{
				string meshName = mesh.Name;

				if (mesh.Name == null)
					meshName = "DefaultName";

				// Add mesh Instance Group
				meshInstanceGroups.Add(meshName, new MeshInstanceGroup(meshName));

				// Build bounding volumes
				Matrix meshTransform = boneMatrices[mesh.ParentBone.Index];
				BoundingSphere boundingSphere = new BoundingSphere();
				meshInstanceGroups[meshName].boundingBox = BuildBoundingBox(mesh, ref boundingSphere);

				// Add instance data
				meshInstanceGroups[meshName].instances.Add(new MeshInstance(boundingSphere));
				meshInstanceGroups[meshName].visibleInstances.Add(new MeshInstance(boundingSphere));
				meshInstanceGroups[meshName].tempTransforms[0] = new Matrix();

				// Add dynamic vertex buffers
				meshInstanceGroups[meshName].instanceVB =
					CreateInstanceVB(graphicsDevice, meshInstanceGroups[meshName].instances);

				// Add mesh textures
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
					modelTextures.Add(meshPart.Effect.Parameters["Texture"].GetValueTexture2D());
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
		
		public DynamicVertexBuffer CreateInstanceVB(
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

		public DynamicVertexBuffer UpdateInstanceVB(MeshInstanceGroup instanceGroup)
		{			
			// Copy transform data to InstanceData structure
			for (int i = 0; i < instanceGroup.totalVisible; i++)
				instanceGroup.tempTransforms[i] = instanceGroup.visibleInstances[i].Transform;

			// Update vertex buffer
			instanceGroup.instanceVB.SetData(instanceGroup.tempTransforms);

			return instanceGroup.instanceVB;
		}

		/// <summary>
		/// Add a new instance of this model.
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
			}

			return this;
		}

		/// <summary>
		/// Add a new instance of this model with no transformation.
		/// </summary>

		public InstancedModel NewInstance()
		{
			int i = 0;

			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				instanceGroup.instances.Add(new MeshInstance(model.Meshes[i++].BoundingSphere));
				instanceGroup.visibleInstances.Add(null);

				Array.Resize(ref instanceGroup.tempTransforms, instanceGroup.tempTransforms.Length + 1);
			}

			return this;
		}

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