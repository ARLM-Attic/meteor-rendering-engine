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
	public abstract class RenderProfile
	{
		/// List to keep all renderers in order
		protected Dictionary<string, BaseShader> renderTasks;

		/// Reference to the ContentManagers to load assets
		protected ContentManager content;
		protected ResourceContentManager resxContent;

		/// Track all possible starting points for this profile
		/// (Currently not yet implemented)
		protected Dictionary<string, BaseShader> startingPoints;
		protected Dictionary<string, BaseShader>.Enumerator iter;

		/// <summary>
		/// Use the graphics device given by the graphics service
		/// </summary>
		public GraphicsDevice graphicsDevice;

		/// <summary>
		/// Final render output to the buffer for this profile
		/// </summary>
		protected RenderTarget2D output;

		/// <summary>
		/// Returns the final output render target
		/// </summary>
		public RenderTarget2D Output
		{
			get { return output; }
		}

		/// <summary>
		/// Render targets to pool for this profile
		/// </summary>
		protected List<RenderTarget2D> renderTaskTargets;

		/// Render targets to display for debugging purposes
		protected List<RenderTarget2D> debugRenderTargets;
		public List<RenderTarget2D> DebugRenderTargets
		{
			get { return debugRenderTargets; }
		}

		/// <summary>
		/// Constructor for render profile
		/// </summary>
		/// <param name="service"></param>
		/// <param name="content"></param>

		public RenderProfile(GraphicsDevice graphics, ResourceContentManager content)
		{
			// Build a map of available RenderShaders
			renderTasks = new Dictionary<string, BaseShader>();
			startingPoints = new Dictionary<string, BaseShader>();
			iter = startingPoints.GetEnumerator();

			// Create the render target pools
			debugRenderTargets = new List<RenderTarget2D>();
			renderTaskTargets = new List<RenderTarget2D>();

			//this.Disposed += new EventHandler<EventArgs>(DisposeRenderers);
			this.resxContent = content;
			this.graphicsDevice = graphics;

			Initialize();
		}

		/// <summary>
		/// Clear the render target pools
		/// </summary> 

		public virtual void Initialize()
		{
			debugRenderTargets.Clear();
			renderTaskTargets.Clear();
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
			SurfaceFormat surfaceFormat, DepthFormat depthFormat, int samples = 1)
		{
			return AddRenderTarget(width, height, surfaceFormat, depthFormat,
				RenderTargetUsage.DiscardContents, samples);
		}

		/// <summary>
		/// Add a RenderTarget to the list of targets to use, with specified usage.
		/// </summary> 

		public RenderTarget2D AddRenderTarget(int width, int height, SurfaceFormat surfaceFormat,
			DepthFormat depthFormat, RenderTargetUsage usage, int samples = 1)
		{
			renderTaskTargets.Add(new RenderTarget2D(
				graphicsDevice, width, height, false, surfaceFormat, depthFormat, samples, usage));

			return renderTaskTargets.Last();
		}

		/// <summary>
		/// Execute the render profile drawing process.
		/// </summary>
		/// <param name="gameTime"></param>

		public abstract void Draw();

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