﻿using SharpDX;
using System;
using System.Threading.Tasks;

namespace Engine
{
    using Engine.Common;
    using Engine.Effects;

    /// <summary>
    /// Scattered sky
    /// </summary>
    public class SkyScattering : Drawable
    {
        /// <summary>
        /// Vernier scale
        /// </summary>
        /// <param name="cos">Cosine</param>
        /// <returns>Returns Vernier scale value</returns>
        private static float VernierScale(float cos)
        {
            float icos = 1.0f - cos;

            return 0.25f * (float)Math.Exp(-0.00287f + icos * (0.459f + icos * (3.83f + icos * (-6.80f + (icos * 5.25f)))));
        }
        /// <summary>
        /// Mie phase
        /// </summary>
        /// <param name="cos">Cosine</param>
        /// <returns>Returns Mie phase value</returns>
        private static float GetMiePhase(float cos)
        {
            float coscos = cos * cos;
            float g = -0.991f;
            float gg = g * g;

            return 1.5f * ((1.0f - gg) / (2.0f + gg)) * (1.0f + coscos) / (float)Math.Pow(Math.Abs(1.0f + gg - 2.0f * g * cos), 1.5f);
        }

        /// <summary>
        /// Vertex buffer descriptor
        /// </summary>
        private BufferDescriptor vertexBuffer = null;
        /// <summary>
        /// Index buffer descriptor
        /// </summary>
        private BufferDescriptor indexBuffer = null;
        /// <summary>
        /// Rayleigh scattering constant value
        /// </summary>
        private float rayleighScattering;
        /// <summary>
        /// Mie scattering constant value
        /// </summary>
        private float mieScattering;
        /// <summary>
        /// Wave length
        /// </summary>
        private Color4 wavelength;
        /// <summary>
        /// Sphere inner radius
        /// </summary>
        private float sphereInnerRadius;
        /// <summary>
        /// Sphere outer radius
        /// </summary>
        private float sphereOuterRadius;

        /// <summary>
        /// Rayleigh scattering * 4 * PI
        /// </summary>
        public float RayleighScattering4PI { get; private set; }
        /// <summary>
        /// Mie scattering * 4 * PI
        /// </summary>
        public float MieScattering4PI { get; private set; }
        /// <summary>
        /// Inverse wave length * 4
        /// </summary>
        public Color4 InvWaveLength4 { get; private set; }
        /// <summary>
        /// Scattering Scale
        /// </summary>
        public float ScatteringScale { get; private set; }

        /// <summary>
        /// Planet radius
        /// </summary>
        public float PlanetRadius { get; set; }
        /// <summary>
        /// Planet atmosphere radius from surface
        /// </summary>
        public float PlanetAtmosphereRadius { get; set; }
        /// <summary>
        /// Rayleigh scattering constant value
        /// </summary>
        public float RayleighScattering
        {
            get
            {
                return this.rayleighScattering;
            }
            set
            {
                this.rayleighScattering = value;
                this.RayleighScattering4PI = value * 4.0f * MathUtil.Pi;
            }
        }
        /// <summary>
        /// Rayleigh scale depth value
        /// </summary>
        public float RayleighScaleDepth { get; set; }
        /// <summary>
        /// Mie scattering constant value
        /// </summary>
        public float MieScattering
        {
            get
            {
                return this.mieScattering;
            }
            set
            {
                this.mieScattering = value;
                this.MieScattering4PI = value * 4.0f * MathUtil.Pi;
            }
        }
        /// <summary>
        /// Mie phase assymetry value
        /// </summary>
        public float MiePhaseAssymetry { get; set; }
        /// <summary>
        /// Mie scale depth value
        /// </summary>
        public float MieScaleDepth { get; set; }
        /// <summary>
        /// Light wave length
        /// </summary>
        public Color4 WaveLength
        {
            get
            {
                return this.wavelength;
            }
            set
            {
                this.wavelength = value;
                this.InvWaveLength4 = new Color4(
                    1f / (float)Math.Pow(value.Red, 4.0f),
                    1f / (float)Math.Pow(value.Green, 4.0f),
                    1f / (float)Math.Pow(value.Blue, 4.0f),
                    1.0f);
            }
        }
        /// <summary>
        /// Sky brightness
        /// </summary>
        public float Brightness { get; set; }
        /// <summary>
        /// Sphere inner radius
        /// </summary>
        public float SphereInnerRadius
        {
            get
            {
                return this.sphereInnerRadius;
            }
            set
            {
                this.sphereInnerRadius = value;

                this.CalcScale();
            }
        }
        /// <summary>
        /// Sphere outter radius
        /// </summary>
        public float SphereOuterRadius
        {
            get
            {
                return this.sphereOuterRadius;
            }
            set
            {
                this.sphereOuterRadius = value;

                this.CalcScale();
            }
        }
        /// <summary>
        /// HDR exposure
        /// </summary>
        public float HDRExposure { get; set; }
        /// <summary>
        /// Resolution
        /// </summary>
        public SkyScatteringResolutions Resolution { get; set; }
        /// <summary>
        /// Returns true if the buffers were ready
        /// </summary>
        public bool BuffersReady
        {
            get
            {
                if (this.vertexBuffer?.Ready != true)
                {
                    return false;
                }

                if (this.indexBuffer?.Ready != true)
                {
                    return false;
                }

                if (this.indexBuffer.Count <= 0)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Sky scattering description class</param>
        public SkyScattering(Scene scene, SkyScatteringDescription description)
            : base(scene, description)
        {
            this.PlanetRadius = description.PlanetRadius;
            this.PlanetAtmosphereRadius = description.PlanetAtmosphereRadius;

            this.RayleighScattering = description.RayleighScattering;
            this.RayleighScaleDepth = description.RayleighScaleDepth;
            this.MieScattering = description.MieScattering;
            this.MiePhaseAssymetry = description.MiePhaseAssymetry;
            this.MieScaleDepth = description.MieScaleDepth;

            this.WaveLength = description.WaveLength;
            this.Brightness = description.Brightness;
            this.HDRExposure = description.HDRExposure;
            this.Resolution = description.Resolution;

            this.sphereInnerRadius = 1.0f;
            this.sphereOuterRadius = this.sphereInnerRadius * 1.025f;
            this.CalcScale();

            this.InitializeBuffers(description.Name);
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~SkyScattering()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Remove data from buffer manager
                this.BufferManager?.RemoveVertexData(this.vertexBuffer);
                this.BufferManager?.RemoveIndexData(this.indexBuffer);
            }
        }

        /// <inheritdoc/>
        public override void Update(UpdateContext context)
        {
            var keyLight = context.Lights.KeyLight;
            if (keyLight != null)
            {
                context.Lights.BaseFogColor = this.GetFogColor(keyLight.Direction);
            }
        }
        /// <inheritdoc/>
        public override void Draw(DrawContext context)
        {
            if (!Visible)
            {
                return;
            }

            var keyLight = context.Lights.KeyLight;
            if (keyLight == null)
            {
                return;
            }

            if (!BuffersReady)
            {
                return;
            }

            bool draw = context.ValidateDraw(this.BlendMode);
            if (!draw)
            {
                return;
            }

            Counters.InstancesPerFrame++;
            Counters.PrimitivesPerFrame += this.indexBuffer.Count / 3;

            var effect = DrawerPool.EffectDefaultSkyScattering;
            var technique = GetScatteringTechnique(effect);

            this.BufferManager.SetIndexBuffer(this.indexBuffer);
            this.BufferManager.SetInputAssembler(technique, this.vertexBuffer, Topology.TriangleList);

            effect.UpdatePerFrame(
                Matrix.Translation(context.EyePosition),
                context.ViewProjection,
                keyLight.Direction,
                new EffectSkyScatterState
                {
                    PlanetRadius = this.PlanetRadius,
                    PlanetAtmosphereRadius = this.PlanetAtmosphereRadius,
                    SphereOuterRadius = this.SphereOuterRadius,
                    SphereInnerRadius = this.SphereInnerRadius,
                    SkyBrightness = this.Brightness,
                    RayleighScattering = this.RayleighScattering,
                    RayleighScattering4PI = this.RayleighScattering4PI,
                    MieScattering = this.MieScattering,
                    MieScattering4PI = this.MieScattering4PI,
                    InvWaveLength4 = this.InvWaveLength4,
                    Scale = this.ScatteringScale,
                    RayleighScaleDepth = this.RayleighScaleDepth,
                    BackColor = context.Lights.FogColor,
                    HdrExposure = this.HDRExposure,
                });

            var graphics = this.Game.Graphics;

            for (int p = 0; p < technique.PassCount; p++)
            {
                graphics.EffectPassApply(technique, p, 0);

                graphics.DrawIndexed(this.indexBuffer.Count, this.indexBuffer.BufferOffset, this.vertexBuffer.BufferOffset);
            }
        }
        /// <summary>
        /// Gets the sky scatterfing effect technique base on resolution
        /// </summary>
        /// <param name="effect">Effect</param>
        /// <returns>Returns the sky scatterfing effect technique</returns>
        private EngineEffectTechnique GetScatteringTechnique(EffectDefaultSkyScattering effect)
        {
            EngineEffectTechnique technique;
            if (this.Resolution == SkyScatteringResolutions.High)
            {
                technique = effect.SkyScatteringHigh;
            }
            else if (this.Resolution == SkyScatteringResolutions.Medium)
            {
                technique = effect.SkyScatteringMedium;
            }
            else
            {
                technique = effect.SkyScatteringLow;
            }

            return technique;
        }

        /// <summary>
        /// Initialize buffers
        /// </summary>
        /// <param name="name">Buffer name</param>
        private void InitializeBuffers(string name)
        {
            var sphere = GeometryUtil.CreateSphere(1, 10, 75);

            var vertices = VertexPosition.Generate(sphere.Vertices);
            var indices = GeometryUtil.ChangeCoordinate(sphere.Indices);

            this.vertexBuffer = this.BufferManager.AddVertexData(name, false, vertices);
            this.indexBuffer = this.BufferManager.AddIndexData(name, false, indices);
        }
        /// <summary>
        /// Calc current scattering scale from sphere radius values
        /// </summary>
        private void CalcScale()
        {
            this.ScatteringScale = 1.0f / (this.SphereOuterRadius - this.SphereInnerRadius);
        }

        /// <summary>
        /// Gets the fog color base on light direction
        /// </summary>
        /// <param name="lightDirection">Light direction</param>
        /// <returns>Returns the fog color</returns>
        public Color4 GetFogColor(Vector3 lightDirection)
        {
            Color4 outColor = new Color4(0f, 0f, 0f, 0f);

            Helper.GetAnglesFromVector(Vector3.ForwardLH, out float yaw, out _);
            float originalYaw = yaw;

            float pitch = MathUtil.DegreesToRadians(10.0f);

            uint samples = 10;

            for (uint i = 0; i < samples; i++)
            {
                Helper.GetVectorFromAngles(yaw, pitch, out Vector3 scatterPos);

                scatterPos *= this.PlanetRadius + this.PlanetAtmosphereRadius;
                scatterPos.Y -= this.PlanetRadius;

                this.GetColor(scatterPos, lightDirection, out Color4 tmpColor);

                outColor += tmpColor;

                if (i <= samples / 2)
                {
                    yaw += MathUtil.DegreesToRadians(5.0f);
                }
                else
                {
                    originalYaw += MathUtil.DegreesToRadians(-5.0f);

                    yaw = originalYaw;
                }

                yaw = MathUtil.Mod(yaw, MathUtil.TwoPi);
            }

            if (samples > 0)
            {
                outColor *= (1f / (float)samples);
            }

            return outColor;
        }
        /// <summary>
        /// Gets the color at scatter position based on light direction
        /// </summary>
        /// <param name="scatterPosition">Scatter position</param>
        /// <param name="lightDirection">Light direction</param>
        /// <param name="outColor">Resulting color</param>
        private void GetColor(Vector3 scatterPosition, Vector3 lightDirection, out Color4 outColor)
        {
            float viewerHeight = 1f;
            Vector3 eyePosition = new Vector3(0, viewerHeight, 0);

            float scale = 1.0f / (this.SphereOuterRadius - this.SphereInnerRadius);
            float scaleOverScaleDepth = scale / this.RayleighScaleDepth;
            float rayleighBrightness = this.RayleighScattering * this.Brightness * 0.25f;
            float mieBrightness = this.MieScattering * this.Brightness * 0.25f;

            Vector3 position = scatterPosition / this.PlanetRadius;
            position.Y += this.SphereInnerRadius;

            Vector3 eyeDirection = position - eyePosition;
            float sampleLength = eyeDirection.Length() * 0.5f;
            float scaledLength = sampleLength * this.MieScaleDepth;
            eyeDirection.Normalize();
            Vector3 sampleRay = eyeDirection * sampleLength;
            float startAngle = Vector3.Dot(eyeDirection, eyePosition);

            float scaleDepth = (float)Math.Exp(scaleOverScaleDepth * (this.SphereInnerRadius - viewerHeight));
            float startOffset = scaleDepth * VernierScale(startAngle);

            Vector3 samplePoint = eyePosition + sampleRay * 0.5f;

            Color3 frontColor = Color3.Black;

            for (uint i = 0; i < 2; i++)
            {
                float depth = (float)Math.Exp(scaleOverScaleDepth * (this.SphereInnerRadius - viewerHeight));

                float height = samplePoint.Length();
                float lightAngle = Vector3.Dot(-lightDirection, samplePoint) / height;
                float cameraAngle = Vector3.Dot(eyeDirection, samplePoint) / height;

                float scatter = (startOffset + depth * (VernierScale(lightAngle) - VernierScale(cameraAngle)));

                Color3 attenuate = Color3.Black;
                attenuate[0] = (float)Math.Exp(-scatter * (this.InvWaveLength4[0] * this.RayleighScattering4PI + this.MieScattering4PI));
                attenuate[1] = (float)Math.Exp(-scatter * (this.InvWaveLength4[1] * this.RayleighScattering4PI + this.MieScattering4PI));
                attenuate[2] = (float)Math.Exp(-scatter * (this.InvWaveLength4[2] * this.RayleighScattering4PI + this.MieScattering4PI));

                frontColor += attenuate * depth * scaledLength;

                samplePoint += sampleRay;
            }

            Color3 rayleighColor = frontColor * (this.InvWaveLength4.RGB() * rayleighBrightness);

            Vector3 direction = Vector3.Normalize(eyePosition - position);
            float miePhase = GetMiePhase(Vector3.Dot(-lightDirection, direction));
            Color3 mieColor = frontColor * mieBrightness;

            Color3 color = rayleighColor + (miePhase * mieColor);

            Vector3 expColor = Vector3.Zero;
            expColor.X = 1.0f - (float)Math.Exp(-this.HDRExposure * color.Red);
            expColor.Y = 1.0f - (float)Math.Exp(-this.HDRExposure * color.Green);
            expColor.Z = 1.0f - (float)Math.Exp(-this.HDRExposure * color.Blue);
            expColor.Normalize();

            outColor = new Color4(expColor, 1f);
        }
    }

    /// <summary>
    /// Sky scattering extensions
    /// </summary>
    public static class SkyScatteringExtensions
    {
        /// <summary>
        /// Adds a component to the scene
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Description</param>
        /// <param name="usage">Component usage</param>
        /// <param name="order">Processing order</param>
        /// <returns>Returns the created component</returns>
        public static async Task<SkyScattering> AddComponentSkyScattering(this Scene scene, SkyScatteringDescription description, SceneObjectUsages usage = SceneObjectUsages.None, int order = 0)
        {
            SkyScattering component = null;

            await Task.Run(() =>
            {
                component = new SkyScattering(scene, description);

                scene.AddComponent(component, usage, order);
            });

            return component;
        }
    }
}
