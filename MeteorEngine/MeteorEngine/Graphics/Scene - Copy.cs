using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Rendering;
using Meteor.Resources;
using SkinnedModel;

namespace Meteor.Resources
{
    public class Scene
    {
        /// For loading scene content
        ContentManager content;

        /// List of models in the scene
        public Dictionary <String, InstancedModel> staticModels;
		public Dictionary <String, InstancedModel> skinnedModels;

		public InstancedModel[] staticModelsList;
		public InstancedModel[] skinnedModelsList;

		/// Directional light list
		public List<DirectionLight> directionalLights = new List<DirectionLight>();

		/// Point light position to setup current lights
		public List<PointLight> pointLights = new List<PointLight>();

		/// Instanced data used for rendering
		public List<PointLight> visibleLights = new List<PointLight>();

		/// Ambient lighting
		public float ambientLight = 0.0f;

		public List<PointLight> VisiblePointLights
		{
			get
			{
				return visibleLights;
			}
		}

		public int totalLights
		{
			get
			{
				return visibleLights.Count;
			}
		}

		/// Vertex buffer to hold the instance data
		public DynamicVertexBuffer boxVertexBuffer;
		public VertexPositionColor[] boxPrimitiveList;

		/// Skybox mesh
		InstancedModel skyboxModel;

		public InstancedModel Skybox
		{
			get
			{
				return skyboxModel;
			}
		}

		/// Scene rendering stats
		public int totalPolys;
		public bool debug = false;
		public int visibleMeshes = 0;
		public int culledMeshes = 0;
		public int drawCalls = 0;

        public Scene(ContentManager content)
        {
            this.content = content;

			staticModels = new Dictionary <string, InstancedModel>();
			skinnedModels = new Dictionary <string, InstancedModel>();

			staticModelsList = new InstancedModel[1];
			skinnedModelsList = new InstancedModel[1];

			//boxVertexBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(VertexPositionColor), 2, BufferUsage.None);
        }

		/// <summary>
		/// Load any additional scene components
		/// </summary>

		public void LoadContent()
		{
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
        /// Helper to add a new model to the scene with 
        /// the same name key as the file for the model
        /// </summary>
		
        public InstancedModel AddModel(String modelPath)
        {
            staticModels.Add(modelPath, new InstancedModel(modelPath, content));
			InstancedModel instancedModel = staticModels[modelPath];
			
			// Grow array to store bounding box vertices
			int modelCount = staticModels.Count + skinnedModels.Count;
			int totalStaticModels = staticModels.Count;

			Array.Resize(ref staticModelsList, totalStaticModels);
			Array.Resize(ref boxPrimitiveList, modelCount);

			staticModelsList[totalStaticModels - 1] = staticModels[modelPath];

			return staticModels[modelPath];
        }

		/// <summary>
		/// Helper to add a new skinned model to the scene with 
		/// the same name key as the file for the model
		/// </summary>

		public InstancedModel AddSkinnedModel(String modelPath)
		{
			skinnedModels.Add(modelPath, new InstancedModel(modelPath, content));
			InstancedModel instancedModel = skinnedModels[modelPath];
			
			// Look up our custom skinning information.
			SkinningData skinningData = instancedModel.model.Tag as SkinningData;
			
			if (skinningData == null)
				throw new InvalidOperationException
					("This model does not contain a SkinningData tag.");
			
			// Create an animation player, and start decoding an animation clip.
			instancedModel.animationPlayer = new AnimationPlayer(skinningData);

			AnimationClip clip = skinningData.AnimationClips["Take 001"];
			instancedModel.animationPlayer.StartClip(clip);

			// Grow array to store bounding box vertices
			int modelCount = staticModels.Count + skinnedModels.Count;
			int totalSkinnedModels = skinnedModels.Count;

			Array.Resize(ref skinnedModelsList, totalSkinnedModels);
			Array.Resize(ref boxPrimitiveList, modelCount);

			staticModelsList[totalSkinnedModels - 1] = instancedModel;
			
			return instancedModel;
		}

		/// <summary>
		/// Helper to add a skybox which will be added to a special Skybox list
		/// </summary>

		public InstancedModel AddSkybox(String modelPath)
		{
			skyboxModel = new InstancedModel(modelPath, content);
			return skyboxModel;
		}	

        /// <summary>
        /// Helper to add a new model to the scene with the same name key as the file for the model
        /// </summary>
		/// 
        public InstancedModel Model(String modelKey)
        {
			if (staticModels.ContainsKey(modelKey))
			{
				return staticModels[modelKey];
			}
			else
			{
				return skinnedModels[modelKey];
			}
        }

		public void Update(GameTime gameTime)
		{
			drawCalls = 0;

			foreach (InstancedModel skinnedModel in skinnedModels.Values)
			{
				if (skinnedModel.animationPlayer != null)
				{
					//skinnedModel.boneMatrices = skinnedModel.animationPlayer.GetSkinTransforms();
					skinnedModel.animationPlayer.Update(gameTime.ElapsedGameTime, true, Matrix.Identity);

					float currentAnimTime = (float)gameTime.TotalGameTime.TotalSeconds;
					
					skinnedModel.Rotate(0, MathHelper.ToDegrees(-currentAnimTime / 1.7f + MathHelper.Pi), 0).
						Translate(new Vector3(
						(float)Math.Cos(currentAnimTime / 1.7f) * 70 + 20, skinnedModel.position.Y,
						(float)Math.Sin(currentAnimTime / 1.7f) * 70)).updateMatrix();	
					
				}

				// Finished updating mesh
			}
		}

    }
}