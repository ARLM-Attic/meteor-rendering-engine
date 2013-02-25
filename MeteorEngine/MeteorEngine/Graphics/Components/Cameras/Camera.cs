using System;
using Microsoft.Xna.Framework;

namespace Meteor.Resources
{
    /// <summary>
    /// Controllable camera class
    /// </summary>
    public class Camera
    {
		protected float cameraArc = 0;
		protected float targetArc = 0;

        public float CameraArc
        {
            get { return cameraArc; }
        }

		protected float cameraRotation = -90;
		protected float targetRotation = -90;

        public float CameraRotation
        {
            get { return cameraRotation; }
        }

		protected Matrix worldMatrix;
		public Matrix WorldMatrix
		{
			get { return worldMatrix; }
		}

		/// Camera's view matrix
		public Matrix view;

		/// Camera's projecition matrix
		public Matrix projection;

		/// Camera position
		public Vector3 position;

		/// Bounding volume of view frustum
		public BoundingFrustum frustum;

		/// Corners of bounding frustum
		public Vector3[] frustumCorners;

		/// X and Y aspect
		public Vector2 viewAspect;

		/// Camera field of view
		public float viewAngle;

		/// H/V aspect ratio
		public float AspectRatio
		{
			get { return (float)viewAspect.X / (float)viewAspect.Y; }
		}

        public float nearPlaneDistance = 2.5f;
        public float farPlaneDistance = 8000f;
		public float nearSplitPlaneDistance;
		public float farSplitPlaneDistance;

		/// <summary>
		/// Return frustum split info based on cascaded shadow mapping.
		/// Split distances can be interpolated between linear and logarithmic distance 
		/// depending on the lambda coefficient. 
		/// 
		/// The limit scales back by how far shadows will be rendered. This is useful for
		/// better looking shadows at shorter distances.
		/// </summary>

		public Vector2 GetFrustumSplit(int split, int numSplits, float lambda = 0.25f)
		{
			split = (split > numSplits) ? numSplits : split;

			float farDistance = farPlaneDistance;

			// CLi = n*(f/n)^(i/numsplits)
			// CUi = n + (f-n)*(i/numsplits)
			// Ci = CLi*(lambda) + CUi*(1-lambda)

			float fLog = nearPlaneDistance *
				(float)Math.Pow((farDistance / nearPlaneDistance), (split + 1) / (float)numSplits);
			float fLinear = nearPlaneDistance + (farDistance - nearPlaneDistance) * 
				((split + 1) / (float)numSplits);

			// make sure border values are right
			farSplitPlaneDistance = fLog * lambda + fLinear * (1 - lambda);

			fLog = nearPlaneDistance *
				(float)Math.Pow((farDistance / nearPlaneDistance), split / (float)numSplits);
			fLinear = nearPlaneDistance + (farDistance - nearPlaneDistance) *
				(split / (float)numSplits);

			nearSplitPlaneDistance = fLog * lambda + fLinear * (1 - lambda);
			if (split > 0)
				nearSplitPlaneDistance *= 0.8f;

			//* ((split + 1) * (split + 1))
			return new Vector2(nearSplitPlaneDistance, farSplitPlaneDistance);
		}

		/// <summary>
		/// Default camera constructor with default position
		/// </summary>

        public Camera()
        {
            position.Y = 4f;

			nearSplitPlaneDistance = nearPlaneDistance;
			farSplitPlaneDistance = farPlaneDistance;

			frustum = new BoundingFrustum(Matrix.Identity);
        }

		/// <summary>
		/// Camera constructor with a given position and lookAt location.
		/// </summary>

		public Camera(Vector3 pos, Vector2 orientation)
		{
			position = pos;

			cameraRotation = orientation.X;
			cameraArc = orientation.Y;

			targetRotation = orientation.X; // yaw
			targetArc = orientation.Y; // pitch

			nearSplitPlaneDistance = nearPlaneDistance;
			farSplitPlaneDistance = farPlaneDistance;
		}

        /// <summary>
        /// Sets up the camera with a default viewport and world matrix
        /// </summary>
        public void Initialize(float width, float height)
        {
            // Add your initialization code here
			viewAspect.X = (int)width;
			viewAspect.Y = (int)height;
			viewAngle = MathHelper.PiOver4 * 4f / 3f;

			frustum = new BoundingFrustum(Matrix.Identity);
			frustumCorners = new Vector3[8];
			worldMatrix = Matrix.Identity;

			float aspectRatio = (float)viewAspect.X / (float)viewAspect.Y;
			projection = Matrix.CreatePerspectiveFieldOfView(
				viewAngle, aspectRatio, nearPlaneDistance, farPlaneDistance);

			UpdateMatrices();
        }

        /// <summary>
        /// Update the near and far clipping planes of the viewport
        /// </summary>
		public void UpdateNearFar(Vector2 clipPlanes)
        {
            nearPlaneDistance = clipPlanes.X;
            farPlaneDistance = clipPlanes.Y;

			float aspectRatio = (float)viewAspect.X / (float)viewAspect.Y;
			projection = Matrix.CreatePerspectiveFieldOfView(
				viewAngle, aspectRatio, nearPlaneDistance, farPlaneDistance);

			UpdateMatrices();
        }

		/// <summary>
		/// Set Euler angle-based orientation
		/// </summary>
		public void SetOrientation(Vector2 orientation)
		{
			targetRotation = orientation.X; // yaw
			targetArc = orientation.Y; // pitch			
		}

		public virtual void Update(GameTime gameTime = null)
		{
			UpdateMatrices();
		}

		/// <summary>
		/// Pass matrices from an external source
		/// </summary>
		public virtual void SetMatrices(Matrix world, Matrix view, Matrix projection)
		{
			this.worldMatrix = world;
			this.view = view;
			this.projection = projection;

			UpdateMatrices();
		}

        /// <summary>
        /// Set the camera's matrix transformations
        /// </summary>
		protected virtual void UpdateMatrices()
        {
			frustum.Matrix = view * projection;
        }
	}

	/// <summary>
	/// Gets an array of points for a camera's BoundingFrustum.
	/// </summary>

	public static partial class BoundingFrustumExtention
	{
		public static Vector3[] GetCorners(this BoundingFrustum frustum, Camera camera)
		{
			// Replace nearSplitPlaneDistance with nearPlaneDistance for
			// traditional cascaded shadow maps

			// Calculate the near and far plane centers
			Vector3 nearPlaneCenter = camera.position +
				Vector3.Normalize(camera.WorldMatrix.Forward) * camera.nearSplitPlaneDistance;
			Vector3 farPlaneCenter = camera.position +
				Vector3.Normalize(camera.WorldMatrix.Forward) * camera.farSplitPlaneDistance;

			// Get the vertical and horizontal extent locations from the center
			float nearExtentDistance = (float)Math.Tan(camera.viewAngle / 2f) * camera.nearSplitPlaneDistance;
			Vector3 nearExtentY = nearExtentDistance * camera.WorldMatrix.Up;
			Vector3 nearExtentX = nearExtentDistance * camera.AspectRatio * camera.WorldMatrix.Left;

			float farExtentDistance = (float)Math.Tan(camera.viewAngle / 2f) * camera.farSplitPlaneDistance;
			Vector3 farExtentY = farExtentDistance * camera.WorldMatrix.Up;
			Vector3 farExtentX = farExtentDistance * camera.AspectRatio * camera.WorldMatrix.Left;

			// Calculate the frustum corners by adding/subtracting the extents
			// Starting clockwise and from the near plane first
			camera.frustumCorners[0] = nearPlaneCenter + nearExtentY - nearExtentX;
			camera.frustumCorners[1] = nearPlaneCenter + nearExtentY + nearExtentX; // min
			camera.frustumCorners[2] = nearPlaneCenter - nearExtentY + nearExtentX;
			camera.frustumCorners[3] = nearPlaneCenter - nearExtentY - nearExtentX;

			camera.frustumCorners[4] = farPlaneCenter + farExtentY - farExtentX;
			camera.frustumCorners[5] = farPlaneCenter + farExtentY + farExtentX;
			camera.frustumCorners[6] = farPlaneCenter - farExtentY + farExtentX;
			camera.frustumCorners[7] = farPlaneCenter - farExtentY - farExtentX; // max

			return camera.frustumCorners;
		}
	}
}


