﻿using SharpDX;

namespace Engine
{
    /// <summary>
    /// Directional light
    /// </summary>
    public interface ISceneLightDirectional : ISceneLight
    {
        /// <summary>
        /// Light direction
        /// </summary>
        Vector3 Direction { get; }

        /// <summary>
        /// Shadow map index
        /// </summary>
        uint ShadowMapCount { get; set; }
        /// <summary>
        /// From light view * projection matrix array
        /// </summary>
        Matrix[] FromLightVP { get; set; }
    }
}
