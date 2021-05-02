﻿using SharpDX;

namespace Engine.Effects
{
    using Engine.Common;

    /// <summary>
    /// Terrain shadow effect
    /// </summary>
    public class EffectShadowTerrain : Drawer
    {
        /// <summary>
        /// Shadow mapping technique
        /// </summary>
        public readonly EngineEffectTechnique TerrainShadowMap = null;

        /// <summary>
        /// World view projection effect variable
        /// </summary>
        private readonly EngineEffectVariableMatrix worldViewProjectionVar = null;

        /// <summary>
        /// World view projection matrix
        /// </summary>
        protected Matrix WorldViewProjection
        {
            get
            {
                return worldViewProjectionVar.GetMatrix();
            }
            set
            {
                worldViewProjectionVar.SetMatrix(value);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="effect">Effect code</param>
        /// <param name="compile">Compile code</param>
        public EffectShadowTerrain(Graphics graphics, byte[] effect, bool compile)
            : base(graphics, effect, compile)
        {
            TerrainShadowMap = Effect.GetTechniqueByName("TerrainShadowMap");

            worldViewProjectionVar = Effect.GetVariableMatrix("gWorldViewProjection");
        }

        /// <summary>
        /// Update per frame data
        /// </summary>
        /// <param name="viewProjection">View * projection</param>
        public void UpdatePerFrame(Matrix viewProjection)
        {
            WorldViewProjection = viewProjection;
        }
    }
}
