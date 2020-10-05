﻿using SharpDX;
using System;

namespace Engine.Animation
{
    /// <summary>
    /// Bone animation
    /// </summary>
    public struct JointAnimation : IEquatable<JointAnimation>
    {
        /// <summary>
        /// Joint name
        /// </summary>
        public readonly string Joint;
        /// <summary>
        /// Keyframe list
        /// </summary>
        public readonly Keyframe[] Keyframes;
        /// <summary>
        /// Start time
        /// </summary>
        public readonly float StartTime;
        /// <summary>
        /// End time
        /// </summary>
        public readonly float EndTime;
        /// <summary>
        /// Animation duration
        /// </summary>
        public float Duration
        {
            get
            {
                return EndTime - StartTime;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public JointAnimation(string jointName, Keyframe[] keyframes)
        {
            Joint = jointName;
            Keyframes = keyframes;
            StartTime = keyframes[0].Time;
            EndTime = keyframes[keyframes.Length - 1].Time;

            //Pre-normalize rotations
            for (int i = 0; i < Keyframes.Length; i++)
            {
                Keyframes[i].Rotation.Normalize();
            }
        }

        /// <summary>
        /// Interpolate bone transformation
        /// </summary>
        /// <param name="time">Time</param>
        /// <returns>Return interpolated transformation</returns>
        public Matrix Interpolate(float time)
        {
            Interpolate(time, out Vector3 translation, out Quaternion rotation, out Vector3 scale);

            //Create the combined transformation matrix
            return
                Matrix.Scaling(scale) *
                Matrix.RotationQuaternion(rotation) *
                Matrix.Translation(translation);
        }
        /// <summary>
        /// Interpolate bone transformation
        /// </summary>
        /// <param name="time">Time</param>
        /// <param name="translation">Gets the interpolated translation</param>
        /// <param name="rotation">Gets the interpolated rotation</param>
        /// <param name="scale">Gets the interpolated scale</param>
        public void Interpolate(float time, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            translation = Vector3.Zero;
            rotation = Quaternion.Identity;
            scale = Vector3.One;

            if (Keyframes != null)
            {
                var deltaTime = 0.0f;
                if (Duration > 0.0f)
                {
                    deltaTime = time % Duration;
                }

                var currFrame = 0;
                while (currFrame < Keyframes.Length - 1)
                {
                    if (deltaTime < Keyframes[currFrame + 1].Time)
                    {
                        break;
                    }
                    currFrame++;
                }

                if (currFrame >= Keyframes.Length)
                {
                    currFrame = 0;
                }

                var nextFrame = (currFrame + 1) % Keyframes.Length;

                var currKey = Keyframes[currFrame];
                var nextKey = Keyframes[nextFrame];

                var diffTime = nextKey.Time - currKey.Time;
                if (diffTime < 0.0)
                {
                    diffTime += Duration;
                }

                if (diffTime > 0.0)
                {
                    //Interpolate
                    var factor = (deltaTime - currKey.Time) / diffTime;

                    translation = currKey.Translation + (nextKey.Translation - currKey.Translation) * factor;
                    rotation = Quaternion.Slerp(currKey.Rotation, nextKey.Rotation, factor);
                    scale = currKey.Scale + (nextKey.Scale - currKey.Scale) * factor;
                }
                else
                {
                    //Use current frame
                    translation = currKey.Translation;
                    rotation = currKey.Rotation;
                    scale = currKey.Scale;
                }
            }
        }
        /// <summary>
        /// Gets text representation
        /// </summary>
        /// <returns>Returns text representation</returns>
        public override string ToString()
        {
            return $"Start: {StartTime:0.00000}; End: {EndTime:0.00000}; Keyframes: {Keyframes.Length}";
        }
        /// <summary>
        /// Gets whether the current instance is equal to the other instance
        /// </summary>
        /// <param name="other">The other instance</param>
        /// <returns>Returns true if both instances are equal</returns>
        public bool Equals(JointAnimation other)
        {
            return
                Joint == other.Joint &&
                Helper.ListIsEqual(Keyframes, other.Keyframes) &&
                StartTime == other.StartTime &&
                EndTime == other.EndTime;
        }
    }
}
