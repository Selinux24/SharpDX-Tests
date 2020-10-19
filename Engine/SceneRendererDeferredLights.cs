﻿using SharpDX.Direct3D;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine
{
    using Engine.Common;
    using Engine.Effects;
    using SharpDX.Direct3D11;

    /// <summary>
    /// Light drawer for deferred renderer
    /// </summary>
    class SceneRendererDeferredLights : IDisposable
    {
        /// <summary>
        /// Render helper geometry buffer slot
        /// </summary>
        public static int BufferSlot { get; set; } = 0;

        /// <summary>
        /// Light geometry
        /// </summary>
        struct LightGeometry
        {
            /// <summary>
            /// Geometry offset
            /// </summary>
            public int Offset;
            /// <summary>
            /// Index count
            /// </summary>
            public int IndexCount;
        }

        /// <summary>
        /// Window vertex buffer
        /// </summary>
        private Buffer lightGeometryVertexBuffer;
        /// <summary>
        /// Vertex buffer binding
        /// </summary>
        private VertexBufferBinding lightGeometryVertexBufferBinding;
        /// <summary>
        /// Window index buffer
        /// </summary>
        private Buffer lightGeometryIndexBuffer;
        /// <summary>
        /// Input layout for directional and hemispheric lights
        /// </summary>
        private InputLayout globalLightInputLayout;
        /// <summary>
        /// Input layout for point lights
        /// </summary>
        private InputLayout pointLightInputLayout;
        /// <summary>
        /// Input layout for spot ligths
        /// </summary>
        private InputLayout spotLightInputLayout;
        /// <summary>
        /// Input layout for result light map
        /// </summary>
        private InputLayout combineLightsInputLayout;
        /// <summary>
        /// Screen geometry
        /// </summary>
        private LightGeometry screenGeometry;
        /// <summary>
        /// Point light geometry
        /// </summary>
        private LightGeometry pointLightGeometry;
        /// <summary>
        /// Spot ligth geometry
        /// </summary>
        private LightGeometry spotLightGeometry;
        /// <summary>
        /// Stencil pass rasterizer (No Cull, No depth limit)
        /// </summary>
        private EngineRasterizerState rasterizerStencilPass = null;
        /// <summary>
        /// Lighting pass rasterizer (Cull Front faces, No depth limit)
        /// </summary>
        private EngineRasterizerState rasterizerLightingPass = null;
        /// <summary>
        /// Depth stencil state for volume marking
        /// </summary>
        private EngineDepthStencilState depthStencilVolumeMarking = null;
        /// <summary>
        /// Depth stencil state for volume drawing
        /// </summary>
        private EngineDepthStencilState depthStencilVolumeDrawing = null;

        /// <summary>
        /// Graphics
        /// </summary>
        protected Graphics Graphics = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="graphics">Graphics</param>
        public SceneRendererDeferredLights(Graphics graphics)
        {
            this.Graphics = graphics;

            this.globalLightInputLayout = graphics.CreateInputLayout(DrawerPool.EffectDeferredComposer.DeferredDirectionalLight.GetSignature(), VertexPosition.Input(BufferSlot));
            this.pointLightInputLayout = graphics.CreateInputLayout(DrawerPool.EffectDeferredComposer.DeferredPointLight.GetSignature(), VertexPosition.Input(BufferSlot));
            this.spotLightInputLayout = graphics.CreateInputLayout(DrawerPool.EffectDeferredComposer.DeferredSpotLight.GetSignature(), VertexPosition.Input(BufferSlot));
            this.combineLightsInputLayout = graphics.CreateInputLayout(DrawerPool.EffectDeferredComposer.DeferredCombineLights.GetSignature(), VertexPosition.Input(BufferSlot));

            //Stencil pass rasterizer state
            this.rasterizerStencilPass = EngineRasterizerState.StencilPass(graphics);

            //Counter clockwise cull rasterizer state
            this.rasterizerLightingPass = EngineRasterizerState.LightingPass(graphics);

            //Depth-stencil state for volume marking (Value != 0 if object is inside of the current drawing volume)
            this.depthStencilVolumeMarking = EngineDepthStencilState.VolumeMarking(graphics);

            //Depth-stencil state for volume drawing (Process pixels if stencil value != stencil reference)
            this.depthStencilVolumeDrawing = EngineDepthStencilState.VolumeDrawing(graphics);
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~SceneRendererDeferredLights()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        /// <param name="disposing">Free managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.lightGeometryVertexBuffer?.Dispose();
                this.lightGeometryVertexBuffer = null;
                this.lightGeometryIndexBuffer?.Dispose();
                this.lightGeometryIndexBuffer = null;

                this.globalLightInputLayout?.Dispose();
                this.globalLightInputLayout = null;
                this.pointLightInputLayout?.Dispose();
                this.pointLightInputLayout = null;
                this.spotLightInputLayout?.Dispose();
                this.spotLightInputLayout = null;
                this.combineLightsInputLayout?.Dispose();
                this.combineLightsInputLayout = null;

                this.rasterizerStencilPass?.Dispose();
                this.rasterizerStencilPass = null;
                this.rasterizerLightingPass?.Dispose();
                this.rasterizerLightingPass = null;
                this.depthStencilVolumeMarking?.Dispose();
                this.depthStencilVolumeMarking = null;
                this.depthStencilVolumeDrawing?.Dispose();
                this.depthStencilVolumeDrawing = null;
            }
        }

        /// <summary>
        /// Updates the internal buffers according to the new render dimension
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        public void Update(Graphics graphics, int width, int height)
        {
            List<VertexPosition> verts = new List<VertexPosition>();
            List<uint> indx = new List<uint>();

            CreateScreen(verts, indx, width, height);

            CreatePointLight(verts, indx);

            CreateSpotLight(verts, indx);

            if (this.lightGeometryVertexBuffer == null)
            {
                this.lightGeometryVertexBuffer = graphics.CreateVertexBuffer("Deferred Redenderer Light Geometry", verts, true);
                this.lightGeometryVertexBufferBinding = new VertexBufferBinding(this.lightGeometryVertexBuffer, verts[0].GetStride(), 0);
            }
            else
            {
                graphics.WriteDiscardBuffer(this.lightGeometryVertexBuffer, verts);
            }

            if (this.lightGeometryIndexBuffer == null)
            {
                this.lightGeometryIndexBuffer = graphics.CreateIndexBuffer("Deferred Redenderer Light Geometry", indx, true);
            }
            else
            {
                graphics.WriteDiscardBuffer(this.lightGeometryIndexBuffer, indx);
            }
        }
        /// <summary>
        /// Creates the geometry to draw the screen
        /// </summary>
        /// <param name="verts">Vertex list</param>
        /// <param name="indx">Index list</param>
        /// <param name="width">Screen width</param>
        /// <param name="height">Screen height</param>
        private void CreateScreen(List<VertexPosition> verts, List<uint> indx, int width, int height)
        {
            var screen = GeometryUtil.CreateScreen(width, height);

            this.screenGeometry.Offset = indx.Count;
            this.screenGeometry.IndexCount = screen.Indices.Count();

            screen.Indices.ToList().ForEach(i =>
            {
                //Sum offsets
                indx.Add(i + (uint)verts.Count);
            });

            verts.AddRange(VertexPosition.Generate(screen.Vertices));
        }
        /// <summary>
        /// Creates the geometry to draw a point light
        /// </summary>
        /// <param name="verts">Vertex list</param>
        /// <param name="indx">Index list</param>
        private void CreatePointLight(List<VertexPosition> verts, List<uint> indx)
        {
            var sphere = GeometryUtil.CreateSphere(1, 16, 16);

            this.pointLightGeometry.Offset = indx.Count;
            this.pointLightGeometry.IndexCount = sphere.Indices.Count();

            sphere.Indices.ToList().ForEach(i =>
            {
                //Sum offsets
                indx.Add(i + (uint)verts.Count);
            });

            verts.AddRange(VertexPosition.Generate(sphere.Vertices));
        }
        /// <summary>
        /// Creates the geometry to draw a spot light
        /// </summary>
        /// <param name="verts">Vertex list</param>
        /// <param name="indx">Index list</param>
        private void CreateSpotLight(List<VertexPosition> verts, List<uint> indx)
        {
            var sphere = GeometryUtil.CreateSphere(1, 16, 16);

            this.spotLightGeometry.Offset = indx.Count;
            this.spotLightGeometry.IndexCount = sphere.Indices.Count();

            sphere.Indices.ToList().ForEach(i =>
            {
                //Sum offsets
                indx.Add(i + (uint)verts.Count);
            });

            verts.AddRange(VertexPosition.Generate(sphere.Vertices));
        }

        /// <summary>
        /// Draws a single light
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="geometry">Geometry</param>
        /// <param name="effectTechnique">Technique</param>
        private void DrawSingleLight(Graphics graphics, LightGeometry geometry, EngineEffectTechnique effectTechnique)
        {
            for (int p = 0; p < effectTechnique.PassCount; p++)
            {
                graphics.EffectPassApply(effectTechnique, p, 0);

                graphics.DrawIndexed(geometry.IndexCount, geometry.Offset, 0);
            }
        }
        /// <summary>
        /// Binds light geometry to the input assembler
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        public void BindGeometry(Graphics graphics)
        {
            graphics.IAPrimitiveTopology = PrimitiveTopology.TriangleList;
            graphics.IASetVertexBuffers(BufferSlot, this.lightGeometryVertexBufferBinding);
            graphics.IASetIndexBuffer(this.lightGeometryIndexBuffer, Format.R32_UInt, 0);
        }
        /// <summary>
        /// Binds the hemispheric/directional (global) light input layout to the input assembler
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        public void BindGobalLight(Graphics graphics)
        {
            graphics.IAInputLayout = this.globalLightInputLayout;
            Counters.IAInputLayoutSets++;
        }
        /// <summary>
        /// Draws a directional light
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="effect">Effect</param>
        public void DrawDirectional(Graphics graphics, EffectDeferredComposer effect)
        {
            var effectTechnique = effect.DeferredDirectionalLight;

            for (int p = 0; p < effectTechnique.PassCount; p++)
            {
                graphics.EffectPassApply(effectTechnique, p, 0);

                graphics.DrawIndexed(
                    this.screenGeometry.IndexCount,
                    this.screenGeometry.Offset,
                    0);
            }
        }
        /// <summary>
        /// Binds the point light input layout to the input assembler
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        public void BindPoint(Graphics graphics)
        {
            graphics.IAInputLayout = this.pointLightInputLayout;
            Counters.IAInputLayoutSets++;
        }
        /// <summary>
        /// Draws a point light
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="effect">Effect</param>
        public void DrawPoint(Graphics graphics, EffectDeferredComposer effect)
        {
            var geometry = this.pointLightGeometry;

            this.SetRasterizerStencilPass();
            this.SetDepthStencilVolumeMarking();
            graphics.ClearDepthStencilBuffer(graphics.DefaultDepthStencil, false, true);
            this.DrawSingleLight(graphics, geometry, effect.DeferredPointStencil);

            this.SetRasterizerLightingPass();
            this.SetDepthStencilVolumeDrawing();
            this.DrawSingleLight(graphics, geometry, effect.DeferredPointLight);
        }
        /// <summary>
        /// Binds the spot light input layout to the input assembler
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        public void BindSpot(Graphics graphics)
        {
            graphics.IAInputLayout = this.spotLightInputLayout;
            Counters.IAInputLayoutSets++;
        }
        /// <summary>
        /// Draws a spot light
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="effect">Effect</param>
        public void DrawSpot(Graphics graphics, EffectDeferredComposer effect)
        {
            var geometry = this.spotLightGeometry;

            this.SetRasterizerStencilPass();
            this.SetDepthStencilVolumeMarking();
            graphics.ClearDepthStencilBuffer(graphics.DefaultDepthStencil, false, true);
            this.DrawSingleLight(graphics, geometry, effect.DeferredSpotStencil);

            this.SetRasterizerLightingPass();
            this.SetDepthStencilVolumeDrawing();
            this.DrawSingleLight(graphics, geometry, effect.DeferredSpotLight);
        }
        /// <summary>
        /// Binds the result box input layout to the input assembler
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        public void BindResult(Graphics graphics)
        {
            graphics.IAPrimitiveTopology = PrimitiveTopology.TriangleList;
            graphics.IASetVertexBuffers(BufferSlot, this.lightGeometryVertexBufferBinding);
            graphics.IASetIndexBuffer(this.lightGeometryIndexBuffer, Format.R32_UInt, 0);

            graphics.IAInputLayout = this.combineLightsInputLayout;
        }
        /// <summary>
        /// Draws the resulting light composition
        /// </summary>
        /// <param name="graphics">Graphics device</param>
        /// <param name="effect">Effect</param>
        public void DrawResult(Graphics graphics, EffectDeferredComposer effect)
        {
            var effectTechnique = effect.DeferredCombineLights;

            for (int p = 0; p < effectTechnique.PassCount; p++)
            {
                graphics.EffectPassApply(effectTechnique, p, 0);

                graphics.DrawIndexed(this.screenGeometry.IndexCount, this.screenGeometry.Offset, 0);
            }
        }

        /// <summary>
        /// Sets stencil pass rasterizer
        /// </summary>
        private void SetRasterizerStencilPass()
        {
            this.Graphics.SetRasterizerState(this.rasterizerStencilPass);
        }
        /// <summary>
        /// Stes lighting pass rasterizer
        /// </summary>
        public void SetRasterizerLightingPass()
        {
            this.Graphics.SetRasterizerState(this.rasterizerLightingPass);
        }
        /// <summary>
        /// Sets depth stencil for volume marking
        /// </summary>
        public void SetDepthStencilVolumeMarking()
        {
            this.Graphics.SetDepthStencilState(this.depthStencilVolumeMarking);
        }
        /// <summary>
        /// Sets depth stencil for volume drawing
        /// </summary>
        public void SetDepthStencilVolumeDrawing()
        {
            this.Graphics.SetDepthStencilState(this.depthStencilVolumeDrawing);
        }
    }
}
