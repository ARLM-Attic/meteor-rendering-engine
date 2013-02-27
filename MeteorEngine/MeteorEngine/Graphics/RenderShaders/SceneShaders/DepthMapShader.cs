using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	class DepthMapShader : BaseShader
	{
		/// Shadow pass
		RenderTarget2D depthRT;

		/// Create shadow maps
		Effect depthEffect;
		Effect terrainDepthEffect;

		/// Debug point lights
		public bool stippled = false;

		/// Measures the next time to update some shadow maps
		int shadowUpdateTimer = 0;

		/// Camera to represent viewpoint of light
		Camera lightCamera;

		/// Texture dimensions for individual shadow cascade
		const int shadowMapSize = 768;

		/// Total number of cascades for CSM
		const int numCascades = 4;

		/// Arrangement of depth maps in atlas
		const int mapsPerRow = 2;
		const int mapsPerCol = 2;

		/// Textures to cache depth maps
		Texture2D[] depthMapCache;

		/// Ratio of linear to logarithmic split in view cascades
		public float splitLambda = 0.9f;

		Matrix[] lightViewProj;
		Matrix[] lightProjection;

		/// <summary>
		/// Constructor for LightShader, which renders both directional lights 
		/// and point lights.
		/// </summary>
		/// <param name="profile"></param>
		/// <param name="content"></param>

		public DepthMapShader(RenderProfile profile, ResourceContentManager content)
			: base(profile, content)
		{
			halfPixel.X = 0.5f / (float)(backBufferWidth * bufferScaling);
			halfPixel.Y = 0.5f / (float)(backBufferHeight * bufferScaling);

			// Depth map target
			depthRT = profile.AddRenderTarget(shadowMapSize * mapsPerRow, shadowMapSize * mapsPerCol,
				SurfaceFormat.Single, DepthFormat.Depth24);

			outputTargets = new RenderTarget2D[] { depthRT };

			// Set depth map cache textures
			depthMapCache = new Texture2D[2] {
				new Texture2D(graphicsDevice, shadowMapSize, shadowMapSize),
				new Texture2D(graphicsDevice, shadowMapSize, shadowMapSize)
			};

			lightCamera = new Camera();
			lightCamera.farPlaneDistance = 5000f;
			lightCamera.Initialize(shadowMapSize, shadowMapSize);

			lightViewProj = new Matrix[numCascades];
			lightProjection = new Matrix[numCascades];

			// Load depth mapping shader effects
			depthEffect = content.Load<Effect>("depth");
			terrainDepthEffect = content.Load<Effect>("terrainDepth");
		}

		/// <summary>
		/// Update and draw all directional and point lights
		/// </summary>

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Start();
			Draw(scene, camera);

			// Increment the shadow update time
			shadowUpdateTimer = (shadowUpdateTimer == 8) ? 0 : shadowUpdateTimer + 1;

			renderStopWatch.Stop();
			return outputs;
		}

		/// <summary>
		/// Draw the shadow map(s) for directional lights
		/// </summary>

		private void Draw(Scene scene, Camera camera)
		{
			int lightID = 0;

			foreach (DirectionLight light in scene.directionalLights)
			{
				if (light.castsShadows)
				{
					graphicsDevice.SetRenderTarget(depthRT);
					graphicsDevice.Clear(Color.White);
					graphicsDevice.BlendState = BlendState.Opaque;
					graphicsDevice.DepthStencilState = DepthStencilState.Default;

					for (int cascade = 0; cascade < numCascades; cascade++)
					{
						// Skip update of far shadow cascades in intervals
						//if (cascade > 1 && shadowUpdateTimer != 0)
						//	break;

						// Set camera's near and far view distance
						camera.GetFrustumSplit(cascade, numCascades, splitLambda);

						// Adjust viewport settings to draw to the correct portion
						// of the render target
						Viewport viewport = graphicsDevice.Viewport;

						viewport.Width = shadowMapSize;
						viewport.Height = shadowMapSize;
						viewport.X = shadowMapSize * (cascade % mapsPerRow);
						viewport.Y = shadowMapSize * (cascade / mapsPerRow);
						graphicsDevice.Viewport = viewport;

						// Update view matrices
						CreateLightViewProjMatrix(light.direction, lightCamera);
						terrainDepthEffect.Parameters["LightViewProj"].SetValue(lightCamera.view * lightCamera.projection);

						// Draw the terrain here
						//sceneRenderer.DrawTerrainDefault(scene, lightCamera, terrainDepthEffect);

						depthEffect.Parameters["LightViewProj"].SetValue(lightCamera.view * lightCamera.projection);
						depthEffect.Parameters["nearClip"].SetValue(lightCamera.nearPlaneDistance);

						// Cull models from this point of view
						sceneCuller.CullModelMeshes(scene, lightCamera);
						sceneRenderer.UseTechnique("Default");
						sceneRenderer.Draw(scene, depthEffect);

						// Cache the shadow map to a texture
						if (cascade > 1 && shadowUpdateTimer == 0)
						{
							//graphicsDevice.SetRenderTarget(null);							
						}
					}

					lightID++;
				}
			}
		}

		/// <summary>
		/// Creates the WorldViewProjection matrix from the perspective of the 
		/// light using the cameras bounding frustum to determine what is visible 
		/// in the scene.
		/// </summary>
		/// <returns>The WorldViewProjection for the light</returns>
		void CreateLightViewProjMatrix(Vector3 lightDirection, Camera lightCamera)
		{
			// Matrix with that will rotate in points the direction of the light
			Matrix lightRotation = Matrix.CreateLookAt(Vector3.Zero, lightDirection, Vector3.Up);
			camera.frustum.GetCorners(camera);

			// Transform the positions of the corners into the direction of the light
			for (int i = 0; i < camera.frustumCorners.Length; i++)
			{
				Vector3.Transform(ref camera.frustumCorners[i], ref lightRotation, out camera.frustumCorners[i]);
			}

			// Find the smallest box around the points
			// Create initial variables to hold min and max xyz values for the boundingBox
			Vector3 cornerMax = new Vector3(float.MinValue);
			Vector3 cornerMin = new Vector3(float.MaxValue);

			for (int i = 0; i < camera.frustumCorners.Length; i++)
			{
				// update our values from this vertex
				cornerMin = Vector3.Min(cornerMin, camera.frustumCorners[i]);
				cornerMax = Vector3.Max(cornerMax, camera.frustumCorners[i]);
			}

			BoundingBox lightBox = new BoundingBox(cornerMin, cornerMax);

			bool fixShadowJittering = true;
			if (fixShadowJittering)
			{
				// I borrowed this code from some forum that I don't remember anymore =/
				// We snap the camera to 1 pixel increments so that moving the camera does not cause the shadows to jitter.
				// This is a matter of integer dividing by the world space size of a texel
				// The camera will snap along texel-sized increments

				float diagonalLength = (camera.frustumCorners[1] - camera.frustumCorners[7]).Length();
				//diagonalLength *= 1.1f;
				float worldsUnitsPerTexel = diagonalLength / (float)shadowMapSize;

				Vector3 vBorderOffset = (new Vector3(diagonalLength, diagonalLength, diagonalLength) -
										 (lightBox.Max - lightBox.Min)) * 0.5f;
				lightBox.Max += vBorderOffset;
				lightBox.Min -= vBorderOffset;

				lightBox.Min /= worldsUnitsPerTexel;
				lightBox.Min.X = (float)Math.Floor(lightBox.Min.X);
				lightBox.Min.Y = (float)Math.Floor(lightBox.Min.Y);
				lightBox.Min.Z = (float)Math.Floor(lightBox.Min.Z);
				lightBox.Min *= worldsUnitsPerTexel;

				lightBox.Max /= worldsUnitsPerTexel;
				lightBox.Max.X = (float)Math.Floor(lightBox.Max.X);
				lightBox.Max.Y = (float)Math.Floor(lightBox.Max.Y);
				lightBox.Max.Z = (float)Math.Floor(lightBox.Max.Z);
				lightBox.Max *= worldsUnitsPerTexel;
			}

			Vector3 boxSize = lightBox.Max - lightBox.Min;
			Vector3 halfBoxSize = boxSize * 0.5f;

			// The position of the light should be in the center of the back panel of the box. 
			Vector3 lightPosition = lightBox.Min + halfBoxSize;
			lightPosition.Z = lightBox.Min.Z;

			// We need the position back in world coordinates so we transform 
			// the light position by the inverse of the lights rotation
			lightPosition = Vector3.Transform(lightPosition, Matrix.Invert(lightRotation));

			// Create the view matrix for the light
			lightCamera.view = Matrix.CreateLookAt(lightPosition, lightPosition + lightDirection, Vector3.Up);

			// Create the projection matrix for the light
			// The projection is orthographic since we are using a directional light
			float nearScale = 10f;
			lightCamera.projection =
				Matrix.CreateOrthographic(boxSize.X, boxSize.Y, -boxSize.Z * nearScale, boxSize.Z / 2f);

			// Finally, update the view frustum's matrix
			lightCamera.Update();
		}
	}
}
