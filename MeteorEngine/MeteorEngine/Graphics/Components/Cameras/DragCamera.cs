using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Meteor.Resources
{
	/// <summary>
	/// Controllable camera class
	/// </summary>
	public class DragCamera : Camera
	{
		/// <summary>
		/// Adjust smoothing to create a more fluid moving camera.
		/// Too much smoothing will cause a disorienting feel.
		/// </summary>
		public float smoothing = 2f;
		public float moveSpeed = 0.0625f;

		KeyboardState currentKeyboardState = new KeyboardState();
		GamePadState currentGamePadState = new GamePadState();

		bool mouseLeftHeld;

		public DragCamera()
		{
			position.Y = 4f;
		}

		public DragCamera(Vector3 pos, Vector2 orientation)
		{
			position = pos;
			cameraYawRotation = orientation.X;
			cameraArcRotation = orientation.Y;

			targetYawRotation = orientation.X;
			targetArcRotation = orientation.Y;
		}

		/// <summary>
		/// Set the camera's matrix transformations
		/// </summary>
		protected override void UpdateMatrices()
		{
			worldMatrix =
				Matrix.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(cameraArcRotation)) *
				Matrix.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(cameraYawRotation));
			view = Matrix.CreateLookAt(position, position + worldMatrix.Forward, worldMatrix.Up);

			frustum.Matrix = view * projection;
		}

		/// Default position to keep the mouse pointer centered

		Vector2 lastMousePos = new Vector2(640, 360);

		public override void Update(GameTime gameTime)
		{
			float time = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			HandleControls(gameTime);
			UpdateMatrices();
		}

		/// <summary>
		/// Allows the game component to update itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		private void HandleControls(GameTime gameTime)
		{
			currentKeyboardState = Keyboard.GetState();
			currentGamePadState = GamePad.GetState(PlayerIndex.One);

			float time = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			MouseState mouseState = Mouse.GetState();

			// Check right mouse button for a speed boost
			float selectedMoveSpeed = (mouseState.RightButton == ButtonState.Pressed) ? 
				moveSpeed * 8f : moveSpeed;

			if (mouseState.LeftButton == ButtonState.Pressed)
			{
				if (mouseLeftHeld == false)
				{
					// Don't move camera for the first click, so mouse
					// position could be set first
					mouseLeftHeld = true;

					lastMousePos.X = mouseState.X;
					lastMousePos.Y = mouseState.Y;
				}
				else
				{
					targetYawRotation += (float)(lastMousePos.X - mouseState.X) * time / 30f;
					targetArcRotation += (float)(lastMousePos.Y - mouseState.Y) * time / 30f;
				}

				lastMousePos.X = mouseState.X;
				lastMousePos.Y = mouseState.Y;

				// Reset mouse position
				Mouse.SetPosition((int)lastMousePos.X, (int)lastMousePos.Y);
			}
			else
			{
				mouseLeftHeld = false;
			}

			// Check for input to move the camera forward and back
			if (currentKeyboardState.IsKeyDown(Keys.W))
			{
				position += worldMatrix.Forward * time * selectedMoveSpeed;
			}

			if (currentKeyboardState.IsKeyDown(Keys.S))
			{
				position -= worldMatrix.Forward * time * selectedMoveSpeed;
			}

			cameraArcRotation += currentGamePadState.ThumbSticks.Right.Y * time;
			cameraArcRotation += targetArcRotation - (cameraArcRotation / smoothing);

			// Limit the arc movement.
			if (targetArcRotation > 90.0f)
				targetArcRotation = 90.0f;
			else if (targetArcRotation < -90.0f)
				targetArcRotation = -90.0f;

			// Check for input to move the camera sideways
			if (currentKeyboardState.IsKeyDown(Keys.D))
			{
				position += worldMatrix.Right * time * selectedMoveSpeed;
			}

			if (currentKeyboardState.IsKeyDown(Keys.A))
			{
				position += worldMatrix.Left * time * selectedMoveSpeed;
			}

			cameraYawRotation += currentGamePadState.ThumbSticks.Right.X * time;
			cameraYawRotation += targetYawRotation - (cameraYawRotation / smoothing);		
		}
	}
}