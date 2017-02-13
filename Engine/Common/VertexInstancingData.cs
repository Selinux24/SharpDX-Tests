﻿using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.DXGI;
using InputClassification = SharpDX.Direct3D11.InputClassification;
using InputElement = SharpDX.Direct3D11.InputElement;

namespace Engine.Common
{
    /// <summary>
    /// Instancing data
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexInstancingData : IBufferData
    {
        /// <summary>
        /// Defined input colection
        /// </summary>
        public static InputElement[] GetInput(int slot)
        {
            return new InputElement[]
            {
                new InputElement("localTransform", 0, Format.R32G32B32A32_Float, 0, slot, InputClassification.PerInstanceData, 1),
                new InputElement("localTransform", 1, Format.R32G32B32A32_Float, 16, slot, InputClassification.PerInstanceData, 1),
                new InputElement("localTransform", 2, Format.R32G32B32A32_Float, 32, slot, InputClassification.PerInstanceData, 1),
                new InputElement("localTransform", 3, Format.R32G32B32A32_Float, 48, slot, InputClassification.PerInstanceData, 1),
                new InputElement("animationData", 0, Format.R32G32B32_UInt, 64, slot, InputClassification.PerInstanceData, 1),
                new InputElement("textureIndex", 0, Format.R32_Float, 76, slot, InputClassification.PerInstanceData, 1),
            };
        }

        /// <summary>
        /// Local transformation for the instance
        /// </summary>
        public Matrix Local;
        /// <summary>
        /// Clip index in animation palette
        /// </summary>
        public uint ClipIndex;
        /// <summary>
        /// Animation offset in current clip
        /// </summary>
        public uint AnimationOffset;
        /// <summary>
        /// Padding
        /// </summary>
        public uint Padding;
        /// <summary>
        /// Texture index
        /// </summary>
        public float TextureIndex;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="local">Local transform</param>
        /// <param name="clipIndex">Clip index</param>
        /// <param name="animationOffset">Animation offset</param>
        /// <param name="textureIndex">Texture index</param>
        public VertexInstancingData(Matrix local, uint clipIndex = 0, uint animationOffset = 0, float textureIndex = 0)
        {
            this.Local = local;
            this.ClipIndex = clipIndex;
            this.AnimationOffset = animationOffset;
            this.Padding = 0;
            this.TextureIndex = textureIndex;
        }

        /// <summary>
        /// Size in bytes
        /// </summary>
        public int GetStride()
        {
            return Marshal.SizeOf(typeof(VertexInstancingData));
        }
    };
}
