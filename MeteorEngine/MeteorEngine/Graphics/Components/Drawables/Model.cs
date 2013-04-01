using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkinnedModel;

namespace Meteor.Resources
{
	using XnaModel = Microsoft.Xna.Framework.Graphics.Model;

	public class Model : ITransformable
	{
		/// Model representing the object
		public object modelTag { private set; get; }

		/// List of ModelMeshes
		public List<ModelMesh> modelMeshes { private set; get; }

		/// List of Materials for this model
		public List<Material> materials { private set; get; }

		/// Pointer for the last updated instance
		private int lastInstance;

		/// Model's diffuse color textures
		public List<Texture2D> textures;

		/// Model's normal/bump map textures
		public List<Texture2D> normalMapTextures;
		
		/// Model's scale/rotate/translate parameters
		public Vector3 translation { set; get; }
		public Vector3 rotation { set; get; }
		public float scale { set; get; }

		/// Model's transformation matrix
		public Matrix worldTransform { protected set; get; }
		public Matrix World { get { return worldTransform; } }
		
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
		/// Create an empty Model for loading with later.
		/// </summary>

		public Model()
		{
			// Set up model lists
			textures = new List<Texture2D>();
			normalMapTextures = new List<Texture2D>();
			modelMeshes = new List<ModelMesh>();
			meshInstanceGroups = new Dictionary<string, MeshInstanceGroup>();

			// Bounding box data
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Set world transformation matrix
			scale = (scale == 0) ? 1f : scale;
			worldTransform = Matrix.CreateScale(scale) *
				Matrix.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) *
				Matrix.CreateTranslation(translation);
		}

		/// <summary>
		/// Create a Model and set up mesh data.
		/// </summary>

		public Model(XnaModel model)
		{
			// Set up model lists
			textures = new List<Texture2D>();
			normalMapTextures = new List<Texture2D>();
			materials = new List<Material>();

			modelMeshes = new List<ModelMesh>();
			meshInstanceGroups = new Dictionary<string, MeshInstanceGroup>();

			// Bounding box data
			boxVertices = new VertexPositionColor[BoundingBox.CornerCount];

			// Set up model mesh and texture data
			SetModelData(model);
		}

		/// <summary>
		/// Set up model mesh and texture data.
		/// </summary>
		/// <param name="model"></param>

		public void SetModelData(XnaModel model)
		{
			// Set world transformation matrix
			scale = (scale == 0) ? 1f : scale;
			worldTransform = Matrix.CreateScale(scale) *
				Matrix.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) *
				Matrix.CreateTranslation(translation);

			// Add model matrices
			boneMatrices = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneMatrices);
			int index = 0;

			// Animation data
			modelTag = model.Tag;
			animationPlayer = null;

			// Extract textures and create bounding boxes
			foreach (ModelMesh mesh in model.Meshes)
			{
				string meshName = mesh.Name;

				if (mesh.Name == null || mesh.Name == "(null)")
					meshName = "DefaultName_" + index++;

				// Add to modelMesh list
				modelMeshes.Add(mesh);

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

				// Extract textures from each mesh
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					Material meshMaterial = new Material();

					Texture2D texture = meshPart.Effect.Parameters["Texture"].GetValueTexture2D();
					Texture2D normalMap = meshPart.Effect.Parameters["NormalMap"].GetValueTexture2D();

					if (texture != null)
						meshMaterial.textures.Add("Texture", texture);
					meshMaterial.textures.Add("NormalMap", normalMap);

					materials.Add(meshMaterial);
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
		public void BuildMeshData(GraphicsDevice graphicsDevice, MeshInstanceGroup instanceGroup)
		{
			int totalInstances = instanceGroup.instances.Count;
#if XNA
			graphicsDevice.SetVertexBuffers(null);
#elif MONOGAME
			graphicsDevice.SetVertexBuffer(null);
#endif
			/// Resize the vertex buffer for instances if needed
			if (instanceGroup.instanceVB == null ||
				totalInstances > instanceGroup.instanceVB.VertexCount)
			{
				instanceGroup.instanceVB = CreateInstanceVB(graphicsDevice, instanceGroup.instances);
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

		public Model NewInstance(Matrix transform)
		{
			int i = 0;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				instanceGroup.instances.Add(new MeshInstance(modelMeshes[i++].BoundingSphere, transform));
				instanceGroup.visibleInstances.Add(null);

				Array.Resize(ref instanceGroup.tempTransforms, instanceGroup.tempTransforms.Length + 1);
				lastInstance = instanceGroup.instances.Count - 1; 
			}

			return this;
		}

		/// <summary>
		/// Add a new instance of this model with no transformation.
		/// </summary>

		public Model NewInstance()
		{
			int i = 0;
			bool foundExisting = false;
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				for (int j = 0; j < instanceGroup.instances.Count; j++)
				{
					if (instanceGroup.instances[j] == null)
					{
						instanceGroup.instances[j] = new MeshInstance(modelMeshes[i++].BoundingSphere);
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
				instanceGroup.instances.Add(new MeshInstance(modelMeshes[i++].BoundingSphere));
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

		public Model NewInstances(int capacity)
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

		public Model Update(Matrix worldTransform)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;
				instanceGroup.instances[last].UpdateTransform(worldTransform);
			}

			return this;
		}

		/// <summary>
		/// Returns the last instance of this model.
		/// </summary>

		public Matrix WorldMatrix
		{
			get 
			{ 
				Matrix matrix = Matrix.Identity;
				foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
				{
					int last = instanceGroup.instances.Count - 1;
					matrix = instanceGroup.instances[last].Transform;
				}
				return matrix;
			}
		}

		/// <summary>
		/// Helper to transform model and chain to another method
		/// </summary>

		public Model Transform(Matrix transform)
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

		public Model Translate(float x, float y, float z)
		{
			foreach (MeshInstanceGroup instanceGroup in meshInstanceGroups.Values)
			{
				int last = instanceGroup.instances.Count - 1;

				instanceGroup.instances[last].position = new Vector3(x, y, z);
				instanceGroup.instances[last].UpdateMatrix();
			}

			return this;
		}

		public Model Translate(Vector3 translate)
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

		public Model Scale(float x, float y, float z)
		{
			return this;
		}

		public Model Scale(float scale)
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

		public Model Rotate(float x, float y, float z)
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