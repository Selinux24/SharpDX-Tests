﻿using SharpDX;

namespace Engine
{
    using Engine.Common;

    /// <summary>
    /// Drawable object
    /// </summary>
    public abstract class Drawable : Updatable, IDrawable, ICullable
    {
        /// <summary>
        /// Game class
        /// </summary>
        public virtual Game Game { get { return this.Scene.Game; } }
        /// <summary>
        /// Buffer manager
        /// </summary>
        public virtual BufferManager BufferManager { get { return this.Scene.BufferManager; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Description</param>
        public Drawable(Scene scene, SceneObjectDescription description) : base(scene, description)
        {

        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="context">Context</param>
        public abstract void Draw(DrawContext context);

        /// <summary>
        /// Performs culling test
        /// </summary>
        /// <param name="frustum">Frustum</param>
        /// <param name="distance">If the object is inside the volume, returns the distance</param>
        /// <returns>Returns true if the object is outside of the frustum</returns>
        /// <remarks>By default, returns true and distance = float.MaxValue</remarks>
        public virtual bool Cull(BoundingFrustum frustum, out float? distance)
        {
            distance = float.MaxValue;

            return false;
        }
        /// <summary>
        /// Performs culling test
        /// </summary>
        /// <param name="box">Box</param>
        /// <param name="distance">If the object is inside the volume, returns the distance</param>
        /// <returns>Returns true if the object is outside of the box</returns>
        /// <remarks>By default, returns true and distance = float.MaxValue</remarks>
        public virtual bool Cull(BoundingBox box, out float? distance)
        {
            distance = float.MaxValue;

            return false;
        }
        /// <summary>
        /// Performs culling test
        /// </summary>
        /// <param name="sphere">Sphere</param>
        /// <param name="distance">If the object is inside the volume, returns the distance</param>
        /// <returns>Returns true if the object is outside of the sphere</returns>
        /// <remarks>By default, returns true and distance = float.MaxValue</remarks>
        public virtual bool Cull(BoundingSphere sphere, out float? distance)
        {
            distance = float.MaxValue;

            return false;
        }
    }
}
