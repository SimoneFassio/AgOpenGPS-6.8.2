using AgOpenGPS.Core.DrawLib;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgOpenGPS
{
    /// <summary>
    /// Vertex Buffer Object for triangle strip data with vec3 structure.
    /// First element of each patch contains color (RGB in easting/northing/heading),
    /// subsequent elements are vertices (easting, northing, heading=0).
    /// </summary>
    public class Vec3TriangleStripArray : VertexArrayBase
    {
        private struct PatchInfo
        {
            public int Offset;      // First vertex index in VBO
            public int Count;       // Number of vertices in this patch (excl. color)
            public byte R, G, B;    // Color of this patch
        }

        private readonly PatchInfo[] _patches;
        private readonly double[] _vertexData; // Keep for frustum culling
        private bool _isInitialized = false;

        /// <summary>
        /// Factory method that safely creates a Vec3TriangleStripArray with OpenGL context check.
        /// </summary>
        public static Vec3TriangleStripArray TryCreate(List<List<vec3>> patches)
        {
            try
            {
                return new Vec3TriangleStripArray(patches);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vec3TriangleStripArray.TryCreate failed: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        public Vec3TriangleStripArray(List<List<vec3>> patches) : base(2)
        {
            if (patches == null || patches.Count == 0)
            {
                _patches = new PatchInfo[0];
                _vertexData = new double[0];
                Length = 0;
                return;
            }

            _patches = new PatchInfo[patches.Count];

            // Step 1: Calculate total vertex count and extract colors
            int totalVertices = 0;
            for (int i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];
                if (patch.Count > 1) // Color + at least 1 vertex
                {
                    _patches[i].Offset = totalVertices;
                    _patches[i].Count = patch.Count - 1; // Exclude color element

                    // Color is in first element: RGB in easting/northing/heading
                    _patches[i].R = (byte)patch[0].easting;
                    _patches[i].G = (byte)patch[0].northing;
                    _patches[i].B = (byte)patch[0].heading;

                    totalVertices += patch.Count - 1;
                }
                else
                {
                    _patches[i].Count = 0;
                }
            }

            Length = totalVertices;

            // Step 2: Create vertex data array (only easting/northing, no heading)
            _vertexData = new double[totalVertices * 2]; // 2D vertices (x, y)
            int dataIdx = 0;

            for (int i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];
                if (patch.Count > 1)
                {
                    // Skip first element (color), start at index 1
                    for (int j = 1; j < patch.Count; j++)
                    {
                        _vertexData[dataIdx++] = patch[j].easting;
                        _vertexData[dataIdx++] = patch[j].northing;
                    }
                }
            }

            // Step 3: Upload to GPU - only if there is data
            if (totalVertices > 0)
            {
                try
                {
                    Bind();
                    GL.BufferData(
                        BufferTarget.ArrayBuffer,
                        totalVertices * 2 * sizeof(double),
                        _vertexData,
                        BufferUsageHint.DynamicDraw); // DynamicDraw because patches can change
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Vec3TriangleStripArray constructor error: {ex.Message}");
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// Draw all patches without frustum culling.
        /// </summary>
        public void DrawAllPatches()
        {
            Bind();
            for (int i = 0; i < _patches.Length; i++)
            {
                if (_patches[i].Count > 0)
                {
                    GL.Color3(_patches[i].R, _patches[i].G, _patches[i].B);
                    GL.DrawArrays(PrimitiveType.TriangleStrip, _patches[i].Offset, _patches[i].Count);
                }
            }
        }

        /// <summary>
        /// Draw only patches that are visible according to frustum culling.
        /// </summary>
        /// <param name="camEasting">Camera easting position</param>
        /// <param name="camNorthing">Camera northing position</param>
        /// <param name="frustumHalfSize">Half size of frustum (±50 meters default)</param>
        public void DrawVisiblePatches(double camEasting, double camNorthing, double frustumHalfSize = 50.0)
        {
            // Safety check: don't draw if VBO is not properly initialized
            if (!_isInitialized || Length == 0)
            {
                return;
            }

            double pivEplus = camEasting + frustumHalfSize;
            double pivEminus = camEasting - frustumHalfSize;
            double pivNplus = camNorthing + frustumHalfSize;
            double pivNminus = camNorthing - frustumHalfSize;

            Bind();

            for (int i = 0; i < _patches.Length; i++)
            {
                if (_patches[i].Count == 0)
                    continue;

                // Frustum culling: check every 3rd vertex of this patch
                bool isDraw = false;
                int offset = _patches[i].Offset * 2; // *2 because we have x,y pairs
                int count = _patches[i].Count;

                // Check every 3rd vertex (as in original code)
                for (int v = 0; v < count; v += 3)
                {
                    int idx = offset + (v * 2);
                    double easting = _vertexData[idx];
                    double northing = _vertexData[idx + 1];

                    if (easting > pivEplus) continue;
                    if (easting < pivEminus) continue;
                    if (northing > pivNplus) continue;
                    if (northing < pivNminus) continue;

                    // Vertex is in frustum, draw this patch
                    isDraw = true;
                    break;
                }

                if (isDraw)
                {
                    GL.Color3(_patches[i].R, _patches[i].G, _patches[i].B);
                    GL.DrawArrays(PrimitiveType.TriangleStrip, _patches[i].Offset, count);
                }
            }

            // Unbind VBO to prevent state leakage
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
    }
}
