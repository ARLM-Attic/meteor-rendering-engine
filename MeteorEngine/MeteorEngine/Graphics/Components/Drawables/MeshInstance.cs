
using System;
using System.Collections;
using Microsoft.Xna.Framework;

namespace Meteor.Resources
{
	/// <summary>
	/// Class which holds and sets up matrix and color info for a mesh instance.
	/// It also helps update the position of the mesh's BoundingSphere.
	/// </summary>

	public class MeshInstance
	{
		/// Mesh instance matrix
		Matrix transform;
		public Matrix Transform
		{
			get { return transform; }
		}

		/// The largest factor to scale by
		public float largestScale;

		/// Check whether this instance is closer or farther than another instance
		public int CompareTo(object other)
		{
			MeshInstance otherInstance = (MeshInstance)other;
			return -(distance.CompareTo(otherInstance.distance));
		}

		/// World position of this instance
		public Vector3 position;

		/// Rotation of this instance
		public Quaternion rotation;

		/// Scale of this instance
		public Vector3 scaling;

		/// Bounding sphere of object
		private BoundingSphere bSphere;
		public BoundingSphere BSphere
		{
			get { return bSphere; }
		}

		/// Unchanged bounding sphere of object
		private readonly BoundingSphere originalBSphere;
		public BoundingSphere OriginalBSphere
		{
			get { return originalBSphere; }
		}

		/// Distance to a world position
		public float distance = 0f;

		static Random random = new Random(256);

		/// Constructor sets identity matrix as default
		public MeshInstance(BoundingSphere sphere)
		{
			transform = Matrix.Identity;

			originalBSphere = sphere;
			bSphere = sphere;

			int r = random.Next() << 24;
			int g = random.Next() << 16;
			int b = random.Next() << 8;

			scaling = new Vector3(1, 1, 1);
			largestScale = 1f;
		}

		/// New instance with an Entity and transform matrix
		public MeshInstance(BoundingSphere sphere, Matrix instanceTransform)
		{
			transform = instanceTransform;
			transform.Decompose(out this.scaling, out this.rotation, out this.position);
			
			originalBSphere = sphere;
			bSphere = originalBSphere.Transform(transform);
		}

		/// <summary>
		/// Update instance's world matrix based on scale, rotation, and translation
		/// </summary>

		public Matrix UpdateMatrix()
		{
			transform = Matrix.CreateScale(scaling) *
				Matrix.CreateFromQuaternion(rotation) *
				Matrix.CreateTranslation(position);

			// Update the bounding sphere
			bSphere = originalBSphere.Transform(transform);

			return transform;
		}

		/// <summary>
		/// Set the distance to a particular world position
		/// </summary>
		public void UpdateDistanceTo(Vector3 dest)
		{
			distance = Vector3.Distance(position, dest);
		}

		/// <summary>
		/// Helper for picking a random number inside the specified range
		/// </summary>
		static float RandomNumberBetween(float min, float max)
		{
			return MathHelper.Lerp(min, max, (float)random.NextDouble());
		}
	}
}
