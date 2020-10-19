﻿using System.Collections.Generic;

namespace Engine.Common
{
    using Engine.Content;

    /// <summary>
    /// Model base description
    /// </summary>
    public abstract class BaseModelDescription : SceneObjectDescription
    {
        /// <summary>
        /// Optimize geometry
        /// </summary>
        public bool Optimize { get; set; } = true;
        /// <summary>
        /// Instancing model
        /// </summary>
        public bool Instanced { get; protected set; }
        /// <summary>
        /// Instances
        /// </summary>
        public int Instances { get; set; } = 1;
        /// <summary>
        /// Load animation
        /// </summary>
        public bool LoadAnimation { get; set; } = true;
        /// <summary>
        /// Load normal maps
        /// </summary>
        public bool LoadNormalMaps { get; set; } = true;
        /// <summary>
        /// Use anisotropic filtering
        /// </summary>
        public bool UseAnisotropicFiltering { get; set; } = false;
        /// <summary>
        /// Dynamic buffers
        /// </summary>
        public bool Dynamic { get; set; } = false;
        /// <summary>
        /// Content info
        /// </summary>
        public ContentDescription Content { get; set; } = new ContentDescription();

        /// <summary>
        /// Constructor
        /// </summary>
        protected BaseModelDescription()
            : base()
        {
            this.Instanced = false;
        }

        /// <summary>
        /// Reads a model content from description
        /// </summary>
        public IEnumerable<ModelContent> ReadModelContent()
        {
            // Read model content
            if (Content != null)
            {
                return Content.ReadModelContent();
            }
            else
            {
                throw new EngineException("No geometry found in description.");
            }
        }
    }
}
