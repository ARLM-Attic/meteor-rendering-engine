using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;
using Meteor.Rendering;
using MeteorEngine;

namespace Meteor
{
    public class Core : DrawableComponent
    {
        /// Cameras to render with
		List <Camera> cameras; 

        Camera currentCamera;

        /// Scenes used for rendering
		List <Scene> scenes; 
        Scene currentScene;

		/// Render profiles used for rendering
		List <RenderProfile> renderProfiles;

		/// <summary>
		/// Property for current renderer in use
		/// </summary>
		RenderProfile currentRenderProfile;
		public RenderProfile Renderer
		{
			get { return currentRenderProfile; }
		}

		/// Utility classes
		RenderStats renderStats;
		QuadRenderComponent quadRenderer;
		StringBuilder debugString;

        /// Parameters to set render options
        int rtIndex = 0;
		bool debugText = true;

        public enum RtView
        {
            final,
            diffuse,
            normal,
            lights
        };
        
        /// Specifies which render target to display
        public RtView rtView;

        /// Useful for all DrawableComponents
        SpriteFont font;
        SpriteBatch spriteBatch;

		/// <summary>
		/// Content loaders (by file or by resource)
		/// </summary>
        ContentManager content;
		ResourceContentManager resxContent;

		Texture2D nullTexture;

		float targetWidth;
		float targetHeight;

		/// Used to draw scenes
		SceneRenderComponent sceneRenderer;

        /// Input control
        KeyboardState currentKeyboardState = new KeyboardState();
        KeyboardState lastKeyboardState = new KeyboardState();

		/// <summary>
		/// Constructor without a default scene
		/// </summary>

		public Core(IServiceProvider services)
			: base(services)
		{
			content = new ContentManager(services, "MeteorEngine.Content");
			resxContent = new ResourceContentManager(services, MeteorContentResource.ResourceManager);
			renderStats = new RenderStats();

			currentRenderProfile = null;
			renderProfiles = new List<RenderProfile>();
			debugString = new StringBuilder(64, 64);

			// Setup rendering components
			cameras = new List<Camera>();
			scenes = new List<Scene>();
		}

		/// <summary>
		/// Constructor with a default scene
		/// </summary>

        public Core(IServiceProvider services, Scene scene)
            : base(services)
        {
			content = new ContentManager(services, "MeteorEngine.Content");
			resxContent = new ResourceContentManager(services, MeteorContentResource.ResourceManager);
			renderStats = new RenderStats();

			currentRenderProfile = null;
			renderProfiles = new List<RenderProfile>();
			debugString = new StringBuilder(64, 64);

            // Setup rendering components
			cameras = new List<Camera>();
			scenes = new List<Scene>();

			// Add a default scene
			AddScene(scene);
        }

		public override void Initialize()
		{
			base.Initialize();
		}

		public Scene AddScene(Scene scene)
		{
			scenes.Add(scene);
			currentScene = scenes[scenes.Count - 1];

			return currentScene;
		}

		public void RemoveScenes()
		{
			scenes.Clear();
		}

		public Camera AddCamera(Camera camera)
		{
			cameras.Add(camera);

			currentCamera = cameras[cameras.Count - 1];
			currentCamera.Initialize(targetWidth, targetHeight);

			return currentCamera;
		}

		/// <summary>
		/// Create and add a render profile based on its type
		/// </summary>

		public void AddRenderProfile(Type renderProfileType)
		{
			/// Instantiate a render profile with the resource content manager
			RenderProfile profile =
				(RenderProfile)Activator.CreateInstance(renderProfileType, ServiceContainer, resxContent);

			renderProfiles.Add(profile);
			currentRenderProfile = renderProfiles[renderProfiles.Count - 1];
			currentRenderProfile.MapInputs(currentScene, currentCamera);
		}

		/// <summary>
		/// Update viewport and rendertarget profiles for when window has been resized
		/// </summary>

		public void SetViewportSize(int viewportWidth, int viewportHeight)
		{
			Viewport v = graphicsDevice.Viewport;

			v.Width = viewportWidth;
			v.Height = viewportHeight;

			graphicsDevice.Viewport = v;

			targetWidth = (float)graphicsDevice.Viewport.Width;
			targetHeight = (float)graphicsDevice.Viewport.Height;

			// Restart the render profile
			currentCamera.Initialize(targetWidth, targetHeight);
			currentRenderProfile.Initialize();

			// Reset the inputs
			currentRenderProfile.MapInputs(currentScene, currentCamera);
		}

        protected override void LoadContent()
        {
            // Load debug font
            font = resxContent.Load<SpriteFont>("defaultFont");

            // Miscellaneous stuff
            spriteBatch = new SpriteBatch(graphicsDevice);

			targetWidth = graphicsDevice.Viewport.Width;
			targetHeight = graphicsDevice.Viewport.Height;

			// Load up all available render profiles
			sceneRenderer = new SceneRenderComponent(graphicsDevice, resxContent);
			quadRenderer = new QuadRenderComponent(graphicsDevice);

			nullTexture = resxContent.Load<Texture2D>("null_color");
        }

        public override void Update(GameTime gameTime)
        {
			renderStats.Update(gameTime);

            lastKeyboardState = currentKeyboardState;
            currentKeyboardState = Keyboard.GetState();
			
			// Toggle debug render target display
			if (currentKeyboardState.IsKeyDown(Keys.E) &&
				lastKeyboardState.IsKeyUp(Keys.E))
			{
				rtIndex = 1 - rtIndex;
			}

			// Toggle debug text
			if (currentKeyboardState.IsKeyDown(Keys.Q) &&
				lastKeyboardState.IsKeyUp(Keys.Q))
			{
				debugText = !debugText;
			}

			// Toggle debug meshes
			if (currentKeyboardState.IsKeyDown(Keys.V) &&
				lastKeyboardState.IsKeyUp(Keys.V))
			{
				currentScene.debug = !currentScene.debug;
			}

			// If resources are null, skip updating
			if (currentCamera == null && currentScene == null)
			{
				base.Update(gameTime);			
				return;
			}

			if (currentCamera is FreeCamera)
			{
				(currentCamera as FreeCamera).Update(gameTime);
			}
			else if (currentCamera is DragCamera)
			{
				(currentCamera as DragCamera).Update(gameTime);
			}
			else
			{
				currentCamera.Update();
			}
			currentScene.Update(gameTime);
			
			base.Update(gameTime);
        }
        
        /// <summary>
        /// Main drawing function
        /// </summary>

        public override void Draw(GameTime gameTime)
        {
			RenderTarget2D output = null;

            // Draw the final image
			if (currentRenderProfile != null)
			{				
				currentRenderProfile.Draw(gameTime);
				output = currentRenderProfile.Output;

				graphicsDevice.SetRenderTarget(null);
				//graphicsDevice.Clear(Color.Transparent);
				
				spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.LinearClamp,
					DepthStencilState.None, RasterizerState.CullCounterClockwise);
				spriteBatch.Draw(output, new Rectangle(0, 0,
					(int)targetWidth, (int)targetHeight), Color.White);
				spriteBatch.End();
				
				/// Setup for bounding boxes
				sceneRenderer.DrawBoundingBoxes(currentScene, this.currentCamera);
			}

            if (rtIndex == 1)
                DrawDebugData();

			if (debugText == true)
				DrawDebugText(renderStats.frameRate, (int)renderStats.totalFrames);

			base.Draw(gameTime);
			renderStats.Finish();
        }

        /// <summary>
        /// Draw rendering stats and debug targets
        /// </summary>

        private void DrawDebugData()
        {
            int halfWidth = graphicsDevice.Viewport.Width / 4;
            int halfHeight = graphicsDevice.Viewport.Height / 4;

            //Set up Drawing Rectangle
            Rectangle rect = new Rectangle(0, 0, halfWidth, halfHeight);

			spriteBatch.Begin(0, BlendState.Opaque, SamplerState.PointClamp, 
				DepthStencilState.Default, RasterizerState.CullCounterClockwise);

			for (int i = 0; i < currentRenderProfile.DebugTargets.Count; i++)
			{
				if (i == 3) rect.Height = halfWidth;

				spriteBatch.Draw(currentRenderProfile.DebugTargets[i], rect, null, Color.White);
				rect.X += halfWidth;
			}

			spriteBatch.End();
        }

        /// <summary>
        /// Display text showing render settings and performance
        /// </summary>

        public void DrawDebugText(float frameRate, int totalFrames)
        {
            // Draw FPS counter
			int height = 0;

			spriteBatch.Begin();		
			spriteBatch.Draw(nullTexture, new Rectangle(0, 0, 240, font.LineSpacing * 7 + 4), 
				new Color(0, 0, 0, 120));
			
			spriteBatch.DrawString(font, debugString.Append("FPS: ").Concat(frameRate),
				new Vector2(4, height), Color.LawnGreen);
			debugString.Clear();
			spriteBatch.DrawString(font, debugString.Append("Frame ").Concat(totalFrames),
				new Vector2(4, font.LineSpacing + height), Color.White);
			debugString.Clear();

			//Color color = (renderMethod == 0) ? Color.LawnGreen : Color.Orange;
            //String rendering = (renderMethod == RenderMethod.deferred) ?
             //   "Using deferred rendering" : "Using light pre-pass rendering";
			debugString.Append("GPU: ").Concat((float)renderStats.GpuTime, 2);
			debugString.Append("ms");

			// Print out rendering times
			Color timeColor = (renderStats.GpuTime >= 10) ? Color.Yellow : Color.LawnGreen;
			timeColor = (renderStats.GpuTime >= 16) ? Color.Orange : timeColor;

			spriteBatch.DrawString(font, debugString,
				new Vector2(4, font.LineSpacing * 2 + height), timeColor);
			debugString.Clear();

			debugString.Concat(currentScene.totalPolys).Append(" triangles ");
			debugString.Concat(currentScene.totalLights).Append(" lights");

			spriteBatch.DrawString(font, debugString,
				new Vector2(4, font.LineSpacing * 3 + height), Color.White);
			debugString.Clear();

			//spriteBatch.DrawString(font, debugString.Append("(P) ").Append(rendering),
			//	new Vector2(4, font.LineSpacing * 4 + height), color);
			//debugString.Clear();

			spriteBatch.DrawString(font, debugString.Append("Visible meshes: ").Concat(currentScene.visibleMeshes),
				new Vector2(4, font.LineSpacing * 5 + height), Color.White);
			debugString.Clear();

			long totalMemory = GC.GetTotalMemory(false);
			spriteBatch.DrawString(font, debugString.Append("Total memory: ").Concat(totalMemory, 0),
				new Vector2(4, font.LineSpacing * 6 + height), Color.White);
			debugString.Clear();

			spriteBatch.End(); /*
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, 
				DepthStencilState.None, RasterizerState.CullNone);
			
			foreach (InstancedModel instancedModel in currentScene.staticModels.Values)
			{
				for (int i = 0; i < instancedModel.ScreenPos.Length; i++)
				{
					if (instancedModel.ScreenPos[i].X > -1 || instancedModel.ScreenPos[i].Y > -1)
					{
						float distance = Vector3.Distance(currentCamera.Position, instancedModel.MeshPos[i]);

						Vector2 screenPos = instancedModel.ScreenPos[i];
						screenPos.X *= targetWidth;
						screenPos.Y *= targetHeight;

						spriteBatch.Draw(nullTexture, new Rectangle(
							(int)(screenPos.X), (int)(screenPos.Y),
							(int)(40000f / distance), (int)(8000f / distance)),
							new Color(0, 0, 0, 120));

						spriteBatch.DrawString(font, debugString.Append("OBJ_Mesh "),
							screenPos, Color.White, 0f, Vector2.Zero, 250f / distance, 
							SpriteEffects.None, 1);
						debugString.Clear();

						Vector2 offsetPos = screenPos;
						offsetPos.Y += 4000f / distance;

						spriteBatch.DrawString(font, debugString.
							Concat(instancedModel.MeshPos[i].X, 3).Append(", ").
							Concat(instancedModel.MeshPos[i].Y, 3).Append(", ").
							Concat(instancedModel.MeshPos[i].Z, 3), offsetPos, 
							Color.White, 0f, Vector2.Zero, 250f / distance, SpriteEffects.None, 1);
						debugString.Clear();		
					}
				}
			}		

            spriteBatch.End(); */
        }
    }
}