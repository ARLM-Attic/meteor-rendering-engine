﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class DiffuseShader : BaseShader
    {
        /// Color and specular intensity
        RenderTarget2D diffuseRT;

		/// External sources for GBuffer effects
		Effect gBufferEffect;
		Effect terrainGBufferEffect;

        public DiffuseShader(RenderProfile profile, ContentManager content)
            : base(profile, content) 
		{
			hasSceneInput = true;

            // Diffuse render target
			diffuseRT = profile.AddRenderTarget(
				(int)(backBufferWidth * bufferScaling),
				(int)(backBufferHeight * bufferScaling),
				SurfaceFormat.Rgba1010102, DepthFormat.Depth24);

			// Set new half-pixel values to reflect new sizes
			halfPixel.X = 0.5f / (float)(backBufferWidth * bufferScaling);
			halfPixel.Y = 0.5f / (float)(backBufferHeight * bufferScaling);

			outputTargets = new RenderTarget2D[] 
			{
				diffuseRT
			};

			// Load the shader effects
			gBufferEffect = content.Load<Effect>("renderGBuffer");
			terrainGBufferEffect = content.Load<Effect>("terrainGBuffer");
        }

        /// <summary>
        /// Simply draw the scene to the render target
        /// </summary> 

        public RenderTarget2D[] Draw(Scene scene, Camera camera)
        {
			renderStopWatch.Reset();
			renderStopWatch.Restart();

            // Prepare the forward rendering
			graphicsDevice.SetRenderTarget(diffuseRT);
			graphicsDevice.Clear(Color.Transparent);
			graphicsDevice.DepthStencilState = DepthStencilState.Default;
			graphicsDevice.BlendState = BlendState.Opaque;
			
			// Sampler states for the diffuse map
			graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
			graphicsDevice.SamplerStates[1] = SamplerState.PointWrap;

			// Cull the objects
			sceneCuller.CullLights(scene, camera);
			sceneCuller.CullModelMeshes(scene, camera);

            // Forward render the scene
			sceneRenderer.UseTechnique("DiffuseRenderTerrain");
			sceneRenderer.DrawTerrain(scene, camera, terrainGBufferEffect);

			sceneRenderer.UseTechnique("DiffuseRender");
			sceneRenderer.Draw(scene, camera, gBufferEffect);
			
			// Render the skybox and update sampler state
			graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			sceneRenderer.UseTechnique("Skybox");
			sceneRenderer.DrawSkybox(scene, camera);

			renderStopWatch.Stop();
			
			return outputs;
        }
    }
}
