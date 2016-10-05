﻿using SharpDX;

namespace Engine
{
    using Engine.Common;

    /// <summary>
    /// Minimap description
    /// </summary>
    public class MinimapDescription : DrawableDescription
    {
        /// <summary>
        /// Top position
        /// </summary>
        public int Top;
        /// <summary>
        /// Left position
        /// </summary>
        public int Left;
        /// <summary>
        /// Width
        /// </summary>
        public int Width;
        /// <summary>
        /// Height
        /// </summary>
        public int Height;
        /// <summary>
        /// Terrain to draw
        /// </summary>
        public Drawable[] Drawables;
        /// <summary>
        /// Minimap render area
        /// </summary>
        public BoundingBox MinimapArea;

        /// <summary>
        /// Constructor
        /// </summary>
        public MinimapDescription()
            : base()
        {
            this.Static = true;
            this.AlwaysVisible = true;
            this.CastShadow = false;
            this.DeferredEnabled = false;
            this.EnableDepthStencil = false;
            this.EnableAlphaBlending = true;
        }
    }
}
