using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Resources;
using SkinnedModel;

namespace Meteor.Resources
{
	using XnaModel = Microsoft.Xna.Framework.Graphics.Model;

	public class Scene
	{
		/// For loading scene content
		ContentManager content;

		/// Used mainly in vertex buffer creation
		GraphicsDevice graphicsDevice;

		/// List of models in the scene
		public Dictionary<String, Model> staticModels;
		public Dictionary<String, Model> skinnedModels;

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
		public Terrain terrain;

		public List<PointLight> VisiblePointLights
		{
			get { return visibleLights; }
		}

		public int totalLights
		{
			get { return visibleLights.Count; }
		}

		/// Skybox mesh
		Model skyboxModel;

		public Model Skybox
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
			staticModels = new Dictionary<string, Model>();
			skinnedModels = new Dictionary<string, Model>();
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
		private XnaModel FindModel(String directory, String modelPath)
		{
			XnaModel model = null;
			String path = "Models\\" + modelPath;

			if (Directory.Exists(content.RootDirectory + "\\Models\\" + directory))
				path = "Models\\" + directory + "\\" + modelPath;

			model = content.Load<XnaModel>(path);
			return model;
		}

		/// <summary>
		/// Wrapper to find the model with just the file path
		/// </summary>

		private XnaModel FindModel(String modelPath)
		{
			return FindModel(modelPath, modelPath);
		}

		/// <summary>
		/// Add model from a specified directory and filename, setting
		/// the same key name as the file for the model
		/// </summary>

		private Model AddModel(String directory, String modelPath)
		{
			staticModels.Add(modelPath, new Model(FindModel(directory, modelPath), graphicsDevice));

			return staticModels[modelPath];
		}

		/// <summary>
		/// Wrapper to add a model with a full file path
		/// </summary>

		private Model AddModel(String modelPath)
		{
			return AddModel(modelPath, modelPath);
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public Model Model(String modelKey)
		{
			return (staticModels.ContainsKey(modelKey)) ?
				staticModels[modelKey] : AddModel(modelKey);
		}

		/// <summary>
		/// Helper to add a new skinned model to the scene with 
		/// the same name key as the file for the model
		/// </summary>

		public Model AddSkinnedModel(String modelPath, String take = "Take 001")
		{
			Model skinnedModel = new Model(FindModel(modelPath), graphicsDevice);

			// Look up our custom skinning information.
			SkinningData skinningData = skinnedModel.modelTag as SkinningData;

			if (skinningData == null)
				throw new InvalidOperationException
					("This model does not contain a SkinningData tag.");

			// Create an animation player, and start decoding an animation clip.
			skinnedModel.animationPlayer = new AnimationPlayer(skinningData);

			AnimationClip clip = skinningData.AnimationClips[take];
			skinnedModel.animationPlayer.StartClip(clip);

			// Add to the model list
			skinnedModels.Add(modelPath, skinnedModel);

			return skinnedModels[modelPath];
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public Model SkinnedModel(String modelKey, String take = "Take 001")
		{
			return (skinnedModels.ContainsKey(modelKey)) ?
				skinnedModels[modelKey] : AddSkinnedModel(modelKey, take);
		}

		/// <summary>
		/// Helper to add a skybox which will be added to a special Skybox list
		/// </summary>

		public Model AddSkybox(String modelPath)
		{
			skyboxModel = new Model(FindModel(modelPath), graphicsDevice);
			return skyboxModel;
		}

		/// <summary>
		/// Create and add a terrain mesh from an image
		/// </summary>
		/// <param name="imagePath"></param>

		public void AddTerrain(Terrain terrain)
		{
			// Set up terrain map
			this.terrain = terrain;
			this.terrain.GenerateFromImage();
		}

		/// <summary>
		/// Update dynamic models, such as skinned mesh animations
		/// </summary>

		public void Update(GameTime gameTime)
		{
			foreach (Model skinnedModel in skinnedModels.Values)
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