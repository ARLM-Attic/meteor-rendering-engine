using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Rendering
{
	class LightShader : BaseRenderer
	{
		/// Light pass
		RenderTarget2D lightRT;

		/// Shadow pass
		RenderTarget2D[] depthRT;

		/// Cube map shadows
		RenderTargetCube cubeDepthRT;

		/// Handles directional lights
		Effect directionalLightEffect;

		/// Handles point lights
		Effect pointLightEffect;

		/// Create shadow maps
		Effect depthEffect;

		/// Sphere used for point lighting
		Model sphereModel;

		/// Debug point lights
		public bool wireframe = false;

		/// Used for shadow mappings
		Camera lightCamera;
		const int shadowMapSize = 768;
		const int numCascades = 8;

		public float shadowBrightness = 0.1f;
		Matrix[] lightViewProj;

		BlendState alphaBlendState = new BlendState()
		{
			AlphaSourceBlend = Blend.One,
			AlphaDestinationBlend = Blend.One,

			ColorSourceBlend = Blend.One,
			ColorDestinationBlend = Blend.One
		};

		BlendState blend = new BlendState
		{
			ColorWriteChannels = ColorWriteChannels.Red,
			ColorWriteChannels1 = ColorWriteChannels.Green
		};

		DepthStencilState cwDepthState = new DepthStencilState()
		{
			DepthBufferWriteEnable = false,
			DepthBufferFunction = CompareFunction.LessEqual
		};

		DepthStencilState ccwDepthState = new DepthStencilState()
		{
			DepthBufferWriteEnable = false,
			DepthBufferFunction = CompareFunction.GreaterEqual
		};

		public LightShader(RenderProfile profile, ContentManager content)
			: base(profile, content)
		{
			// Lighting render target
			lightRT = profile.AddRenderTarget(backBufferWidth, backBufferHeight,
				SurfaceFormat.HdrBlendable, DepthFormat.None);

			// Light and combined effect targets
			depthRT = new RenderTarget2D[3];
			for (int i = 0; i < 3; i++)
			{
				depthRT[i] = profile.AddRenderTarget(shadowMapSize * 4, shadowMapSize * 2,
					SurfaceFormat.Vector2, DepthFormat.Depth24);
			}

			cubeDepthRT = new RenderTargetCube(GraphicsDevice, 1024, false,
				SurfaceFormat.Rg32, DepthFormat.Depth24);

			//GraphicsDevice.SetRenderTarget(cubeDepthRT);

			outputTargets = new RenderTarget2D[]
			{
				lightRT, depthRT[0], depthRT[0], depthRT[0]
			};

			lightCamera = new Camera();
			lightCamera.Initialize(shadowMapSize, shadowMapSize);
			lightViewProj = new Matrix[numCascades];

			// Load the shader effects
			directionalLightEffect = content.Load<Effect>("Effects\\directionalLight");
			pointLightEffect = content.Load<Effect>("Effects\\pointLight");

			// Load shadow mapping shader effects
			depthEffect = content.Load<Effect>("Effects\\depth");

			// Set constant parameters
			directionalLightEffect.Parameters["halfPixel"].SetValue(halfPixel);
			pointLightEffect.Parameters["halfPixel"].SetValue(halfPixel);

			// Load the point light model
			sphereModel = content.Load<Model>("Models\\ball");
		}

		/// <summary>
		/// Update and draw all directional and point lights
		/// </summary>

		public override RenderTarget2D[] Draw()
		{
			renderStopWatch.Reset();
			renderStopWatch.Restart();	

			if (inputTargets != null)
			{
				DrawDirectionalLightShadows(scene, camera);

				GraphicsDevice.BlendState = alphaBlendState;
				GraphicsDevice.SetRenderTarget(lightRT);
				GraphicsDevice.Clear(Color.Transparent);
				GraphicsDevice.DepthStencilState = DepthStencilState.None;

				if (scene.totalLights > 0)
					DrawPointLights(scene, camera, inputTargets);

				// Make some lights
				DrawDirectionalLights(scene, camera, inputTargets);
			}

			renderStopWatch.Stop();
			return outputs;
		}

		/// <summary>
		/// Set common parameters to reduce state changes
		/// </summary> 

		private void SetCommonParameters(Effect effect, Camera camera, RenderTarget2D[] targets)
		{
			// Set Matrix parameters
			effect.Parameters["View"].SetValue(camera.View);
			effect.Parameters["Projection"].SetValue(camera.Projection);

			// Set the G-Buffer parameters
			effect.Parameters["normalMap"].SetValue(targets[0]);
			effect.Parameters["depthMap"].SetValue(targets[1]);

			effect.Parameters["camPosition"].SetValue(camera.Position);
			effect.Parameters["invertViewProj"].SetValue(Matrix.Invert(camera.View * camera.Projection));
			effect.Parameters["inverseView"].SetValue(Matrix.Invert(camera.View));
		}

		/// <summary>
		/// Draw directional lights to the map
		/// </summary>

		private void DrawDirectionalLights(Scene scene, Camera camera,
			RenderTarget2D[] targets)
		{
			SetCommonParameters(directionalLightEffect, camera, targets);
			directionalLightEffect.Parameters["ambient"].SetValue(scene.ambientLight);
			int j = 0;

			foreach (DirectionLight light in scene.directionalLights)
			{
				directionalLightEffect.Parameters["lightDirection"].SetValue(light.direction);
				directionalLightEffect.Parameters["lightColor"].SetValue(light.color.ToVector3());
				directionalLightEffect.Parameters["lightIntensity"].SetValue(light.intensity);

				if (light.castsShadows)
				{
					directionalLightEffect.CurrentTechnique = directionalLightEffect.Techniques["Shadowed"];

					// Project the shadow maps onto the scene
					Vector2 shadowMapPixelSize = new Vector2(
						1f / ((float)shadowMapSize * 4f), 1f / ((float)shadowMapSize * 2f));

					// Set the common parameters for all shadow maps
					directionalLightEffect.Parameters["shadowMapPixelSize"].SetValue(shadowMapPixelSize);
					directionalLightEffect.Parameters["shadowMapSize"].SetValue(shadowMapSize);
					directionalLightEffect.Parameters["shadowBrightness"].SetValue(shadowBrightness);

					// Calculate view projection matrices for shadow map views
					float[] splitNearFar = new float[numCascades];

					for (int i = 0; i < numCascades; i++)
					{
						camera.GetFrustumSplit(i, numCascades);
						splitNearFar[i] = camera.farSplitPlaneDistance / camera.farPlaneDistance;

						CreateLightViewProjMatrix(light.direction, lightCamera);
						lightViewProj[i] = lightCamera.View * lightCamera.Projection;
					}

					directionalLightEffect.Parameters["cascadeSplits"].SetValue(splitNearFar);

					directionalLightEffect.Parameters["lightViewProj"].SetValue(lightViewProj);
					directionalLightEffect.Parameters["shadowMap"].SetValue(depthRT[j]);
					j++;
				}
				else
				{
					directionalLightEffect.CurrentTechnique = directionalLightEffect.Techniques["NoShadow"];
				}

				EffectPass pass = directionalLightEffect.CurrentTechnique.Passes[0];

				pass.Apply();
				quadRenderer.Render(Vector2.One * -1, Vector2.One);
			}
		}

		/// <summary>
		/// Draw the shadow maps for directional lights
		/// </summary>

		private void DrawDirectionalLightShadows(Scene scene, Camera camera)
		{
			sceneRenderer.IgnoreCulling(scene, camera);
			int j = 0;

			foreach (DirectionLight light in scene.directionalLights)
			{
				if (light.castsShadows)
				{
					float yaw = (float)Math.Atan2(light.direction.X, light.direction.Y);
					float pitch = (float)Math.Atan2(light.direction.Z,
						Math.Sqrt((light.direction.X * light.direction.X) +
								  (light.direction.Y * light.direction.Y)));

					lightCamera.SetOrientation(new Vector2(yaw, pitch));
					lightCamera.Update();

					GraphicsDevice.SetRenderTarget(depthRT[j]);
					GraphicsDevice.Clear(Color.White);

					for (int cascade = 0; cascade < numCascades; cascade++)
					{
						GraphicsDevice.DepthStencilState = DepthStencilState.Default;

						// Adjust viewport settings to draw to the correct portion
						// of the render target
						Viewport defaultView = GraphicsDevice.Viewport;
						defaultView.Width = shadowMapSize;
						defaultView.Height = shadowMapSize;
						defaultView.X = shadowMapSize * (cascade % 4);
						defaultView.Y = shadowMapSize * (cascade / 4);
						GraphicsDevice.Viewport = defaultView;

						camera.GetFrustumSplit(cascade, numCascades);

						// Update view matrices
						CreateLightViewProjMatrix(light.direction, lightCamera);

						depthEffect.Parameters["LightViewProj"].SetValue(lightCamera.View * lightCamera.Projection);
						depthEffect.Parameters["nearClip"].SetValue(lightCamera.nearPlaneDistance);

						// Cull models from this point of view
						sceneRenderer.CullModelMeshes(scene, lightCamera);
						sceneRenderer.Draw(scene, depthEffect);
					}
					j++;
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
			Matrix lightRotation = Matrix.CreateLookAt(Vector3.Zero, -lightDirection, Vector3.Up);
			camera.Frustum.GetCorners(camera);

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
			Vector3 boxSize = lightBox.Max - lightBox.Min;
			Vector3 halfBoxSize = boxSize * 0.5f;

			// The position of the light should be in the center of the back panel of the box. 
			Vector3 lightPosition = lightBox.Min + halfBoxSize;
			lightPosition.Z = lightBox.Min.Z;

			// We need the position back in world coordinates so we transform 
			// the light position by the inverse of the lights rotation
			lightPosition = Vector3.Transform(lightPosition, Matrix.Invert(lightRotation));

			// Create the view matrix for the light
			lightCamera.View = Matrix.CreateLookAt(lightPosition, lightPosition + lightDirection, Vector3.Up);

			// Create the projection matrix for the light
			// The projection is orthographic since we are using a directional light
			float projectionScale = 1.25f;
			boxSize *= projectionScale;
			lightCamera.Projection = Matrix.CreateOrthographic(boxSize.X, boxSize.Y, -boxSize.Z, boxSize.Z);
		}

		/// Vertex buffer to hold the instance data
		DynamicVertexBuffer instanceVertexBuffer;

		/// To store instance transform matrices in a vertex buffer, we use this custom
		/// vertex type which encodes 4x4 matrices as a set of four Vector4 values.
		static VertexDeclaration instanceVertexDeclaration = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
			new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
			new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
			new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
			new VertexElement(64, VertexElementFormat.Color, VertexElementUsage.Color, 1)
		);

		List<PointLight.InstanceData> innerLights = new List<PointLight.InstanceData>();
		List<PointLight.InstanceData> outerLights = new List<PointLight.InstanceData>();

		/// <summary>
		/// Draw all visible point light spheres.
		/// </summary>

		private void DrawPointLights(Scene scene, Camera camera, RenderTarget2D[] targets)
		{
			SetCommonParameters(pointLightEffect, camera, targets);
			pointLightEffect.Parameters["lightIntensity"].SetValue(scene.pointLights[0].intensity);

			// Create the list of lights for this update

			Vector3 lightPosition = Vector3.Zero;
			Vector3 radiusVector = Vector3.Zero;
			float radius = 1;

			/// Separate the lights into two groups, depending on where the
			/// attenuation distance is relative to the camera's position.
			/// "Inner" lights have the camera inside its lit area, while
			/// "outer" lights don't contain the camera at all.
			/// </summary>

			BoundingFrustum cameraFrustum = new BoundingFrustum(camera.View * camera.Projection);
			innerLights.Clear();
			outerLights.Clear();

			foreach (PointLight light in scene.VisiblePointLights)
			{
				lightPosition.X = light.instance.transform.M41;
				lightPosition.Y = light.instance.transform.M42;
				lightPosition.Z = light.instance.transform.M43;

				radiusVector.X = light.instance.transform.M11;
				radiusVector.Y = light.instance.transform.M12;
				radiusVector.Z = light.instance.transform.M13;

				float camToCenter = Vector3.Distance(camera.Position, lightPosition);
				radius = radiusVector.Length();

				BoundingSphere bSphere = new BoundingSphere(lightPosition, radius * 1.25f);
				PlaneIntersectionType planeIntersectionType;
				cameraFrustum.Near.Intersects(ref bSphere, out planeIntersectionType);

				if (planeIntersectionType != PlaneIntersectionType.Back)
				{
					innerLights.Add(light.instance);
				}
				else
				{
					outerLights.Add(light.instance);
				}
			}

			// Set the culling mode based on the camera's position relative to the light

			// Draw the inner lights culling clockwise triangles
			GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;
			GraphicsDevice.DepthStencilState = cwDepthState;
			DrawLightGroup(innerLights);

			// Flip the culling mode for the outer lights, also resetting it to default
			GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
			GraphicsDevice.DepthStencilState = ccwDepthState;
			DrawLightGroup(outerLights);
		}

		/// <summary>
		/// Draw each instanced light group
		/// </summary>

		private void DrawLightGroup(List<PointLight.InstanceData> lights)
		{
			int totalInstances = lights.Count;
			if (totalInstances <= 0) return;

			// If we have more instances than room in our vertex buffer, grow it 
			// to the neccessary size.
			if ((instanceVertexBuffer == null) ||
				(totalInstances > instanceVertexBuffer.VertexCount))
			{
				if (instanceVertexBuffer != null)
					instanceVertexBuffer.Dispose();

				instanceVertexBuffer = new DynamicVertexBuffer(
					GraphicsDevice, instanceVertexDeclaration, totalInstances, BufferUsage.WriteOnly);
			}

			// Transfer the latest instance transform matrices into the instanceVertexBuffer
			// Optionally, use the instance color as well
			instanceVertexBuffer.SetData(lights.ToArray(), 0, totalInstances, SetDataOptions.Discard);

			// Draw the point light

			foreach (ModelMesh mesh in sphereModel.Meshes)
			{
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					// Tell the GPU to read from both the model vertex buffer plus our instanceVertexBuffer
					GraphicsDevice.SetVertexBuffers(
						new VertexBufferBinding(meshPart.VertexBuffer, meshPart.VertexOffset, 0),
						new VertexBufferBinding(instanceVertexBuffer, 0, 1)
					);

					GraphicsDevice.Indices = meshPart.IndexBuffer;

					EffectPass pass = pointLightEffect.CurrentTechnique.Passes[0];

					pass.Apply();

					GraphicsDevice.DrawInstancedPrimitives(
						PrimitiveType.TriangleList, 0, 0,
						meshPart.NumVertices, meshPart.StartIndex,
						meshPart.PrimitiveCount, totalInstances);
				}
			}
			// Finish rendering spheres
		}

		/// <summary>
		/// Remove the vertex buffer and sphere model.
		/// </summary> 

		protected new void DisposeResources()
		{
			//if (instanceVertexBuffer != null)
			//	instanceVertexBuffer.Dispose();
			//sphereModel.Meshes.GetEnumerator().Dispose();

			base.DisposeResources();
		}
	}
}
