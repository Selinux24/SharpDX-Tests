﻿using SharpDX;

namespace Engine
{
    /// <summary>
    /// Text drawer description
    /// </summary>
    public class TextDrawerDescription : DrawableDescription
    {
        /// <summary>
        /// Generates a new description
        /// </summary>
        /// <param name="font">Font name</param>
        /// <param name="size">Size</param>
        /// <param name="textColor">Text color</param>
        /// <returns>Returns the new generated description</returns>
        public static TextDrawerDescription Generate(string font, int size, Color textColor)
        {
            return Generate(font, size, textColor, Color.Transparent);
        }
        /// <summary>
        /// Generates a new description
        /// </summary>
        /// <param name="font">Font name</param>
        /// <param name="size">Size</param>
        /// <param name="textColor">Text color</param>
        /// <param name="shadowColor">Shadow color</param>
        /// <returns>Returns the new generated description</returns>
        public static TextDrawerDescription Generate(string font, int size, Color textColor, Color shadowColor)
        {
            return new TextDrawerDescription()
            {
                Font = font,
                FontSize = size,
                TextColor = textColor,
                ShadowColor = shadowColor,
            };
        }

        /// <summary>
        /// Font name
        /// </summary>
        public string Font;
        /// <summary>
        /// Font size
        /// </summary>
        public int FontSize;
        /// <summary>
        /// Text color
        /// </summary>
        public Color4 TextColor;
        /// <summary>
        /// Shadow color
        /// </summary>
        public Color4 ShadowColor;

        /// <summary>
        /// Constructor
        /// </summary>
        public TextDrawerDescription()
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