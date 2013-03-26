using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Meteor.Resources
{
	public class RenderStats
	{
		/// Framerate measuring
		private float frameCounter;
		public float frameRate;
		public long totalFrames;

		/// Measure how much time since past update
		private TimeSpan elapsedTime;

		/// Specific time to update at certain intervals
		private TimeSpan frameStepTime;

		/// Timer to track rendering time
		private Stopwatch gpuWatch;
		private double gpuTime;

		/// Total number of triangles rendered
		public int totalTriangles { private set; get; }

		/// Number of visible lights drawn
		public int totalLights { private set; get; }

		/// Number of visible meshes (as instances) drawn
		public int visibleMeshes { private set; get; }

		public double GpuTime
		{
			get { return gpuTime; }
		}

		/// <summary>
		/// Create a new render stopwatch
		/// </summary>

		public RenderStats()
		{
			gpuWatch = new Stopwatch();
		}

		/// <summary>
		/// Update the frames per second counter.
		/// </summary>
		/// <param name="gameTime"></param>

		public void Update(GameTime gameTime)
		{
			gpuWatch.Stop();
			gpuTime = gpuWatch.Elapsed.TotalMilliseconds;

			// Measure our framerate every half second
			elapsedTime += gameTime.ElapsedGameTime;
			frameStepTime += gameTime.TotalGameTime;

			if (elapsedTime > TimeSpan.FromSeconds(0.5))
			{
				elapsedTime -= TimeSpan.FromSeconds(0.5);
				frameCounter = 0;
				frameRate = (float)(1000 / gameTime.ElapsedGameTime.TotalMilliseconds);
			}
		}

		/// <summary>
		/// Collect rendering statistics for a scene
		/// </summary>

		public void SceneStats(Scene scene)
		{
			totalTriangles += scene.totalPolys;
			totalLights += scene.totalLights;
			visibleMeshes += scene.visibleMeshes;
		}

		/// <summary>
		/// Restart the counter
		/// </summary>

		public void Finish()
		{
			frameCounter++;
            totalFrames++;

			// Reset geometry stats
			totalTriangles = 0;
			totalLights = 0;
			visibleMeshes = 0;

			gpuWatch.Reset();
			gpuWatch.Restart();
		}
	}
}