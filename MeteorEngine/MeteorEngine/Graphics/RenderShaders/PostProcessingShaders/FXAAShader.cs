using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace Meteor.Rendering
{
	/// <summary>
	/// Screen-space antialiasing
	/// A nice post-effect for deferred rendering
	/// </summary>

	public class FXAAShader : BaseShader
	{
		/// Final combined pass
		RenderTarget2D finalRT;

		/// Combines lights with diffuse color
		Effect fxaaEffect;

		public FXAAShader(RenderProfile profile, ContentManager content)
			: base(profile, content) 
		{
			// Light and combined effect targets
			finalRT = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Color, DepthFormat.None);

			outputTargets = new RenderTarget2D[]
			{
				finalRT
			};

			// Load the shader effects
			fxaaEffect = content.Load<Effect>("Effects\\fxaa");
		}

		/// <summary>
		/// Draw the anti-aliasing effect
		/// </summary>

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Reset();
			renderStopWatch.Restart();

			fxaaEffect.CurrentTechnique = fxaaEffect.Techniques[0];

			GraphicsDevice.BlendState = BlendState.AlphaBlend;
			GraphicsDevice.SetRenderTarget(finalRT);
			GraphicsDevice.Clear(Color.Transparent);

			// FXAA effect
			fxaaEffect.Parameters["halfPixel"].SetValue(halfPixel);
			fxaaEffect.Parameters["Texture"].SetValue(inputTargets[0]);
			fxaaEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			renderStopWatch.Stop();

			return outputs;
		}
	}
}
