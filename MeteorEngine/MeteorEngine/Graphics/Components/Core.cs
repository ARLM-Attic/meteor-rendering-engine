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

        /// Useful for all DrawableComponents
        SpriteFont font;
        SpriteBatch spriteBatch;

		/// <summary>
		/// Content loaders (by file or by resource)
		/// </summary>
        ContentManager content;
		ResourceContentManager resxContent;

		Texture2D nullTexture;

		/// Used to draw scenes
		SceneRenderer sceneRenderer;

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
			currentCamera.Initialize(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

			return currentCamera;
		}

		/// <summary>
		/// Create and add a render profile based on its type
		/// </summary>

		public void AddRenderProfile(Type renderProfileType)
		{
			/// Instantiate a render profile with the resource content manager
			RenderProfile profile =
				(RenderProfile)Activator.CreateInstance(renderProfileType, graphicsDevice, resxContent);

			renderProfiles.Add(profile);
			currentRenderProfile = profile;
			currentRenderProfile.MapInputs(currentScene, currentCamera);
		}

		/// <summary>
		/// Update viewport and rendertarget profiles for when window has been resized
		/// </summary>

		public void SetViewportSize(int viewportWidth, int viewportHeight)
		{
			Viewport viewport = graphicsDevice.Viewport;

			viewport.Width = Math.Max(1, viewportWidth);
			viewport.Height = Math.Max(1, viewportHeight);

			// Resize the viewport for newer render targets
			graphicsDevice.Viewport = viewport;
			//graphicsDevice.PresentationParameters.BackBufferHeight = viewportWidth;
			//graphicsDevice.PresentationParameters.BackBufferWidth = viewportHeight;
			//graphicsDevice.Reset(graphicsDevice.PresentationParameters);

			// Reset the camera and render profile
			currentCamera.Initialize(viewportWidth, viewportHeight);
			currentRenderProfile.Initialize();

			// Reset the profile inputs
			currentRenderProfile.MapInputs(currentScene, currentCamera);
		}

		/// <summary>
		/// Load debug resources used by the high-level renderer
		/// </summary>

        protected override void LoadContent()
        {
            // Load debug resources
            font = resxContent.Load<SpriteFont>("defaultFont");
            spriteBatch = new SpriteBatch(graphicsDevice);

			// Load up all resource renderers
			sceneRenderer = new SceneRenderer(graphicsDevice, resxContent);
			quadRenderer = new QuadRenderComponent(graphicsDevice);

			nullTexture = resxContent.Load<Texture2D>("null_color");
        }

		/// <summary>
		/// Handles input and updates scenes
		/// </summary>

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

			foreach (Scene scene in scenes)
				scene.Update(gameTime);

			currentCamera.Update(gameTime);

			base.Update(gameTime);
        }
        
        /// <summary>
        /// Main drawing function
        /// </summary>

        public override void Draw(GameTime gameTime)
        {
			List<RenderTarget2D> outputs = new List<RenderTarget2D>();

			// Queue the render targets for each profile
			foreach (RenderProfile renderProfile in renderProfiles)
			{
				if (renderProfile != null)
				{
					renderProfile.Draw();
					outputs.Add(renderProfile.Output);
				}
			}

			// Draw the final image
			graphicsDevice.SetRenderTarget(null);
			graphicsDevice.Clear(Color.Transparent);

			// Set final render target as a transparent layer
			spriteBatch.Begin(0, BlendState.NonPremultiplied, SamplerState.LinearClamp,
				DepthStencilState.None, RasterizerState.CullCounterClockwise);

			foreach (RenderTarget2D output in outputs)
			{
				spriteBatch.Draw(output, new Rectangle(0, 0,
					graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), Color.White);
			}

			spriteBatch.End();

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

			for (int i = 0; i < currentRenderProfile.DebugRenderTargets.Count; i++)
			{
				if (i == 3) rect.Height = halfWidth;

				spriteBatch.Draw(currentRenderProfile.DebugRenderTargets[i], rect, null, Color.White);
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
			spriteBatch.Draw(nullTexture, new Rectangle(0, 0, 250, font.LineSpacing * 7 + 4), 
				new Color(0, 0, 0, 120));
			
			spriteBatch.DrawString(font, debugString.Append("FPS: ").Concat(frameRate),
				new Vector2(4, height), Color.LawnGreen);
			debugString.Clear();
			spriteBatch.DrawString(font, debugString.Append("Frame ").Concat(totalFrames),
				new Vector2(4, font.LineSpacing + height), Color.White);
			debugString.Clear();

			// Draw GPU ms counter
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

			// Display camera position

			debugString.Concat(currentCamera.Position.X).Append(", ");
			debugString.Concat(currentCamera.Position.Y).Append(", ");
			debugString.Concat(currentCamera.Position.Z).Append(" ");

			spriteBatch.DrawString(font, debugString,
				new Vector2(4, font.LineSpacing * 4 + height), Color.White);
			debugString.Clear();

			// Display mesh and memory data

			spriteBatch.DrawString(font, debugString.Append("Visible meshes: ").Concat(currentScene.visibleMeshes),
				new Vector2(4, font.LineSpacing * 5 + height), Color.White);
			debugString.Clear();

			long totalMemory = GC.GetTotalMemory(false);
			spriteBatch.DrawString(font, debugString.Append("Total memory: ").Concat(totalMemory, 0),
				new Vector2(4, font.LineSpacing * 6 + height), Color.White);
			debugString.Clear();

			spriteBatch.End();
        }
    }
}