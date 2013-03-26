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
		SmallGBufferShader smallGBuffer;

		/// Used for drawing the light map
		LightShader lights;

		/// Draws depth map for shadows
		DepthMapShader depth;

		/// Forward render with diffuse color
		DiffuseShader diffuse;

		/// Helper to copy image
		CopyShader copy;

		/// Comination render for final image
		CompositeShader composite;

		/// Render post process blur
		BlurShader blur;

		/// The bloom shader
		BloomShader bloom;

		/// Depth of field effect
		DepthOfFieldShader dof;

		/// FXAA effect
		FXAAShader fxaa;

		/// SSAO effect
		SSAOShader ssao;

		/// <summary>
		/// Constructor to initialize the renderer
		/// </summary>

		public LightPrePassRenderer(GraphicsDevice graphics, 
			ContentManager content) : base(graphics, content) { }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			smallGBuffer = new SmallGBufferShader(this, content);
			lights = new LightShader(this, content);
			depth = new DepthMapShader(this, content);
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

		public override void MapInputs()
		{
			// Map the renderer inputs to outputs
			smallGBuffer.SetInputs(null);
			diffuse.SetInputs(null);
			depth.SetInputs(null);
			lights.SetInputs(smallGBuffer.outputs[0], smallGBuffer.outputs[1], diffuse.outputs[0], depth.outputs[0]);
			ssao.SetInputs(smallGBuffer.outputs);
			composite.SetInputs(diffuse.outputs[0], lights.outputs[0], ssao.outputs[0], smallGBuffer.outputs[1]);
			fxaa.SetInputs(composite.outputs);
			blur.SetInputs(composite.outputs);
			copy.SetInputs(composite.outputs);
			dof.SetInputs(composite.outputs[0], copy.outputs[0], smallGBuffer.outputs[1]);
			bloom.SetInputs(composite.outputs);

			(composite as CompositeShader).includeSSAO = false;

			// Set the debug targets
			debugRenderTargets.Add(diffuse.outputs[0]);
			debugRenderTargets.Add(smallGBuffer.outputs[0]);
			debugRenderTargets.Add(lights.outputs[0]);
			debugRenderTargets.Add(depth.outputs[0]);
		}

		public override void Draw(Scene scene, Camera camera)
		{
			// Create the lighting map
			smallGBuffer.Draw(scene, camera);
			depth.Draw(scene, camera);
			lights.Draw(scene, camera);

			// Forward render the scene with diffuse only 
			diffuse.Draw(scene, camera);

			// Combine with lighting
			composite.Draw();

			// Post effects
			//copy.Draw();
			//blur.Draw();
			
			//dof.Draw();
			output = bloom.Draw()[0];
		}
	}
}
