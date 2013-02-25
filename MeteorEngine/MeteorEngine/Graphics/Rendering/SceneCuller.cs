using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Meteor.Resources;

namespace Meteor.Rendering
{
	class MeshPrioritySort : IComparer<Scene.OrderedInstancedMeshData>
	{
		public int Compare(Scene.OrderedInstancedMeshData rp1,
			Scene.OrderedInstancedMeshData rp2)
		{
			int returnValue = 1;
			returnValue = rp2.priority.CompareTo(rp1.priority);

			return returnValue;
		}
	}

	class MeshDistanceSort : IComparer<MeshInstance>
	{
		public int Compare(MeshInstance instance1, MeshInstance instance2)
		{
			return instance1.distance.CompareTo(instance2.distance);
		}
	}

	/// <summary>
	/// Culls all possible objects in a scene 
	/// </summary>

	public class SceneCuller
	{
		/// Cached list of instances from last model culling.
		private List<MeshInstance> visibleInstances = new List<MeshInstance>();

		/// Minimum distance to limit full mesh rendering.
		public float maxLODdistance = 25000f;

		/// <summary>
		/// Cull an InstancedModel and its mesh groups.
		/// </summary>

		public void CullModelInstances(Camera camera, InstancedModel instancedModel)
		{
			int meshIndex = 0;
			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				// Pre-cull mesh parts
				instanceGroup.totalVisible = 0;

				foreach (MeshInstance meshInstance in instanceGroup.instances)
				{
					// Add mesh and instances to visible list if they're contained in the frustum
					if (camera.frustum.Contains(meshInstance.BSphere) != ContainmentType.Disjoint)
					{
						instanceGroup.visibleInstances[instanceGroup.totalVisible] = meshInstance;
						instanceGroup.totalVisible++;
					}
				}

				int fullMeshInstances = 0;

				// Out of the visible instances, sort those by distance
				for (int i = 0; i < instanceGroup.totalVisible; i++)
				{
					MeshInstance meshInstance = instanceGroup.visibleInstances[i];
					meshInstance.distance = Vector3.Distance(camera.position, meshInstance.position);

					// Use a second loop-through to separate the full meshes from the imposters.
					// Meshes closer than the limit distance will be moved to the front of the list,
					// and those beyond will be put into a separate bucket for imposter rendering.

					if (meshInstance.distance < maxLODdistance)
						instanceGroup.visibleInstances[fullMeshInstances++] = meshInstance;
				}
				// Update the new, filtered amount of full meshes to draw
				instanceGroup.totalVisible = fullMeshInstances;

				//instanceGroup.instances.Sort((a, b) => a.distance.CompareTo(b.distance));

				meshIndex++;
			}
			// Finished culling this model
		}

		/// <summary>
		/// Check all meshes in a scene that are outside the camera view frustum.
		/// </summary>

		public void CullModelMeshes(Scene scene, Camera camera)
		{
			scene.culledMeshes = 0;

			CullFromList(camera, scene.staticModels);
			CullFromList(camera, scene.skinnedModels);
			CullFromList(camera, scene.blendModels);
		}

		/// <summary>
		/// Wrapper to cull meshes from a specified list.
		/// </summary>

		public void CullFromList(Camera camera, Dictionary<String, InstancedModel> modelList)
		{
			visibleInstances.Clear();

			foreach (InstancedModel instancedModel in modelList.Values)
				CullModelInstances(camera, instancedModel);
			
			// Finished culling all models
			int total = visibleInstances.Count;
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

				if (camera.frustum.Contains(bounds) != ContainmentType.Disjoint)
				{
					scene.visibleLights.Add(light);
				}
			}
			// Finished culling lights
		}
	}
}
