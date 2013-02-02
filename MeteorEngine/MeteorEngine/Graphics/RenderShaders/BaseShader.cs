﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public abstract class BaseShader
	{
		/// Used to draw screen space quads
		protected QuadRenderComponent quadRenderer;

		/// Used to draw scenes
		protected SceneRenderer sceneRenderer;

		// Backbuffer size for render targets
		protected int backBufferWidth;
		protected int backBufferHeight;

		// Useful for all DrawableComponents
		protected ContentManager content;
		protected ResourceContentManager resxContent;

		public bool hasSceneInput;
		protected Stopwatch renderStopWatch;

		// List to hold generic RenderInputs
		public Dictionary<string, RenderInput> renderInputs;

		// Graphics device for rendering
		protected GraphicsDevice graphicsDevice;

		// Scene to use for rendering
		protected Scene scene;

		// Camera to use for input
		protected Camera camera;

		// Render target inputs
		protected RenderTarget2D[] inputTargets;

		// Render target outputs
		protected RenderTarget2D[] outputTargets;

		public virtual RenderTarget2D[] outputs
		{
			get
			{
				return outputTargets;
			}
		}

		// Texture pixel offset
		protected Vector2 halfPixel;

		/// <summary>
		/// Return length of time it took to render this frame.
		/// </summary> 

		public double renderTime
		{
			get
			{
				return renderStopWatch.Elapsed.TotalMilliseconds;
			}
		}

		/// <summary>
		/// Base renderer where custom RenderTasks derive from, with their
		/// own set of Scenes and render targets for inputs and outputs.
		/// </summary> 

		public BaseShader(RenderProfile profile, ResourceContentManager content)
		{
			hasSceneInput = true;
			this.resxContent = content;

			graphicsDevice = profile.graphicsDevice;

			renderStopWatch = new Stopwatch();
			renderInputs = new Dictionary<string, RenderInput>();

			// Setup rendering components
			quadRenderer = new QuadRenderComponent(graphicsDevice);
			sceneRenderer = new SceneRenderer(graphicsDevice, content);
			LoadContent();
		}

		/// <summary>
		/// Set appropriate data to draw and handle render targets according
		/// to graphics device settings.
		/// </summary> 

		private void LoadContent()
		{
			PresentationParameters pp = graphicsDevice.PresentationParameters;

			//get the sizes of the backbuffer, in order to have matching render targets   
			backBufferWidth = (int)(pp.BackBufferWidth * 1.5f);
			backBufferHeight = (int)(pp.BackBufferHeight * 1.5f);

			halfPixel.X = 0.5f / (float)backBufferWidth;
			halfPixel.Y = 0.5f / (float)backBufferHeight;
		}

		/// <summary>
		/// Apply render target inputs from other sources.
		/// </summary> 

		public void SetInputs(params RenderTarget2D[] targets)
		{
			inputTargets = targets;
			if (targets == null)
				return;

			renderInputs.Clear();
			for (int i = 0; i < targets.Length; i++)
			{
				renderInputs.Add("target_" + i, new RenderTargetInput(targets[i]));
			}
		}

		/// <summary>
		/// Apply scene, camera and render target inputs from other sources.
		/// </summary> 

		public void SetInputs(Scene scene, Camera camera, params RenderTarget2D[] targets)
		{
			this.scene = scene;
			this.camera = camera;

			SetInputs(targets);
		}

		/// <summary>
		/// Send the outputs to a destination SceneRenderer.
		/// </summary> 

		public void SetOutputTo(BaseShader shader, string source, string dest)
		{
			RenderInput srcInput = null;
			RenderInput destInput = null;

			if (this.renderInputs.ContainsKey(source))
				srcInput = this.renderInputs[source];

			if (shader.renderInputs.ContainsKey(source))
				destInput = shader.renderInputs[source];

			// If a match is found, copy the input to the output

			if (srcInput != null && destInput != null)
				destInput = srcInput;
		}

		public abstract RenderTarget2D[] Draw();

		/// <summary>
		/// Get rid of the render targets associated with this renderer.
		/// </summary> 

		public void DisposeResources()
		{
			foreach (RenderTargetInput input in renderInputs.Values)
			{
				//input.target.Dispose();
			}
		}
	}
}
