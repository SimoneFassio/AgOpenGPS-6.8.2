using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgOpenGPS
{
    /// <summary>
    /// Cache wrapper for CPatches that supports VBO rendering with automatic invalidation.
    /// Tracks when patches change and rebuilds the VBO only when necessary.
    /// </summary>
    public class CPatchesVboCache : IDisposable
    {
        private readonly CPatches _patches;
        private Vec3TriangleStripArray _vbo;
        private int _lastPatchCount;
        private int _lastHash;

        public CPatchesVboCache(CPatches patches)
        {
            _patches = patches ?? throw new ArgumentNullException(nameof(patches));
            _vbo = null;
            _lastPatchCount = -1;
            _lastHash = 0;
        }

        /// <summary>
        /// Get the VBO, rebuilt if necessary (lazy evaluation).
        /// </summary>
        public Vec3TriangleStripArray GetVbo()
        {
            try
            {
                // Check if VBO needs to be created or rebuilt
                if (_vbo == null || IsDirty())
                {
                    // Don't rebuild if there are no patches
                    if (_patches.patchList.Count == 0)
                    {
                        return null;
                    }
                    RebuildVbo();
                }

                return _vbo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPatchesVboCache.GetVbo error: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Mark the cache as dirty, forcing a rebuild on the next GetVbo() call.
        /// </summary>
        public void Invalidate()
        {
            _lastPatchCount = -1; // Force rebuild
        }

        /// <summary>
        /// Check if the VBO needs to be refreshed.
        /// </summary>
        private bool IsDirty()
        {
            // Quick check: patch count changed?
            int currentCount = _patches.patchList.Count;
            if (currentCount != _lastPatchCount)
            {
                return true;
            }

            // Check content changes via hash
            int currentHash = CalculatePatchListHash();
            if (currentHash != _lastHash)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculate a simple hash of the patchList for change detection.
        /// This is a lightweight check, not a cryptographic hash.
        /// </summary>
        private int CalculatePatchListHash()
        {
            unchecked // Allow overflow
            {
                int hash = 17;
                foreach (var patch in _patches.patchList)
                {
                    hash = hash * 31 + patch.Count;
                    // We could include more elements here if needed,
                    // but count alone is often enough to detect changes
                }
                return hash;
            }
        }

        /// <summary>
        /// Rebuild the VBO with the current patch data.
        /// </summary>
        private void RebuildVbo()
        {
            var sw = Stopwatch.StartNew();

            // Dispose old VBO if present
            _vbo?.Dispose();

            // Create new VBO
            _vbo = new Vec3TriangleStripArray(_patches.patchList);

            // Update state
            _lastPatchCount = _patches.patchList.Count;
            _lastHash = CalculatePatchListHash();

            sw.Stop();
            Debug.WriteLine($"CPatchesVboCache.RebuildVbo: {_patches.patchList.Count} patches, {sw.Elapsed.TotalMilliseconds:F2}ms");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _vbo?.Dispose();
                _vbo = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CPatchesVboCache()
        {
            Dispose(false);
        }
    }
}
