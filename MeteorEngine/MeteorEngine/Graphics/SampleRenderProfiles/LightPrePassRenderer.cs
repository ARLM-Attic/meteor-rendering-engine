using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class LightPrePassRenderer : RenderProfile
	{
		/// Used for drawing the GBuffer
		BaseShader smallGBuffer;

		/// Used for drawing the light map
		BaseShader lights;

		/// Forward render with color map
		BaseShader diffuse;

		/// Helper to copy image
		BaseShader copy;

		/// Comination render for final image
		BaseShader composite;

		/// Render post process blur
		BaseShader blur;

		/// The bloom shader
		BaseShader bloom;

		/// Depth of field effect
		BaseShader dof;

		/// FXAA effect
		BaseShader fxaa;

		/// SSAO effect
		BaseShader ssao;

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public LightPrePassRenderer(IServiceProvider service, ContentManager content)
			: base(service, content) { Initialize(); }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			smallGBuffer = new SmallGBufferShader(this, content);
			lights = new LightShader(this, content);
			diffuse = new DiffuseShader(this, content);
			composite = new CompositeShader(this, content);
			blur = new BlurShader(this, content);
			copy = new CopyShader(this, content);
			ssao = new SSAOShader(this, content);
			dof = new DepthOfFieldShader(this, content);
			bloom = new BloomShader(this, content);
			fxaa = new FXAAShader(this, content);
		}

		/// <summary>
		/// Map all render target inputs to link the shaders
		/// </summary>

		public override void MapInputs(Scene scene, Camera camera)
		{
			// Map the renderer inputs to outputs
			smallGBuffer.SetInputs(scene, camera, null);
			diffuse.SetInputs(scene, camera, null);
			lights.SetInputs(scene, camera, smallGBuffer.outputs);
			composite.SetInputs(scene, camera, diffuse.outputs[0], lights.outputs[0], ssao.outputs[0]);
			fxaa.SetInputs(composite.outputs);
			blur.SetInputs(fxaa.outputs);
			copy.SetInputs(fxaa.outputs);
			dof.SetInputs(fxaa.outputs[0], copy.outputs[0], smallGBuffer.outputs[1]);
			ssao.SetInputs(scene, camera, smallGBuffer.outputs);
			bloom.SetInputs(fxaa.outputs);

			(composite as CompositeShader).includeSSAO = 0;

			// Set the debug targets
			debugRenderTargets.Add(diffuse.outputs[0]);
			debugRenderTargets.Add(smallGBuffer.outputs[0]);
			debugRenderTargets.Add(lights.outputs[0]);
			debugRenderTargets.Add(lights.outputs[1]);
		}

		public override void Draw(GameTime gameTime)
		{
			// Forward render the scene with diffuse only 
			diffuse.Draw();

			// Create the lighting map
			smallGBuffer.Draw();
			lights.Draw();

			// Combine with lighting
			composite.Draw();

			// Post effects
			fxaa.Draw();

			// Copy DLAA render output
			//copy.Draw();
			//blur.Draw();

			//dof.Draw();
			output = bloom.Draw()[0];
		}
	}
}
