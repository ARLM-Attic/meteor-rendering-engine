using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// Data structure to contain all instance data
	/// and related vertex buffer for instancing
	public class MeshInstanceGroup
	{
		/// List of all instances present for this mesh
		public List<MeshInstance> instances;

		/// List of all visible instances after culling
		public List<MeshInstance> visibleInstances;

		/// Number of visible instances after culling
		public int totalVisible = 0;

		/// Temporary transforms for copying
		public Matrix[] tempTransforms;

		/// Tightest bounding box that fits this mesh
		public BoundingBox boundingBox;

		/// Vertex buffer for all mesh instances
		public DynamicVertexBuffer instanceVB;

		/// Name take from the mesh
		public string meshName;

		/// <summary>
		/// Default constructor
		/// </summary>
		public MeshInstanceGroup()
		{
			instances = new List<MeshInstance>();
			visibleInstances = new List<MeshInstance>();
			tempTransforms = new Matrix[1];

			meshName = "default";
		}
	}
}
