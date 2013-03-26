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
		EffectParameter halfPixelParam;

		/// Final combined pass
		RenderTarget2D[] finalRT;

		public override RenderTarget2D[] outputs
		{
			get { return finalRT; }
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

			finalRT[1] = profile.AddRenderTarget(backBufferWidth / 4,
				backBufferHeight / 4, SurfaceFormat.Rgba1010102, DepthFormat.None);
			finalRT[2] = profile.AddRenderTarget(backBufferWidth / 4,
				backBufferHeight / 4, SurfaceFormat.Rgba1010102, DepthFormat.None);

			// Load the shader effects
			blurEffect = LoadEffect("blur");
			blur = new GaussianBlur(backBufferWidth, backBufferHeight, 0.5f, blurEffect);

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
			halfPixelParam = blurEffect.Parameters["halfPixel"];

			threshold.SetValue(0.75f);
			bloomIntensity.SetValue(1f);
			halfPixelParam.SetValue(halfPixel);

			saturation.SetValue(1.25f);
			contrast.SetValue(1.05f);
		}

		/// <summary>
		/// Draw the blur effect
		/// </summary>

		public RenderTarget2D[] Draw()
		{
			int totalPasses;
			renderStopWatch.Start();

			blurEffect.CurrentTechnique = blurEffect.Techniques["SimpleBloom"];

			totalPasses = blurEffect.CurrentTechnique.Passes.Count;

			// 1st pass
			graphicsDevice.SetRenderTarget(finalRT[2]);
			graphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[1]);
			blurEffect.Parameters["blurMap"].SetValue(inputTargets[0]);

			blurEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// 2nd pass
			graphicsDevice.SetRenderTarget(finalRT[1]);
			graphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[2]);
			sampleWeights.SetValue(blur.sampleWeightsH);
			sampleOffsets.SetValue(blur.sampleOffsetsH);

			blurEffect.CurrentTechnique.Passes[1].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// 3rd pass
			graphicsDevice.SetRenderTarget(finalRT[2]);
			graphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(finalRT[1]);
			sampleWeights.SetValue(blur.sampleWeightsV);
			sampleOffsets.SetValue(blur.sampleOffsetsV);

			blurEffect.CurrentTechnique.Passes[2].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// 4th pass
			graphicsDevice.SetRenderTarget(finalRT[0]);
			graphicsDevice.Clear(Color.Transparent);

			diffuseMap.SetValue(inputTargets[0]);
			blurMap.SetValue(finalRT[2]);

			blurEffect.CurrentTechnique.Passes[3].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);
					
			// Finished	
			renderStopWatch.Stop();
			return outputs;
		}
	}
}
