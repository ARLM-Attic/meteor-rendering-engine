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

        public DiffuseShader(RenderProfile profile, ContentManager content)
            : base(profile, content) 
		{
			hasSceneInput = true;

            // Diffuse render target
			diffuseRT = profile.AddRenderTarget(backBufferWidth,
                backBufferHeight, SurfaceFormat.Color, DepthFormat.Depth24);

			outputTargets = new RenderTarget2D[] 
			{
				diffuseRT
			};
        }

        /// <summary>
        /// Simply draw the scene to the render target
        /// </summary> 

        public override RenderTarget2D[] Draw()
        {
            // Prepare the forward rendering
            GraphicsDevice.SetRenderTarget(diffuseRT);
			GraphicsDevice.Clear(Color.Transparent);
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;
			GraphicsDevice.BlendState = BlendState.Opaque;
			
			// Sampler states for the diffuse map
			GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

			sceneRenderer.CullLights(scene, camera);
			sceneRenderer.CullModelMeshes(scene, camera);
			
            // Forward render the scene
			sceneRenderer.UseTechnique("DiffuseRender");
			sceneRenderer.Draw(scene, camera);
			
			// Render the skybox and update sampler state
			GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			sceneRenderer.UseTechnique("Skybox");
			sceneRenderer.DrawSkybox(scene, camera);
			
			return outputs;
        }
    }
}
