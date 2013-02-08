using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SkinnedModel;

namespace Meteor.Resources
{
	public class Scene
	{
		/// For loading scene content
		ContentManager content;

		/// Used mainly in vertex buffer creation
		GraphicsDevice graphicsDevice;

		/// List of models in the scene
		public Dictionary<String, InstancedModel> staticModels;
		public Dictionary<String, InstancedModel> skinnedModels;
		public Dictionary<String, InstancedModel> blendModels;

		/// Directional light list
		public List<DirectionLight> directionalLights = new List<DirectionLight>();

		/// Point light position to setup current lights
		public List<PointLight> pointLights = new List<PointLight>();

		/// Instanced data used for rendering
		public List<PointLight> visibleLights = new List<PointLight>();

		/// Ambient lighting
		public Vector3 ambientLight = Vector3.Zero;

		/// <summary>
		/// Terrain height map generator
		/// </summary>
		public TerrainMap terrain;

		public List<PointLight> VisiblePointLights
		{
			get { return visibleLights; }
		}

		public int totalLights
		{
			get { return visibleLights.Count; }
		}

		public class OrderedMeshData
		{
			public string modelName;
			public int meshID;
			public float priority;
		};

		public List<OrderedMeshData> orderedMeshes;

		/// Skybox mesh
		InstancedModel skyboxModel;

		public InstancedModel Skybox
		{
			get { return skyboxModel; }
		}

		/// Scene rendering stats
		public int totalPolys;
		public bool debug = false;
		public int visibleMeshes = 0;
		public int culledMeshes = 0;
		public int drawCalls = 0;

		/// <summary>
		/// Create a new scene to reference content with.
		/// </summary>

		public Scene(ContentManager content, GraphicsDevice graphicsDevice)
		{
			this.content = content;
			this.graphicsDevice = graphicsDevice;

			// Set up lists for models
			staticModels = new Dictionary<string, InstancedModel>();
			skinnedModels = new Dictionary<string, InstancedModel>();
			blendModels = new Dictionary<string, InstancedModel>();

			// Ordered list for model sorting
			orderedMeshes = new List<OrderedMeshData>();
		}

		/// <summary>
		/// Helper to add a new point light to the scene and return a reference to it
		/// </summary>

		public PointLight AddPointLight(Vector3 position, Color color, float radius,
			float intensity)
		{
			PointLight light = new PointLight(position, color, radius, intensity);
			pointLights.Add(light);

			return pointLights.Last();
		}

		/// <summary>
		/// Attempt to find the location of the model to be loaded.
		/// </summary>
		private Model FindModel(String directory, String modelPath)
		{
			Model model = null;
			String path = "Models\\" + modelPath;

			if (Directory.Exists(content.RootDirectory + "\\Models\\" + directory))
				path = "Models\\" + directory + "\\" + modelPath;

			model = content.Load<Model>(path);
			return model;
		}

		/// <summary>
		/// Wrapper to find the model with just the file path
		/// </summary>

		private Model FindModel(String modelPath)
		{
			return FindModel(modelPath, modelPath);
		}

		/// <summary>
		/// Add model from a specified directory and filename, setting
		/// the same key name as the file for the model
		/// </summary>

		private InstancedModel AddModel(String directory, String modelPath)
		{
			staticModels.Add(modelPath, new InstancedModel(FindModel(directory, modelPath), graphicsDevice));

			for (int i = 0; i < staticModels[modelPath].model.Meshes.Count; i++)
			{
				orderedMeshes.Add(new OrderedMeshData());
			}

			return staticModels[modelPath];
		}

		/// <summary>
		/// Wrapper to add a model with a full file path
		/// </summary>

		private InstancedModel AddModel(String modelPath)
		{
			return AddModel(modelPath, modelPath);
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public InstancedModel Model(String modelKey)
		{
			return (staticModels.ContainsKey(modelKey)) ?
				staticModels[modelKey] : AddModel(modelKey);
		}

		/// <summary>
		/// Helper to add a new skinned model to the scene with 
		/// the same name key as the file for the model
		/// </summary>

		public InstancedModel AddSkinnedModel(String modelPath, String take = "Take 001")
		{
			skinnedModels.Add(modelPath, new InstancedModel(FindModel(modelPath), graphicsDevice));
			InstancedModel instancedModel = skinnedModels[modelPath];

			// Look up our custom skinning information.
			SkinningData skinningData = instancedModel.model.Tag as SkinningData;

			if (skinningData == null)
				throw new InvalidOperationException
					("This model does not contain a SkinningData tag.");

			// Create an animation player, and start decoding an animation clip.
			instancedModel.animationPlayer = new AnimationPlayer(skinningData);

			AnimationClip clip = skinningData.AnimationClips[take];
			instancedModel.animationPlayer.StartClip(clip);

			return skinnedModels[modelPath];
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public InstancedModel SkinnedModel(String modelKey)
		{
			return (skinnedModels.ContainsKey(modelKey)) ?
				skinnedModels[modelKey] : AddSkinnedModel(modelKey);
		}

		/// <summary>
		/// Helper to add a skybox which will be added to a special Skybox list
		/// </summary>

		public InstancedModel AddSkybox(String modelPath)
		{
			skyboxModel = new InstancedModel(FindModel(modelPath), graphicsDevice);
			return skyboxModel;
		}

		/// <summary>
		/// Helper to add a skybox which will be added to a special Skybox list
		/// </summary>

		private InstancedModel AddBlendModel(String modelPath)
		{
			blendModels.Add(modelPath, new InstancedModel(FindModel(modelPath), graphicsDevice));
			InstancedModel instancedModel = blendModels[modelPath];

			return blendModels[modelPath];
		}

		/// <summary>
		/// Create and add a terrain mesh from an image
		/// </summary>
		/// <param name="imagePath"></param>

		public void AddTerrain(String imagePath, String texture)
		{
			// Set up terrain map
			terrain = new TerrainMap(content, graphicsDevice);

			terrain.GenerateFromImage(imagePath, texture);
		}

		/// <summary>
		/// Return the terrain mesh for this scene
		/// </summary>

		public TerrainMap Terrain()
		{
			return terrain;
		}

		/// <summary>
		/// Update dynamic models, such as skinned mesh animations
		/// </summary>

		public void Update(GameTime gameTime)
		{
			foreach (InstancedModel skinnedModel in skinnedModels.Values)
			{
				if (skinnedModel.animationPlayer != null)
				{
					skinnedModel.animationPlayer.playSpeed = 1f;
					skinnedModel.animationPlayer.Update(gameTime.ElapsedGameTime, true, Matrix.Identity);					
				}
				// Finished updating mesh
			}
		}
	}
}