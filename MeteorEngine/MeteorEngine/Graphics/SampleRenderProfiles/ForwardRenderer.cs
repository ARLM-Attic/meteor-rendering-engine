using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;
using Meteor.Rendering;

namespace Meteor.Rendering
{
	public class ForwardRenderer : RenderProfile
	{
		/// Used for drawing the GBuffer
		ForwardShader forward;

		/// Draws depth map for shadows
		DepthMapShader depth;

		/// Render post process blur
		BlurShader blur;

		/// The bloom shader
		BloomShader bloom;

		/// Helper to copy image
		CopyShader copy;

		/// Depth of field effect
		DepthOfFieldShader dof;

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public ForwardRenderer(GraphicsDevice graphics, 
			ResourceContentManager content) : base(graphics, content) { }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			forward = new ForwardShader(this, resxContent);
			depth = new DepthMapShader(this, resxContent);
			dof = new DepthOfFieldShader(this, resxContent);
			blur = new BlurShader(this, resxContent);
			copy = new CopyShader(this, resxContent);
			bloom = new BloomShader(this, resxContent);
		}

		/// <summary>
		/// Map all render target inputs to link the shaders
		/// </summary>

		public override void MapInputs(Scene scene, Camera camera)
		{
			// Map the renderer inputs to outputs
			forward.SetInputs(scene, camera, null);
			depth.SetInputs(scene, camera, null);
			copy.SetInputs(forward.outputs);
			blur.SetInputs(forward.outputs);
			bloom.SetInputs(forward.outputs);

			// Set the debug targets
			debugRenderTargets.Add(forward.outputs[0]);
			debugRenderTargets.Add(depth.outputs[0]);
		}

		public override void Draw()
		{
			// Render the depth shadow maps first
			//depth.Draw();
			forward.Draw();

			/*
			// Post effects
			copy.Draw();
			blur.Draw();
			dof.Draw();
			*/
			output = bloom.Draw()[0];
		}
	}
}
