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
		/// Constructor to initialize the renderer
		/// </summary>

		public LightPrePassRenderer(GraphicsDevice graphics, 
			ResourceContentManager content) : base(graphics, content) { }

		/// <summary>
		/// Load all the renderers needed for this profile
		/// </summary>

		public override void Initialize()
		{
			base.Initialize();

			smallGBuffer = new SmallGBufferShader(this, resxContent);
			lights = new LightShader(this, resxContent);
			diffuse = new DiffuseShader(this, resxContent);
			composite = new CompositeShader(this, resxContent);
			blur = new BlurShader(this, resxContent);
			copy = new CopyShader(this, resxContent);
			ssao = new SSAOShader(this, resxContent);
			dof = new DepthOfFieldShader(this, resxContent);
			bloom = new BloomShader(this, resxContent);
			fxaa = new FXAAShader(this, resxContent);
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
			ssao.SetInputs(scene, camera, smallGBuffer.outputs);
			composite.SetInputs(scene, camera, 
				diffuse.outputs[0], lights.outputs[0], ssao.outputs[0], smallGBuffer.outputs[1]);
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
			debugRenderTargets.Add(lights.outputs[1]);
		}

		public override void Draw()
		{
			// Create the lighting map
			smallGBuffer.Draw();
			lights.Draw();

			// Forward render the scene with diffuse only 
			diffuse.Draw();

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
