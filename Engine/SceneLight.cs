﻿using SharpDX;

namespace Engine
{
    /// <summary>
    /// Scene ligth
    /// </summary>
    public abstract class SceneLight : ISceneLight
    {
        /// <summary>
        /// Light name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Enables or disables the light
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Gets or sets whether the light casts shadow
        /// </summary>
        public bool CastShadow { get; set; }
        /// <summary>
        /// Diffuse color
        /// </summary>
        public Color4 DiffuseColor { get; set; }
        /// <summary>
        /// Specular color
        /// </summary>
        public Color4 SpecularColor { get; set; }
        /// <summary>
        /// Free use variable
        /// </summary>
        public virtual object State { get; set; }
        /// <summary>
        /// Parent local transform matrix
        /// </summary>
        /// <summary>
        /// Parent local transform matrix
        /// </summary>
        public virtual Matrix ParentTransform { get; set; } = Matrix.Identity;
        /// <summary>
        /// First shadow map index
        /// </summary>
        public int ShadowMapIndex { get; set; } = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        protected SceneLight()
        {

        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Light name</param>
        /// <param name="castShadow">Light casts shadow</param>
        /// <param name="diffuse">Diffuse color contribution</param>
        /// <param name="specular">Specular color contribution</param>
        /// <param name="enabled">Lights is enabled</param>
        protected SceneLight(string name, bool castShadow, Color4 diffuse, Color4 specular, bool enabled)
        {
            this.Name = name;
            this.Enabled = enabled;
            this.CastShadow = castShadow;
            this.DiffuseColor = diffuse;
            this.SpecularColor = specular;
        }

        /// <summary>
        /// Clears all light shadow parameters
        /// </summary>
        public virtual void ClearShadowParameters()
        {
            this.ShadowMapIndex = -1;
        }
        /// <summary>
        /// Clones current light
        /// </summary>
        /// <returns>Returns a new instante with same data</returns>
        public abstract ISceneLight Clone();

        /// <summary>
        /// Gets the text representation of the light
        /// </summary>
        /// <returns>Returns the text representation of the light</returns>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(this.Name))
            {
                return string.Format("{0}; Enabled: {1}", this.Name, this.Enabled);
            }
            else
            {
                return string.Format("{0}; Enabled {1}", this.GetType(), this.Enabled);
            }
        }
    }
}
