using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgOpenGPS
{
    /// <summary>
    /// Vertex Buffer Object for tram line rendering using vec2 data.
    /// Supports multiple line strips (tramList) and boundary lines (outer/inner).
    /// Uses modern OpenGL vertex attributes for compatibility with Vec3TriangleStripArray.
    /// </summary>
    public class TramLineVbo : IDisposable
    {
        private struct LineStripInfo
        {
            public int Offset;
            public int Count;
        }

        private readonly LineStripInfo[] _tramStrips;
        private readonly int _boundaryOuterOffset;
        private readonly int _boundaryOuterCount;
        private readonly int _boundaryInnerOffset;
        private readonly int _boundaryInnerCount;
        private readonly int _totalVertices;
        private bool _isInitialized = false;
        private int _vboId;
        private bool _isDisposed;

        public int Length => _totalVertices;

        public TramLineVbo(List<List<vec2>> tramList, List<vec2> bndOuter, List<vec2> bndInner)
        {
            _vboId = GL.GenBuffer();
            int numTramStrips = tramList != null ? tramList.Count : 0;

            _tramStrips = new LineStripInfo[numTramStrips];

            // Calculate total vertices and offsets
            int totalVertices = 0;

            // Process tram list
            for (int i = 0; i < numTramStrips; i++)
            {
                _tramStrips[i].Offset = totalVertices;
                _tramStrips[i].Count = tramList[i].Count;
                totalVertices += tramList[i].Count;
            }

            // Boundary outer
            _boundaryOuterOffset = totalVertices;
            _boundaryOuterCount = bndOuter != null ? bndOuter.Count : 0;
            totalVertices += _boundaryOuterCount;

            // Boundary inner
            _boundaryInnerOffset = totalVertices;
            _boundaryInnerCount = bndInner != null ? bndInner.Count : 0;
            totalVertices += _boundaryInnerCount;

            _totalVertices = totalVertices;

            // Create vertex data array
            double[] vertexData = new double[totalVertices * 2];
            int dataIdx = 0;

            // Copy tram list vertices
            for (int i = 0; i < numTramStrips; i++)
            {
                for (int j = 0; j < tramList[i].Count; j++)
                {
                    vertexData[dataIdx++] = tramList[i][j].easting;
                    vertexData[dataIdx++] = tramList[i][j].northing;
                }
            }

            // Copy boundary outer
            if (bndOuter != null)
            {
                for (int i = 0; i < bndOuter.Count; i++)
                {
                    vertexData[dataIdx++] = bndOuter[i].easting;
                    vertexData[dataIdx++] = bndOuter[i].northing;
                }
            }

            // Copy boundary inner
            if (bndInner != null)
            {
                for (int i = 0; i < bndInner.Count; i++)
                {
                    vertexData[dataIdx++] = bndInner[i].easting;
                    vertexData[dataIdx++] = bndInner[i].northing;
                }
            }

            // Upload to GPU
            if (totalVertices > 0)
            {
                try
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vboId);
                    GL.BufferData(
                        BufferTarget.ArrayBuffer,
                        totalVertices * 2 * sizeof(double),
                        vertexData,
                        BufferUsageHint.DynamicDraw);

                    // Setup vertex attribute pointer (modern OpenGL)
                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Double, false, 0, 0);

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    _isInitialized = false;
                }
            }

            // Unbind after setup
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vboId);
            // Re-setup vertex attribute pointer to ensure it points to our VBO
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Double, false, 0, 0);
        }

        /// <summary>
        /// Draw tram lines with specified display mode.
        /// </summary>
        /// <param name="displayMode">1 = All, 2 = Lines only, 3 = Outer only</param>
        /// <param name="alpha">Alpha transparency value (0.0-1.0)</param>
        public void DrawTramLines(int displayMode, float alpha = 1.0f)
        {
            if (!_isInitialized || Length == 0)
            {
                return;
            }

            // Set color BEFORE binding VBO (uses current color, not color array)
            byte alphaByte = (byte)(alpha * 255);
            GL.Color4((byte)0, (byte)245, (byte)0, alphaByte); // Green tram lines (0, 245, 0)

            // Bind VBO
            Bind();

            // Draw tram strips (mode 1 or 2)
            if (displayMode == 1 || displayMode == 2)
            {
                for (int i = 0; i < _tramStrips.Length; i++)
                {
                    if (_tramStrips[i].Count > 0)
                    {
                        GL.DrawArrays(PrimitiveType.LineStrip, _tramStrips[i].Offset, _tramStrips[i].Count);
                    }
                }
            }

            // Draw boundary lines (mode 1 or 3)
            if (displayMode == 1 || displayMode == 3)
            {
                if (_boundaryOuterCount > 0)
                {
                    GL.DrawArrays(PrimitiveType.LineStrip, _boundaryOuterOffset, _boundaryOuterCount);
                }

                if (_boundaryInnerCount > 0)
                {
                    GL.DrawArrays(PrimitiveType.LineStrip, _boundaryInnerOffset, _boundaryInnerCount);
                }
            }

            // Unbind VBO to prevent state leakage
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    if (_vboId != 0)
                    {
                        GL.DeleteBuffer(_vboId);
                        _vboId = 0;
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TramLineVbo()
        {
            Dispose(false);
        }
    }
}
