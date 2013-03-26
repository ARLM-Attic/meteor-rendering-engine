using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	/// <summary>
	/// Effect class for a regular GBuffer, containing diffuse/albedo rendertarget.
	/// Other GBuffer targets and the clear GBuffer effect are inherited from SmallGBuffer.
	/// </summary>
	public class GBufferShader : SmallGBufferShader
	{
		/// Color and specular intensity
		RenderTarget2D diffuseRT;
		RenderTarget2D specularRT;

		/// <summary>
		/// Load the GBuffer content
		/// </summary> 

		public GBufferShader(RenderProfile profile, ContentManager content)
			: base(profile, content)
		{
			// Diffuse/albedo render target
			diffuseRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), 
				SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 4);

			// Specular render target
			specularRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling),
				SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 4);

			outputTargets = new RenderTarget2D[4];
			outputTargets[0] = normalRT;
			outputTargets[1] = depthRT;
			outputTargets[2] = diffuseRT;
			outputTargets[3] = specularRT;

			bindingTargets = new RenderTargetBinding[4];
			bindingTargets[0] = outputTargets[0];
			bindingTargets[1] = outputTargets[1];
			bindingTargets[2] = outputTargets[2];
			bindingTargets[3] = outputTargets[3];
		}

		/// <summary>
		/// Clear the GBuffer and render scene to it
		/// </summary> 

		public RenderTarget2D[] Draw(Scene scene, Camera camera)
		{
			renderStopWatch.Reset();
			renderStopWatch.Restart();

			// Set the G-Buffer
			graphicsDevice.BlendState = BlendState.Opaque;
			graphicsDevice.SetRenderTargets(bindingTargets);
			graphicsDevice.DepthStencilState = DepthStencilState.Default;
			graphicsDevice.Clear(Color.Transparent);

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
			sceneCuller.CullLights(scene, camera);
			sceneCuller.CullTerrainPatches(scene, camera);
			sceneCuller.CullModelMeshes(scene, camera);

			// Render the scene
			sceneRenderer.UseTechnique("GBufferTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainGBufferEffect);

			sceneRenderer.UseTechnique("GBuffer");
			sceneRenderer.Draw(scene, camera, gBufferEffect);

			// Render the skybox and update the sampler state
			graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			sceneRenderer.UseTechnique("Skybox");
			sceneRenderer.DrawSkybox(scene, camera);

			renderStopWatch.Stop();
			return outputTargets;
		}
	}

	/// <summary>
	/// A smaller GBuffer class, with only depth and normal data rendertargets.
	/// Mostly useful for light pre-pass rendering.
	/// </summary>

	public class SmallGBufferShader : BaseShader
	{
		/// Normals and specular power
		protected RenderTarget2D normalRT;

		/// Scene depth
		protected RenderTarget2D depthRT;

		/// External sources for GBuffer effects
		protected Effect gBufferEffect;
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

		public SmallGBufferShader(RenderProfile profile, ContentManager content)
			: base(profile, content)
		{
			// Normal render targets
			normalRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling),
				SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 4);

			depthRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling), 
				SurfaceFormat.Rg32, DepthFormat.None, 4);

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
			gBufferEffect = content.Load<Effect>("renderGBuffer");
			terrainGBufferEffect = content.Load<Effect>("terrainGBuffer");
			clearBufferEffect = content.Load<Effect>("clearGBuffer");
		}

		/// <summary>
		/// Clear the small GBuffer and render scene to it
		/// </summary> 

		public RenderTarget2D[] Draw(Scene scene, Camera camera)
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
			sceneCuller.CullLights(scene, camera);
			sceneCuller.CullModelMeshes(scene, camera);

			// Render the scene
			sceneRenderer.UseTechnique("SmallGBufferTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainGBufferEffect);

			sceneRenderer.UseTechnique("SmallGBuffer");
			sceneRenderer.Draw(scene, camera, gBufferEffect);

			renderStopWatch.Stop();
			return outputTargets;
		}
	}
}