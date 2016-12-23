﻿
namespace Engine
{
    /// <summary>
    /// Cube-map description
    /// </summary>
    public class CubemapDescription : DrawableDescription
    {
        /// <summary>
        /// Cube map geometry enumeration
        /// </summary>
        public enum CubeMapGeometryEnum
        {
            /// <summary>
            /// Box
            /// </summary>
            Box,
            /// <summary>
            /// Sphere
            /// </summary>
            Sphere,
            /// <summary>
            /// Semisphere
            /// </summary>
            Semispehere
        }

        /// <summary>
        /// Content path
        /// </summary>
        public string ContentPath = "Resources";
        /// <summary>
        /// Texture
        /// </summary>
        public string Texture;
        /// <summary>
        /// Radius
        /// </summary>
        public float Radius;
        /// <summary>
        /// Cubemap geometry
        /// </summary>
        public CubeMapGeometryEnum Geometry = CubeMapGeometryEnum.Sphere;
        /// <summary>
        /// Reverse geometry faces
        /// </summary>
        public bool ReverseFaces = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public CubemapDescription()
            : base()
        {
            this.Static = true;
            this.AlwaysVisible = false;
            this.CastShadow = false;
            this.DeferredEnabled = true;
            this.EnableDepthStencil = false;
            this.EnableAlphaBlending = false;
        }
    }
}
