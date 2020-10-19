﻿using SharpDX;
using System.Collections.Generic;

namespace Engine
{
    /// <summary>
    /// Point light
    /// </summary>
    public class SceneLightPoint : SceneLight, ISceneLightPoint
    {
        /// <summary>
        /// Initial transform
        /// </summary>
        private Matrix initialTransform = Matrix.Identity;
        /// <summary>
        /// Initial radius
        /// </summary>
        private float initialRadius = 1f;
        /// <summary>
        /// Initial intensity
        /// </summary>
        private float initialIntensity = 1f;

        /// <summary>
        /// Ligth position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Light radius
        /// </summary>
        public float Radius { get; set; }
        /// <summary>
        /// Intensity
        /// </summary>
        public float Intensity { get; set; }
        /// <summary>
        /// Gets the bounding sphere of the active light
        /// </summary>
        public BoundingSphere BoundingSphere
        {
            get
            {
                return new BoundingSphere(this.Position, this.Radius);
            }
        }
        /// <summary>
        /// Parent local transform matrix
        /// </summary>
        public override Matrix ParentTransform
        {
            get
            {
                return base.ParentTransform;
            }
            set
            {
                base.ParentTransform = value;

                this.UpdateLocalTransform();
            }
        }
        /// <summary>
        /// Local matrix
        /// </summary>
        public Matrix Local
        {
            get
            {
                return Matrix.Scaling(this.Radius) * Matrix.Translation(this.Position);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected SceneLightPoint()
            : base()
        {

        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Light name</param>
        /// <param name="castShadow">Light casts shadow</param>
        /// <param name="diffuse">Diffuse color contribution</param>
        /// <param name="specular">Specular color contribution</param>
        /// <param name="enabled">Light is enabled</param>
        /// <param name="description">Light description</param>
        public SceneLightPoint(
            string name, bool castShadow, Color4 diffuse, Color4 specular, bool enabled,
            SceneLightPointDescription description)
            : base(name, castShadow, diffuse, specular, enabled)
        {
            this.initialTransform = description.Transform;
            this.initialRadius = description.Radius;
            this.initialIntensity = description.Intensity;

            this.UpdateLocalTransform();
        }

        /// <summary>
        /// Updates local transform
        /// </summary>
        private void UpdateLocalTransform()
        {
            var trn = this.initialTransform * base.ParentTransform;

            trn.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation);
            this.Radius = initialRadius * scale.X;
            this.Intensity = initialIntensity * scale.X;
            this.Position = translation;
        }

        /// <summary>
        /// Clones current light
        /// </summary>
        /// <returns>Returns a new instante with same data</returns>
        public override ISceneLight Clone()
        {
            return new SceneLightPoint()
            {
                Name = this.Name,
                Enabled = this.Enabled,
                CastShadow = this.CastShadow,
                DiffuseColor = this.DiffuseColor,
                SpecularColor = this.SpecularColor,
                State = this.State,

                Position = this.Position,
                Radius = this.Radius,
                Intensity = this.Intensity,

                initialTransform = this.initialTransform,
                initialRadius = this.initialRadius,
                initialIntensity = this.initialIntensity,

                ParentTransform = this.ParentTransform,
            };
        }
        /// <summary>
        /// Gets the light volume
        /// </summary>
        /// <param name="sliceCount">Sphere slice count (vertical subdivisions - meridians)</param>
        /// <param name="stackCount">Sphere stack count (horizontal subdivisions - parallels)</param>
        /// <returns>Returns a line list representing the light volume</returns>
        public IEnumerable<Line3D> GetVolume(int sliceCount, int stackCount)
        {
            return Line3D.CreateWiredSphere(this.BoundingSphere, sliceCount, stackCount);
        }
    }
}
