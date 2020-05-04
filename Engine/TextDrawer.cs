﻿using SharpDX;
using System.Threading.Tasks;

namespace Engine
{
    using Engine.Common;
    using Engine.Effects;

    /// <summary>
    /// Text drawer
    /// </summary>
    public class TextDrawer : Drawable, IScreenFitted
    {
        /// <summary>
        /// Vertex buffer descriptor
        /// </summary>
        private readonly BufferDescriptor vertexBuffer = null;
        /// <summary>
        /// Index buffer descriptor
        /// </summary>
        private readonly BufferDescriptor indexBuffer = null;
        /// <summary>
        /// Index count
        /// </summary>
        private int indexDrawCount = 0;
        /// <summary>
        /// Vertices
        /// </summary>
        private VertexPositionTexture[] vertices;
        /// <summary>
        /// Indices
        /// </summary>
        private uint[] indices;
        /// <summary>
        /// Update buffers flag
        /// </summary>
        private bool updateBuffers = false;
        /// <summary>
        /// Text translation matrix
        /// </summary>
        private Matrix local;
        /// <summary>
        /// Text shadow translarion matrix
        /// </summary>
        private Matrix localShadow;

        /// <summary>
        /// Font map
        /// </summary>
        private FontMap fontMap = null;
        /// <summary>
        /// View * projection matrix
        /// </summary>
        private Matrix viewProjection;
        /// <summary>
        /// Text position in 2D screen
        /// </summary>
        private Vector2 position = Vector2.Zero;
        /// <summary>
        /// Text area rectangle
        /// </summary>
        private Rectangle? rectangle = null;
        /// <summary>
        /// The text draws vertically centered on screen
        /// </summary>
        private bool centerVertically = false;
        /// <summary>
        /// The text draws horizontally centered on screen
        /// </summary>
        private bool centerHorizontally = false;
        /// <summary>
        /// Text
        /// </summary>
        private string text = null;

        /// <summary>
        /// Font name
        /// </summary>
        public readonly string Font = null;
        /// <summary>
        /// Gets or sets text to draw
        /// </summary>
        public string Text
        {
            get
            {
                return this.text;
            }
            set
            {
                if (!string.Equals(this.text, value))
                {
                    this.text = value;

                    this.MapText();
                }
            }
        }
        /// <summary>
        /// Gets character count
        /// </summary>
        public int CharacterCount
        {
            get
            {
                if (!string.IsNullOrEmpty(this.text))
                {
                    return this.text.Length;
                }
                else
                {
                    return 0;
                }
            }
        }
        /// <summary>
        /// Gets or sets text fore color
        /// </summary>
        public Color4 TextColor { get; set; }
        /// <summary>
        /// Gets or sets text shadow color
        /// </summary>
        public Color4 ShadowColor { get; set; }
        /// <summary>
        /// Gets or sets relative position of shadow
        /// </summary>
        public Vector2 ShadowDelta { get; set; }
        /// <summary>
        /// Gets or sets the text color alpha multiplier
        /// </summary>
        public float AlphaMultplier { get; set; } = 1.2f;

        /// <summary>
        /// Gets or sest text position in 2D screen
        /// </summary>
        public Vector2 Position
        {
            get
            {
                return this.position;
            }
            set
            {
                this.position = value;

                this.MapText();
                this.UpdatePosition();
            }
        }
        /// <summary>
        /// Gets or sets text left position in 2D screen
        /// </summary>
        public int Left
        {
            get
            {
                return (int)this.position.X;
            }
            set
            {
                this.position.X = value;

                if (this.rectangle.HasValue)
                {
                    var rect = this.rectangle.Value;
                    rect.Left = (int)this.position.X;
                }

                this.UpdatePosition();
            }
        }
        /// <summary>
        /// Gets or sets text top position in 2D screen
        /// </summary>
        public int Top
        {
            get
            {
                return (int)this.position.Y;
            }
            set
            {
                this.position.Y = value;

                if (this.rectangle.HasValue)
                {
                    var rect = this.rectangle.Value;
                    rect.Top = (int)this.position.Y;
                }

                this.UpdatePosition();
            }
        }
        /// <summary>
        /// Gets text width
        /// </summary>
        public int Width { get; private set; }
        /// <summary>
        /// Gets text height
        /// </summary>
        public int Height { get; private set; }
        /// <summary>
        /// Gets or sets the rectangle were the text must be drawn
        /// </summary>
        public Rectangle? Rectangle
        {
            get
            {
                return this.rectangle;
            }
            set
            {
                this.rectangle = value;

                this.MapText();
                this.UpdatePosition();
            }
        }
        /// <summary>
        /// Gets whether the internal buffers were ready or not
        /// </summary>
        public bool BuffersReady
        {
            get
            {
                return this.vertexBuffer?.Ready == true && this.indexBuffer?.Ready == true;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Text description</param>
        public TextDrawer(Scene scene, TextDrawerDescription description)
            : base(scene, description)
        {
            this.Font = string.Format("{0} {1}", description.Font, description.FontSize);

            this.viewProjection = Sprite.CreateViewOrthoProjection(
                this.Game.Form.RenderWidth.NextPair(),
                this.Game.Form.RenderHeight.NextPair());

            this.fontMap = FontMap.Map(this.Game, description.Font, description.FontSize, description.Style);

            VertexPositionTexture[] verts = new VertexPositionTexture[FontMap.MAXTEXTLENGTH * 4];
            uint[] idx = new uint[FontMap.MAXTEXTLENGTH * 6];

            this.vertexBuffer = this.BufferManager.AddVertexData(description.Name, true, verts);
            this.indexBuffer = this.BufferManager.AddIndexData(description.Name, true, idx);

            this.TextColor = description.TextColor;
            this.ShadowColor = description.ShadowColor;
            this.ShadowDelta = description.ShadowDelta;

            this.MapText();
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~TextDrawer()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <summary>
        /// Dispose
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Remove data from buffer manager
                this.BufferManager?.RemoveVertexData(this.vertexBuffer);
                this.BufferManager?.RemoveIndexData(this.indexBuffer);

                //Remove the font map reference
                this.fontMap = null;
            }
        }

        /// <summary>
        /// Draw text
        /// </summary>
        /// <param name="context">Context</param>
        public override void Draw(DrawContext context)
        {
            if (!BuffersReady)
            {
                return;
            }

            var mode = context.DrawerMode;

            if (mode.HasFlag(DrawerModes.TransparentOnly) && !string.IsNullOrWhiteSpace(this.text))
            {
                var graphics = this.Game.Graphics;

                if (this.updateBuffers)
                {
                    this.BufferManager.WriteVertexBuffer(this.vertexBuffer, this.vertices);
                    this.BufferManager.WriteIndexBuffer(this.indexBuffer, this.indices);

                    this.indexDrawCount = this.indices.Length;

                    this.updateBuffers = false;
                }

                this.BufferManager.SetIndexBuffer(this.indexBuffer);

                var effect = DrawerPool.EffectDefaultFont;
                var technique = effect.FontDrawer;

                this.BufferManager.SetInputAssembler(technique, this.vertexBuffer, Topology.TriangleList);

                graphics.SetBlendDefaultAlpha();
                graphics.SetDepthStencilZDisabled();

                if (this.ShadowColor != Color.Transparent)
                {
                    //Draw shadow
                    this.DrawText(effect, technique, this.localShadow, this.ShadowColor);
                }

                //Draw text
                this.DrawText(effect, technique, this.local, this.TextColor);
            }
        }
        /// <summary>
        /// Resize
        /// </summary>
        public void Resize()
        {
            this.viewProjection = Sprite.CreateViewOrthoProjection(
                this.Game.Form.RenderWidth.NextPair(),
                this.Game.Form.RenderHeight.NextPair());
        }
        /// <summary>
        /// Centers vertically the text
        /// </summary>
        public void CenterVertically()
        {
            this.centerVertically = true;

            this.UpdatePosition();
        }
        /// <summary>
        /// Centers horinzontally the text
        /// </summary>
        public void CenterHorizontally()
        {
            this.centerHorizontally = true;

            this.UpdatePosition();
        }

        /// <summary>
        /// Draw text
        /// </summary>
        /// <param name="effect">Effect</param>
        /// <param name="technique">Technique</param>
        /// <param name="local">Local transform</param>
        /// <param name="color">Text color</param>
        private void DrawText(EffectDefaultFont effect, EngineEffectTechnique technique, Matrix local, Color4 color)
        {
            effect.UpdatePerFrame(
                local,
                this.viewProjection,
                color.RGB(),
                color.Alpha * AlphaMultplier,
                this.fontMap.Texture);

            var graphics = this.Game.Graphics;

            for (int p = 0; p < technique.PassCount; p++)
            {
                graphics.EffectPassApply(technique, p, 0);

                graphics.DrawIndexed(this.indexDrawCount, this.indexBuffer.BufferOffset, this.vertexBuffer.BufferOffset);
            }
        }
        /// <summary>
        /// Map text
        /// </summary>
        private void MapText()
        {
            var rect = rectangle ?? new Rectangle(0, 0, this.Game.Form.RenderWidth, this.Game.Form.RenderHeight);

            this.fontMap.MapSentence(
                this.text,
                rect,
                out this.vertices, out this.indices, out Vector2 size);

            this.Width = (int)size.X;
            this.Height = (int)size.Y;

            this.updateBuffers = true;
        }
        /// <summary>
        /// Update text translation matrices
        /// </summary>
        private void UpdatePosition()
        {
            float x;
            if (this.centerHorizontally)
            {
                x = -(this.Width * 0.5f);
            }
            else
            {
                x = +this.position.X - this.Game.Form.RelativeCenter.X;
            }

            float y;
            if (this.centerVertically)
            {
                y = +(this.Height * 0.5f);
            }
            else
            {
                y = -this.position.Y + this.Game.Form.RelativeCenter.Y;
            }

            this.local = Matrix.Translation((int)x, (int)y, 0f);

            x += this.ShadowDelta.X;
            y += this.ShadowDelta.Y;

            this.localShadow = Matrix.Translation((int)x, (int)y, 0f);
        }
    }

    /// <summary>
    /// Text drawer extensions
    /// </summary>
    public static class TextDrawerExtensions
    {
        /// <summary>
        /// Adds a component to the scene
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Description</param>
        /// <param name="usage">Component usage</param>
        /// <param name="order">Processing order</param>
        /// <returns>Returns the created component</returns>
        public static async Task<TextDrawer> AddComponentTextDrawer(this Scene scene, TextDrawerDescription description, SceneObjectUsages usage = SceneObjectUsages.None, int order = 0)
        {
            TextDrawer component = null;

            await Task.Run(() =>
            {
                component = new TextDrawer(scene, description);

                scene.AddComponent(component, usage, order);
            });

            return component;
        }
    }
}
