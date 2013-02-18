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

		/// <summary>
		/// Cull an InstancedModel and its mesh groups.
		/// </summary>

		public void CullModelInstances(Camera camera, InstancedModel instancedModel)
		{
			// Pre-cull mesh parts
			int meshIndex = 0;
			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				instanceGroup.totalVisible = 0;

				// Out of the visible instances, sort them by instance
				foreach (MeshInstance meshInstance in instanceGroup.instances)
					meshInstance.distance = Vector3.Distance(camera.Position, meshInstance.position);

				//instanceGroup.instances.Sort((a, b) => a.distance.CompareTo(b.distance));

				foreach (MeshInstance meshInstance in instanceGroup.instances)
				{
					// Add mesh and instances to visible list if they're contained in the frustum
					if (camera.Frustum.Contains(meshInstance.BSphere) != ContainmentType.Disjoint)
					{
						instanceGroup.visibleInstances[instanceGroup.totalVisible] = meshInstance;
						instanceGroup.totalVisible++;

						//if (instanceGroup.totalVisible >= 25)
						//	break;
					}
				}
				meshIndex++;
			}
			// Finished culling this model
		}

		/// <summary>
		/// Check all meshes in a scene that are outside the camera view frustum.
		/// </summary>

		public void CullModelMeshes(Scene scene, Camera camera)
		{
			scene.visibleMeshes = 0;
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

				if (camera.Frustum.Contains(bounds) != ContainmentType.Disjoint)
				{
					scene.visibleLights.Add(light);
				}
			}
			// Finished culling lights
		}
	}
}
