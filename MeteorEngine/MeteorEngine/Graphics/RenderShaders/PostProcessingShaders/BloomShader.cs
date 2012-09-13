using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class BloomShader : BaseShader
	{
		/// Configurable parameters
		EffectParameter threshold;
		EffectParameter bloomIntensity;
		EffectParameter saturation;
		EffectParameter contrast;

		EffectParameter sampleWeights;
		EffectParameter sampleOffsets;

		EffectParameter diffuseMap;
		EffectParameter blurMap;

		/// Final combined pass
		RenderTarget2D[] finalRT;

		public override RenderTarget2D[] outputs
		{
			get
			{
				return finalRT;
			}
		}

		/// Effect for blurring and blooming
		Effect blurEffect;
		GaussianBlur blur;

		public BloomShader(RenderProfile profile, ContentManager content)
			: base(profile, content)
		{
			finalRT = new RenderTarget2D[3];

			// Light and combined effect targets
			finalRT[0] = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Rgba1010102, DepthFormat.None);

			finalRT[1] = profile.AddRenderTarget(backBufferWidth / 2,
				backBufferHeight / 2, SurfaceFormat.Rgba1010102, DepthFormat.None);
			finalRT[2] = profile.AddRenderTarget(backBufferWidth / 2,
				backBufferHeight / 2, SurfaceFormat.Rgba1010102, DepthFormat.None);

			// Load the shader effects
			blurEffect = content.Load<Effect>("Effects\\blur");
			blur = new GaussianBlur(backBufferWidth, backBufferHeight, 2f, blurEffect);

			// Set shader parameters
			threshold = blurEffect.Parameters["threshold"];
			bloomIntensity = blurEffect.Parameters["bloomFactor"];
			saturation = blurEffect.Parameters["saturation"];
			contrast = blurEffect.Parameters["contrast"];

			// Blur parameters
			sampleWeights = blurEffect.Parameters["sampleWeights"];
			sampleOffsets = blurEffect.Parameters["sampleOffsets"];

			// Texture parameters
			diffuseMap = blurEffect.Parameters["diffuseMap"];
			blurMap = blurEffect.Parameters["blurMap"];

			threshold.SetValue(0.25f);
			bloomIntensity.SetValue(0.85f);
			saturation.SetValue(0.7f);
			contrast.SetValue(1.05f);
		}

		/// <summary>
		/// Draw the blur effect
		/// </summary>

		public override RenderTarget2D[] Draw()
		{
			int totalPasses;

			renderStopWatch.Reset();
			renderStopWatch.Restart();

			blurEffect.CurrentTechnique = blurEffect.Techniques["SimpleBloom"];

			totalPasses = blurEffect.CurrentTechnique.Passes.Count;

			// Prepare 1st pass
			GraphicsDevice.SetRenderTarget(finalRT[2]);
			GraphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[1]);
			blurEffect.Parameters["blurMap"].SetValue(inputTargets[0]);

			blurEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// Prepare 2nd pass
			GraphicsDevice.SetRenderTarget(finalRT[1]);
			GraphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[2]);
			sampleWeights.SetValue(blur.sampleWeightsH);
			sampleOffsets.SetValue(blur.sampleOffsetsH);

			blurEffect.CurrentTechnique.Passes[1].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// Prepare 3rd pass
			GraphicsDevice.SetRenderTarget(finalRT[2]);
			GraphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[1]);
			sampleWeights.SetValue(blur.sampleWeightsV);
			sampleOffsets.SetValue(blur.sampleOffsetsV);

			blurEffect.CurrentTechnique.Passes[2].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// Prepare 4th pass
			GraphicsDevice.SetRenderTarget(finalRT[0]);
			GraphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(inputTargets[0]);
			blurMap.SetValue(finalRT[2]);

			blurEffect.CurrentTechnique.Passes[3].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);
						
			renderStopWatch.Stop();

			return outputs;
		}
	}
}
