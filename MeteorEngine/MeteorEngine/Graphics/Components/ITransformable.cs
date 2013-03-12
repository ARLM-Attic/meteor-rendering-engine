using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Meteor.Resources;

namespace Meteor.Resources
{
	public interface ITransformable
	{
		/// Object translation parameter
		Vector3 translation { set; get; }

		/// Object rotation parameter
		Vector3 rotation { set; get; }

		/// Object uniform scaling parameter
		float scale { set; get; }

		/// Object's transformation matrix
		Matrix worldTransform { get; }
	}
}