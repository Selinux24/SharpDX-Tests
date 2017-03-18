﻿using SharpDX;

namespace Engine
{
    using Engine.Common;

    /// <summary>
    /// Particle emitter
    /// </summary>
    public class ParticleEmitter
    {
        /// <summary>
        /// Emitter position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Emitter velocity
        /// </summary>
        public Vector3 Velocity { get; set; }
        /// <summary>
        /// Emission rate
        /// </summary>
        public float EmissionRate { get; set; }
        /// <summary>
        /// Emitter duration
        /// </summary>
        public float Duration { get; set; }
        /// <summary>
        /// Gets or sets wheter the emitter duration is infinite
        /// </summary>
        public bool InfiniteDuration { get; set; }
        /// <summary>
        /// Gets or sets the maximum distance from camera
        /// </summary>
        public float MaximumDistance { get; set; }
        /// <summary>
        /// Distance from camera
        /// </summary>
        public float Distance { get; set; }
        /// <summary>
        /// Gets wheter the emitter is active
        /// </summary>
        public bool Active
        {
            get
            {
                return (this.InfiniteDuration || this.Duration > 0);
            }
        }
        /// <summary>
        /// Gets wheter the emitter particles is visible
        /// </summary>
        public bool Visible
        {
            get
            {
                return this.Distance <= this.MaximumDistance;
            }
        }

        /// <summary>
        /// Total particle system time
        /// </summary>
        public float TotalTime { get; private set; }
        /// <summary>
        /// Elapsed time
        /// </summary>
        public float ElapsedTime { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ParticleEmitter()
        {
            this.Position = Vector3.Zero;
            this.Velocity = Vector3.Up;
            this.EmissionRate = 1f;
            this.Duration = 0f;
            this.InfiniteDuration = false;
            this.MaximumDistance = GameEnvironment.LODDistanceLow;
            this.Distance = 0f;
        }

        /// <summary>
        /// Updates internal state
        /// </summary>
        /// <param name="context">Updating context</param>
        public virtual void Update(UpdateContext context)
        {
            this.ElapsedTime = context.GameTime.ElapsedSeconds;

            this.TotalTime += this.ElapsedTime;

            if (!this.InfiniteDuration)
            {
                this.Duration -= this.ElapsedTime;
            }

            this.Distance = Vector3.Distance(this.Position, context.EyePosition);
        }
  
        /// <summary>
        /// Gets the maximum number of particles running at the same time
        /// </summary>
        /// <param name="maxParticleDuration">Maximum particle duration</param>
        /// <returns>Returns the maximum number of particles running at the same time</returns>
        public int GetMaximumConcurrentParticles(float maxParticleDuration)
        {
            float maxActiveParticles = maxParticleDuration * (1f / this.EmissionRate);

            return (int)(maxActiveParticles != (int)maxActiveParticles ? maxActiveParticles + 1 : maxActiveParticles);
        }
    }
}
