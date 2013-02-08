using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace Meteor.Rendering
{
	public class GBufferShader : SmallGBufferShader
	{
		/// Other GBuffer targets and the clear Effect
		/// are inherited from SmallGBuffer

		/// Color and specular intensity
		RenderTarget2D diffuseRT;

		/// <summary>
		/// Load the GBuffer content
		/// </summary> 

		public GBufferShader(RenderProfile profile, ResourceContentManager content)
			: base(profile, content)
		{
			// Diffuse/albedo render target

			diffuseRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), 
				SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

			outputTargets = new RenderTarget2D[3];
			outputTargets[0] = normalRT;
			outputTargets[1] = depthRT;
			outputTargets[2] = diffuseRT;

			bindingTargets = new RenderTargetBinding[3];
			bindingTargets[0] = outputTargets[2];
			bindingTargets[1] = outputTargets[0];
			bindingTargets[2] = outputTargets[1];
		}

		/// <summary>
		/// Clear the GBuffer and render scene to it
		/// </summary> 

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Reset();
			renderStopWatch.Restart();

			// Set the G-Buffer
			graphicsDevice.BlendState = BlendState.Opaque;
			graphicsDevice.SetRenderTargets(bindingTargets);
			graphicsDevice.DepthStencilState = DepthStencilState.Default;
			graphicsDevice.Clear(Color.Black);

			// Reset the sampler states after SpriteBatch
			graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
			graphicsDevice.SamplerStates[1] = SamplerState.PointWrap;
			// HDR render target
			graphicsDevice.SamplerStates[4] = SamplerState.PointWrap;

			// Clear the G-Buffer
			clearBufferEffect.CurrentTechnique = clearBufferEffect.Techniques["Clear"];
			clearBufferEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// Cull the objects
			SceneCuller.CullLights(scene, camera);
			sceneRenderer.CullModelMeshes(scene, camera);

			// Render the scene
			sceneRenderer.UseTechnique("GBuffer");
			sceneRenderer.Draw(scene, camera);

			sceneRenderer.UseTechnique("GBufferTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainGBufferEffect);

			// Render the skybox
			// Update the sampler state
			graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			sceneRenderer.UseTechnique("Skybox");
			sceneRenderer.DrawSkybox(scene, camera);

			renderStopWatch.Stop();

			return outputTargets;
		}
	}

	public class SmallGBufferShader : BaseShader
	{
		/// Normals and specular power
		protected RenderTarget2D normalRT;

		/// Scene depth
		protected RenderTarget2D depthRT;

		/// External source for GBuffer effect
		protected Effect terrainGBufferEffect;

		/// Source effect for clearing GBuffer
		protected Effect clearBufferEffect;

		/// <summary>
		/// Render binding target array used by both GBuffer shaders.
		/// </summary>
		protected RenderTargetBinding[] bindingTargets;

		/// <summary>
		/// Load the GBuffer content
		/// </summary> 

		public SmallGBufferShader(RenderProfile profile, ResourceContentManager content)
			: base(profile, content)
		{
			// Normal render targets
			normalRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), 
				SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

			depthRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), SurfaceFormat.Rg32, DepthFormat.None);

			// Set new half-pixel values to reflect new sizes
			halfPixel.X = 0.5f / (float)(backBufferWidth * bufferScaling);
			halfPixel.Y = 0.5f / (float)(backBufferHeight * bufferScaling);

			bindingTargets = new RenderTargetBinding[2];
			bindingTargets[0] = normalRT;
			bindingTargets[1] = depthRT;

			outputTargets = new RenderTarget2D[2];
			outputTargets[0] = normalRT;
			outputTargets[1] = depthRT;

			// Load the shader effects
			terrainGBufferEffect = content.Load<Effect>("terrainGBuffer");
			clearBufferEffect = content.Load<Effect>("clearGBuffer");
		}

		/// <summary>
		/// Clear the small GBuffer and render scene to it
		/// </summary> 

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Reset();
			renderStopWatch.Restart();

			// Set the small GBuffer
			graphicsDevice.BlendState = BlendState.Opaque;
			graphicsDevice.SetRenderTargets(bindingTargets);
			graphicsDevice.Clear(Color.Transparent);
			graphicsDevice.DepthStencilState = DepthStencilState.Default;

			// Reset the sampler states after SpriteBatch
			graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
			graphicsDevice.SamplerStates[1] = SamplerState.PointWrap;
			// HDR render target
			graphicsDevice.SamplerStates[4] = SamplerState.PointWrap;

			// Clear the small GBuffer
			clearBufferEffect.CurrentTechnique = clearBufferEffect.Techniques["ClearSmall"];
			clearBufferEffect.CurrentTechnique.Passes[0].Apply();
			quadRenderer.Render(Vector2.One * -1, Vector2.One);

			// Cull objects
			SceneCuller.CullLights(scene, camera);
			sceneRenderer.CullModelMeshes(scene, camera);

			// Render the scene
			sceneRenderer.UseTechnique("SmallGBuffer");
			sceneRenderer.Draw(scene, camera);

			sceneRenderer.UseTechnique("SmallGBufferTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainGBufferEffect);

			renderStopWatch.Stop();
			return outputTargets;
		}
	}
}