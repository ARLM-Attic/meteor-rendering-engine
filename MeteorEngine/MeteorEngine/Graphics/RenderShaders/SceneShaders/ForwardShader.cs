﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class ForwardShader : BaseShader
    {
        /// Color and specular intensity
        RenderTarget2D forwardLightingRT;

		/// Forward rendering effects
		Effect forwardRenderEffect;
		Effect terrainForwardRenderEffect;

		/// Array for looping
		Effect[] renderEffects;

        public ForwardShader(RenderProfile profile, ContentManager content)
            : base(profile, content) 
		{
			hasSceneInput = true;

            // Diffuse render target
			forwardLightingRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling),
				SurfaceFormat.Color, DepthFormat.Depth24, 4);

			// Set new half-pixel values to reflect new sizes
			halfPixel.X = 0.5f / (float)(backBufferWidth * bufferScaling);
			halfPixel.Y = 0.5f / (float)(backBufferHeight * bufferScaling);

			outputTargets = new RenderTarget2D[] 
			{
				forwardLightingRT
			};

			// Load the shader effects
			forwardRenderEffect = LoadEffect("forwardRender");
			terrainForwardRenderEffect = LoadEffect("terrainForwardRender");

			renderEffects = new Effect[2] { forwardRenderEffect, terrainForwardRenderEffect };
        }

        /// <summary>
        /// Simply draw the scene to the render target
        /// </summary> 

        public RenderTarget2D[] Draw(Scene scene, Camera camera)
        {
			renderStopWatch.Reset();
			renderStopWatch.Restart();

            // Prepare the forward rendering
            graphicsDevice.SetRenderTarget(forwardLightingRT);
			graphicsDevice.Clear(Color.Transparent);
			graphicsDevice.DepthStencilState = DepthStencilState.Default;
			
			// Sampler states for the diffuse map
			graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
			graphicsDevice.SamplerStates[1] = SamplerState.PointWrap;

			// Cull the objects
			sceneCuller.CullLights(scene, camera);
			sceneCuller.CullTerrainPatches(scene, camera);
			sceneCuller.CullModelMeshes(scene, camera);

			forwardRenderEffect.Parameters["camPosition"].SetValue(camera.position);
			terrainForwardRenderEffect.Parameters["CameraPosition"].SetValue(camera.position);

			foreach (Effect renderEffect in renderEffects)
			{
				renderEffect.Parameters["ambientTerm"].SetValue(scene.ambientLight);
				renderEffect.Parameters["inverseView"].SetValue(Matrix.Invert(camera.view));

				// For now, only the first light is applied
				foreach (DirectionLight light in scene.directionalLights)
				{
					renderEffect.Parameters["lightDirection"].SetValue(light.direction);
					renderEffect.Parameters["lightColor"].SetValue(
						new Vector4(light.color.ToVector3(), light.intensity));
				}
			}
            // Forward render the scene
			sceneRenderer.UseTechnique("ForwardRender");
			sceneRenderer.Draw(scene, camera, forwardRenderEffect);

			sceneRenderer.UseTechnique("ForwardRenderTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainForwardRenderEffect);
			
			// Render the skybox and update sampler state
			graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			sceneRenderer.UseTechnique("Skybox");
			sceneRenderer.DrawSkybox(scene, camera);

			renderStopWatch.Stop();
			
			return outputs;
        }
    }
}
