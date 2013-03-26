using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	/// <summary>
	/// Screen-space ambient occlusion
	/// </summary>

	public class SSAOShader : BaseShader
	{
		// Basic parameters
		public float radius = 1.5f;
		public float intensity = 3f;
		public float scale = 1f;
		public float bias = 0.1f;
		public bool applyBlur = false;

		/// Final combined pass
		RenderTarget2D[] finalRT;

		/// Normal map of random values to sample from
		Texture2D randomMap;

		/// Combines lights with diffuse color
		Effect ssaoEffect;

		/// Blur effect for SSAO
		Effect blurEffect;

		/// Implementation for blur
		GaussianBlur blur;

		public SSAOShader(RenderProfile profile, ContentManager content)	
			: base(profile, content) 
		{
			// Light and combined effect targets
			finalRT = new RenderTarget2D[2];

			finalRT[0] = profile.AddRenderTarget(backBufferWidth / 1,
				backBufferHeight / 1, SurfaceFormat.Alpha8, DepthFormat.None);
						
			finalRT[1] = profile.AddRenderTarget(backBufferWidth / 1,
				backBufferHeight / 1, SurfaceFormat.Alpha8, DepthFormat.None);
			
			outputTargets = new RenderTarget2D[]
			{
				finalRT[0], finalRT[1]
			};

			randomMap = content.Load<Texture2D>("random");

			// Load the shader effects
			ssaoEffect = content.Load<Effect>("ssao");
			ssaoEffect.Parameters["halfPixel"].SetValue(halfPixel);

            blurEffect = content.Load<Effect>("blur");
			blurEffect.Parameters["halfPixel"].SetValue(halfPixel);

			ssaoEffect.Parameters["g_radius"].SetValue(radius);
			ssaoEffect.Parameters["g_intensity"].SetValue(intensity);
			ssaoEffect.Parameters["g_scale"].SetValue(scale);
			ssaoEffect.Parameters["g_bias"].SetValue(bias);

			//ssaoEffect.Parameters["RandomMap"].SetValue(randomMap);

			// Initialize blur
			blur = new GaussianBlur(backBufferWidth, backBufferHeight, 2f, blurEffect);
		}

		/// <summary>
		/// Draw the anti-aliasing effect
		/// </summary>

		public RenderTarget2D[] Draw(Camera camera)
		{
			renderStopWatch.Start();
			ssaoEffect.CurrentTechnique = ssaoEffect.Techniques[0];

			graphicsDevice.BlendState = BlendState.Opaque;
			graphicsDevice.SetRenderTarget(finalRT[0]);
			graphicsDevice.Clear(Color.White);

			// SSAO effect
			ssaoEffect.Parameters["View"].SetValue(camera.view);
			ssaoEffect.Parameters["Projection"].SetValue(camera.projection);
			ssaoEffect.Parameters["invertViewProj"].SetValue(Matrix.Invert(camera.view * camera.projection));
			ssaoEffect.Parameters["invertProjection"].SetValue(Matrix.Invert(camera.projection));

			ssaoEffect.Parameters["NormalBuffer"].SetValue(inputTargets[0]);
			ssaoEffect.Parameters["DepthBuffer"].SetValue(inputTargets[1]);
			ssaoEffect.Parameters["RandomMap"].SetValue(randomMap);

			ssaoEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);
			
			// Blur the SSAO for noise reduction
			blurEffect.CurrentTechnique = blurEffect.Techniques["GaussianBlur"];
			
			if (applyBlur)
			{
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
			}
			
			renderStopWatch.Stop();
			return outputs;
		}
	}
}
