using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Resources;

namespace Meteor.Rendering
{
	class MeshPrioritySort : IComparer<Scene.OrderedMeshData>
	{
		public int Compare(Scene.OrderedMeshData rp1,
			Scene.OrderedMeshData rp2)
		{
			int returnValue = 1;
			returnValue = rp2.priority.CompareTo(rp1.priority);

			return returnValue;
		}
	}

	public class SceneRenderer
	{
		/// Basic effect to be used for skinned meshes, for now
		BasicEffect basicEffect;

		/// Effect technique used by the scene
		String shaderTechnique;

		/// Scene stats used in rendering
		public int totalPolys;
		public bool debug = false;

		/// For loading scene content
		ContentManager content;
		GraphicsDevice graphicsDevice;
		Texture2D testNormal;
		Texture2D blankTexture, blankSpecular;

		MeshPrioritySort meshPrioritySort;

		/// Containers for temp data, to avoid calling the GC
		Vector3[] boxCorners;
		Matrix[] tempBones;

		/// Swap space for vertex buffer bindings
		VertexBufferBinding[] bindings;

	    public SceneRenderer(GraphicsDevice device, ResourceContentManager content)
        {
			this.graphicsDevice = device; 
			this.content = content;

            // Use standard GBuffer as a default
            shaderTechnique = "GBuffer";

			testNormal = content.Load<Texture2D>("null_normal");
			blankTexture = content.Load<Texture2D>("null_color");
			blankSpecular = content.Load<Texture2D>("null_specular");

			// Helpers for sorting and drawing debug shapes
			meshPrioritySort = new MeshPrioritySort();
			ShapeRenderer.Initialize(graphicsDevice);

			// Effect for bounding box drawing
			basicEffect = new BasicEffect(device);
			basicEffect.LightingEnabled = false;
			basicEffect.TextureEnabled = false;
			basicEffect.VertexColorEnabled = true;

			boxCorners = new Vector3[8];
			tempBones = new Matrix[60];

			// Create the instance data vertex buffer.
			bindings = new VertexBufferBinding[2];
		}

		/// <summary>
		/// Set current model effect technique
		/// </summary>
		public void UseTechnique(String technique)
		{
			shaderTechnique = technique;
		}

		/// <summary>
		/// Remove any lights outside of the viewable frustum.
		/// </summary>

		public void CullLights(Scene scene, Camera camera)
		{
			Vector3 lightPosition = Vector3.Zero;
			Vector3 radiusVector = Vector3.Zero;

			// Refresh the list of visible point lights
			scene.visibleLights.Clear();
			BoundingSphere bounds = new BoundingSphere();

			// Pre-cull point lights
			foreach (PointLight light in scene.pointLights)
			{
				lightPosition.X = light.instance.transform.M41;
				lightPosition.Y = light.instance.transform.M42;
				lightPosition.Z = light.instance.transform.M43;

				radiusVector.X = light.instance.transform.M11;
				radiusVector.Y = light.instance.transform.M12;
				radiusVector.Z = light.instance.transform.M13;

				float radius = radiusVector.Length();

				// Create bounding sphere to check which lights are in view

				bounds.Center = lightPosition;
				bounds.Radius = radius;

				if (camera.Frustum.Contains(bounds) != ContainmentType.Disjoint)
				{
					scene.visibleLights.Add(light);
				}
			}
			// Finished culling lights
		}

		/// <summary>
		/// Check for meshes that are outside the view frustum.
		/// </summary>

		public void CullModelMeshes(Scene scene, Camera camera)
		{
			scene.visibleMeshes = 0;
			scene.culledMeshes = 0;

			CullFromModelList(scene, camera, scene.staticModels);
			CullFromModelList(scene, camera, scene.skinnedModels);
			CullFromModelList(scene, camera, scene.blendModels);
		}

		/// <summary>
		/// Remove all scene meshes from the culling list.
		/// </summary>

		public void IgnoreCulling(Scene scene, Camera camera)
		{
			scene.visibleMeshes = 0;
			scene.culledMeshes = 0;

			MakeModelsVisible(scene, camera, scene.staticModels);
			MakeModelsVisible(scene, camera, scene.skinnedModels);
		}

		/// <summary>
		/// Cull meshes from a specified list.
		/// </summary>

		private void CullFromModelList(Scene scene, Camera camera, Dictionary<String, InstancedModel> modelList)
		{
			// Pre-cull mesh parts
			
			foreach (InstancedModel instancedModel in modelList.Values)
			{
				int meshIndex = 0;
				foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
				{				
					instanceGroup.totalVisible = 0;
					foreach (MeshInstance meshInstance in instanceGroup.instances)
					{			
						// Add mesh and instances to visible list if they're contained in the frustum

						if (camera.Frustum.Contains(meshInstance.BSphere) != ContainmentType.Disjoint)
						{
							scene.visibleMeshes++;
							instanceGroup.visibleInstances[instanceGroup.totalVisible] = meshInstance;
							instanceGroup.totalVisible++;
						}
						else
						{
							scene.culledMeshes++;
						}
					}
					meshIndex++;
				}			
				// Finished culling this model
			}
		}

		/// <summary>
		/// Remove culled meshes from a specified list.
		/// </summary>

		public void MakeModelVisible(Dictionary<String, InstancedModel> modelList, String modelName, int meshID)
		{
			int meshIndex = 0;
			bool found = false;

			foreach (ModelMesh mesh in modelList[modelName].model.Meshes)
			{
				if (meshIndex == meshID)
				{
					found = true;
				}
				if (found == true) break;
				meshIndex++;
			}
			// Finished adding this model mesh
		}

		/// <summary>
		/// Remove culled meshes from a specified list.
		/// </summary>

		private void MakeModelsVisible(Scene scene, Camera camera,
			Dictionary<String, InstancedModel> modelList)
		{
			// Pre-cull mesh parts
			if (scene.orderedMeshes.Count == 0)
				return;
		}

		/// <summary>
		/// Draw the entire scene to the GBuffer
		/// </summary>
		/// <param name="camera"></param>

		public void Draw(Scene scene, Camera camera)
		{
			Viewport viewport = graphicsDevice.Viewport;
			viewport.MinDepth = 0.0f;
			viewport.MaxDepth = 0.99999f;
			graphicsDevice.Viewport = viewport;
			graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

			totalPolys = 0;
			scene.totalPolys = 0;

			// Update the viewport for proper rendering order

			foreach (InstancedModel instancedModel in scene.staticModels.Values)
				DrawModel(instancedModel, camera, this.shaderTechnique);

			foreach (InstancedModel skinnedModel in scene.skinnedModels.Values)
				DrawModel(skinnedModel, camera, this.shaderTechnique + "Animated");

			scene.totalPolys = totalPolys;
		}

		/// <summary>
		/// Draw with a custom effect
		/// </summary>

		public void Draw(Scene scene, Effect effect, BlendState blendState,
			RasterizerState rasterizerState)
		{
			graphicsDevice.DepthStencilState = DepthStencilState.Default; 
			graphicsDevice.RasterizerState = rasterizerState;
			graphicsDevice.BlendState = blendState;

			graphicsDevice.SetVertexBuffer(null);

			foreach (InstancedModel instancedModel in scene.staticModels.Values)
				DrawModel(instancedModel, effect, "Default");

			foreach (InstancedModel skinnedModel in scene.skinnedModels.Values)
				DrawModel(skinnedModel, effect, "DefaultAnimated");

			// Finished drawing visible meshes
		}

		/// <summary>
		/// Overloads for drawing custom effects
		/// </summary>	

		public void Draw(Scene scene, Effect effect)
		{
			Draw(scene, effect, BlendState.Opaque, RasterizerState.CullNone);
		}

		public void Draw(Scene scene, Effect effect, BlendState blendState)
		{
			Draw(scene, effect, blendState, RasterizerState.CullNone);
		}

		public void Draw(Scene scene, Effect effect, RasterizerState rasterizerState)
		{
			Draw(scene, effect, BlendState.Opaque, rasterizerState);
		}

		/// <summary>
		/// Vertex declaration for mesh instancing, storing a 4x4 world transformation matrix
		/// </summary>

		VertexDeclaration instanceVertexDec = new VertexDeclaration
		(
			new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
			new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
			new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
			new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3)
		);	

		/// <summary>
		/// A bit hacky way to limit bone transforms. Will fix soon.
		/// </summary>
		/// <param name="instancedModel"></param>

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
		/// Draw all visible meshes for this model with its default effect.
		/// </summary>

		private void DrawModel(InstancedModel instancedModel, Camera camera, string tech)
		{
			TrimBoneTransforms(instancedModel);
			int meshIndex = 0;

			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				int totalInstances = instanceGroup.instances.Count;

				/// Resize the vertex buffer for instances if needed
				if (totalInstances > instanceGroup.instanceVB.VertexCount)
				{
					instanceGroup.instanceVB =
						instancedModel.CreateInstanceVB(graphicsDevice, instanceGroup.instances);
				}
				else
				{
					instancedModel.UpdateInstanceVB(instanceGroup);
				}

				// Retrieve the current mesh from the mesh list
				ModelMesh mesh = instancedModel.model.Meshes[meshIndex];

				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					// Skip rendering the mesh parts if they aren't visible
					if (instanceGroup.totalVisible == 0)
						continue;

					VertexBuffer meshInstanceVB = instanceGroup.instanceVB;

					bindings[0] = new VertexBufferBinding(meshPart.VertexBuffer, meshPart.VertexOffset, 0);
					bindings[1] = new VertexBufferBinding(meshInstanceVB, 0, 1);

					// Bind both mesh vertex buffer and per-instance matrix data
					graphicsDevice.SetVertexBuffers(bindings);
					graphicsDevice.Indices = meshPart.IndexBuffer;

					// Assign effect and curent technique
					Effect effect = meshPart.Effect;
					effect.CurrentTechnique = effect.Techniques[tech];

					Matrix world = instancedModel.boneMatrices[mesh.ParentBone.Index];
					effect.Parameters["World"].SetValue(world);

					// Set bones if the model is animated
					if (instancedModel.animationPlayer != null)
						effect.Parameters["bones"].SetValue(tempBones);

					if (instancedModel.Textures[meshIndex] == null)
						effect.Parameters["Texture"].SetValue(blankTexture);

					effect.Parameters["View"].SetValue(camera.View);
					effect.Parameters["Projection"].SetValue(camera.Projection);
					effect.Parameters["WorldInverseTranspose"].SetValue(
						Matrix.Transpose(Matrix.Invert(world * mesh.ParentBone.Transform)));
					effect.Parameters["CameraPosition"].SetValue(camera.Position);

					for (int i = 0; i < effect.CurrentTechnique.Passes.Count; i++)
					{
						effect.CurrentTechnique.Passes[i].Apply();
						
						graphicsDevice.DrawInstancedPrimitives(
							PrimitiveType.TriangleList, 0, 0,
							meshPart.NumVertices, meshPart.StartIndex,
							meshPart.PrimitiveCount, instanceGroup.totalVisible);
					}

					totalPolys += meshPart.PrimitiveCount * totalInstances;
				}

				// Finished drawing mesh parts
				meshIndex++;
			}
			// End model rendering
		}

		/// <summary>
		/// Draw instanced model with a custom effect
		/// </summary>	

		public void DrawModel(InstancedModel instancedModel, Effect effect, String tech = "Default")
		{			
			TrimBoneTransforms(instancedModel);
			int meshIndex = 0;

			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				int totalInstances = instanceGroup.instances.Count;			

				/// Resize the vertex buffer for instances if needed
				if (totalInstances > instanceGroup.instanceVB.VertexCount)
				{
					instanceGroup.instanceVB =
						instancedModel.CreateInstanceVB(graphicsDevice, instanceGroup.instances);
				}
				else
				{
					instancedModel.UpdateInstanceVB(instanceGroup);
				}

				// Retrieve the current mesh from the mesh list
				ModelMesh mesh = instancedModel.model.Meshes[meshIndex];

				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					// Skip rendering the mesh parts if they aren't visible
					if (instanceGroup.totalVisible == 0)
						continue;

					VertexBuffer meshInstanceVB = instanceGroup.instanceVB;

					bindings[0] = new VertexBufferBinding(meshPart.VertexBuffer, meshPart.VertexOffset, 0);
					bindings[1] = new VertexBufferBinding(meshInstanceVB, 0, 1);

					// Bind both mesh vertex buffer and per-instance matrix data
					graphicsDevice.SetVertexBuffers(bindings);
					graphicsDevice.Indices = meshPart.IndexBuffer;

					// Assign effect technique
					effect.CurrentTechnique = effect.Techniques[tech];

					Matrix world = instancedModel.boneMatrices[mesh.ParentBone.Index];
					effect.Parameters["World"].SetValue(world);
					effect.Parameters["Texture"].SetValue(instancedModel.Textures[meshIndex]);

					// Set bones if the model is animated
					if (instancedModel.animationPlayer != null)
						effect.Parameters["bones"].SetValue(tempBones);

					for (int i = 0; i < effect.CurrentTechnique.Passes.Count; i++)
					{
						effect.CurrentTechnique.Passes[i].Apply();

						graphicsDevice.DrawInstancedPrimitives(
							PrimitiveType.TriangleList, 0, 0,
							meshPart.NumVertices, meshPart.StartIndex,
							meshPart.PrimitiveCount, instanceGroup.totalVisible);
					}
				}

				// Finished drawing mesh parts
				meshIndex++;
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
			viewport.MinDepth = 0.99999f;
			viewport.MaxDepth = 1.0f;
			graphicsDevice.Viewport = viewport;

			if (scene.Skybox == null)
				return;
			
			// Make skybox visible and copy instance data
			scene.Skybox.MeshInstanceGroups["DefaultName"].totalVisible = 1;
			scene.Skybox.MeshInstanceGroups["DefaultName"].visibleInstances[0] =
				scene.Skybox.MeshInstanceGroups["DefaultName"].instances[0];

			scene.Skybox.Translate(camera.Position);
			DrawModel(scene.Skybox, camera, this.shaderTechnique);
		}
		
		/// <summary>
		/// Draw all visible bounding boxes
		/// </summary>

		public void DrawBoundingBoxes(Scene scene, Camera camera)
		{
			if (scene.debug == true)
			{
				//CullModelMeshes(scene, camera);

				basicEffect.View = camera.View;
				basicEffect.Projection = camera.Projection;

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
		/// Draw debug bounding box
		/// </summary>
		/// <param name="model"></param>
		/// <param name="camera"></param>

		private void DrawBoundingBoxes(InstancedModel model, Camera camera)
		{
			int meshIndex = 0;

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

				foreach (MeshInstance meshInstance in instancedGroup.instances)
				{
					Matrix modelTransform = meshInstance.Transform;

					for (int i = boxCorners.Length; i-- > 0; )
					{
						model.boxVertices[i].Position = Vector3.Transform(boxCorners[i], modelTransform);
						model.boxVertices[i].Color = Color.Cyan;
					}

					// Transform the temporary bounding boxes with the model instance's world matrix
					// TODO: Update these boxes only when intances are updated

					// Render the bounding box for this instance
					if (camera.Frustum.Contains(meshInstance.BSphere) == ContainmentType.Contains)
					{
						// Add a bounding sphere to the list of shapes to draw
						ShapeRenderer.AddBoundingSphere(meshInstance.BSphere, Color.Red);

						for (int i = 0; i < basicEffect.CurrentTechnique.Passes.Count; i++)
						{
							basicEffect.CurrentTechnique.Passes[i].Apply();
							graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(
								PrimitiveType.LineList, model.boxVertices, 0, 8,
								InstancedModel.bBoxIndices, 0, 12);
						}
					}

					// Render our shapes now
					ShapeRenderer.Draw(camera.View, camera.Projection);
				}			
				meshIndex++;
			}
			
			// End box rendering
		}
	}
}
