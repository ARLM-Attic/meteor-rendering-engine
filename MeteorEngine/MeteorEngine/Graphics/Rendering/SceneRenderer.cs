using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public class SceneRenderer
	{
		/// Basic effect for bounding box drawing
		BasicEffect basicEffect;

		/// Effect that's currently used by the scene
		Effect currentEffect;

		/// Effect technique used by the scene
		String shaderTechnique;

		/// Scene stats used in rendering
		public int totalPolys;

		/// Farthest depth value to render scene from.
		const float farDepth = 0.99999f;

		/// Resources used for loading and rendering scene content
		ContentManager content;
		GraphicsDevice graphicsDevice;

		/// Spritebatch to draw debug data on screen
		SpriteBatch spriteBatch;

		/// Dummy textures to use in case they are missing in the model.
		Texture2D blankNormal, blankTexture, blankSpecular;

		/// Rendering imposters for the scenes
		Imposter imposter;

		/// Containers for temp data, to avoid calling the GC
		Vector3[] boxCorners;
		Matrix[] tempBones;

		/// Swap space for vertex buffer bindings
		VertexBufferBinding[] vertexBufferBindings;

		/// Vertex data for a dummy box
		VertexBuffer dummyBoxVB;

		/// <summary>
		/// Create a SceneRenderer with graphics device and content manager.
		/// </summary>

	    public SceneRenderer(GraphicsDevice device, ResourceContentManager content)
        {
			this.graphicsDevice = device; 
			this.content = content;

			spriteBatch = new SpriteBatch(graphicsDevice);
			imposter = new Imposter(graphicsDevice, content);

            // Use standard GBuffer as a default
            shaderTechnique = "GBuffer";
			currentEffect = null;

			blankNormal = new Texture2D(device, 1, 1);
			blankTexture = new Texture2D(device, 1, 1);
			blankSpecular = new Texture2D(device, 1, 1);

			// Create the dummy textures
			Color[][] pixels = new Color[3][];
			pixels[0] = new Color[] { Color.White};
			pixels[1] = new Color[] { Color.Transparent}; 
			pixels[2] = new Color[] { new Color(0.5f, 0.5f, 1.0f, 1.0f) };

			blankTexture.SetData<Color>(pixels[0]);
			blankSpecular.SetData<Color>(pixels[1]);
			blankNormal.SetData<Color>(pixels[2]);

			// Helper for drawing debug shapes
			ShapeRenderer.Initialize(graphicsDevice);

			// Effect for bounding box drawing
			basicEffect = new BasicEffect(device);
			basicEffect.LightingEnabled = false;
			basicEffect.TextureEnabled = false;
			basicEffect.VertexColorEnabled = true;

			// Create a resuable DummyBox
			dummyBoxVB = ShapeRenderer.AddDummyBox();

			boxCorners = new Vector3[8];
			tempBones = new Matrix[60];

			// Create the instance data vertex buffer.
			vertexBufferBindings = new VertexBufferBinding[2];
		}

		/// <summary>
		/// Set current model effect technique
		/// </summary>
		public void UseTechnique(String technique)
		{
			shaderTechnique = technique;
		}

		/// <summary>
		/// Draw the entire scene with an effect using camera parameters
		/// </summary>

		public void Draw(Scene scene, Camera camera, Effect effect, BlendState blendState)
		{
			Viewport viewport = graphicsDevice.Viewport;
			viewport.MinDepth = 0.0f;
			viewport.MaxDepth = farDepth;
			graphicsDevice.Viewport = viewport;
			graphicsDevice.BlendState = blendState;
			graphicsDevice.RasterizerState = RasterizerState.CullNone;

			totalPolys = 0;
			scene.visibleMeshes = 0;

			currentEffect = effect;

			// Set camera parameters
			currentEffect.Parameters["View"].SetValue(camera.view);
			currentEffect.Parameters["Projection"].SetValue(camera.projection);
			currentEffect.Parameters["CameraPosition"].SetValue(camera.position);

			foreach (InstancedModel instancedModel in scene.staticModels.Values)					
				scene.visibleMeshes += DrawModel(instancedModel, this.shaderTechnique);

			foreach (InstancedModel skinnedModel in scene.skinnedModels.Values)
				scene.visibleMeshes += DrawModel(skinnedModel, this.shaderTechnique + "Animated");

			scene.totalPolys = totalPolys;

			// Debug bounding volumes
			DrawBoundingVolumes(scene, camera);
		}

		/// <summary>
		/// Draw with a custom effect not requiring a camera
		/// </summary>

		public void Draw(Scene scene, Effect effect, BlendState blendState)
		{
			Viewport viewport = graphicsDevice.Viewport;
			viewport.MinDepth = 0.0f;
			viewport.MaxDepth = farDepth;
			graphicsDevice.Viewport = viewport;
			graphicsDevice.BlendState = blendState;
			graphicsDevice.RasterizerState = RasterizerState.CullNone;

			currentEffect = effect;

			foreach (InstancedModel instancedModel in scene.staticModels.Values)
				DrawModel(instancedModel, effect, this.shaderTechnique);

			foreach (InstancedModel skinnedModel in scene.skinnedModels.Values)
				DrawModel(skinnedModel, effect, this.shaderTechnique + "Animated");

			// Finished drawing visible meshes
		}

		/// <summary>
		/// Overload for drawing with an effect with camera parameters
		/// </summary>	

		public void Draw(Scene scene, Camera camera, Effect effect)
		{
			Draw(scene, camera, effect, BlendState.Opaque);
		}

		/// <summary>
		/// Overload for drawing custom effects not requiring a camera
		/// </summary>	

		public void Draw(Scene scene, Effect effect)
		{
			Draw(scene, effect, BlendState.Opaque);
		}

		/// <summary>
		/// Wrapper to draw the terrain with a specified effect and technique
		/// </summary>

		public void DrawTerrain(Scene scene, Camera camera, Effect effect)
		{
			if (scene.terrain != null)
			{
				effect.CurrentTechnique = effect.Techniques[shaderTechnique];
				scene.totalPolys += scene.terrain.Draw(camera, effect, blankTexture);
			}
		}

		/// <summary>
		/// Wrapper to just draw the terrain with the desired effect
		/// </summary>

		public void DrawTerrainDefault(Scene scene, Camera camera, Effect effect)
		{
			if (scene.terrain != null)
				scene.totalPolys += scene.terrain.Draw(camera, effect, blankTexture);
		}

		/// <summary>
		/// A bit hacky way to limit bone transforms. Will fix soon.
		/// </summary>

		private void TrimBoneTransforms(InstancedModel instancedModel)
		{
			instancedModel.model.CopyAbsoluteBoneTransformsTo(instancedModel.boneMatrices);

			if (instancedModel.animationPlayer != null)
			{
				Matrix[] bones = instancedModel.animationPlayer.GetSkinTransforms();
				int maxBones = (bones.Count() > 60) ? 60 : bones.Count();

				// Not very efficient to do, need a way to avoid copying arrays
				Array.Copy(bones, tempBones, maxBones);
			}
		}

		/// <summary>
		/// Draw all visible meshes for this model with camera effect parameters.
		/// </summary>

		private int DrawModel(InstancedModel instancedModel, String technique)
		{
			UseTechnique(technique);
			TrimBoneTransforms(instancedModel);

			int meshIndex = 0;
			int visibleInstances = 0;

			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				instancedModel.PrepareMeshData(graphicsDevice, instanceGroup);

				// Retrieve the current mesh from the mesh list
				ModelMesh mesh = instancedModel.model.Meshes[meshIndex];
				Matrix world = instancedModel.boneMatrices[mesh.ParentBone.Index];

				// Set world matrix for all mesh parts
				currentEffect.Parameters["World"].SetValue(world);

				foreach (ModelMeshPart meshPart in instancedModel.model.Meshes[meshIndex].MeshParts)
				{
					// Set bones if the model is animated
					if (instancedModel.animationPlayer != null)
						currentEffect.Parameters["bones"].SetValue(tempBones);

					Texture2D textureValue = (instancedModel.textures[meshIndex] != null) ?
						instancedModel.textures[meshIndex] : blankTexture;

					currentEffect.Parameters["Texture"].SetValue(textureValue);
					currentEffect.Parameters["NormalMap"].SetValue(instancedModel.normalMapTextures[meshIndex]);

					DrawInstancedMeshPart(meshPart, instanceGroup);
					meshIndex++;
				}
				// Finished drawing mesh parts
				visibleInstances += instanceGroup.totalVisible;
			}
			// Finished model rendering
			return visibleInstances;
		}

		/// <summary>
		/// Draw instanced model with a custom effect without camera parameters
		/// </summary>	

		private int DrawModel(InstancedModel instancedModel, Effect effect, String technique)
		{
			UseTechnique(technique);
			TrimBoneTransforms(instancedModel);

			int meshIndex = 0;
			int visibleInstances = 0;

			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				instancedModel.PrepareMeshData(graphicsDevice, instanceGroup);

				// Retrieve the current mesh from the mesh list
				ModelMesh mesh = instancedModel.model.Meshes[meshIndex];
				Matrix world = instancedModel.boneMatrices[mesh.ParentBone.Index];

				// Set world matrix for all mesh parts
				currentEffect.Parameters["World"].SetValue(world);

				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					// Set bones if the model is animated
					if (instancedModel.animationPlayer != null)
						currentEffect.Parameters["bones"].SetValue(tempBones);

					Texture2D textureValue = (instancedModel.textures[meshIndex] != null) ?
						instancedModel.textures[meshIndex] : blankTexture;

					currentEffect.Parameters["Texture"].SetValue(textureValue);

					DrawInstancedMeshPart(meshPart, instanceGroup);
					meshIndex++;
				}
				// Finished drawing mesh parts
				visibleInstances += instanceGroup.totalVisible;
			}	
			// Finished model rendering
			return visibleInstances;
		}

		/// <summary>
		/// Draw a mesh part with the given effect
		/// </summary>
		
		private void DrawInstancedMeshPart(ModelMeshPart meshPart, MeshInstanceGroup instanceGroup)
		{
			// Skip rendering the mesh parts if they aren't visible
			if (instanceGroup.totalVisible == 0)
				return;

			vertexBufferBindings[0] = new VertexBufferBinding(meshPart.VertexBuffer, meshPart.VertexOffset, 0);
			vertexBufferBindings[1] = new VertexBufferBinding(instanceGroup.instanceVB, 0, 1);

			// Bind both mesh vertex buffer and per-instance matrix data
			graphicsDevice.SetVertexBuffers(vertexBufferBindings);
			graphicsDevice.Indices = meshPart.IndexBuffer;

			// Assign effect technique
			currentEffect.CurrentTechnique = currentEffect.Techniques[shaderTechnique];

			for (int i = 0; i < currentEffect.CurrentTechnique.Passes.Count; i++)
			{
				currentEffect.CurrentTechnique.Passes[i].Apply();

				// Or draw the dummyBox
				graphicsDevice.DrawInstancedPrimitives(
					PrimitiveType.TriangleList, 0, 0,
					meshPart.NumVertices, meshPart.StartIndex,
					meshPart.PrimitiveCount, instanceGroup.totalVisible);
			}
			// Add to the scene's polygon count
			totalPolys += meshPart.NumVertices * instanceGroup.totalVisible;
		}

		/// <summary>
		/// Draw a mesh dummy box with a default effect
		/// </summary>

		private void DrawInstancedMeshDummyBox(MeshInstanceGroup instanceGroup)
		{
			// Skip rendering the mesh parts if they aren't visible
			if (instanceGroup.totalVisible == 0)
				return;

			vertexBufferBindings[0] = new VertexBufferBinding(dummyBoxVB, 0, 0);
			vertexBufferBindings[1] = new VertexBufferBinding(instanceGroup.instanceVB, 0, 1);

			// Bind both mesh vertex buffer and per-instance matrix data
			graphicsDevice.SetVertexBuffers(vertexBufferBindings);

			short[] boxIndices = new short[36];

			for (short i = 0; i < 36; i++)
				boxIndices[i] = i;

			IndexBuffer dummyBoxIB = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 36, BufferUsage.WriteOnly);
			dummyBoxIB.SetData(boxIndices);
			graphicsDevice.Indices = dummyBoxIB;

			// Assign effect technique
			currentEffect.CurrentTechnique = currentEffect.Techniques["BasicMesh"];

			for (int i = 0; i < currentEffect.CurrentTechnique.Passes.Count; i++)
			{
				currentEffect.CurrentTechnique.Passes[i].Apply();

				// Draw the dummyBox
				graphicsDevice.DrawInstancedPrimitives(
					PrimitiveType.TriangleList, 0, 0,
					dummyBoxVB.VertexCount, 0,
					dummyBoxIB.IndexCount / 3, instanceGroup.totalVisible);
			}
		}
		
		/// <summary>
		/// Draw the scene's skybox
		/// </summary>
		
		public void DrawSkybox(Scene scene, Camera camera)
		{
			graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
			graphicsDevice.RasterizerState = RasterizerState.CullNone;
			
			// Confine the depth range to a very far distance
			Viewport viewport = graphicsDevice.Viewport;
			viewport.MinDepth = farDepth;
			viewport.MaxDepth = 1.0f;
			graphicsDevice.Viewport = viewport;

			if (scene.Skybox == null)
				return;
			
			// Make skybox visible and copy instance data
			scene.Skybox.MeshInstanceGroups["DefaultName_0"].totalVisible = 1;
			scene.Skybox.MeshInstanceGroups["DefaultName_0"].visibleInstances[0] =
				scene.Skybox.MeshInstanceGroups["DefaultName_0"].instances[0];

			scene.Skybox.Translate(camera.position);
			DrawModel(scene.Skybox, this.shaderTechnique);
		}
		
		/// <summary>
		/// Draw all visible bounding boxes
		/// </summary>

		public void DrawBoundingVolumes(Scene scene, Camera camera)
		{
			if (scene.debug == true)
			{
				basicEffect.View = camera.view;
				basicEffect.Projection = camera.projection;

				foreach (InstancedModel instancedModel in scene.staticModels.Values)
				{
					DrawBoundingBoxes(instancedModel, camera);
				}

				foreach (InstancedModel skinnedModel in scene.skinnedModels.Values)
				{
					DrawBoundingBoxes(skinnedModel, camera);
				}
			}
		}

		/// <summary>
		/// Draw debug bounding boxes
		/// </summary>
		[Conditional("DEBUG")]
		private void DrawBoundingBoxes(InstancedModel model, Camera camera)
		{
			int meshIndex = 0;
			Viewport v = graphicsDevice.Viewport;
			float farDistance = camera.farPlaneDistance;

			// Matrices to project into screen space
			Matrix worldViewProjection = camera.view * camera.projection;
			Matrix invClient = Matrix.Invert(Matrix.CreateOrthographicOffCenter(0, v.Width, v.Height, 0, -1, 1));

			foreach (MeshInstanceGroup instancedGroup in model.MeshInstanceGroups.Values)
			{
				BoundingBox box = instancedGroup.boundingBox;

				// Assign the box corners
				boxCorners[0] = new Vector3(box.Min.X, box.Max.Y, box.Max.Z);
				boxCorners[1] = new Vector3(box.Max.X, box.Max.Y, box.Max.Z); // maximum
				boxCorners[2] = new Vector3(box.Max.X, box.Min.Y, box.Max.Z);
				boxCorners[3] = new Vector3(box.Min.X, box.Min.Y, box.Max.Z);
				boxCorners[4] = new Vector3(box.Min.X, box.Max.Y, box.Min.Z);
				boxCorners[5] = new Vector3(box.Max.X, box.Max.Y, box.Min.Z);
				boxCorners[6] = new Vector3(box.Max.X, box.Min.Y, box.Min.Z);
				boxCorners[7] = new Vector3(box.Min.X, box.Min.Y, box.Min.Z); // minimum
				
				//Color[] colors = { Color.Cyan, Color.White, Color.Magenta, Color.Blue,
				//	Color.Green, Color.Yellow, Color.Red, Color.Black };XZ

				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
				int visible = 0;

				foreach (MeshInstance meshInstance in instancedGroup.visibleInstances)
				{
					if (meshInstance == null)
						continue;

					// If we have exceeded the visible count, we have already rendered
					// all visible instances
					if (visible >= instancedGroup.totalVisible)
						break;

					Matrix modelTransform = meshInstance.Transform;

					Vector2 rectMin = new Vector2(v.Width, v.Height);
					Vector2 rectMax = Vector2.Zero;
					float minDistance = camera.farPlaneDistance;

					for (int i = 0; i < boxCorners.Length; i++)
					{
						model.boxVertices[i].Position = Vector3.Transform(boxCorners[i], modelTransform);
						model.boxVertices[i].Color = Color.Cyan;
						
						// Determin the closest distance point in the bounding box
						float camDistance = Vector3.Distance(model.boxVertices[i].Position, camera.position);
						minDistance = Math.Min(camDistance, minDistance);

						// Project the corners of the bounding box onto screen space
						
						Vector4 position = new Vector4(model.boxVertices[i].Position, 1);
						Vector4.Transform(ref position, ref worldViewProjection, out position);
						position /= position.W;

						Vector2 clientResult = Vector2.Transform(new Vector2(position.X, position.Y), invClient);

						rectMin.X = (int)Math.Min((float)clientResult.X, (float)rectMin.X);
						rectMin.Y = (int)Math.Min((float)clientResult.Y, (float)rectMin.Y);
						rectMax.X = (int)Math.Max((float)clientResult.X, (float)rectMax.X);
						rectMax.Y = (int)Math.Max((float)clientResult.Y, (float)rectMax.Y);
					}

					Vector3 topLeft = v.Unproject(new Vector3(rectMin, minDistance), camera.projection, camera.view, camera.WorldMatrix);
					Vector3 bottomRight = v.Unproject(new Vector3(rectMax, minDistance), camera.projection, camera.view, camera.WorldMatrix);

					// Transform the temporary bounding boxes with the model instance's world matrix
					// TODO: Update these boxes only when intances are updated

					// Render the bounding box for this instance
					if (camera.frustum.Contains(meshInstance.BSphere) != ContainmentType.Disjoint)
					{
						// Add a bounding sphere to the list of shapes to draw
						//ShapeRenderer.AddBoundingSphere(meshInstance.BSphere, Color.Red);
						
						for (int i = 0; i < basicEffect.CurrentTechnique.Passes.Count; i++)
						{
							basicEffect.CurrentTechnique.Passes[i].Apply();
							graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(
								PrimitiveType.LineList, model.boxVertices, 0, 8,
								InstancedModel.bBoxIndices, 0, 12);
						} 
					}

					// Render our shapes now
					//ShapeRenderer.Draw(camera.View, camera.Projection);

					// Add to the total visible
					visible++;
				}			
				spriteBatch.End();

				meshIndex++;
			}
			
			// End box rendering
		}
	}
}
