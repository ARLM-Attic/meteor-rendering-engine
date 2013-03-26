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
	public class DeferredRenderer : RenderProfile
	{
		/// Used for drawing the GBuffer
		GBufferShader gBuffer;

		/// Used for drawing the light map
		LightShader lights;

		/// Draws depth map for shadows
		DepthMapShader depth;

		/// Comination render for final image
		CompositeShader composite;

		/// Render post process blur
		BlurShader blur;

		/// The bloom shader
		BloomShader bloom;

		/// Helper to copy image
		CopyShader copy;

		/// Depth of field effect
		DepthOfFieldShader dof;

		/// SSAO effect
		SSAOShader ssao;

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public DeferredRenderer(GraphicsDevice graphics, 
			ContentManager content) : base(graphics, content) { }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			gBuffer = new GBufferShader(this, content);
			lights = new LightShader(this, content);
			depth = new DepthMapShader(this, content);
			composite = new CompositeShader(this, content);
			dof = new DepthOfFieldShader(this, content);
			blur = new BlurShader(this, content);
			copy = new CopyShader(this, content);
			bloom = new BloomShader(this, content);
			ssao = new SSAOShader(this, content);
		}

		/// <summary>
		/// Map all render target inputs to link the shaders
		/// </summary>

		public override void MapInputs()
		{
			// Map the renderer inputs to outputs
			gBuffer.SetInputs(null);
			depth.SetInputs(null);
			lights.SetInputs(gBuffer.outputs[0], gBuffer.outputs[1],
				gBuffer.outputs[3], depth.outputs[0]);
			composite.SetInputs(gBuffer.outputs[2], lights.outputs[0], ssao.outputs[0], gBuffer.outputs[1]);
			copy.SetInputs(composite.outputs);
			blur.SetInputs(composite.outputs);
			ssao.SetInputs(gBuffer.outputs[0], gBuffer.outputs[1]);
			dof.SetInputs(composite.outputs[0], copy.outputs[0], gBuffer.outputs[1]);
			bloom.SetInputs(dof.outputs);

			composite.includeSSAO = false;

			// Set the debug targets
			debugRenderTargets.Add(gBuffer.outputs[2]);
			debugRenderTargets.Add(gBuffer.outputs[0]);
			debugRenderTargets.Add(lights.outputs[0]);
			debugRenderTargets.Add(depth.outputs[0]);
		}

		public override void Draw(Scene scene, Camera camera)
		{
			// Create the lighting map
			gBuffer.Draw(scene, camera);
			depth.Draw(scene, camera);
			lights.Draw(scene, camera);

			// Composite drawing
			composite.Draw();
			
			// Post effects
			copy.Draw();
			blur.Draw();
			dof.Draw();
			
			output = bloom.Draw()[0];
		}
	}
}
