﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace Meteor.Rendering
{
	public class DepthOfFieldShader : BaseShader
    {
        /// Final combined pass
        RenderTarget2D dofRT;

		/// The blurred image to blend with the original
		RenderTarget2D blurFactorRT;

        /// DOF effect shader
        Effect blurEffect;

		/// Focal distance
		public float focalDistance = 0.15f;

		/// Range of focus
		public float focalRange = 0.95f;

		public DepthOfFieldShader(RenderProfile profile, ContentManager content)
            : base(profile, content)
        {
            // Light and combined effect targets
            dofRT = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Rgba1010102, DepthFormat.None);
			blurFactorRT = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Rgba1010102, DepthFormat.None);

			outputTargets = new RenderTarget2D[] {
				dofRT, blurFactorRT
			};

            // Load the shader effects
            blurEffect = content.Load<Effect>("blur");
        }

        /// <summary>
        /// Draw the blur effect
        /// </summary>

		public RenderTarget2D[] Draw()
        {
            blurEffect.CurrentTechnique = blurEffect.Techniques["DepthOfField"];

            // Depth of field blur effect
			graphicsDevice.SetRenderTarget(dofRT);
			graphicsDevice.Clear(Color.Transparent);

            blurEffect.Parameters["halfPixel"].SetValue(halfPixel);
			blurEffect.Parameters["focalDistance"].SetValue(focalDistance);
			blurEffect.Parameters["focalRange"].SetValue(focalRange);

            blurEffect.Parameters["diffuseMap"].SetValue(inputTargets[0]);
			blurEffect.Parameters["blurMap"].SetValue(inputTargets[1]);
			blurEffect.Parameters["depthMap"].SetValue(inputTargets[2]);

            blurEffect.CurrentTechnique.Passes[0].Apply();
            quadRenderer.Render(Vector2.One * -1, Vector2.One);

			return outputs;
        }

		/// <summary>
		/// Draw the blur effect with blurred edges
		/// </summary>

		public RenderTarget2D DrawBlurFactor(RenderTarget2D[] targets)
		{
			blurEffect.CurrentTechnique = blurEffect.Techniques["ImprovedDOF"];

			// Depth of field blur effect
			graphicsDevice.SetRenderTarget(blurFactorRT);
			graphicsDevice.Clear(Color.Transparent);

			blurEffect.Parameters["halfPixel"].SetValue(halfPixel);

			blurEffect.Parameters["diffuseMap"].SetValue(targets[0]);
			blurEffect.Parameters["blurMap"].SetValue(targets[1]);
			blurEffect.Parameters["depthMap"].SetValue(targets[2]);

			blurEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			return dofRT;
		}
    }
}
