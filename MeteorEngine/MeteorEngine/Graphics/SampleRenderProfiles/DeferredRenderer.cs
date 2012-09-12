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

		/// Forward render with color map
		DiffuseShader diffuse;

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

		/// DLAA effect	
		FXAAShader fxaa;

		/// SSAO effect
		SSAOShader ssao;

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public DeferredRenderer(IServiceProvider service, ContentManager content)
			: base(service, content) { Initialize(); }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			gBuffer = new GBufferShader(this, content);
			lights = new LightShader(this, content);
			diffuse = new DiffuseShader(this, content);
			composite = new CompositeShader(this, content);
			dof = new DepthOfFieldShader(this, content);
			blur = new BlurShader(this, content);
			copy = new CopyShader(this, content);
			bloom = new BloomShader(this, content);
			fxaa = new FXAAShader(this, content);
			ssao = new SSAOShader(this, content);
		}

		/// <summary>
		/// Map all render target inputs to link the shaders
		/// </summary>

		public override void MapInputs(Scene scene, Camera camera)
		{
			// Map the renderer inputs to outputs
			gBuffer.SetInputs(scene, camera, null);
			lights.SetInputs(scene, camera, gBuffer.outputs);
			composite.SetInputs(scene, camera, gBuffer.outputs[2], lights.outputs[0], ssao.outputs[0]);
			fxaa.SetInputs(composite.outputs);
			copy.SetInputs(fxaa.outputs);
			blur.SetInputs(fxaa.outputs);
			ssao.SetInputs(scene, camera, gBuffer.outputs[0], gBuffer.outputs[1],
				lights.outputs[3]);
			dof.SetInputs(fxaa.outputs[0], copy.outputs[0], gBuffer.outputs[1]);
			bloom.SetInputs(composite.outputs);

			composite.includeSSAO = 0;

			// Set the debug targets
			debugRenderTargets.Add(gBuffer.outputs[2]);
			debugRenderTargets.Add(gBuffer.outputs[1]);
			debugRenderTargets.Add(lights.outputs[0]);
			debugRenderTargets.Add(lights.outputs[1]);
		}

		public override void Draw(GameTime gameTime)
		{
			// Create the lighting map
			gBuffer.Draw();
			lights.Draw();

			// Composite drawing
			composite.Draw();

			// Post effects
			//fxaa.Draw();
			//copy.Draw();
			//blur.Draw();

			//dof.Draw();
			output = bloom.Draw()[0];
		}
	}
}
