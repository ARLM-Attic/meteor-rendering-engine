
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Meteor.Resources
{
    public class QuadRenderComponent
    {     
		/// Rendering data
        VertexDeclaration vertexDecl = null;
        VertexPositionTexture[] verts = null;
		VertexPositionColor[] coloredVerts = null;

        short[] ib = null;

		/// Graphics device passed to the object at construction
		GraphicsDevice device;

		public VertexBuffer vertexBuffer;
		public IndexBuffer indexBuffer;

        public QuadRenderComponent(GraphicsDevice device)
        {
            this.device = device;
            vertexDecl = VertexPositionTexture.VertexDeclaration;

            verts = new VertexPositionTexture[]
            {
                new VertexPositionTexture(
                    new Vector3(0,0,1),
                    new Vector2(1,1)),
                new VertexPositionTexture(
                    new Vector3(0,0,1),
                    new Vector2(0,1)),
                new VertexPositionTexture(
                    new Vector3(0,0,1),
                    new Vector2(0,0)),
                new VertexPositionTexture(
                    new Vector3(0,0,1),
                    new Vector2(1,0))
            };
			coloredVerts = new VertexPositionColor[4];

            ib = new short[] { 0, 1, 2, 2, 3, 0 };

			// Set the vertex and index buffers

			vertexBuffer = new VertexBuffer(device, 
				typeof(VertexPositionTexture), 4, BufferUsage.None);
			vertexBuffer.SetData<VertexPositionTexture>(verts);

			indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits,
				sizeof(short) * ib.Length, BufferUsage.None);
			indexBuffer.SetData<short>(ib);
        } 

		/// <summary>
		/// Draw the quad with screen space extents
		/// </summary>

        public void Render(Vector2 v1, Vector2 v2)
        {
            verts[0].Position.X = v2.X;
            verts[0].Position.Y = v1.Y;

            verts[1].Position.X = v1.X;
            verts[1].Position.Y = v1.Y;

            verts[2].Position.X = v1.X;
            verts[2].Position.Y = v2.Y;

            verts[3].Position.X = v2.X;
            verts[3].Position.Y = v2.Y;

            device.DrawUserIndexedPrimitives<VertexPositionTexture>
				(PrimitiveType.TriangleList, verts, 0, 4, ib, 0, 2);
        }

		/// <summary>
		/// Draw the quad with depth
		/// </summary>

		public void Render(Vector2 v1, Vector2 v2, float depth)
		{
			verts[0].Position.X = v2.X;
			verts[0].Position.Y = v1.Y;
			verts[0].Position.Z = depth;

			verts[1].Position.X = v1.X;
			verts[1].Position.Y = v1.Y;
			verts[1].Position.Z = depth;

			verts[2].Position.X = v1.X;
			verts[2].Position.Y = v2.Y;
			verts[2].Position.Z = depth;

			verts[3].Position.X = v2.X;
			verts[3].Position.Y = v2.Y;
			verts[3].Position.Z = depth;

			device.DrawUserIndexedPrimitives<VertexPositionTexture>
				(PrimitiveType.TriangleList, verts, 0, 4, ib, 0, 2);
		}

		public void SetVertices(Vector2 v1, Vector2 v2)
		{
#if XNA
			device.SetVertexBuffers(null);
#elif MONOGAME
			device.SetVertexBuffer(null);
#endif
			verts[0].Position.X = v2.X;
			verts[0].Position.Y = v1.Y;

			verts[1].Position.X = v1.X;
			verts[1].Position.Y = v1.Y;

			verts[2].Position.X = v1.X;
			verts[2].Position.Y = v2.Y;

			verts[3].Position.X = v2.X;
			verts[3].Position.Y = v2.Y;

			vertexBuffer.SetData<VertexPositionTexture>(verts);
		}

		/// <summary>
		/// Draw a quad in any orientation
		/// </summary>

		public void Render(Vector3 v1, Vector3 v2)
		{
			verts[0].Position = v1;
			verts[1].Position = new Vector3(v2.X, v1.Y, v2.Z);
			verts[2].Position = v2;
			verts[3].Position = new Vector3(v1.X, v2.Y, v1.Z);

			device.DrawUserIndexedPrimitives<VertexPositionTexture>
				(PrimitiveType.TriangleList, verts, 0, 4, ib, 0, 2);
		}

		/// <summary>
		/// Draw a quad in any orientation with color
		/// </summary>

		public void Render(Vector3 v1, Vector3 v2, Color color)
		{
			coloredVerts[0].Position = v1;
			coloredVerts[1].Position = new Vector3(v2.X, v1.Y, v2.Z);
			coloredVerts[2].Position = v2;
			coloredVerts[3].Position = new Vector3(v1.X, v2.Y, v1.Z);

			device.DrawUserIndexedPrimitives<VertexPositionColor>
				(PrimitiveType.TriangleList, coloredVerts, 0, 4, ib, 0, 2);
		}

		/// <summary>
		/// Use instanced rendering
		/// </summary>
#if XNA
		public void RenderInstanced(DynamicVertexBuffer dynamicVertexBuffer, int totalInstances)
		{
			// Tell the GPU to read from both the model vertex buffer plus our instanceVertexBuffer
			device.SetVertexBuffers(
				new VertexBufferBinding(vertexBuffer, 0, 0),
				new VertexBufferBinding(dynamicVertexBuffer, 0, 1)
			);

			device.Indices = indexBuffer;

			device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 
				4, 0, 2, totalInstances);
		}
#endif
    }
}
