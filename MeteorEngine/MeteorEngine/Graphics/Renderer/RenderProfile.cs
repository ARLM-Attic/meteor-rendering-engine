using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public abstract class RenderProfile : DrawableComponent
	{
		/// List to keep all renderers in order
		protected Dictionary<string, BaseShader> renderTasks;

		/// Reference to the ContentManager to load assets
		protected ContentManager content;

		/// Track all possible starting points for this profile
		/// (Currently not yet implemented)
		protected Dictionary<string, BaseShader> startingPoints;
		protected Dictionary<string, BaseShader>.Enumerator iter;

		/// Render targets used by all the rendering tasks
		protected List<RenderTarget2D> renderTaskTargets;

		protected RenderTarget2D output;

		public RenderTarget2D Output
		{
			get
			{
				return output;
			}
		}

		public List<RenderTarget2D> RenderTaskTargets
		{
			get
			{
				return renderTaskTargets;
			}
		}

		/// Render targets to display for debugging purposes
		protected List<RenderTarget2D> debugRenderTargets;

		public List<RenderTarget2D> DebugTargets
		{
			get
			{
				return debugRenderTargets;
			}
		}

		public RenderProfile(IServiceProvider service, ContentManager content)
			: base(service)
		{
			// Build a map of available RenderShaders
			renderTasks = new Dictionary<string, BaseShader>();
			startingPoints = new Dictionary<string, BaseShader>();
			iter = startingPoints.GetEnumerator();

			debugRenderTargets = new List<RenderTarget2D>();
			renderTaskTargets = new List<RenderTarget2D>();

			this.Disposed += new EventHandler<EventArgs>(DisposeRenderers);
			this.content = content;
		}

		/// <summary>
		/// Initialize the SceneRenderer and call LoadContent.
		/// </summary> 

		public override void Initialize()
		{
			debugRenderTargets.Clear();
			renderTaskTargets.Clear();

			base.Initialize();
		}

		/// <summary>
		/// Mapping input and output of scenes, cameras and render targets.
		/// </summary> 

		public abstract void MapInputs(Scene scene, Camera camera);

		/// <summary>
		/// Helper to add a render task and return that one after newly added
		/// Currently does nothing other than make a list
		/// </summary> 

		protected BaseShader AddRenderTask(BaseShader renderTask)
		{
			renderTasks.Add("Test", renderTask);
			return renderTasks.Last().Value;
		}

		/// <summary>
		/// Add a RenderTarget to the list of targets to use.
		/// </summary> 

		public RenderTarget2D AddRenderTarget(int width, int height,
			SurfaceFormat surfaceFormat, DepthFormat depthFormat)
		{
			return AddRenderTarget(width, height, surfaceFormat, depthFormat,
				RenderTargetUsage.DiscardContents);
		}

		/// <summary>
		/// Add a RenderTarget to the list of targets to use, with specified usage.
		/// </summary> 

		public RenderTarget2D AddRenderTarget(int width, int height, SurfaceFormat surfaceFormat,
			DepthFormat depthFormat, RenderTargetUsage usage)
		{
			renderTaskTargets.Add(new RenderTarget2D(
				graphicsDevice, width, height, false, surfaceFormat, depthFormat, 1, usage));

			return renderTaskTargets.Last();
		}

		public override void Draw(GameTime gameTime)
		{
			base.Draw(gameTime);
		}

		/// <summary>
		/// Dispose of all contents of Renderers used by this render profile.
		/// </summary> 

		public void DisposeRenderers(Object sender, EventArgs e)
		{
			foreach (BaseShader renderTask in renderTasks.Values)
				renderTask.DisposeResources();

			foreach (RenderTarget2D target in renderTaskTargets)
				target.Dispose();

			renderTaskTargets.Clear();
		}
	}
}