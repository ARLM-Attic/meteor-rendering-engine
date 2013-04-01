using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	public class Material
	{
		/// Model's textures and the effect parameters
		public Dictionary<String, Texture2D> textures;

		/// <summary>
		/// Initialize lists
		/// </summary>
		public Material()
		{
			textures = new Dictionary<String, Texture2D>();
		}
	}
}
