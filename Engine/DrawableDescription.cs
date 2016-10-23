﻿
namespace Engine
{
    /// <summary>
    /// Drawable description
    /// </summary>
    public abstract class DrawableDescription
    {
        /// <summary>
        /// Name
        /// </summary>
        public string Name = null;
        /// <summary>
        /// Is Static
        /// </summary>
        public bool Static = false;
        /// <summary>
        /// Always visible
        /// </summary>
        public bool AlwaysVisible = false;
        /// <summary>
        /// Gets or sets whether the object cast shadow
        /// </summary>
        public bool CastShadow = false;
        /// <summary>
        /// Can be renderer by the deferred renderer
        /// </summary>
        public bool DeferredEnabled = true;
        /// <summary>
        /// Enables z-buffer writting
        /// </summary>
        public bool EnableDepthStencil = true;
        /// <summary>
        /// Enables transparent blending
        /// </summary>
        public bool EnableAlphaBlending = false;
    }
}