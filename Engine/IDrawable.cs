﻿
namespace Engine
{
    using Engine.Common;

    /// <summary>
    /// Drawable interface
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// Visible
        /// </summary>
        bool Visible { get; set; }

        /// <summary>
        /// Draw shadows
        /// </summary>
        /// <param name="context">Context</param>
        void DrawShadows(DrawContextShadows context);
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="context">Context</param>
        void Draw(DrawContext context);
    }
}
