using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	class CopyShader : BaseShader
	{
		/// For making render target copies
		RenderTarget2D copyRT;

		public override RenderTarget2D[] outputs
		{
			get
			{
				RenderTarget2D[] rtArray =
				{
					copyRT
				};
				return rtArray;
			}
		}

		Effect gBufferEffect;

		public CopyShader(RenderProfile profile, ResourceContentManager content)
			: base(profile, content)
		{
			hasSceneInput = true;

			copyRT = profile.AddRenderTarget(backBufferWidth,
				backBufferHeight, SurfaceFormat.Rgba1010102, DepthFormat.Depth24);

			gBufferEffect = content.Load<Effect>("renderGBuffer");
		}

		/// <summary>
		/// Copy the view to another render target
		/// </summary> 

		public override RenderTarget2D[] Draw()
		{
			// Prepare the rendering
			graphicsDevice.SetRenderTarget(copyRT);
			graphicsDevice.Clear(Color.Transparent);
			graphicsDevice.BlendState = BlendState.Opaque;

			// Copy to a new render target
			gBufferEffect.CurrentTechnique = gBufferEffect.Techniques["PassThrough"];
			gBufferEffect.Parameters["halfPixel"].SetValue(halfPixel);
			gBufferEffect.Parameters["Texture"].SetValue(inputTargets[0]);

			gBufferEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			return outputs;
		}
	}
}
