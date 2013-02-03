﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class BlurShader : BaseShader
    {
        /// Final combined pass
        RenderTarget2D[] finalRT;

        public override RenderTarget2D[] outputs
        {
            get
            {
                return finalRT;
            }
        }

		/// Gaussian blur helper
        Effect blurEffect;
		GaussianBlur blur;

		public BlurShader(RenderProfile profile, ResourceContentManager content)
            : base(profile, content) 
		{
			float blurStep = 2f;

            finalRT = new RenderTarget2D[2];

            // Light and combined effect targets
			finalRT[0] = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Color, DepthFormat.None);
			finalRT[1] = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Color, DepthFormat.None);

            // Load the shader effects
            blurEffect = content.Load<Effect>("blur");

			blur = new GaussianBlur(backBufferWidth, backBufferHeight, blurStep, blurEffect);
			blurEffect.Parameters["halfPixel"].SetValue(halfPixel);
        }

        /// <summary>
        /// Draw the blur effect
        /// </summary>

        public override RenderTarget2D[] Draw()
        {	
            finalRT[0] = inputTargets[0]; // This is the composite render target
            int totalPasses;

			renderStopWatch.Reset();
			renderStopWatch.Restart();

            blurEffect.CurrentTechnique = blurEffect.Techniques["GaussianBlur"];
            totalPasses = blurEffect.CurrentTechnique.Passes.Count;

			// blur effect

            for (int i = 0; i < 2; i++)
            {
                graphicsDevice.SetRenderTarget(finalRT[1 - i % 2]);
                graphicsDevice.Clear(Color.Transparent);

				blurEffect.Parameters["diffuseMap"].SetValue(finalRT[i % 2]);

				// Use horizontal weights for even pass, vertical for odd pass
				if (i % 2 == 0)
				{
					blurEffect.Parameters["sampleWeights"].SetValue(blur.sampleWeightsH);
					blurEffect.Parameters["sampleOffsets"].SetValue(blur.sampleOffsetsH);
				}
				else
				{
					blurEffect.Parameters["sampleWeights"].SetValue(blur.sampleWeightsV);
					blurEffect.Parameters["sampleOffsets"].SetValue(blur.sampleOffsetsV);
				}

                blurEffect.CurrentTechnique.Passes[i].Apply();
                quadRenderer.Render(Vector2.One * -1, Vector2.One);
            }

			renderStopWatch.Stop();
            
            return finalRT;
        }
    }
}
