
using System;
using System.Collections;
using Microsoft.Xna.Framework;

namespace Meteor.Resources
{
	/// <summary>
	/// Class which holds and sets up matrix and color info for a mesh instance.
	/// It also helps update the position of the mesh's BoundingSphere.
	/// </summary>
	/// 
	public class EntityInstance
	{
		/// Mesh instance matrix
		Matrix transform;
		public Matrix Transform
		{
			get { return transform; }
		}

		/// The largest factor to scale by
		public float largestScale;

		/// Stores all the vertex declaration data for this instance
		public struct InstanceData
		{
			public Matrix transform;
			//public uint color;
		}
		public InstanceData instanceData;

		/// Check whether this instance is closer or farther than another instance
		public int CompareTo(object other)
		{
			EntityInstance otherInstance = (EntityInstance)other;
			return -(distance.CompareTo(otherInstance.distance));
		}

		/// Color associated with this instance
		public int color;

		/// World position of this instance
		public Vector3 position;

		/// Rotation of this instance
		public Quaternion rotation;

		/// Scale of this instance
		public Vector3 scaling;

		/// Distance to a world position
		public float distance = 0f;

		static Random random = new Random(256);

		/// Constructor sets identity matrix as default
		public EntityInstance()
		{
			transform = Matrix.Identity;

			int r = random.Next() << 24;
			int g = random.Next() << 16;
			int b = random.Next() << 8;

			scaling = new Vector3(1, 1, 1);
			largestScale = 1f;
			//instanceData.color = 0xffffffff; //(255 << 24) + r + g + b;
		}

		/// New instance with an Entity and transform matrix
		public EntityInstance(Matrix instanceTransform)
		{
			transform = instanceTransform;
			transform.Decompose(out this.scaling, out this.rotation, out this.position);

			int r = random.Next() << 16;
			int g = random.Next() << 8;
			int b = random.Next();

			//instanceData.color = 0xffffffff; // (255 << 24) + r + g + b;
		}

		/// <summary>
		/// Update instance's world matrix based on scale, rotation, and translation
		/// </summary>

		public Matrix UpdateMatrix()
		{
			transform = Matrix.CreateScale(scaling) *
				Matrix.CreateFromQuaternion(rotation) *
				Matrix.CreateTranslation(position);

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
