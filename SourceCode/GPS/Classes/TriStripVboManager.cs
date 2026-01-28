using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;

namespace AgOpenGPS
{
    /// <summary>
    /// Manages VBO caching for all triangle strip patches (backbuffer rendering).
    /// Provides hardware detection, cache invalidation, and statistics for debugging.
    /// </summary>
    public class TriStripVboManager : IDisposable
    {
        private readonly List<CPatchesVboCache> _caches;
        private bool? _vboAvailable; // nullable for lazy detection
        private bool _isDisposed;
        private bool _useVboRendering = false; // DISABLED by default for safety

        /// <summary>
        /// Statistics for debugging and performance monitoring.
        /// </summary>
        public class Statistics
        {
            public int TotalSections { get; set; }
            public int TotalPatches { get; set; }
            public int TotalVertices { get; set; }
            public long LastDrawTimeMs { get; set; }
            public int VboRebuildCount { get; set; }
        }

        private Statistics _stats = new Statistics();

        public TriStripVboManager(List<CPatches> triStrip)
        {
            if (triStrip == null)
                throw new ArgumentNullException(nameof(triStrip));

            _caches = new List<CPatchesVboCache>(triStrip.Count);
            _vboAvailable = null; // Lazy detection - not in constructor!

            // Create a cache for each section
            // Note: We don't create VBOs in the constructor, those are created lazily during GetVbo()
            foreach (var patches in triStrip)
            {
                _caches.Add(new CPatchesVboCache(patches));
            }
        }

        /// <summary>
        /// Check if VBO rendering is available on this hardware.
        /// </summary>
        private bool CheckVboAvailability()
        {
            try
            {
                // Check OpenGL version
                string version = GL.GetString(StringName.Version);

                // VBOs are available in OpenGL 2.0+
                // We check for VBO extension presence or version 2.0+
                int major, minor;
                if (ParseVersion(version, out major, out minor))
                {
                    if (major > 2 || (major == 2 && minor >= 0))
                    {
                        return true;
                    }
                }

                // Fallback: check for VBO extension
                string extensions = GL.GetString(StringName.Extensions);
                if (extensions != null && extensions.Contains("GL_ARB_vertex_buffer_object"))
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Parse OpenGL version string into major/minor numbers.
        /// </summary>
        private bool ParseVersion(string versionString, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrEmpty(versionString))
                return false;

            // Format is usually "2.1.0" or "3.3.0 NVIDIA..."
            string[] parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether VBO rendering is available.
        /// </summary>
        public bool IsVboAvailable
        {
            get
            {
                // Lazy check VBO availability on first access
                if (_vboAvailable == null)
                {
                    _vboAvailable = CheckVboAvailability();
                }
                return _vboAvailable.Value && _useVboRendering;
            }
        }

        /// <summary>
        /// Enable or disable VBO rendering (can be changed at runtime).
        /// </summary>
        public bool EnableVboRendering
        {
            get => _useVboRendering;
            set
            {
                if (value != _useVboRendering)
                {
                    _useVboRendering = value;
                }
            }
        }

        /// <summary>
        /// Draw all triangle strips with VBO rendering (if available).
        /// </summary>
        /// <param name="camEasting">Camera easting position for frustum culling</param>
        /// <param name="camNorthing">Camera northing position for frustum culling</param>
        /// <param name="frustumHalfSize">Half size of frustum (±50 meters default)</param>
        public void DrawVisiblePatches(double camEasting, double camNorthing, double frustumHalfSize = 50.0)
        {
            if (!IsVboAvailable || _isDisposed)
            {
                return;
            }

            var sw = Stopwatch.StartNew();
            int totalVertices = 0;

            for (int i = 0; i < _caches.Count; i++)
            {
                try
                {
                    var vbo = _caches[i].GetVbo();
                    if (vbo != null)
                    {
                        // Count vertices for statistics
                        totalVertices += vbo.Length;

                        // Draw with frustum culling
                        vbo.DrawVisiblePatches(camEasting, camNorthing, frustumHalfSize);
                    }
                }
                catch (Exception)
                {
                    // Silently handle errors
                }
            }

            sw.Stop();

            // Update statistics
            _stats.TotalSections = _caches.Count;
            _stats.TotalVertices = totalVertices;
            _stats.LastDrawTimeMs = (long)sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// Mark all caches as dirty, forcing a rebuild on the next draw.
        /// </summary>
        public void InvalidateAll()
        {
            foreach (var cache in _caches)
            {
                cache.Invalidate();
            }
            _stats.VboRebuildCount++;
        }

        /// <summary>
        /// Mark a specific section cache as dirty.
        /// </summary>
        public void Invalidate(int sectionIndex)
        {
            if (sectionIndex >= 0 && sectionIndex < _caches.Count)
            {
                _caches[sectionIndex].Invalidate();
                _stats.VboRebuildCount++;
            }
        }

        /// <summary>
        /// Get statistics for debugging.
        /// </summary>
        public Statistics GetStatistics()
        {
            // Update total patches count
            if (IsVboAvailable)
            {
                int totalPatches = 0;
                for (int i = 0; i < _caches.Count; i++)
                {
                    var vbo = _caches[i].GetVbo();
                    if (vbo != null)
                    {
                        // We don't have direct access to patch count from VBO,
                        // so we store this elsewhere or count from length
                        // For now we use an estimate
                        totalPatches += vbo.Length / 60; // Average 60 vertices per patch
                    }
                }
                _stats.TotalPatches = totalPatches;
            }

            return _stats;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose alle VBO caches
                    foreach (var cache in _caches)
                    {
                        cache?.Dispose();
                    }
                    _caches.Clear();
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TriStripVboManager()
        {
            Dispose(false);
        }
    }
}
