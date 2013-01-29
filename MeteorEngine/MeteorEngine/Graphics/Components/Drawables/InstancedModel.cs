using System;
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
			get { return modelTextures; }
		}

		/// List to keep bounding boxes for all model meshes
		Dictionary<string, BoundingBox> boundingBoxes;
		public Dictionary<string, BoundingBox> BoundingBoxes
		{
			get { return boundingBoxes; }
		}

		public Dictionary<string, BoundingBox> tempBoxes;

		/// List to keep position of meshes
		Vector3[] meshPos;
		public Vector3[] MeshPos
		{
			get { return meshPos; }
		}

		/// Array to keep screen space locations of each mesh
		Vector2[] screenPos;
		public Vector2[] ScreenPos
		{
			get { return screenPos; }
		}

		/// Array for the instancing groups of each mesh
		/// Instances are grouped by mesh name
		MeshInstanceGroup[] meshInstanceGroups;
		public MeshInstanceGroup[] MeshInstanceGroups
		{
			get { return meshInstanceGroups; }
		}

		/// Data structure to contain all instance data
		/// and related vertex buffer for instancing
		public class MeshInstanceGroup
		{
			public List<EntityInstance> instances;
			public List<EntityInstance> visibleInstances;
			public Matrix[] tempMatrices;

			public DynamicVertexBuffer instanceVB;
			public string meshName;

			public MeshInstanceGroup(string name)
			{
				instances = new List<EntityInstance>();
				visibleInstances = new List<EntityInstance>();
				tempMatrices = new Matrix[1];

				meshName = name;
			}
		}

		/// Initialize an array of bounding box indices
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
			meshInstanceGroups = new MeshInstanceGroup[model.Meshes.Count];

			// Bounding box data
			boundingBoxes = new Dictionary<string, BoundingBox>();
			tempBoxes = new Dictionary<string, BoundingBox>();
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Set up positions
			meshPos = new Vector3[model.Meshes.Count];
			screenPos = new Vector2[model.Meshes.Count];

			// Add model matrices
			boneMatrices = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneMatrices);

			// Extract textures and create bounding boxes

			int meshIndex = 0;
			foreach (ModelMesh mesh in model.Meshes)
			{
				string meshName = mesh.Name;

				if (mesh.Name == null)
					meshName = "DefaultName";

				// Build bounding boxes
				Matrix meshTransform = boneMatrices[mesh.ParentBone.Index];
				boundingBoxes.Add(meshName, BuildBoundingBox(mesh, meshTransform));

				// Add instance data
				meshInstanceGroups[meshIndex] = new MeshInstanceGroup(meshName);
				meshInstanceGroups[meshIndex].instances.Add(new EntityInstance());

				//Array.Resize(ref meshInstanceGroups[meshIndex].tempMatrices, 1);
				meshInstanceGroups[meshIndex].tempMatrices[0] = new Matrix();

				// Add dynamic vertex buffers
				meshInstanceGroups[meshIndex].instanceVB =
					CreateInstanceVB(graphicsDevice, meshInstanceGroups[meshIndex].instances);

				// Add mesh textures
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
					modelTextures.Add(meshPart.Effect.Parameters["Texture"].GetValueTexture2D());

				meshIndex++;
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
		/// Create an instance vertex buffer from instancing data
		/// </summary>
		
		public DynamicVertexBuffer CreateInstanceVB(
			GraphicsDevice graphicsDevice, List<EntityInstance> meshInstances)
		{
			int totalInstances = meshInstances.Count;
			EntityInstance.InstanceData[] instances = new EntityInstance.InstanceData[totalInstances];

			// Copy transform data to InstanceData structure
			for (int i = 0; i < totalInstances; i++)
				instances[i].transform = meshInstances[i].Transform;

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

			int totalInstances = instanceGroup.instances.Count;
			
			// Copy transform data to InstanceData structure
			for (int i = 0; i < totalInstances; i++)
				instanceGroup.tempMatrices[i] = instanceGroup.instances[i].Transform;

			// Update vertex buffer
			instanceGroup.instanceVB.SetData(instanceGroup.tempMatrices);

			return instanceGroup.instanceVB;
		}

		/// <summary>
		/// Add a new instance of this model.
		/// Returns the object, useful for adding transformations to this instance.
		/// </summary>

		public InstancedModel NewInstance(Matrix transform)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
			{
				instanceGroup.instances.Add(new EntityInstance(transform));
				Array.Resize(ref instanceGroup.tempMatrices, instanceGroup.tempMatrices.Length + 1);
			}

			return this;
		}

		/// <summary>
		/// Add a new instance of this model with no transformation.
		/// </summary>

		public InstancedModel NewInstance()
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
			{
				instanceGroup.instances.Add(new EntityInstance(Matrix.Identity));
				Array.Resize(ref instanceGroup.tempMatrices, instanceGroup.tempMatrices.Length + 1);
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
				foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
				{
					position = instanceGroup.instances[0].position;
					break;
				}
				return position;
			}
		}

		/// <summary>
		/// Helpers to translate model and chain to another method
		/// </summary>

		public InstancedModel Translate(float x, float y, float z)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].position = new Vector3(x, y, z);
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		public InstancedModel Translate(Vector3 translate)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
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
			int i = 0;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
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

			int i = 0;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].rotation = Quaternion.CreateFromYawPitchRoll(y, x, z);
				instanceGroup.instances[last].UpdateMatrix();
			}
			return this;
		}
	}
}