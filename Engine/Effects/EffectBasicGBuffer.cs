﻿using System;
using SharpDX;
using Device = SharpDX.Direct3D11.Device;
using EffectMatrixVariable = SharpDX.Direct3D11.EffectMatrixVariable;
using EffectScalarVariable = SharpDX.Direct3D11.EffectScalarVariable;
using EffectShaderResourceVariable = SharpDX.Direct3D11.EffectShaderResourceVariable;
using EffectTechnique = SharpDX.Direct3D11.EffectTechnique;
using EffectVariable = SharpDX.Direct3D11.EffectVariable;
using ShaderResourceView = SharpDX.Direct3D11.ShaderResourceView;

namespace Engine.Effects
{
    using Engine.Common;

    /// <summary>
    /// Basic effect
    /// </summary>
    public class EffectBasicGBuffer : Drawer
    {
        /// <summary>
        /// Maximum number of bones in a skeleton
        /// </summary>
        public const int MaxBoneTransforms = 96;

        /// <summary>
        /// Position color drawing technique
        /// </summary>
        public readonly EffectTechnique PositionColor = null;
        /// <summary>
        /// Position color skinned drawing technique
        /// </summary>
        public readonly EffectTechnique PositionColorSkinned = null;
        /// <summary>
        /// Position normal color drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalColor = null;
        /// <summary>
        /// Position normal color skinned drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalColorSkinned = null;
        /// <summary>
        /// Position texture drawing technique
        /// </summary>
        public readonly EffectTechnique PositionTexture = null;
        /// <summary>
        /// Position texture skinned drawing technique
        /// </summary>
        public readonly EffectTechnique PositionTextureSkinned = null;
        /// <summary>
        /// Position normal texture drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalTexture = null;
        /// <summary>
        /// Position normal texture skinned drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalTextureSkinned = null;
        /// <summary>
        /// Position normal texture tangent drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalTextureTangent = null;
        /// <summary>
        /// Position normal texture tangent skinned drawing technique
        /// </summary>
        public readonly EffectTechnique PositionNormalTextureTangentSkinned = null;

        /// <summary>
        /// World matrix effect variable
        /// </summary>
        private EffectMatrixVariable world = null;
        /// <summary>
        /// Inverse world matrix effect variable
        /// </summary>
        private EffectMatrixVariable worldInverse = null;
        /// <summary>
        /// World view projection effect variable
        /// </summary>
        private EffectMatrixVariable worldViewProjection = null;
        /// <summary>
        /// Shadow transform effect variable
        /// </summary>
        private EffectMatrixVariable shadowTransform = null;
        /// <summary>
        /// Enable shados effect variable
        /// </summary>
        private EffectScalarVariable enableShadows = null;
        /// <summary>
        /// Material effect variable
        /// </summary>
        private EffectVariable material = null;
        /// <summary>
        /// Texture index effect variable
        /// </summary>
        private EffectScalarVariable textureIndex = null;
        /// <summary>
        /// Bone transformation matrices effect variable
        /// </summary>
        private EffectMatrixVariable boneTransforms = null;
        /// <summary>
        /// Texture effect variable
        /// </summary>
        private EffectShaderResourceVariable textures = null;
        /// <summary>
        /// Normal map effect variable
        /// </summary>
        private EffectShaderResourceVariable normalMap = null;
        /// <summary>
        /// Shadow map effect variable
        /// </summary>
        private EffectShaderResourceVariable shadowMap = null;

        /// <summary>
        /// World matrix
        /// </summary>
        protected Matrix World
        {
            get
            {
                return this.world.GetMatrix();
            }
            set
            {
                this.world.SetMatrix(value);
            }
        }
        /// <summary>
        /// Inverse world matrix
        /// </summary>
        protected Matrix WorldInverse
        {
            get
            {
                return this.worldInverse.GetMatrix();
            }
            set
            {
                this.worldInverse.SetMatrix(value);
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
        /// Shadow transform matrix
        /// </summary>
        protected Matrix ShadowTransform
        {
            get
            {
                return this.shadowTransform.GetMatrix();
            }
            set
            {
                this.shadowTransform.SetMatrix(value);
            }
        }
        /// <summary>
        /// Enable shadows
        /// </summary>
        protected float EnableShadows
        {
            get
            {
                return this.enableShadows.GetFloat();
            }
            set
            {
                this.enableShadows.Set(value);
            }
        }
        /// <summary>
        /// Material
        /// </summary>
        protected BufferMaterials Material
        {
            get
            {
                using (DataStream ds = this.material.GetRawValue(default(BufferMaterials).Stride))
                {
                    ds.Position = 0;

                    return ds.Read<BufferMaterials>();
                }
            }
            set
            {
                using (DataStream ds = DataStream.Create<BufferMaterials>(new BufferMaterials[] { value }, true, false))
                {
                    ds.Position = 0;

                    this.material.SetRawValue(ds, default(BufferMaterials).Stride);
                }
            }
        }
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
        /// Texture index
        /// </summary>
        protected int TextureIndex
        {
            get
            {
                return (int)this.textureIndex.GetFloat();
            }
            set
            {
                this.textureIndex.Set((float)value);
            }
        }
        /// <summary>
        /// Texture
        /// </summary>
        protected ShaderResourceView Textures
        {
            get
            {
                return this.textures.GetResource();
            }
            set
            {
                this.textures.SetResource(value);
            }
        }
        /// <summary>
        /// Normal map
        /// </summary>
        protected ShaderResourceView NormalMap
        {
            get
            {
                return this.normalMap.GetResource();
            }
            set
            {
                this.normalMap.SetResource(value);
            }
        }
        /// <summary>
        /// Shadow map
        /// </summary>
        protected ShaderResourceView ShadowMap
        {
            get
            {
                return this.shadowMap.GetResource();
            }
            set
            {
                this.shadowMap.SetResource(value);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="device">Graphics device</param>
        /// <param name="effect">Effect code</param>
        /// <param name="compile">Compile code</param>
        public EffectBasicGBuffer(Device device, byte[] effect, bool compile)
            : base(device, effect, compile)
        {
            this.PositionColor = this.Effect.GetTechniqueByName("PositionColor");
            this.PositionColorSkinned = this.Effect.GetTechniqueByName("PositionColorSkinned");
            this.PositionNormalColor = this.Effect.GetTechniqueByName("PositionNormalColor");
            this.PositionNormalColorSkinned = this.Effect.GetTechniqueByName("PositionNormalColorSkinned");
            this.PositionTexture = this.Effect.GetTechniqueByName("PositionTexture");
            this.PositionTextureSkinned = this.Effect.GetTechniqueByName("PositionTextureSkinned");
            this.PositionNormalTexture = this.Effect.GetTechniqueByName("PositionNormalTexture");
            this.PositionNormalTextureSkinned = this.Effect.GetTechniqueByName("PositionNormalTextureSkinned");
            this.PositionNormalTextureTangent = this.Effect.GetTechniqueByName("PositionNormalTextureTangent");
            this.PositionNormalTextureTangentSkinned = this.Effect.GetTechniqueByName("PositionNormalTextureTangentSkinned");

            this.AddInputLayout(this.PositionColor, VertexPositionColor.GetInput());
            this.AddInputLayout(this.PositionColorSkinned, VertexSkinnedPositionColor.GetInput());
            this.AddInputLayout(this.PositionNormalColor, VertexPositionNormalColor.GetInput());
            this.AddInputLayout(this.PositionNormalColorSkinned, VertexSkinnedPositionNormalColor.GetInput());
            this.AddInputLayout(this.PositionTexture, VertexPositionTexture.GetInput());
            this.AddInputLayout(this.PositionTextureSkinned, VertexSkinnedPositionTexture.GetInput());
            this.AddInputLayout(this.PositionNormalTexture, VertexPositionNormalTexture.GetInput());
            this.AddInputLayout(this.PositionNormalTextureSkinned, VertexSkinnedPositionNormalTexture.GetInput());
            this.AddInputLayout(this.PositionNormalTextureTangent, VertexPositionNormalTextureTangent.GetInput());
            this.AddInputLayout(this.PositionNormalTextureTangentSkinned, VertexSkinnedPositionNormalTextureTangent.GetInput());

            this.world = this.Effect.GetVariableByName("gWorld").AsMatrix();
            this.worldInverse = this.Effect.GetVariableByName("gWorldInverse").AsMatrix();
            this.worldViewProjection = this.Effect.GetVariableByName("gWorldViewProjection").AsMatrix();
            this.shadowTransform = this.Effect.GetVariableByName("gShadowTransform").AsMatrix();
            this.enableShadows = this.Effect.GetVariableByName("gEnableShadows").AsScalar();
            this.material = this.Effect.GetVariableByName("gMaterial");
            this.boneTransforms = this.Effect.GetVariableByName("gBoneTransforms").AsMatrix();
            this.textureIndex = this.Effect.GetVariableByName("gTextureIndex").AsScalar();
            this.textures = this.Effect.GetVariableByName("gTextureArray").AsShaderResource();
            this.normalMap = this.Effect.GetVariableByName("gNormalMap").AsShaderResource();
            this.shadowMap = this.Effect.GetVariableByName("gShadowMap").AsShaderResource();
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
                    return this.PositionColor;
                }
                else if (vertexType == VertexTypes.PositionColorSkinned)
                {
                    return this.PositionNormalColorSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalColor)
                {
                    return this.PositionNormalColor;
                }
                else if (vertexType == VertexTypes.PositionNormalColorSkinned)
                {
                    return this.PositionNormalColorSkinned;
                }
                else if (vertexType == VertexTypes.PositionTexture)
                {
                    return this.PositionTexture;
                }
                else if (vertexType == VertexTypes.PositionTextureSkinned)
                {
                    return this.PositionTextureSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalTexture)
                {
                    return this.PositionNormalTexture;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureSkinned)
                {
                    return this.PositionNormalTextureSkinned;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureTangent)
                {
                    return this.PositionNormalTextureTangent;
                }
                else if (vertexType == VertexTypes.PositionNormalTextureTangentSkinned)
                {
                    return this.PositionNormalTextureTangentSkinned;
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
        /// <param name="world">World Matrix</param>
        /// <param name="viewProjection">View * projection</param>
        /// <param name="shadowMap">Shadow map texture</param>
        /// <param name="shadowTransform">Shadow transform</param>
        public void UpdatePerFrame(
            Matrix world,
            Matrix viewProjection,
            ShaderResourceView shadowMap,
            Matrix shadowTransform)
        {
            this.World = world;
            this.WorldInverse = Matrix.Invert(world);
            this.WorldViewProjection = world * viewProjection;

            if (shadowMap != null)
            {
                this.EnableShadows = 1.0f;
                this.ShadowMap = shadowMap;
                this.ShadowTransform = shadowTransform;
            }
            else
            {
                this.EnableShadows = 0.0f;
                this.ShadowMap = null;
                this.ShadowTransform = Matrix.Identity;
            }
        }
        /// <summary>
        /// Update per model object data
        /// </summary>
        /// <param name="material">Material</param>
        /// <param name="texture">Texture</param>
        /// <param name="normalMap">Normal map</param>
        /// <param name="textureIndex">Texture index</param>
        public void UpdatePerObject(
            Material material,
            ShaderResourceView texture,
            ShaderResourceView normalMap,
            int textureIndex)
        {
            this.Material = new BufferMaterials(material);
            this.Textures = texture;
            this.NormalMap = normalMap;
            this.TextureIndex = textureIndex;
        }
        /// <summary>
        /// Update per model skin data
        /// </summary>
        /// <param name="finalTransforms">Skinning final transform</param>
        public void UpdatePerSkinning(Matrix[] finalTransforms)
        {
            this.BoneTransforms = finalTransforms;
        }
    }
}