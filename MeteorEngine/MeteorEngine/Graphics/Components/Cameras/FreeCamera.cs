using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Meteor.Resources
{
	/// <summary>
	/// Controllable camera class
	/// </summary>
	public class FreeCamera : Camera
	{
		/// <summary>
		/// Adjust smoothing to create a more fluid moving camera.
		/// Too much smoothing will cause a disorienting feel.
		/// </summary>
		float smoothing = 3.5f;

		float moveSpeed = 0.25f;

		KeyboardState currentKeyboardState = new KeyboardState();
		GamePadState currentGamePadState = new GamePadState();

		public FreeCamera()
		{
			position.Y = 4f;
		}

		public FreeCamera(Vector3 pos, Vector2 orientation)
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

			targetYawRotation += (float)(lastMousePos.X - mouseState.X) * time / 120f;
			targetArcRotation += (float)(lastMousePos.Y - mouseState.Y) * time / 120f;

			// Reset mouse position
			if (new Vector2(mouseState.X, mouseState.Y) != lastMousePos)
				Mouse.SetPosition((int)lastMousePos.X, (int)lastMousePos.Y);

			// Check for input to move the camera forward and back
			if (currentKeyboardState.IsKeyDown(Keys.W))
			{
				position += worldMatrix.Forward * time * moveSpeed;
			}

			if (currentKeyboardState.IsKeyDown(Keys.S))
			{
				position -= worldMatrix.Forward * time * moveSpeed;
			}

			cameraArcRotation += currentGamePadState.ThumbSticks.Right.Y * time * 0.05f;
			cameraArcRotation += targetArcRotation - (cameraArcRotation / smoothing);

			// Limit the arc movement.
			if (targetArcRotation > 90.0f)
				targetArcRotation = 90.0f;
			else if (targetArcRotation < -90.0f)
				targetArcRotation = -90.0f;

			// Check for input to move the camera sideways
			if (currentKeyboardState.IsKeyDown(Keys.D))
			{
				position += worldMatrix.Right * time * moveSpeed;
			}

			if (currentKeyboardState.IsKeyDown(Keys.A))
			{
				position += worldMatrix.Left * time * moveSpeed;
			}

			cameraYawRotation += currentGamePadState.ThumbSticks.Right.X * time * 0.05f;
			cameraYawRotation += targetYawRotation - (cameraYawRotation / smoothing);

			if (currentGamePadState.Buttons.RightStick == ButtonState.Pressed ||
				currentKeyboardState.IsKeyDown(Keys.R))
			{
				cameraArcRotation = -30;
				cameraYawRotation = 0;
			}
			
		}
	}
}