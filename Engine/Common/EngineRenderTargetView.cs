﻿using System;
using System.Collections.Generic;

namespace Engine.Common
{
    using SharpDX.Direct3D11;

    /// <summary>
    /// Render target
    /// </summary>
    public class EngineRenderTargetView : IDisposable
    {
        /// <summary>
        /// Render target list
        /// </summary>
        private List<RenderTargetView1> rtv = new List<RenderTargetView1>();

        /// <summary>
        /// Gets the render target count
        /// </summary>
        public int Count
        {
            get
            {
                return rtv.Count;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public EngineRenderTargetView()
        {

        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rtv">Render target view</param>
        internal EngineRenderTargetView(RenderTargetView1 rtv)
        {
            this.rtv.Add(rtv);
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~EngineRenderTargetView()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        /// <param name="disposing">Free managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < rtv?.Count; i++)
                {
                    rtv[i]?.Dispose();
                    rtv[i] = null;
                }

                rtv?.Clear();
                rtv = null;
            }
        }

        /// <summary>
        /// Adds a new Render Target to the collection
        /// </summary>
        /// <param name="rtv">Render target view</param>
        internal void Add(RenderTargetView1 rtv)
        {
            this.rtv.Add(rtv);
        }

        /// <summary>
        /// Gets the render target
        /// </summary>
        /// <returns>Returns the internal render target</returns>
        internal RenderTargetView1 GetRenderTarget()
        {
            return rtv[0];
        }
        /// <summary>
        /// Gets the render targets
        /// </summary>
        /// <returns>Returns the internal render target list</returns>
        internal RenderTargetView1[] GetRenderTargets()
        {
            return rtv.ToArray();
        }
    }
}
