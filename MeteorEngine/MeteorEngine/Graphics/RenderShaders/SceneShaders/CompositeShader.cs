using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace Meteor.Rendering
{
	public class CompositeShader : BaseShader
	{
		/// Final combined pass
		RenderTarget2D finalRT;

		/// Selected render pass
		public int passIndex = 0;
		public bool includeSSAO = true;

		/// Combines lights with diffuse color
		Effect finalComboEffect;

		Random randomNumber;

		public CompositeShader(RenderProfile profile, ResourceContentManager content)
			: base(profile, content)
		{
			// Light and combined effect targets
			finalRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), 
				SurfaceFormat.Color, DepthFormat.None);

			halfPixel.X = 0.5f / (float)(backBufferWidth * bufferScaling);
			halfPixel.Y = 0.5f / (float)(backBufferHeight * bufferScaling);

			randomNumber = new Random();

			outputTargets = new RenderTarget2D[1];
			outputTargets[0] = finalRT;

			// Load the shader effects
			finalComboEffect = content.Load<Effect>("combination");
		}

		/// <summary>
		/// Draw the final composite scene with lights
		/// </summary>

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Start();

			// Setup combination render
			graphicsDevice.SetRenderTarget(finalRT);
			graphicsDevice.Clear(Color.Transparent);
			graphicsDevice.BlendState = BlendState.Opaque;

			// Reset the sampler states after SpriteBatch
			graphicsDevice.SamplerStates[4] = SamplerState.PointWrap;
			graphicsDevice.SamplerStates[5] = SamplerState.PointWrap;

			// Read render target from previous render passes
			finalComboEffect.Parameters["diffuseMap"].SetValue(inputTargets[0]);
			finalComboEffect.Parameters["lightMap"].SetValue(inputTargets[1]);
			finalComboEffect.Parameters["ssaoMap"].SetValue(inputTargets[2]);
			finalComboEffect.Parameters["depthMap"].SetValue(inputTargets[3]);

			// Combine lighting effects with diffuse color
			finalComboEffect.Parameters["includeSSAO"].SetValue(includeSSAO);
			finalComboEffect.Parameters["flicker"].SetValue(1);
			finalComboEffect.Parameters["ambientTerm"].SetValue(scene.ambientLight);
			finalComboEffect.Parameters["halfPixel"].SetValue(halfPixel);

			finalComboEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			renderStopWatch.Stop();
			return outputs;
		}
	}
}
