﻿using System;

namespace Engine.UI
{
    /// <summary>
    /// Padding
    /// </summary>
    public struct Padding : IEquatable<Padding>
    {
        /// <summary>
        /// Gets the 0 padding
        /// </summary>
        public static Padding Zero
        {
            get
            {
                return new Padding
                {
                    Left = 0,
                    Top = 0,
                    Bottom = 0,
                    Right = 0,
                };
            }
        }

        /// <summary>
        /// Padding left
        /// </summary>
        public float Left { get; set; }
        /// <summary>
        /// Pading top
        /// </summary>
        public float Top { get; set; }
        /// <summary>
        /// Padding botton
        /// </summary>
        public float Bottom { get; set; }
        /// <summary>
        /// Padding right
        /// </summary>
        public float Right { get; set; }

        /// <inheritdoc/>
        public bool Equals(Padding other)
        {
            return
                other.Left == Left &&
                other.Top == Top &&
                other.Bottom == Bottom &&
                other.Right == Right;
        }
        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Padding padding)
            {
                return Equals(padding);
            }

            return false;
        }
        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
                hashCode = (hashCode * 397) ^ Right.GetHashCode();
                return hashCode;
            }
        }
        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Left: {Left}; Top: {Top}; Bottom: {Bottom}; Right: {Right};";
        }

        public static bool operator ==(Padding left, Padding right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(Padding left, Padding right)
        {
            return !left.Equals(right);
        }

        public static implicit operator Padding(int value)
        {
            return new Padding
            {
                Left = value,
                Top = value,
                Bottom = value,
                Right = value,
            };
        }
        public static implicit operator Padding(int[] value)
        {
            if (value?.Length == 1)
            {
                return new Padding
                {
                    Left = value[0],
                    Top = value[0],
                    Bottom = value[0],
                    Right = value[0],
                };
            }

            if (value?.Length == 2)
            {
                return new Padding
                {
                    Left = value[0],
                    Top = value[1],
                    Bottom = value[1],
                    Right = value[0],
                };
            }

            if (value?.Length == 4)
            {
                return new Padding
                {
                    Left = value[1],
                    Top = value[2],
                    Bottom = value[3],
                    Right = value[4],
                };
            }

            return new Padding
            {
                Left = float.NaN,
                Top = float.NaN,
                Bottom = float.NaN,
                Right = float.NaN,
            };
        }
    }
}
