﻿using System.Collections.Generic;

namespace Engine
{
    using Engine.Collections.Generic;
    using Engine.Content;

    /// <summary>
    /// Ground description
    /// </summary>
    public class GroundDescription : SceneObjectDescription
    {
        /// <summary>
        /// Gets a ground description from data
        /// </summary>
        /// <param name="heightmap">Height map</param>
        /// <param name="cellSize">Cell size</param>
        /// <param name="maximumHeight">Maximum height</param>
        /// <param name="heightCurve">Height curve</param>
        /// <param name="textures">Heighmap textures</param>
        /// <param name="quadtreeDepth">Quadtree depth</param>
        public static GroundDescription FromHeightmap(NoiseMap heightmap, float cellSize, float maximumHeight, Curve heightCurve, HeightmapTexturesDescription textures, int quadtreeDepth = 3)
        {
            return new GroundDescription()
            {
                QuadtreeDescription = QuadtreeDescription.Default(quadtreeDepth),
                HeightmapDescription = HeightmapDescription.FromMap(heightmap, cellSize, maximumHeight, heightCurve, textures),
            };
        }
        /// <summary>
        /// Gets a ground description heightmap description
        /// </summary>
        /// <param name="description">Heightmap description</param>
        /// <param name="quadtreeDepth">Quadtree depth</param>
        public static GroundDescription FromHeightmapDescription(HeightmapDescription description, int quadtreeDepth = 3)
        {
            return new GroundDescription()
            {
                QuadtreeDescription = QuadtreeDescription.Default(quadtreeDepth),
                HeightmapDescription = description,
            };
        }
        /// <summary>
        /// Gets a ground description from a file
        /// </summary>
        /// <param name="contentFolder">Content folder</param>
        /// <param name="fileName">File name</param>
        /// <param name="quadtreeDepth">Quadtree depth</param>
        public static GroundDescription FromFile(string contentFolder, string fileName, int quadtreeDepth = 3)
        {
            return new GroundDescription()
            {
                QuadtreeDescription = QuadtreeDescription.Default(quadtreeDepth),
                ContentDescription = ContentDescription.FromFile(contentFolder, fileName),
            };
        }

        /// <summary>
        /// Heightmap description
        /// </summary>
        public HeightmapDescription HeightmapDescription { get; set; }
        /// <summary>
        /// Content
        /// </summary>
        public ContentDescription ContentDescription { get; set; }
        /// <summary>
        /// Quadtree
        /// </summary>
        public QuadtreeDescription QuadtreeDescription { get; set; }
        /// <summary>
        /// Use anisotropic filtering
        /// </summary>
        public bool UseAnisotropic { get; set; } = true;
        /// <summary>
        /// Collision detection mode
        /// </summary>
        public CollisionDetectionMode CollisionDetection { get; set; } = CollisionDetectionMode.Default;

        /// <summary>
        /// Constructor
        /// </summary>
        public GroundDescription()
            : base()
        {
            this.CastShadow = true;
        }

        /// <summary>
        /// Reads a model content from description
        /// </summary>
        public ModelContent ReadModelContent()
        {
            // Read model content
            if (HeightmapDescription != null)
            {
                return HeightmapDescription.ReadModelContent();
            }
            else if (ContentDescription != null)
            {
                return ContentDescription.ReadModelContent();
            }
            else
            {
                throw new EngineException("No geometry found in description.");
            }
        }
        /// <summary>
        /// Reads a quadtree from description
        /// </summary>
        /// <typeparam name="T">Quadtree item type</typeparam>
        /// <param name="items">Quadtree items</param>
        public PickingQuadTree<T> ReadQuadTree<T>(IEnumerable<T> items) where T : IVertexList, IRayIntersectable
        {
            if (QuadtreeDescription != null)
            {
                return new PickingQuadTree<T>(items, QuadtreeDescription);
            }

            return null;
        }
    }
}
