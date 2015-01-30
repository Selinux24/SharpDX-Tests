﻿using System.Collections.Generic;
using SharpDX;

namespace Engine
{
    using Engine.Common;
    using Engine.Content;

    /// <summary>
    /// Line list drawer
    /// </summary>
    public class LineListDrawer : Model
    {
        /// <summary>
        /// Line list mesh
        /// </summary>
        private Mesh lineListMesh
        {
            get
            {
                return this.Meshes[ModelContent.StaticMesh][ModelContent.NoMaterial];
            }
        }
        /// <summary>
        /// Lines dictionary by color
        /// </summary>
        private Dictionary<Color4, List<Line>> lineDictionary = new Dictionary<Color4, List<Line>>();
        /// <summary>
        /// Dictionary changes flag
        /// </summary>
        private bool dictionaryChanged = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        /// <param name="scene">Scene</param>
        /// <param name="lines">Line list</param>
        /// <param name="color">Color</param>
        public LineListDrawer(Game game, Scene3D scene, Line[] lines, Color4 color)
            : base(game, scene, ModelContent.GenerateLineList(lines, color))
        {
            this.EnableAlphaBlending = true;

            this.lineDictionary.Add(color, new List<Line>(lines));

            this.dictionaryChanged = true;
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        /// <param name="scene">Scene</param>
        /// <param name="count">Maximum line count</param>
        public LineListDrawer(Game game, Scene3D scene, int count)
            : base(game, scene, ModelContent.GenerateLineList(new Line[count], Color.Transparent))
        {
            this.EnableAlphaBlending = true;

            this.dictionaryChanged = false;
        }
        /// <summary>
        /// Draw content
        /// </summary>
        /// <param name="gameTime">Game time</param>
        public override void Draw(GameTime gameTime)
        {
            this.WriteDataInBuffer();

            base.Draw(gameTime);
        }
        /// <summary>
        /// Performs frustum culling test
        /// </summary>
        /// <remarks>Culling disabled for this class</remarks>
        public override void FrustumCulling()
        {
            this.Cull = false;
        }

        /// <summary>
        /// Set line list
        /// </summary>
        /// <param name="color">Color</param>
        /// <param name="lines">Line list</param>
        public void SetLines(Color4 color, Line[] lines)
        {
            if (lines != null && lines.Length > 0)
            {
                if (!this.lineDictionary.ContainsKey(color))
                {
                    this.lineDictionary.Add(color, new List<Line>());
                }
                else
                {
                    this.lineDictionary[color].Clear();
                }

                this.lineDictionary[color].AddRange(lines);

                this.dictionaryChanged = true;
            }
            else
            {
                if (this.lineDictionary.ContainsKey(color))
                {
                    this.lineDictionary.Remove(color);

                    this.dictionaryChanged = true;
                }
            }
        }
        /// <summary>
        /// Add lines to list
        /// </summary>
        /// <param name="color">Color</param>
        /// <param name="lines">Line list</param>
        public void AddLines(Color4 color, Line[] lines)
        {
            if (!this.lineDictionary.ContainsKey(color))
            {
                this.lineDictionary.Add(color, new List<Line>());
            }

            this.lineDictionary[color].AddRange(lines);

            this.dictionaryChanged = true;
        }
        /// <summary>
        /// Remove by color
        /// </summary>
        /// <param name="color">Color</param>
        public void ClearLines(Color4 color)
        {
            if (this.lineDictionary.ContainsKey(color))
            {
                this.lineDictionary.Remove(color);
            }

            this.dictionaryChanged = true;
        }
        /// <summary>
        /// Remove all
        /// </summary>
        public void ClearLines()
        {
            this.lineDictionary.Clear();

            this.dictionaryChanged = true;
        }
        /// <summary>
        /// Writes dictionary data in buffer
        /// </summary>
        public void WriteDataInBuffer()
        {
            if (this.dictionaryChanged)
            {
                List<IVertexData> data = new List<IVertexData>();

                foreach (Color4 color in this.lineDictionary.Keys)
                {
                    List<Line> lines = this.lineDictionary[color];
                    if (lines.Count > 0)
                    {
                        for (int i = 0; i < lines.Count; i++)
                        {
                            data.Add(new VertexPositionColor() { Position = lines[i].Point1, Color = color });
                            data.Add(new VertexPositionColor() { Position = lines[i].Point2, Color = color });
                        }
                    }
                }

                this.lineListMesh.WriteVertexData(this.DeviceContext, data.ToArray());

                this.dictionaryChanged = false;
            }
        }
    }
}
