using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgOpenGPS
{
    /// <summary>
    /// Cache wrapper for CTram that supports VBO rendering with automatic invalidation.
    /// Tracks when tram data changes and rebuilds the VBO only when necessary.
    /// </summary>
    public class TramVboCache : IDisposable
    {
        private readonly CTram _tram;
        private TramLineVbo _vbo;
        private int _lastTramListCount;
        private int _lastBndOuterCount;
        private int _lastBndInnerCount;
        private int _lastDisplayMode;

        public TramVboCache(CTram tram)
        {
            _tram = tram ?? throw new ArgumentNullException(nameof(tram));
            _vbo = null;
            _lastTramListCount = -1;
            _lastBndOuterCount = -1;
            _lastBndInnerCount = -1;
            _lastDisplayMode = -1;
        }

        /// <summary>
        /// Get the VBO, rebuilt if necessary (lazy evaluation).
        /// </summary>
        public TramLineVbo GetVbo()
        {
            try
            {
                if (_vbo == null || IsDirty())
                {
                    RebuildVbo();
                }

                return _vbo;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Mark the cache as dirty, forcing a rebuild on the next GetVbo() call.
        /// </summary>
        public void Invalidate()
        {
            _lastTramListCount = -1;
        }

        /// <summary>
        /// Check if the VBO needs to be refreshed.
        /// </summary>
        private bool IsDirty()
        {
            // Check if counts changed
            int currentTramCount = _tram.tramList?.Count ?? 0;
            int currentBndOuterCount = _tram.tramBndOuterArr?.Count ?? 0;
            int currentBndInnerCount = _tram.tramBndInnerArr?.Count ?? 0;
            int currentDisplayMode = _tram.displayMode;

            if (currentTramCount != _lastTramListCount ||
                currentBndOuterCount != _lastBndOuterCount ||
                currentBndInnerCount != _lastBndInnerCount ||
                currentDisplayMode != _lastDisplayMode)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rebuild the VBO with the current tram data.
        /// </summary>
        private void RebuildVbo()
        {
            // Dispose old VBO if present
            _vbo?.Dispose();

            // Create new VBO
            _vbo = new TramLineVbo(_tram.tramList, _tram.tramBndOuterArr, _tram.tramBndInnerArr);

            // Update state
            _lastTramListCount = _tram.tramList?.Count ?? 0;
            _lastBndOuterCount = _tram.tramBndOuterArr?.Count ?? 0;
            _lastBndInnerCount = _tram.tramBndInnerArr?.Count ?? 0;
            _lastDisplayMode = _tram.displayMode;
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

        ~TramVboCache()
        {
            Dispose(false);
        }
    }
}
