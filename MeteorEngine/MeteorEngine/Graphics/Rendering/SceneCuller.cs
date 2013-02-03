using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Meteor.Resources;

namespace Meteor.Rendering
{
	static class SceneCuller
	{
		/// <summary>
		/// Cached list of instances from last model culling.
		/// </summary>
		static List<MeshInstance> visibleInstances = new List<MeshInstance>();
		/*
		/// <summary>
		/// Constructor to initialize cached lists
		/// </summary>
		public SceneCuller()
		{
			List<MeshInstance> visibleInstances = new List<MeshInstance>();
		}
		*/
		/// <summary>
		/// Cull an InstancedModel and its mesh groups.
		/// </summary>

		public static void CullModel(Camera camera, InstancedModel instancedModel)
		{
			// Pre-cull mesh parts
			int meshIndex = 0;
			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				instanceGroup.totalVisible = 0;
				foreach (MeshInstance meshInstance in instanceGroup.instances)
				{
					// Add mesh and instances to visible list if they're contained in the frustum

					if (camera.Frustum.Contains(meshInstance.BSphere) != ContainmentType.Disjoint)
					{
						instanceGroup.visibleInstances[instanceGroup.totalVisible] = meshInstance;
						instanceGroup.totalVisible++;

						// Add instance to list cache.
						//visibleInstances.Add(meshInstance);
					}
					else
					{
						visibleInstances.Add(meshInstance);
					}
				}
				meshIndex++;
			}
			// Finished culling this model
		}

		/// <summary>
		/// Wrapper to cull meshes from a specified list.
		/// </summary>

		public static void CullFromList(Camera camera, Dictionary<String, InstancedModel> modelList)
		{
			visibleInstances.Clear();

			foreach (InstancedModel instancedModel in modelList.Values)
				CullModel(camera, instancedModel);
			
			// Finished culling all models
			int total = visibleInstances.Count;
		}

		/// <summary>
		/// Remove any lights outside of the viewable frustum.
		/// </summary>

		public static void CullLights(Scene scene, Camera camera)
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
	}
}
