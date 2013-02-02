using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Meteor.Resources;

namespace Meteor.Rendering
{
	public abstract class RenderInput
	{		
		protected BaseShader renderer;

		public BaseShader Renderer
		{
			get
			{
				return renderer;
			}
		}
	}

	class SceneInput : RenderInput
	{
		public Scene scene;

		public SceneInput(Scene scene)
		{
			this.scene = scene;
		}
	}

	class CameraInput : RenderInput
	{
		public Camera camera;

		public CameraInput(Camera camera)
		{
			this.camera = camera;
		}
	}

	class RenderTargetInput : RenderInput
	{
		public RenderTarget2D target;

		public RenderTargetInput(RenderTarget2D target)
		{
			this.target = target;
		}
	}
}
