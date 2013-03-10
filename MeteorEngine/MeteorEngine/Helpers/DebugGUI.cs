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

		/// Camera and scene used to obtain debug info
		Scene debugScene;
		Camera debugCamera;

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

			// Set statistic resources
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
		private bool RayIntersectsModel(Ray ray, Meteor.Resources.Model instancedModel, 
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

		/// <summary>
		/// Calculates a world space ray starting at the camera's "eye" and pointing in the direction of the cursor.
		/// <summary>
		public Ray CalculateCursorRay(Matrix projectionMatrix, Matrix viewMatrix)
		{
			mouseState = Mouse.GetState();
			Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);

			// create 2 positions in screenspace using the cursor position. 0 is as
			// close as possible to the camera, 1 is as far away as possible.
			Vector3 nearSource = new Vector3(mousePos, 0f);
			Vector3 farSource = new Vector3(mousePos, 1f);
				
			// Use Viewport.Unproject to tell what those two screen space positions would be in world space.
			Vector3 nearPoint = graphicsDevice.Viewport.Unproject(nearSource,
				projectionMatrix, viewMatrix, Matrix.Identity);

			Vector3 farPoint = graphicsDevice.Viewport.Unproject(farSource,
				projectionMatrix, viewMatrix, Matrix.Identity);

			// find the direction vector that goes from the nearPoint to the farPoint and normalize it....
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

			foreach (Meteor.Resources.Model staticModel in debugScene.staticModels.Values)
			{
				// Check for cursor picking intersection
				if (RayIntersectsModel(cursorRay, staticModel, out instanceMatrix, out bSphere))
				{
					String foundMeshName = "Model";
					foreach (String meshName in staticModel.MeshInstanceGroups.Keys)
					{
						foundMeshName = meshName;
						break;
					}

					// Project from world space to screen space
					Vector3 screenSpace = graphicsDevice.Viewport.Project(Vector3.Zero, debugCamera.projection, 
						debugCamera.view, instanceMatrix);

					// we want to draw the text a little bit above that, so we'll use the screen space position.
					Vector2 textPosition =
						new Vector2((int)screenSpace.X, (int)screenSpace.Y - 60);

					// Calculate string's center and draw using that as the origin.
					Vector2 stringCenter =
						font.MeasureString(foundMeshName) / 2;

					spriteBatch.Draw(nullTexture, 
						new Vector2(textPosition.X - stringCenter.X - 8, textPosition.Y - (int)stringCenter.Y - 2),
						new Rectangle(
						(int)(textPosition.X - stringCenter.X - 8), (int)(textPosition.Y - (int)stringCenter.Y - 2),
						(int)(stringCenter.X * 2 + 16), font.LineSpacing + 4),
						new Color(0, 0, 0, 120));

					// first draw the shadow...
					Vector2 shadowOffset = new Vector2(1, 1);
					spriteBatch.DrawString(font, foundMeshName,
						textPosition + shadowOffset, Color.Black, 0.0f,
						stringCenter, 1.0f, SpriteEffects.None, 0.0f);

					// ...and then the real text on top.
					spriteBatch.DrawString(font, foundMeshName,
						textPosition, Color.White, 0.0f,
						stringCenter, 1.0f, SpriteEffects.None, 0.0f);

					// Add debug shapes around the highlighted object
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
