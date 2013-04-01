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
		private ContentManager content;

		/// Used for graphics and other content managers
		private GameServiceContainer services;

		/// <summary>
		/// Drawable scene assets
		/// </summary>
		
		/// List of Models in the scene
		public Dictionary<String, Model> sceneModels { private set; get; }

		/// List of unique ModelMeshes, for rendering
		public Dictionary<String, ModelMesh> sceneMeshes { private set; get; }

		/// Directional light list
		public List<DirectionLight> directionalLights = new List<DirectionLight>();

		/// Point light list
		public List<PointLight> pointLights = new List<PointLight>();

		/// Instanced data used for rendering
		public List<PointLight> visiblePointLights = new List<PointLight>();

		/// Ambient lighting
		public Vector3 ambientLight = Vector3.Zero;

		/// Terrain height map generator
		public Terrain terrain;

		/// Skybox mesh
		public Model skybox { private set; get; }

		/// Scene rendering stats
		public int totalPolys;
		public int visibleMeshes = 0;

		/// <summary>
		/// Create a new scene to reference content with.
		/// </summary>

		public Scene(GameServiceContainer services)
		{
			this.content = new ContentManager(services, "Content");
			this.services = services;

			// Set up lists for models
			sceneModels = new Dictionary<string, Model>();
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
		/// Add a Model to the scene
		/// </summary>

		public Model Add(String modelPath, Model model)
		{
			Model sceneModel = AddModel(modelPath, model);
			return sceneModel;
		}

		/// <summary>
		/// Add model from a specified directory and filename, setting
		/// the same key name as the file for the model
		/// </summary>

		private Model AddModel(String directory, String modelPath, Model model = null)
		{
			if (model != null)
			{
				XnaModel sourceModel = FindModel(directory, modelPath);
				sceneModels.Add(modelPath, model);
				sceneModels[modelPath].SetModelData(sourceModel);
			}
			else
			{
				sceneModels.Add(modelPath, new Model(FindModel(directory, modelPath)));
			}

			// Add all the meshes to the resource pool
			//foreach (ModelMesh mesh in sceneModels[modelPath].modelMeshes)
			//	sceneMeshes.Add(modelPath + "." + mesh.Name, mesh);

			return sceneModels[modelPath];
		}

		/// <summary>
		/// Wrapper to add a model with a full file path
		/// </summary>

		public Model AddModel(String modelPath, Model model = null)
		{
			return AddModel(modelPath, modelPath, model);
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public Model Model(String modelKey)
		{
			return (sceneModels.ContainsKey(modelKey)) ?
				sceneModels[modelKey] : AddModel(modelKey);
		}

		/// <summary>
		/// Helper to add a new skinned model to the scene with 
		/// the same name key as the file for the model
		/// </summary>

		public Model AddSkinnedModel(String modelPath, String take = "Take 001")
		{
			Model skinnedModel = new Model(FindModel(modelPath));

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
			sceneModels.Add(modelPath, skinnedModel);

			return sceneModels[modelPath];
		}

		/// <summary>
		/// Return a model from the list given the same key
		/// </summary>
		/// 
		public Model SkinnedModel(String modelKey, String take = "Take 001")
		{
			return (sceneModels.ContainsKey(modelKey)) ?
				sceneModels[modelKey] : AddSkinnedModel(modelKey, take);
		}

		/// <summary>
		/// Helper to add a skybox which will be added to a special Skybox list
		/// </summary>

		public Model AddSkybox(String modelPath)
		{
			skybox = new Model(FindModel(modelPath));
			return skybox;
		}

		/// <summary>
		/// Create and add a terrain mesh from an image
		/// </summary>
		/// <param name="imagePath"></param>

		public Terrain AddTerrain(Terrain newTerrain)
		{
			// Set up terrain map
			terrain = newTerrain;
			terrain.GenerateFromImage(services);

			return terrain;
		}

		/// <summary>
		/// Update dynamic models, such as skinned mesh animations
		/// </summary>

		public void Update(GameTime gameTime)
		{
			foreach (Model skinnedModel in sceneModels.Values)
			{
				if (skinnedModel.animationPlayer != null)
				{
					skinnedModel.animationPlayer.playSpeed = 1f;
					//skinnedModel.animationPlayer.Update(gameTime.ElapsedGameTime, true, Matrix.Identity);					
				}
				// Finished updating mesh
			}
		}
	}
}