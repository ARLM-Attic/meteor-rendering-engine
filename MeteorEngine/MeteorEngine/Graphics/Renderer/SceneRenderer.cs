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

	public class SceneRenderComponent
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
		TextureCube environment;
		Texture2D blankTexture, blankSpecular;

		MeshPrioritySort meshPrioritySort;

		/// Containers for temp data, to avoid calling the GC
		Vector3[] boxCorners;
		Matrix[] tempBones;

		/// Swap space for vertex buffer bindings
		VertexBufferBinding[] bindings;

	    public SceneRenderComponent(GraphicsDevice device, ResourceContentManager content)
        {
			this.graphicsDevice = device; 
			this.content = content;

            // Use standard GBuffer as a default
            shaderTechnique = "GBuffer";

			testNormal = content.Load<Texture2D>("null_normal");
			blankTexture = content.Load<Texture2D>("null_color");
			blankSpecular = content.Load<Texture2D>("null_specular");
			environment = content.Load<TextureCube>("skyblue_cube");

			meshPrioritySort = new MeshPrioritySort();

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
		/// Clear all visible meshes from all models
		/// </summary>

		public void CullAllModels(Scene scene)
		{
			scene.culledMeshes = 0;
			/*
			// Pre-cull mesh parts
			foreach (InstancedModel instancedModel in scene.staticModels.Values)
				instancedModel.VisibleMeshes.Clear();

			foreach (InstancedModel instancedModel in scene.skinnedModels.Values)
				instancedModel.VisibleMeshes.Clear();

			foreach (InstancedModel instancedModel in scene.blendModels.Values)
				instancedModel.VisibleMeshes.Clear();
			*/
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
			/*
			foreach (InstancedModel instancedModel in modelList.Values)
			{
				int meshIndex = 0;
				
				foreach (BoundingBox box in instancedModel.BoundingBoxes)
				{			
					instancedModel.tempBoxes[meshIndex] = box;
					instancedModel.tempBoxes[meshIndex].Min = 
						Vector3.Transform(box.Min, instancedModel.MeshInstances[meshIndex][instancedModel.MeshInstances.Count - 1].transform);
					instancedModel.tempBoxes[meshIndex].Max = Vector3.Transform(box.Max, instancedModel.Transform);

					// Add to mesh to visible list if it's contained in the frustum

					if (camera.Frustum.Contains(instancedModel.tempBoxes[meshIndex]) != ContainmentType.Disjoint)
					{
						scene.visibleMeshes++;
					}
					else
					{
						scene.culledMeshes++;
					}

					// Move position into screen space homoegenous coordinates
					Vector4 source = Vector4.Transform(
						instancedModel.MeshPos[meshIndex], camera.View * camera.Projection);
					instancedModel.ScreenPos[meshIndex] = 
						new Vector2((source.X / source.W + 1f) / 2f, (-source.Y / source.W + 1f) / 2f);
					meshIndex++;
				}
				
				// Finished culling this model
			}
			 */
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
			/*
			int i = 0;
			foreach (KeyValuePair<string, InstancedModel> instancedModel in modelList)
			{
				int meshIndex = 0;

				foreach (ModelMesh mesh in instancedModel.Value.model.Meshes)
				{
					float radius = mesh.BoundingSphere.Radius * instancedModel.Value.scaling.X;
					float distance = Vector3.Distance(
						camera.Position, mesh.BoundingSphere.Center + instancedModel.Value.position);
					if (distance < 0.01f) distance = 0.01f;

					// Set mesh metadata
					scene.orderedMeshes[i].modelName = instancedModel.Key;
					scene.orderedMeshes[i].meshID = meshIndex;
					scene.orderedMeshes[i].priority = radius / distance;

					i++;
					scene.visibleMeshes++;
				}
				// Finished adding this model
			}
			*/
			// Sort the order priority
			//scene.orderedMeshes.Sort(meshPrioritySort);
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
		/// Draw all visible meshes for this model with its default effect.
		/// </summary>

		private void DrawModel(InstancedModel instancedModel, Camera camera, string tech)
		{
			// Draw the model.
			instancedModel.model.CopyAbsoluteBoneTransformsTo(instancedModel.boneMatrices);

			// A bit hacky way to limit number of mesh bones. Will fix soon
			if (instancedModel.animationPlayer != null)
			{
				Matrix[] bones = instancedModel.animationPlayer.GetSkinTransforms();
				int maxBones = (bones.Count() > 60) ? 60 : bones.Count();
				Array.Copy(bones, tempBones, maxBones);
			}

			int meshIndex = 0;

			//foreach (ModelMesh mesh in instancedModel.model.Meshes)
			foreach (InstancedModel.MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups)
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

					// A bit hacky way to limit number of mesh bones. Will fix soon
					if (instancedModel.animationPlayer != null)
						effect.Parameters["bones"].SetValue(tempBones);

					if (instancedModel.Textures[meshIndex] == null)
						effect.Parameters["Texture"].SetValue(blankTexture);

					effect.Parameters["EnvironmentMap"].SetValue(environment);
					effect.Parameters["World"].SetValue(world);
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
							meshPart.PrimitiveCount, totalInstances);
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
			// Draw the model.
			instancedModel.model.CopyAbsoluteBoneTransformsTo(instancedModel.boneMatrices);

			// A bit hacky. Will fix soon
			if (instancedModel.animationPlayer != null)
			{
				Matrix[] bones = instancedModel.animationPlayer.GetSkinTransforms();
				int maxBones = (bones.Count() > 60) ? 60 : bones.Count();

				// Not very efficient to do, need a way to avoid copying arrays
				Array.Copy(bones, tempBones, maxBones);
				effect.Parameters["bones"].SetValue(tempBones);
			}

			int meshIndex = 0;	

			foreach (InstancedModel.MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups)
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
					VertexBuffer meshInstanceVB = instanceGroup.instanceVB;

					bindings[0] = new VertexBufferBinding(meshPart.VertexBuffer, meshPart.VertexOffset, 0);
					bindings[1] = new VertexBufferBinding(meshInstanceVB, 0, 1);

					// Bind both mesh vertex buffer and per-instance matrix data
					graphicsDevice.SetVertexBuffers(bindings);
					graphicsDevice.Indices = meshPart.IndexBuffer;

					// Assign effect technique
					effect.CurrentTechnique = effect.Techniques[tech];

					Matrix world = instancedModel.boneMatrices[mesh.ParentBone.Index] * Matrix.Identity;
					effect.Parameters["World"].SetValue(world);
					effect.Parameters["Texture"].SetValue(instancedModel.Textures[meshIndex]);

					for (int i = 0; i < effect.CurrentTechnique.Passes.Count; i++)
					{
						effect.CurrentTechnique.Passes[i].Apply();

						graphicsDevice.DrawInstancedPrimitives(
							PrimitiveType.TriangleList, 0, 0,
							meshPart.NumVertices, meshPart.StartIndex,
							meshPart.PrimitiveCount, totalInstances);
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
			
			// Draw all skybox meshes
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
				CullModelMeshes(scene, camera);

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

			foreach (KeyValuePair<string, BoundingBox> boxEntry in model.BoundingBoxes)
			{
				string boxName = boxEntry.Key;
				BoundingBox box = boxEntry.Value;

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

				foreach (EntityInstance modelInstance in model.MeshInstanceGroups[meshIndex].instances)
				{
					Matrix modelTransform = modelInstance.Transform;

					for (int i = boxCorners.Length; i-- > 0; )
					{
						model.boxVertices[i].Position = Vector3.Transform(boxCorners[i], modelTransform);
						model.boxVertices[i].Color = Color.Cyan;
					}

					// Transform the temporary bounding boxes with the model instance's world matrix
					// TODO: Update these boxes only when intances are updated

					box.Min += modelInstance.position;
					box.Max += modelInstance.position;

					model.tempBoxes[boxName] = box;
					//model.tempBoxes[boxName].Min = box.Min + modelInstance.position;//Vector3.Transform(box.Min, modelTransform);
					//model.tempBoxes[boxName].Max = box.Max + modelInstance.position;//Vector3.Transform(box.Max, modelTransform);

					// Render the bounding box for this instance
					//if (camera.Frustum.Contains(model.tempBoxes[meshIndex]) != ContainmentType.Disjoint)
					{
						for (int i = 0; i < basicEffect.CurrentTechnique.Passes.Count; i++)
						{
							basicEffect.CurrentTechnique.Passes[i].Apply();
							graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(
								PrimitiveType.LineList, model.boxVertices, 0, 8,
								InstancedModel.bBoxIndices, 0, 12);
						}
					}
				}
				
				meshIndex++;
			}
			
			// End box rendering
		}
	}
}
