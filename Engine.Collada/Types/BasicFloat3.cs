﻿using System;

namespace Engine.Collada.Types
{
    [Serializable]
    public class BasicFloat3 : BasicFloatArray
    {
        public BasicFloat3()
        {

        }

        public override string ToString()
        {
            if (this.Values != null && this.Values.Length == 3)
            {
                return string.Format("({0}, {1}, {2})", this.Values[0], this.Values[1], this.Values[2]);
            }
            else
            {
                return base.ToString();
            }
        }
    }
}
