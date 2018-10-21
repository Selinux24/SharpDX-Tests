﻿using System;
using System.Xml.Serialization;

namespace Engine.Collada
{
    using global::Engine.Collada.Types;

    [Serializable]
    public class Point
    {
        [XmlElement("color", typeof(BasicColor))]
        public BasicColor Color { get; set; }
        [XmlElement("constant_attenuation", typeof(BasicFloat))]
        public BasicFloat ConstantAttenuation { get; set; }
        [XmlElement("linear_attenuation", typeof(BasicFloat))]
        public BasicFloat LinearAttenuation { get; set; }
        [XmlElement("quadratic_attenuation", typeof(BasicFloat))]
        public BasicFloat QuadraticAttenuation { get; set; }

        public Point()
        {
            this.ConstantAttenuation = new BasicFloat(1);
            this.LinearAttenuation = new BasicFloat(0);
            this.QuadraticAttenuation = new BasicFloat(0);
        }
    }
}