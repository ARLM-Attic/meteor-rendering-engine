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

		/// World position of this instance
		public Vector3 position;

		/// Rotation of this instance
		public Quaternion rotation;

		/// Scale of this instance
		public Vector3 scaling;

		/// Bounding sphere of object
		public BoundingSphere boundingSphere { private set; get; }

		/// Unchanged bounding sphere of object
		private readonly BoundingSphere originalBSphere;
		public BoundingSphere OriginalBSphere
		{
			get { return originalBSphere; }
		}

		/// Distance to a world position
		public float distance = 0f;

		/// Constructor sets identity matrix as default
		public MeshInstance(BoundingSphere sphere)
		{
			transform = Matrix.Identity;

			originalBSphere = sphere;
			boundingSphere = sphere;

			scaling = new Vector3(1, 1, 1);
			largestScale = 1f;
		}

		/// New instance with an bounding sphere and transform matrix
		public MeshInstance(BoundingSphere sphere, Matrix instanceTransform)
		{
			transform = instanceTransform;
			transform.Decompose(out this.scaling, out this.rotation, out this.position);
			
			originalBSphere = sphere;
			boundingSphere = originalBSphere.Transform(transform);
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
			boundingSphere = originalBSphere.Transform(transform);

			return transform;
		}

		/// <summary>
		/// Update instance's world matrix based on scale, rotation, and translation
		/// </summary>

		public Matrix UpdateTransform(Matrix worldTransform)
		{
			transform = worldTransform;
			position = worldTransform.Translation;

			// Update the bounding sphere
			boundingSphere = originalBSphere.Transform(transform);

			return transform;
		}
	}
}
