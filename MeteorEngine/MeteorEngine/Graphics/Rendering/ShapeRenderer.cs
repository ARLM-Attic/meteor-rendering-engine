
//-----------------------------------------------------------------------------
// DebugShapeRenderer.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
	/// <summary>
	/// A system for handling rendering of various debug shapes.
	/// </summary>
	/// <remarks>
	/// The DebugShapeRenderer allows for rendering line-base shapes in a batched fashion. Games
	/// will call one of the many Add* methods to add a shape to the renderer and then a call to
	/// Draw will cause all shapes to be rendered. This mechanism was chosen because it allows
	/// game code to call the Add* methods wherever is most convenient, rather than having to
	/// add draw methods to all of the necessary objects.
	/// 
	/// Additionally the renderer supports a lifetime for all shapes added. This allows for things
	/// like visualization of raycast bullets. The game would call the AddLine overload with the
	/// lifetime parameter and pass in a positive value. The renderer will then draw that shape
	/// for the given amount of time without any more calls to AddLine being required.
	/// 
	/// The renderer's batching mechanism uses a cache system to avoid garbage and also draws as
	/// many lines in one call to DrawUserPrimitives as possible. If the renderer is trying to draw
	/// more lines than are allowed in the Reach profile, it will break them up into multiple draw
	/// calls to make sure the game continues to work for any game.</remarks>
	public static class ShapeRenderer
	{
		// A single shape in our debug renderer
		class DebugShape
		{
			/// <summary>
			/// The array of vertices the shape can use.
			/// </summary>
			public VertexPositionColor[] Vertices;

			/// <summary>
			/// The number of lines to draw for this shape.
			/// </summary>
			public int LineCount;

			/// <summary>
			/// The length of time to keep this shape visible.
			/// </summary>
			public float Lifetime;
		}

		// We use a cache system to reuse our DebugShape instances to avoid creating garbage
		private static readonly List<DebugShape> cachedShapes = new List<DebugShape>();
		private static readonly List<DebugShape> activeShapes = new List<DebugShape>();

		// Allocate an array to hold our vertices; this will grow as needed by our renderer
		private static VertexPositionColor[] verts = new VertexPositionColor[64];

		// Our graphics device and the effect we use to render the shapes
		private static GraphicsDevice graphics;
		private static BasicEffect effect;

		// An array we use to get corners from frustums and bounding boxes
		private static Vector3[] corners = new Vector3[8];

		// This holds the vertices for our unit sphere that we will use when drawing bounding spheres
		private const int sphereResolution = 40;
		private const int sphereLineCount = (sphereResolution + 1) * 3;
		private static Vector3[] unitSphere;

		/// <summary>
		/// Initializes the renderer.
		/// </summary>
		/// <param name="graphicsDevice">The GraphicsDevice to use for rendering.</param>
		[Conditional("DEBUG")]
		public static void Initialize(GraphicsDevice graphicsDevice)
		{
			// Save the graphics device
			graphics = graphicsDevice;

			// Create and initialize our effect
			effect = new BasicEffect(graphicsDevice);
			effect.VertexColorEnabled = true;
			effect.TextureEnabled = false;
			effect.DiffuseColor = Vector3.One;
			effect.World = Matrix.Identity;

			// Create our unit sphere vertices
			InitializeSphere();
		}

		/// <summary>
		/// Adds a line to be rendered for just one frame.
		/// </summary>
		/// <param name="a">The first point of the line.</param>
		/// <param name="b">The second point of the line.</param>
		/// <param name="color">The color in which to draw the line.</param>
		[Conditional("DEBUG")]
		public static void AddLine(Vector3 a, Vector3 b, Color color)
		{
			AddLine(a, b, color, 0f);
		}

		/// <summary>
		/// Adds a line to be rendered for a set amount of time.
		/// </summary>
		/// <param name="a">The first point of the line.</param>
		/// <param name="b">The second point of the line.</param>
		/// <param name="color">The color in which to draw the line.</param>
		/// <param name="life">The amount of time, in seconds, to keep rendering the line.</param>
		[Conditional("DEBUG")]
		public static void AddLine(Vector3 a, Vector3 b, Color color, float life)
		{
			// Get a DebugShape we can use to draw the line
			DebugShape shape = GetShapeForLines(1, life);

			// Add the two vertices to the shape
			shape.Vertices[0] = new VertexPositionColor(a, color);
			shape.Vertices[1] = new VertexPositionColor(b, color);
		}

		/// <summary>
		/// Adds a triangle to be rendered for just one frame.
		/// </summary>
		/// <param name="a">The first vertex of the triangle.</param>
		/// <param name="b">The second vertex of the triangle.</param>
		/// <param name="c">The third vertex of the triangle.</param>
		/// <param name="color">The color in which to draw the triangle.</param>
		[Conditional("DEBUG")]
		public static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
		{
			AddTriangle(a, b, c, color, 0f);
		}

		/// <summary>
		/// Adds a triangle to be rendered for a set amount of time.
		/// </summary>
		/// <param name="a">The first vertex of the triangle.</param>
		/// <param name="b">The second vertex of the triangle.</param>
		/// <param name="c">The third vertex of the triangle.</param>
		/// <param name="color">The color in which to draw the triangle.</param>
		/// <param name="life">The amount of time, in seconds, to keep rendering the triangle.</param>
		[Conditional("DEBUG")]
		public static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color, float life)
		{
			// Get a DebugShape we can use to draw the triangle
			DebugShape shape = GetShapeForLines(3, life);

			// Add the vertices to the shape
			shape.Vertices[0] = new VertexPositionColor(a, color);
			shape.Vertices[1] = new VertexPositionColor(b, color);
			shape.Vertices[2] = new VertexPositionColor(b, color);
			shape.Vertices[3] = new VertexPositionColor(c, color);
			shape.Vertices[4] = new VertexPositionColor(c, color);
			shape.Vertices[5] = new VertexPositionColor(a, color);
		}

		/// <summary>
		/// Adds a frustum to be rendered for just one frame.
		/// </summary>
		/// <param name="frustum">The frustum to render.</param>
		/// <param name="color">The color in which to draw the frustum.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingFrustum(BoundingFrustum frustum, Color color)
		{
			AddBoundingFrustum(frustum, color, 0f);
		}

		/// <summary>
		/// Adds a frustum to be rendered for a set amount of time.
		/// </summary>
		/// <param name="frustum">The frustum to render.</param>
		/// <param name="color">The color in which to draw the frustum.</param>
		/// <param name="life">The amount of time, in seconds, to keep rendering the frustum.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingFrustum(BoundingFrustum frustum, Color color, float life)
		{
			// Get a DebugShape we can use to draw the frustum
			DebugShape shape = GetShapeForLines(12, life);

			// Get the corners of the frustum
			frustum.GetCorners(corners);

			// Fill in the vertices for the bottom of the frustum
			shape.Vertices[0] = new VertexPositionColor(corners[0], color);
			shape.Vertices[1] = new VertexPositionColor(corners[1], color);
			shape.Vertices[2] = new VertexPositionColor(corners[1], color);
			shape.Vertices[3] = new VertexPositionColor(corners[2], color);
			shape.Vertices[4] = new VertexPositionColor(corners[2], color);
			shape.Vertices[5] = new VertexPositionColor(corners[3], color);
			shape.Vertices[6] = new VertexPositionColor(corners[3], color);
			shape.Vertices[7] = new VertexPositionColor(corners[0], color);

			// Fill in the vertices for the top of the frustum
			shape.Vertices[8] = new VertexPositionColor(corners[4], color);
			shape.Vertices[9] = new VertexPositionColor(corners[5], color);
			shape.Vertices[10] = new VertexPositionColor(corners[5], color);
			shape.Vertices[11] = new VertexPositionColor(corners[6], color);
			shape.Vertices[12] = new VertexPositionColor(corners[6], color);
			shape.Vertices[13] = new VertexPositionColor(corners[7], color);
			shape.Vertices[14] = new VertexPositionColor(corners[7], color);
			shape.Vertices[15] = new VertexPositionColor(corners[4], color);

			// Fill in the vertices for the vertical sides of the frustum
			shape.Vertices[16] = new VertexPositionColor(corners[0], color);
			shape.Vertices[17] = new VertexPositionColor(corners[4], color);
			shape.Vertices[18] = new VertexPositionColor(corners[1], color);
			shape.Vertices[19] = new VertexPositionColor(corners[5], color);
			shape.Vertices[20] = new VertexPositionColor(corners[2], color);
			shape.Vertices[21] = new VertexPositionColor(corners[6], color);
			shape.Vertices[22] = new VertexPositionColor(corners[3], color);
			shape.Vertices[23] = new VertexPositionColor(corners[7], color);
		}

		/// <summary>
		/// Adds a bounding box to be rendered for just one frame.
		/// </summary>
		/// <param name="box">The bounding box to render.</param>
		/// <param name="color">The color in which to draw the bounding box.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingBox(BoundingBox box, Color color)
		{
			AddBoundingBox(box, color, 0f);
		}

		/// <summary>
		/// Adds a bounding box to be rendered for a set amount of time.
		/// </summary>
		/// <param name="box">The bounding box to render.</param>
		/// <param name="color">The color in which to draw the bounding box.</param>
		/// <param name="life">The amount of time, in seconds, to keep rendering the bounding box.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingBox(BoundingBox box, Color color, float life)
		{
			// Get a DebugShape we can use to draw the box
			DebugShape shape = GetShapeForLines(12, life);

			// Get the corners of the box
			box.GetCorners(corners);

			// Fill in the vertices for the bottom of the box
			shape.Vertices[0] = new VertexPositionColor(corners[0], color);
			shape.Vertices[1] = new VertexPositionColor(corners[1], color);
			shape.Vertices[2] = new VertexPositionColor(corners[1], color);
			shape.Vertices[3] = new VertexPositionColor(corners[2], color);
			shape.Vertices[4] = new VertexPositionColor(corners[2], color);
			shape.Vertices[5] = new VertexPositionColor(corners[3], color);
			shape.Vertices[6] = new VertexPositionColor(corners[3], color);
			shape.Vertices[7] = new VertexPositionColor(corners[0], color);

			// Fill in the vertices for the top of the box
			shape.Vertices[8] = new VertexPositionColor(corners[4], color);
			shape.Vertices[9] = new VertexPositionColor(corners[5], color);
			shape.Vertices[10] = new VertexPositionColor(corners[5], color);
			shape.Vertices[11] = new VertexPositionColor(corners[6], color);
			shape.Vertices[12] = new VertexPositionColor(corners[6], color);
			shape.Vertices[13] = new VertexPositionColor(corners[7], color);
			shape.Vertices[14] = new VertexPositionColor(corners[7], color);
			shape.Vertices[15] = new VertexPositionColor(corners[4], color);

			// Fill in the vertices for the vertical sides of the box
			shape.Vertices[16] = new VertexPositionColor(corners[0], color);
			shape.Vertices[17] = new VertexPositionColor(corners[4], color);
			shape.Vertices[18] = new VertexPositionColor(corners[1], color);
			shape.Vertices[19] = new VertexPositionColor(corners[5], color);
			shape.Vertices[20] = new VertexPositionColor(corners[2], color);
			shape.Vertices[21] = new VertexPositionColor(corners[6], color);
			shape.Vertices[22] = new VertexPositionColor(corners[3], color);
			shape.Vertices[23] = new VertexPositionColor(corners[7], color);
		}

		/// <summary>
		/// Adds a bounding sphere to be rendered for just one frame.
		/// </summary>
		/// <param name="sphere">The bounding sphere to render.</param>
		/// <param name="color">The color in which to draw the bounding sphere.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingSphere(BoundingSphere sphere, Color color)
		{
			AddBoundingSphere(sphere, color, 0f);
		}

		/// <summary>
		/// Adds a bounding sphere to be rendered for a set amount of time.
		/// </summary>
		/// <param name="sphere">The bounding sphere to render.</param>
		/// <param name="color">The color in which to draw the bounding sphere.</param>
		/// <param name="life">The amount of time, in seconds, to keep rendering the bounding sphere.</param>
		[Conditional("DEBUG")]
		public static void AddBoundingSphere(BoundingSphere sphere, Color color, float life)
		{
			// Get a DebugShape we can use to draw the sphere
			DebugShape shape = GetShapeForLines(sphereLineCount, life);

			// Iterate our unit sphere vertices
			for (int i = 0; i < unitSphere.Length; i++)
			{
				// Compute the vertex position by transforming the point by the radius and center of the sphere
				Vector3 vertPos = unitSphere[i] * sphere.Radius + sphere.Center;

				// Add the vertex to the shape
				color = (i > 80) ? Color.Red : color;
				color = (i > 160) ? Color.Blue : color;
				shape.Vertices[i] = new VertexPositionColor(vertPos, color);
			}
		}

		/// <summary>
		/// Draws the shapes that were added to the renderer and are still alive.
		/// </summary>
		/// <param name="gameTime">The current game timestamp.</param>
		/// <param name="view">The view matrix to use when rendering the shapes.</param>
		/// <param name="projection">The projection matrix to use when rendering the shapes.</param>
		[Conditional("DEBUG")]
		public static void Draw(Matrix view, Matrix projection)
		{
			// Update our effect with the matrices.
			effect.View = view;
			effect.Projection = projection;

			// Calculate the total number of vertices we're going to be rendering.
			int vertexCount = 0;
			foreach (var shape in activeShapes)
				vertexCount += shape.LineCount * 2;

			// If we have some vertices to draw
			if (vertexCount > 0)
			{
				// Make sure our array is large enough
				if (verts.Length < vertexCount)
				{
					// If we have to resize, we make our array twice as large as necessary so
					// we hopefully won't have to resize it for a while.
					verts = new VertexPositionColor[vertexCount * 2];
				}

				// Now go through the shapes again to move the vertices to our array and
				// add up the number of lines to draw.
				int lineCount = 0;
				int vertIndex = 0;
				foreach (DebugShape shape in activeShapes)
				{
					lineCount += shape.LineCount;
					int shapeVerts = shape.LineCount * 2;
					for (int i = 0; i < shapeVerts; i++)
						verts[vertIndex++] = shape.Vertices[i];
				}

				// Start our effect to begin rendering.
				effect.CurrentTechnique.Passes[0].Apply();

				// We draw in a loop because the Reach profile only supports 65,535 primitives. While it's
				// not incredibly likely, if a game tries to render more than 65,535 lines we don't want to
				// crash. We handle this by doing a loop and drawing as many lines as we can at a time, capped
				// at our limit. We then move ahead in our vertex array and draw the next set of lines.
				int vertexOffset = 0;
				while (lineCount > 0)
				{
					// Figure out how many lines we're going to draw
					int linesToDraw = Math.Min(lineCount, 65535 * 256);

					// Draw the lines
					graphics.DrawUserPrimitives(PrimitiveType.LineList, verts, vertexOffset, linesToDraw);

					// Move our vertex offset ahead based on the lines we drew
					vertexOffset += linesToDraw * 2;

					// Remove these lines from our total line count
					lineCount -= linesToDraw;
				}
			}

			// Go through our active shapes and retire any shapes that have expired to the
			// cache list. 
			bool resort = false;
			for (int i = activeShapes.Count - 1; i >= 0; i--)
			{
				DebugShape s = activeShapes[i];
				s.Lifetime -= 0.0004f;//(float)gameTime.ElapsedGameTime.TotalSeconds;
				if (s.Lifetime <= 0)
				{
					cachedShapes.Add(s);
					activeShapes.RemoveAt(i);
					resort = true;
				}
			}

			// If we move any shapes around, we need to resort the cached list
			// to ensure that the smallest shapes are first in the list.
			if (resort)
				cachedShapes.Sort(CachedShapesSort);
		}

		/// <summary>
		/// Make a DummyBox. Useful for occlusion queries and other tests.
		/// </summary>
		/// <param name="corners"></param>
		public static VertexBuffer AddDummyBox()
		{
			VertexPositionNormalTexture[] boxVertices = new VertexPositionNormalTexture[36];

			// Calculate the position of the vertices on the top face.
			Vector3 topLeftFront = new Vector3(-1.0f, 1.0f, -1.0f);
			Vector3 topLeftBack = new Vector3(-1.0f, 1.0f, 1.0f);
			Vector3 topRightFront = new Vector3(1.0f, 1.0f, -1.0f);
			Vector3 topRightBack = new Vector3(1.0f, 1.0f, 1.0f);

			// Calculate the position of the vertices on the bottom face.
			Vector3 btmLeftFront = new Vector3(-1.0f, -1.0f, -1.0f);
			Vector3 btmLeftBack = new Vector3(-1.0f, -1.0f, 1.0f);
			Vector3 btmRightFront = new Vector3(1.0f, -1.0f, -1.0f);
			Vector3 btmRightBack = new Vector3(1.0f, -1.0f, 1.0f);

			// Normal vectors for each face (needed for lighting / display)
			Vector3 normalFront = new Vector3(0.0f, 0.0f, 1.0f);
			Vector3 normalBack = new Vector3(0.0f, 0.0f, -1.0f);
			Vector3 normalTop = new Vector3(0.0f, 1.0f, 0.0f);
			Vector3 normalBottom = new Vector3(0.0f, -1.0f, 0.0f);
			Vector3 normalLeft = new Vector3(-1.0f, 0.0f, 0.0f);
			Vector3 normalRight = new Vector3(1.0f, 0.0f, 0.0f);

			// UV texture coordinates
			Vector2 textureTopLeft = new Vector2(1.0f, 0.0f);
			Vector2 textureTopRight = new Vector2(0.0f, 0.0f);
			Vector2 textureBottomLeft = new Vector2(1.0f, 1.0f);
			Vector2 textureBottomRight = new Vector2(0.0f, 1.0f);

			// Add the vertices for the front face.
			boxVertices[0] = new VertexPositionNormalTexture(topLeftFront, normalFront, textureTopLeft);
			boxVertices[1] = new VertexPositionNormalTexture(btmLeftFront, normalFront, textureBottomLeft);
			boxVertices[2] = new VertexPositionNormalTexture(topRightFront, normalFront, textureTopRight);
			boxVertices[3] = new VertexPositionNormalTexture(btmLeftFront, normalFront, textureBottomLeft);
			boxVertices[4] = new VertexPositionNormalTexture(btmRightFront, normalFront, textureBottomRight);
			boxVertices[5] = new VertexPositionNormalTexture(topRightFront, normalFront, textureTopRight);

			// Add the vertices for the back face.
			boxVertices[6] = new VertexPositionNormalTexture(topLeftBack, normalBack, textureTopRight);
			boxVertices[7] = new VertexPositionNormalTexture(topRightBack, normalBack, textureTopLeft);
			boxVertices[8] = new VertexPositionNormalTexture(btmLeftBack, normalBack, textureBottomRight);
			boxVertices[9] = new VertexPositionNormalTexture(btmLeftBack, normalBack, textureBottomRight);
			boxVertices[10] = new VertexPositionNormalTexture(topRightBack, normalBack, textureTopLeft);
			boxVertices[11] = new VertexPositionNormalTexture(btmRightBack, normalBack, textureBottomLeft);

			// Add the vertices for the top face.
			boxVertices[12] = new VertexPositionNormalTexture(topLeftFront, normalTop, textureBottomLeft);
			boxVertices[13] = new VertexPositionNormalTexture(topRightBack, normalTop, textureTopRight);
			boxVertices[14] = new VertexPositionNormalTexture(topLeftBack, normalTop, textureTopLeft);
			boxVertices[15] = new VertexPositionNormalTexture(topLeftFront, normalTop, textureBottomLeft);
			boxVertices[16] = new VertexPositionNormalTexture(topRightFront, normalTop, textureBottomRight);
			boxVertices[17] = new VertexPositionNormalTexture(topRightBack, normalTop, textureTopRight);

			// Add the vertices for the bottom face. 
			boxVertices[18] = new VertexPositionNormalTexture(btmLeftFront, normalBottom, textureTopLeft);
			boxVertices[19] = new VertexPositionNormalTexture(btmLeftBack, normalBottom, textureBottomLeft);
			boxVertices[20] = new VertexPositionNormalTexture(btmRightBack, normalBottom, textureBottomRight);
			boxVertices[21] = new VertexPositionNormalTexture(btmLeftFront, normalBottom, textureTopLeft);
			boxVertices[22] = new VertexPositionNormalTexture(btmRightBack, normalBottom, textureBottomRight);
			boxVertices[23] = new VertexPositionNormalTexture(btmRightFront, normalBottom, textureTopRight);

			// Add the vertices for the left face.
			boxVertices[24] = new VertexPositionNormalTexture(topLeftFront, normalLeft, textureTopRight);
			boxVertices[25] = new VertexPositionNormalTexture(btmLeftBack, normalLeft, textureBottomLeft);
			boxVertices[26] = new VertexPositionNormalTexture(btmLeftFront, normalLeft, textureBottomRight);
			boxVertices[27] = new VertexPositionNormalTexture(topLeftBack, normalLeft, textureTopLeft);
			boxVertices[28] = new VertexPositionNormalTexture(btmLeftBack, normalLeft, textureBottomLeft);
			boxVertices[29] = new VertexPositionNormalTexture(topLeftFront, normalLeft, textureTopRight);

			// Add the vertices for the right face. 
			boxVertices[30] = new VertexPositionNormalTexture(topRightFront, normalRight, textureTopLeft);
			boxVertices[31] = new VertexPositionNormalTexture(btmRightFront, normalRight, textureBottomLeft);
			boxVertices[32] = new VertexPositionNormalTexture(btmRightBack, normalRight, textureBottomRight);
			boxVertices[33] = new VertexPositionNormalTexture(topRightBack, normalRight, textureTopRight);
			boxVertices[34] = new VertexPositionNormalTexture(topRightFront, normalRight, textureTopLeft);
			boxVertices[35] = new VertexPositionNormalTexture(btmRightBack, normalRight, textureBottomRight);

			VertexBuffer boxVB = new VertexBuffer(graphics, typeof(VertexPositionNormalTexture), boxVertices.Length,
				BufferUsage.WriteOnly);
			boxVB.SetData(boxVertices);

			return boxVB;
		}

		/// <summary>
		/// Creates the unitSphere array of vertices.
		/// </summary>
		private static void InitializeSphere()
		{
			// We need two vertices per line, so we can allocate our vertices
			unitSphere = new Vector3[sphereLineCount * 2];

			// Compute our step around each circle
			float step = MathHelper.TwoPi / sphereResolution;

			// Used to track the index into our vertex array
			int index = 0;

			// Create the loop on the XY plane first
			for (float a = 0f; a < MathHelper.TwoPi; a += step)
			{
				unitSphere[index++] = new Vector3((float)Math.Cos(a), (float)Math.Sin(a), 0f);
				unitSphere[index++] = new Vector3((float)Math.Cos(a + step), (float)Math.Sin(a + step), 0f);
			}

			// Next on the XZ plane
			for (float a = 0f; a < MathHelper.TwoPi; a += step)
			{
				unitSphere[index++] = new Vector3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
				unitSphere[index++] = new Vector3((float)Math.Cos(a + step), 0f, (float)Math.Sin(a + step));
			}

			// Finally on the YZ plane
			for (float a = 0f; a < MathHelper.TwoPi; a += step)
			{
				unitSphere[index++] = new Vector3(0f, (float)Math.Cos(a), (float)Math.Sin(a));
				unitSphere[index++] = new Vector3(0f, (float)Math.Cos(a + step), (float)Math.Sin(a + step));
			}
		}

		/// <summary>
		/// A method used for sorting our cached shapes based on the size of their vertex arrays.
		/// </summary>
		private static int CachedShapesSort(DebugShape s1, DebugShape s2)
		{
			return s1.Vertices.Length.CompareTo(s2.Vertices.Length);
		}

		/// <summary>
		/// Gets a DebugShape instance for a given line counta and lifespan.
		/// </summary>
		private static DebugShape GetShapeForLines(int lineCount, float life)
		{
			DebugShape shape = null;

			// We go through our cached list trying to find a shape that contains
			// a large enough array to hold our desired line count. If we find such
			// a shape, we move it from our cached list to our active list and break
			// out of the loop.
			int vertCount = lineCount * 2;
			for (int i = 0; i < cachedShapes.Count; i++)
			{
				if (cachedShapes[i].Vertices.Length >= vertCount)
				{
					shape = cachedShapes[i];
					cachedShapes.RemoveAt(i);
					activeShapes.Add(shape);
					break;
				}
			}

			// If we didn't find a shape in our cache, we create a new shape and add it
			// to the active list.
			if (shape == null)
			{
				shape = new DebugShape { Vertices = new VertexPositionColor[vertCount] };
				activeShapes.Add(shape);
			}

			// Set the line count and lifetime of the shape based on our parameters.
			shape.LineCount = lineCount;
			shape.Lifetime = life;

			return shape;
		}
	}
}
