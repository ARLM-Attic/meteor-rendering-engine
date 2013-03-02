using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using Meteor.Resources;
using MeteorEngine;

namespace Meteor
{
	public class DebugGUI : DrawableComponent
	{
		/// <summary>
		/// Content loaders (by file or by resource)
		/// </summary>
		ContentManager content;
		ResourceContentManager resxContent;

		/// <summary>
		/// Get mouse and keyboard states
		/// </summary>
		MouseState mouseState;

		/// Utility classes
		RenderStats renderStats;
		QuadRenderComponent quadRenderer;
		StringBuilder debugString;

		/// Useful for all DrawableComponents
		SpriteFont font;
		SpriteBatch spriteBatch;
		Texture2D nullTexture;

		/// <summary>
		/// Debug GUI constructor
		/// </summary>

		public DebugGUI(IServiceProvider services)
			: base(services)
		{
			content = new ContentManager(services, "MeteorEngine.Content");
			resxContent = new ResourceContentManager(services, MeteorContentResource.ResourceManager);
			renderStats = new RenderStats();

			debugString = new StringBuilder(64, 64);
		}

		/// <summary>
		/// Load debug resources used by the high-level renderer
		/// </summary>

		protected override void LoadContent()
		{
			// Load debug resources
			font = resxContent.Load<SpriteFont>("defaultFont");
			spriteBatch = new SpriteBatch(graphicsDevice);

			// Load up all resource renderers
			quadRenderer = new QuadRenderComponent(graphicsDevice);

			Color[] whitePixel = { Color.White };
			nullTexture = new Texture2D(graphicsDevice, 1, 1);
			nullTexture.SetData<Color>(whitePixel);
		}
		
		/// <summary>
		/// Checks if a ray intersects the bounding sphere of a model.
		/// </summary>
		private bool RayIntersectsModel(Ray ray, InstancedModel instancedModel, 
			out Matrix instanceMatrix, out BoundingSphere bSphere)
		{
			foreach (MeshInstanceGroup instanceGroup in instancedModel.MeshInstanceGroups.Values)
			{
				foreach (MeshInstance meshInstance in instanceGroup.visibleInstances)
				{
					if (meshInstance == null) continue;

					// Sphere collision test in world space.
					if (meshInstance.BSphere.Intersects(ray) != null)
					{
						instanceMatrix = meshInstance.Transform;
						bSphere = meshInstance.BSphere;
						return true;
					}
				}
			}

			// No collision was found
			instanceMatrix = Matrix.Identity;
			bSphere = new BoundingSphere();
			return false;
		}

		// CalculateCursorRay Calculates a world space ray starting at the camera's
		// "eye" and pointing in the direction of the cursor. Viewport.Unproject is used
		// to accomplish this. see the accompanying documentation for more explanation
		// of the math behind this function.
		public Ray CalculateCursorRay(Matrix projectionMatrix, Matrix viewMatrix)
		{
			mouseState = Mouse.GetState();
			Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);

			// create 2 positions in screenspace using the cursor position. 0 is as
			// close as possible to the camera, 1 is as far away as possible.
			Vector3 nearSource = new Vector3(mousePos, 0f);
			Vector3 farSource = new Vector3(mousePos, 1f);

			// use Viewport.Unproject to tell what those two screen space positions
			// would be in world space. we'll need the projection matrix and view
			// matrix, which we have saved as member variables. We also need a world
			// matrix, which can just be identity.
			Vector3 nearPoint = graphicsDevice.Viewport.Unproject(nearSource,
				projectionMatrix, viewMatrix, Matrix.Identity);

			Vector3 farPoint = graphicsDevice.Viewport.Unproject(farSource,
				projectionMatrix, viewMatrix, Matrix.Identity);

			// find the direction vector that goes from the nearPoint to the farPoint
			// and normalize it....
			Vector3 direction = farPoint - nearPoint;
			direction.Normalize();

			// and then create a new ray using nearPoint as the source.
			return new Ray(nearPoint, direction);
		}

		public override void Draw(GameTime gameTime)
		{			
			base.Draw(gameTime);
		}

		/// <summary>
		/// Display the name label(s) of the models in screen space.
		/// </summary>

		public void DrawModelNames(Scene debugScene, Camera debugCamera)
		{
			spriteBatch.Begin();

			// If the cursor is over a model, we'll draw its name.
			Ray cursorRay = CalculateCursorRay(debugCamera.projection, debugCamera.view);
			Matrix instanceMatrix = Matrix.Identity;
			BoundingSphere bSphere;

			foreach (InstancedModel staticModel in debugScene.staticModels.Values)
			{
				// check to see if the cursorRay intersects the model....
				if (RayIntersectsModel(cursorRay, staticModel, out instanceMatrix, out bSphere))
				{
					String foundMeshName = "Model";
					foreach (String meshName in staticModel.MeshInstanceGroups.Keys)
					{
						foundMeshName = meshName;
						break;
					}

					// now we know that we want to draw the model's name. We want to
					// draw the name a little bit above the model: but where's that?
					// SpriteBatch.DrawString takes screen space coordinates, but the 
					// model's position is stored in world space. 

					// we'll use Viewport.Project, which will project a world space
					// point into screen space. We'll project the vector (0,0,0) using
					// the model's world matrix, and the view and projection matrices.
					// that will tell us where the model's origin is on the screen.
					Vector3 screenSpace = graphicsDevice.Viewport.Project(
						Vector3.Zero, debugCamera.projection, debugCamera.view,
						instanceMatrix);

					// we want to draw the text a little bit above that, so we'll use
					// the screen space position - 60 to move up a little bit. A better
					// approach would be to calculate where the top of the model is, and
					// draw there. It's not that much harder to do, but to keep the
					// sample easy, we'll take the easy way out.
					Vector2 textPosition =
						new Vector2(screenSpace.X, screenSpace.Y - 60);

					// we want to draw the text centered around textPosition, so we'll
					// calculate the center of the string, and use that as the origin
					// argument to spriteBatch.DrawString. DrawString automatically
					// centers text around the vector specified by the origin argument.
					Vector2 stringCenter =
						font.MeasureString(foundMeshName) / 2;

					spriteBatch.Draw(nullTexture, new Rectangle(
						(int)(textPosition.X - stringCenter.X - 8), (int)(textPosition.Y - (int)stringCenter.Y - 2),
						(int)(stringCenter.X * 2 + 16), font.LineSpacing + 4),
						new Color(0, 0, 0, 120));

					// to make the text readable, we'll draw the same thing twice, once
					// white and once black, with a little offset to get a drop shadow
					// effect.

					// first we'll draw the shadow...
					Vector2 shadowOffset = new Vector2(1, 1);
					spriteBatch.DrawString(font, foundMeshName,
						textPosition + shadowOffset, Color.Black, 0.0f,
						stringCenter, 1.0f, SpriteEffects.None, 0.0f);

					// ...and then the real text on top.
					spriteBatch.DrawString(font, foundMeshName,
						textPosition, Color.White, 0.0f,
						stringCenter, 1.0f, SpriteEffects.None, 0.0f);

					ShapeRenderer.AddBoundingSphere(bSphere, Color.LawnGreen);
					ShapeRenderer.Draw(debugCamera.view, debugCamera.projection);

					// We can stop here if just one model is needed.
					//break;
				}
			}

			spriteBatch.End();
		}
	}
}
