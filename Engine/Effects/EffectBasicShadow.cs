﻿using System;
using SharpDX;
using Device = SharpDX.Direct3D11.Device;
using EffectMatrixVariable = SharpDX.Direct3D11.EffectMatrixVariable;
using EffectTechnique = SharpDX.Direct3D11.EffectTechnique;

namespace Engine.Effects
{
    using Engine.Common;

    /// <summary>
    /// Basic effect
    /// </summary>
    public class EffectBasicShadow : Drawer
    {
        /// <summary>
        /// Maximum number of bones in a skeleton
        /// </summary>
        public const int MaxBoneTransforms = 96;

        /// <summary>
        /// Position color drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionColor = null;
        /// <summary>
        /// Position color skinned drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionColorSkinned = null;
        /// <summary>
        /// Position normal color drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalColor = null;
        /// <summary>
        /// Position normal color skinned drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalColorSkinned = null;
        /// <summary>
        /// Position texture drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionTexture = null;
        /// <summary>
        /// Position texture skinned drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionTextureSkinned = null;
        /// <summary>
        /// Position normal texture drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalTexture = null;
        /// <summary>
        /// Position normal texture skinned drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalTextureSkinned = null;
        /// <summary>
        /// Position normal texture tangent drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalTextureTangent = null;
        /// <summary>
        /// Position normal texture tangent skinned drawing technique
        /// </summary>
        public readonly EffectTechnique ShadowMapPositionNormalTextureTangentSkinned = null;

        /// <summary>
        /// Bone transformation matrices effect variable
        /// </summary>
        private EffectMatrixVariable boneTransforms = null;
        /// <summary>
        /// World view projection effect variable
        /// </summary>
        private EffectMatrixVariable worldViewProjection = null;

        /// <summary>
        /// Bone transformations
        /// </summary>
        protected Matrix[] BoneTransforms
        {
            get
            {
                return this.boneTransforms.GetMatrixArray<Matrix>(MaxBoneTransforms);
            }
            set
            {
                if (value != null && value.Length > MaxBoneTransforms) throw new Exception(string.Format("Bonetransforms must set {0}. Has {1}", MaxBoneTransforms, value.Length));

                if (value == null)
                {
                    this.boneTransforms.SetMatrix(new Matrix[MaxBoneTransforms]);
                }
                else
                {
                    this.boneTransforms.SetMatrix(value);
                }
            }
        }
        /// <summary>
        /// World view projection matrix
        /// </summary>
        protected Matrix WorldViewProjection
        {
            get
            {
                return this.worldViewProjection.GetMatrix();
            }
            set
            {
                this.worldViewProjection.SetMatrix(value);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="device">Graphics device</param>
        /// <param name="effect">Effect code</param>
        /// <param name="compile">Compile code</param>
        public EffectBasicShadow(Device device, byte[] effect, bool compile)
            : base(device, effect, compile)
        {
            this.ShadowMapPositionColor = this.Effect.GetTechniqueByName("ShadowMapPositionColor");
            this.ShadowMapPositionColorSkinned = this.Effect.GetTechniqueByName("ShadowMapPositionColorSkinned");
            this.ShadowMapPositionNormalColor = this.Effect.GetTechniqueByName("ShadowMapPositionNormalColor");
            this.ShadowMapPositionNormalColorSkinned = this.Effect.GetTechniqueByName("ShadowMapPositionNormalColorSkinned");
            this.ShadowMapPositionTexture = this.Effect.GetTechniqueByName("ShadowMapPositionTexture");
            this.ShadowMapPositionTextureSkinned = this.Effect.GetTechniqueByName("ShadowMapPositionTextureSkinned");
            this.ShadowMapPositionNormalTexture = this.Effect.GetTechniqueByName("ShadowMapPositionNormalTexture");
            this.ShadowMapPositionNormalTextureSkinned = this.Effect.GetTechniqueByName("ShadowMapPositionNormalTextureSkinned");
            this.ShadowMapPositionNormalTextureTangent = this.Effect.GetTechniqueByName("ShadowMapPositionNormalTextureTangent");
            this.ShadowMapPositionNormalTextureTangentSkinned = this.Effect.GetTechniqueByName("ShadowMapPositionNormalTextureTangentSkinned");

            this.AddInputLayout(this.ShadowMapPositionColor, VertexPositionColor.GetInput());
            this.AddInputLayout(this.ShadowMapPositionColorSkinned, VertexSkinnedPositionColor.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalColor, VertexPositionNormalColor.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalColorSkinned, VertexSkinnedPositionNormalColor.GetInput());
            this.AddInputLayout(this.ShadowMapPositionTexture, VertexPositionTexture.GetInput());
            this.AddInputLayout(this.ShadowMapPositionTextureSkinned, VertexSkinnedPositionTexture.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalTexture, VertexPositionNormalTexture.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalTextureSkinned, VertexSkinnedPositionNormalTexture.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalTextureTangent, VertexPositionNormalTextureTangent.GetInput());
            this.AddInputLayout(this.ShadowMapPositionNormalTextureTangentSkinned, VertexSkinnedPositionNormalTextureTangent.GetInput());

            this.boneTransforms = this.Effect.GetVariableByName("gBoneTransforms").AsMatrix();
            this.worldViewProjection = this.Effect.GetVariableByName("gWorldViewProjection").AsMatrix();
        }
        /// <summary>
        /// Get technique by vertex type
        /// </summary>
        /// <param name="vertexType">VertexType</param>
        /// <param name="stage">Stage</param>
        /// <returns>Returns the technique to process the specified vertex type in the specified pipeline stage</returns>
        public override EffectTechnique GetTechnique(VertexTypes vertexType, DrawingStages stage)
        {
            if (stage == DrawingStages.Drawing)
            {
                if (vertexType == VertexTypes.PositionColor)
                {
                    return this.ShadowMapPositionColor;
                }
                else if (vertexType == VertexTypes.PositionColorSkinned)
                {
                    return this.ShadowMapPositionNormalColorSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalColor)
                {
                    return this.ShadowMapPositionNormalColor;
                }
                else if (vertexType == VertexTypes.PositionNormalColorSkinned)
                {
                    return this.ShadowMapPositionNormalColorSkinned;
                }
                else if (vertexType == VertexTypes.PositionTexture)
                {
                    return this.ShadowMapPositionTexture;
                }
                else if (vertexType == VertexTypes.PositionTextureSkinned)
                {
                    return this.ShadowMapPositionTextureSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalTexture)
                {
                    return this.ShadowMapPositionNormalTexture;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureSkinned)
                {
                    return this.ShadowMapPositionNormalTextureSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureTangent)
                {
                    return this.ShadowMapPositionNormalTextureTangent;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureTangentSkinned)
                {
                    return this.ShadowMapPositionNormalTextureTangentSkinned;
                }
                else
                {
                    throw new Exception(string.Format("Bad vertex type for effect and stage: {0} - {1}", vertexType, stage));
                }
            }
            else
            {
                throw new Exception(string.Format("Bad stage for effect: {0}", stage));
            }
        }
        /// <summary>
        /// Update per frame data
        /// </summary>
        /// <param name="world">World matrix</param>
        /// <param name="viewProjection">View * projection matrix</param>
        public void UpdatePerFrame(Matrix world, Matrix viewProjection)
        {
            this.WorldViewProjection = world * viewProjection;
        }
        /// <summary>
        /// Update per model skin data
        /// </summary>
        /// <param name="finalTransforms">Skinning final transforms</param>
        public void UpdatePerSkinning(Matrix[] finalTransforms)
        {
            this.BoneTransforms = finalTransforms;
        }
    }
}