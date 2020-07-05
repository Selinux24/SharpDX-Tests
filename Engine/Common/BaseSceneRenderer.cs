﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Engine.Common
{
    /// <summary>
    /// Base scene renderer
    /// </summary>
    public abstract class BaseSceneRenderer : ISceneRenderer
    {
        /// <summary>
        /// Shadow map size
        /// </summary>
        protected const int DirectionalShadowMapSize = 1024 * 4;
        /// <summary>
        /// Maximum number of directional shadow maps
        /// </summary>
        protected const int MaxDirectionalShadowMaps = 1;
        /// <summary>
        /// Maximum number of cascade shadow maps per directional light
        /// </summary>
        protected const int MaxDirectionalCascadeShadowMaps = 3;
        /// <summary>
        /// Shadow map sampling distances
        /// </summary>
        public static float[] CascadeShadowMapsDistances { get; set; } = new[]
        {
            GameEnvironment.ShadowDistanceHigh,
            GameEnvironment.ShadowDistanceMedium,
            GameEnvironment.ShadowDistanceLow,
        };

        /// <summary>
        /// Cubic shadow map size
        /// </summary>
        protected const int CubicShadowMapSize = 1024;
        /// <summary>
        /// Maximum number of cubic shadow maps
        /// </summary>
        protected const int MaxCubicShadows = 16;

        /// <summary>
        /// Spot light shadow map size
        /// </summary>
        protected const int SpotShadowMapSize = 1024;
        /// <summary>
        /// Max spot shadows
        /// </summary>
        protected const int MaxSpotShadows = 16;

        /// <summary>
        /// Cull index for drawing
        /// </summary>
        protected const int CullIndexDrawIndex = 0;
        /// <summary>
        /// Cull index for low definition shadows
        /// </summary>
        protected const int CullIndexShadowMaps = 100;

        /// <summary>
        /// Shadow mapper for directional lights
        /// </summary>
        protected IShadowMap ShadowMapperDirectional { get; private set; }
        /// <summary>
        /// Cube shadow mapper for point lights
        /// </summary>
        protected IShadowMap ShadowMapperPoint { get; private set; }
        /// <summary>
        /// Shadow mapper for spot lights
        /// </summary>
        protected IShadowMap ShadowMapperSpot { get; private set; }

        /// <summary>
        /// Game
        /// </summary>
        protected Game Game;
        /// <summary>
        /// Renderer width
        /// </summary>
        protected int Width;
        /// <summary>
        /// Renderer height
        /// </summary>
        protected int Height;
        /// <summary>
        /// Update context
        /// </summary>
        protected UpdateContext UpdateContext = null;
        /// <summary>
        /// Draw context
        /// </summary>
        protected DrawContext DrawContext = null;
        /// <summary>
        /// Context for shadow map drawing
        /// </summary>
        protected DrawContextShadows DrawShadowsContext = null;
        /// <summary>
        /// Cull manager
        /// </summary>
        protected SceneCullManager cullManager = null;
        /// <summary>
        /// Directional shadow map
        /// </summary>
        protected EngineShaderResourceView ShadowMapDirectional
        {
            get
            {
                if (this.ShadowMapperDirectional != null)
                {
                    return this.ShadowMapperDirectional.Texture;
                }

                return null;
            }
        }
        /// <summary>
        /// Point lights shadow map
        /// </summary>
        protected EngineShaderResourceView ShadowMapPoint
        {
            get
            {
                if (this.ShadowMapperPoint != null)
                {
                    return this.ShadowMapperPoint.Texture;
                }

                return null;
            }
        }
        /// <summary>
        /// Spot lights shadow map
        /// </summary>
        protected EngineShaderResourceView ShadowMapSpot
        {
            get
            {
                if (this.ShadowMapperSpot != null)
                {
                    return this.ShadowMapperSpot.Texture;
                }

                return null;
            }
        }
        /// <summary>
        /// Gets or sets whether the renderer was updated
        /// </summary>
        protected bool Updated { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        protected BaseSceneRenderer(Game game)
        {
            this.Game = game;

            // Directional shadow mapper
            this.ShadowMapperDirectional = new ShadowMapCascade(game,
                DirectionalShadowMapSize,
                MaxDirectionalCascadeShadowMaps, MaxDirectionalShadowMaps,
                CascadeShadowMapsDistances)
            {
                HighResolutionMap = true
            };

            // Point shadow mapper
            this.ShadowMapperPoint = new ShadowMapPoint(game,
                CubicShadowMapSize, CubicShadowMapSize,
                MaxCubicShadows)
            {
                HighResolutionMap = true
            };

            // Spot shadow mapper
            this.ShadowMapperSpot = new ShadowMapSpot(game,
                SpotShadowMapSize, SpotShadowMapSize,
                MaxSpotShadows)
            {
                HighResolutionMap = true
            };

            this.cullManager = new SceneCullManager();

            this.UpdateContext = new UpdateContext()
            {
                Name = "Primary",
            };

            this.DrawContext = new DrawContext()
            {
                Name = "Primary",
                DrawerMode = DrawerModes.Forward,
            };

            this.DrawShadowsContext = new DrawContextShadows()
            {
                Name = "Shadow mapping",
            };
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~BaseSceneRenderer()
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
                if (this.ShadowMapperDirectional != null)
                {
                    this.ShadowMapperDirectional.Dispose();
                    this.ShadowMapperDirectional = null;
                }

                if (this.ShadowMapperPoint != null)
                {
                    this.ShadowMapperPoint.Dispose();
                    this.ShadowMapperPoint = null;
                }

                if (this.ShadowMapperSpot != null)
                {
                    this.ShadowMapperSpot.Dispose();
                    this.ShadowMapperSpot = null;
                }
            }
        }

        /// <summary>
        /// Resizes buffers
        /// </summary>
        public virtual void Resize()
        {

        }
        /// <summary>
        /// Gets renderer resources
        /// </summary>
        /// <param name="result">Resource type</param>
        /// <returns>Returns renderer specified resource, if renderer produces that resource.</returns>
        public virtual EngineShaderResourceView GetResource(SceneRendererResults result)
        {
            if (result == SceneRendererResults.ShadowMapDirectional) return this.ShadowMapDirectional;
            if (result == SceneRendererResults.ShadowMapPoint) return this.ShadowMapPoint;
            if (result == SceneRendererResults.ShadowMapSpot) return this.ShadowMapSpot;
            return null;
        }
        /// <summary>
        /// Updates scene components
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        public virtual void Update(GameTime gameTime, Scene scene)
        {
            Dictionary<string, double> dict = new Dictionary<string, double>();
            Stopwatch swTotal = Stopwatch.StartNew();

            Matrix viewProj = scene.Camera.View * scene.Camera.Projection;

            this.UpdateContext.GameTime = gameTime;
            this.UpdateContext.View = scene.Camera.View;
            this.UpdateContext.Projection = scene.Camera.Projection;
            this.UpdateContext.NearPlaneDistance = scene.Camera.NearPlaneDistance;
            this.UpdateContext.FarPlaneDistance = scene.Camera.FarPlaneDistance;
            this.UpdateContext.ViewProjection = viewProj;
            this.UpdateContext.EyePosition = scene.Camera.Position;
            this.UpdateContext.EyeDirection = scene.Camera.Direction;
            this.UpdateContext.Lights = scene.Lights;
            this.UpdateContext.CameraVolume = new CullingVolumeCamera(viewProj);

            //Cull lights
            Stopwatch swLights = Stopwatch.StartNew();
            scene.Lights.Cull(this.UpdateContext.CameraVolume, this.UpdateContext.EyePosition);
            swLights.Stop();

            //Update active components
            Stopwatch swUpdate = Stopwatch.StartNew();
            int uIndex = 0;
            scene.GetComponents()
                .Where(c => c.Active)
                .OfType<IUpdatable>()
                .ToList().ForEach(c =>
                {
                    Stopwatch swCUpdate = Stopwatch.StartNew();
                    c.Update(this.UpdateContext);
                    swCUpdate.Stop();

                    var o = c as BaseSceneObject;
                    string cName = o?.Description?.Name ?? c.ToString();
                    dict.Add($"Component Update {uIndex++} {cName}", swCUpdate.Elapsed.TotalMilliseconds);
                });
            this.Updated = true;
            swUpdate.Stop();
            dict.Add($"Components Update", swUpdate.Elapsed.TotalMilliseconds);

            swTotal.Stop();
            dict.Add($"Scene Update", swTotal.Elapsed.TotalMilliseconds);

            Counters.SetStatistics("Scene.Update", string.Format("Update = {0:000000}", swTotal.ElapsedTicks));

            if (this.Game.CollectGameStatus)
            {
                this.Game.GameStatus.Add(dict);
            }
        }
        /// <summary>
        /// Draws scene components
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        public abstract void Draw(GameTime gameTime, Scene scene);

        /// <summary>
        /// Gets opaque components
        /// </summary>
        /// <param name="index">Cull index</param>
        /// <param name="components">Component list</param>
        /// <returns>Returns the opaque components</returns>
        protected virtual List<ISceneObject> GetOpaques(int index, IEnumerable<ISceneObject> components)
        {
            var opaques = components.Where(c =>
            {
                if (!(c is Drawable)) return false;

                if (c is ICullable cull)
                {
                    return !this.cullManager.GetCullValue(index, cull).Culled;
                }

                return true;
            });

            return opaques.ToList();
        }
        /// <summary>
        /// Sorting opaque list comparer
        /// </summary>
        /// <param name="index">Cull index</param>
        /// <param name="c1">First component</param>
        /// <param name="c2">Second component</param>
        /// <returns>Returns sorting order (nearest first)</returns>
        protected virtual int SortOpaques(int index, ISceneObject c1, ISceneObject c2)
        {
            int res = c1.Order.CompareTo(c2.Order);

            if (res == 0)
            {
                res = c1.DepthEnabled.CompareTo(c2.DepthEnabled);
            }

            if (res == 0)
            {
                float d1 = float.MaxValue;
                if (c1 is ICullable cull1)
                {
                    d1 = this.cullManager.GetCullValue(index, cull1).Distance;
                }

                float d2 = float.MaxValue;
                if (c2 is ICullable cull2)
                {
                    d2 = this.cullManager.GetCullValue(index, cull2).Distance;
                }

                res = -d1.CompareTo(d2);
            }

            return res;
        }
        /// <summary>
        /// Gets transparent components
        /// </summary>
        /// <param name="index">Cull index</param>
        /// <param name="components">Component list</param>
        /// <returns>Returns the transparent components</returns>
        protected virtual List<ISceneObject> GetTransparents(int index, IEnumerable<ISceneObject> components)
        {
            var transparents = components.Where(c =>
            {
                if (!c.AlphaEnabled) return false;

                if (!(c is Drawable)) return false;

                if (c is ICullable cull)
                {
                    return !this.cullManager.GetCullValue(index, cull).Culled;
                }

                return true;
            });

            return transparents.ToList();
        }
        /// <summary>
        /// Sorting transparent list comparer
        /// </summary>
        /// <param name="index">Cull index</param>
        /// <param name="c1">First component</param>
        /// <param name="c2">Second component</param>
        /// <returns>Returns sorting order (far first)</returns>
        protected virtual int SortTransparents(int index, ISceneObject c1, ISceneObject c2)
        {
            int res = c1.DepthEnabled.CompareTo(c2.DepthEnabled);
            if (res == 0)
            {
                float d1 = float.MaxValue;
                if (c1 is ICullable cull1)
                {
                    d1 = this.cullManager.GetCullValue(index, cull1).Distance;
                }

                float d2 = float.MaxValue;
                if (c2 is ICullable cull2)
                {
                    d2 = this.cullManager.GetCullValue(index, cull2).Distance;
                }

                res = -d1.CompareTo(d2);
            }

            if (res == 0)
            {
                res = -c1.Order.CompareTo(c2.Order);
            }

            return -res;
        }

        /// <summary>
        /// Draws an opaque object
        /// </summary>
        /// <param name="context">Drawing context</param>
        /// <param name="c">Component</param>
        protected virtual void DrawOpaque(DrawContext context, ISceneObject c)
        {
            if (c is IDrawable drawable)
            {
                var graphics = this.Game.Graphics;

                Counters.MaxInstancesPerFrame += c.InstanceCount;

                graphics.SetRasterizerDefault();

                this.SetBlendStateOpaque(context);

                if (c.DepthEnabled)
                {
                    graphics.SetDepthStencilZEnabled();
                }
                else
                {
                    graphics.SetDepthStencilZDisabled();
                }

                drawable.Draw(context);
            }
        }
        /// <summary>
        /// Draws an transparent object
        /// </summary>
        /// <param name="context">Drawing context</param>
        /// <param name="c">Component</param>
        protected virtual void DrawTransparent(DrawContext context, ISceneObject c)
        {
            if (c is IDrawable drawable)
            {
                var graphics = this.Game.Graphics;

                Counters.MaxInstancesPerFrame += c.InstanceCount;

                graphics.SetRasterizerDefault();

                this.SetBlendStateTransparent(context);

                if (c.DepthEnabled)
                {
                    graphics.SetDepthStencilZEnabled();
                }
                else
                {
                    graphics.SetDepthStencilZDisabled();
                }

                drawable.Draw(context);
            }
        }
        /// <summary>
        /// Sets the opaque blend state
        /// </summary>
        /// <param name="context">Drawing context</param>
        protected virtual void SetBlendStateOpaque(DrawContext context)
        {
            this.Game.Graphics.SetBlendDefault();
        }
        /// <summary>
        /// Sets the transparent blend state
        /// </summary>
        /// <param name="context">Drawing context</param>
        protected virtual void SetBlendStateTransparent(DrawContext context)
        {
            this.Game.Graphics.SetBlendTransparent();
        }

        /// <summary>
        /// Draw shadow maps
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        protected virtual void DoShadowMapping(GameTime gameTime, Scene scene)
        {
            int cullIndex = CullIndexShadowMaps;

            DoDirectionalShadowMapping(gameTime, scene, ref cullIndex);

            DoPointShadowMapping(gameTime, scene, ref cullIndex);

            DoSpotShadowMapping(gameTime, scene, ref cullIndex);
        }
        /// <summary>
        /// Draw directional shadow maps
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        /// <param name="cullIndex">Cull index</param>
        protected virtual void DoDirectionalShadowMapping(GameTime gameTime, Scene scene, ref int cullIndex)
        {
            Dictionary<string, double> dict = new Dictionary<string, double>();

            Stopwatch gStopwatch = new Stopwatch();
            gStopwatch.Start();

            //And there were lights
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var shadowCastingLights = scene.Lights.GetDirectionalShadowCastingLights();
            stopwatch.Stop();
            dict.Add($"DoDirectionalShadowMapping Getting lights", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowCastingLights.Any())
            {
                return;
            }

            //Objects that cast shadows
            stopwatch.Restart();
            var shadowObjs = scene.GetComponents().Where(c => c.Visible && c.CastShadow);
            stopwatch.Stop();
            dict.Add($"DoDirectionalShadowMapping Getting components", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowObjs.Any())
            {
                return;
            }

            //Objects that cast shadows and suitable for culling test
            var toCullShadowObjs = shadowObjs.OfType<ICullable>();
            if (toCullShadowObjs.Any())
            {
                //All objects suitable for culling
                bool allCullingObjects = shadowObjs.Count() == toCullShadowObjs.Count();
                var camVolume = this.DrawContext.CameraVolume;

                stopwatch.Restart();
                var shadowSph = new CullingVolumeSphere(camVolume.Position, camVolume.Radius);
                var doShadows = this.cullManager.Cull(shadowSph, cullIndex, toCullShadowObjs);
                stopwatch.Stop();
                dict.Add($"DoDirectionalShadowMapping - Cull {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                if (allCullingObjects && !doShadows)
                {
                    //All objects suitable for culling but no one pass the culling test
                    return;
                }
            }

            var graphics = this.Game.Graphics;
            int assigned = 0;

            int l = 0;
            foreach (var light in shadowCastingLights)
            {
                light.ClearShadowParameters();

                if (assigned >= MaxDirectionalShadowMaps)
                {
                    continue;
                }

                //Draw shadows
                stopwatch.Restart();
                var shadowMapper = this.DrawShadowsContext.ShadowMap = this.ShadowMapperDirectional;
                shadowMapper.UpdateFromLightViewProjection(scene.Camera, light);
                shadowMapper.Bind(graphics, assigned * MaxDirectionalCascadeShadowMaps);
                this.DrawShadowsContext.EyePosition = shadowMapper.LightPosition;
                this.DrawShadowsContext.ViewProjection = shadowMapper.ToShadowMatrix;
                this.DrawShadowComponents(this.DrawShadowsContext, cullIndex, shadowObjs);
                stopwatch.Stop();
                dict.Add($"DoDirectionalShadowMapping {l} - Draw {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                //Assign light parameters
                light.ShadowMapIndex = assigned;
                light.ShadowMapCount++;

                assigned++;

                cullIndex++;

                l++;
            }

            gStopwatch.Stop();
            dict.Add($"DoDirectionalShadowMapping TOTAL", gStopwatch.Elapsed.TotalMilliseconds);

            if (this.Game.CollectGameStatus)
            {
                this.Game.GameStatus.Add(dict);
            }
        }
        /// <summary>
        /// Draw point light shadow maps
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        /// <param name="cullIndex">Cull index</param>
        protected virtual void DoPointShadowMapping(GameTime gameTime, Scene scene, ref int cullIndex)
        {
            Dictionary<string, double> dict = new Dictionary<string, double>();

            Stopwatch gStopwatch = new Stopwatch();
            gStopwatch.Start();

            //And there were lights
            Stopwatch stopwatch = new Stopwatch();
            var shadowCastingLights = scene.Lights.GetPointShadowCastingLights(scene.Camera.Position);
            stopwatch.Stop();
            dict.Add($"DoPointShadowMapping Getting lights", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowCastingLights.Any())
            {
                return;
            }

            //Draw components if drop shadow (opaque)
            stopwatch.Restart();
            var shadowObjs = scene.GetComponents().Where(c => c.Visible && c.CastShadow);
            stopwatch.Stop();
            dict.Add($"DoPointShadowMapping Getting components", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowObjs.Any())
            {
                return;
            }

            var toCullShadowObjs = shadowObjs.OfType<ICullable>();

            //All objects suitable for culling
            bool allCullingObjects = shadowObjs.Count() == toCullShadowObjs.Count();

            var graphics = this.Game.Graphics;
            int assigned = 0;

            int l = 0;
            foreach (var light in shadowCastingLights)
            {
                light.ClearShadowParameters();

                if (assigned >= MaxCubicShadows)
                {
                    continue;
                }

                cullIndex++;
                l++;

                //Cull test
                stopwatch.Restart();
                var sph = new CullingVolumeSphere(light.Position, light.Radius);
                var doShadows = this.cullManager.Cull(sph, cullIndex, toCullShadowObjs);
                stopwatch.Stop();
                dict.Add($"DoPointShadowMapping {l} - Cull {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                if (allCullingObjects && !doShadows)
                {
                    //All objects suitable for culling but no one pass the culling test
                    continue;
                }

                //Draw shadows
                stopwatch.Restart();
                var shadowMapper = this.DrawShadowsContext.ShadowMap = this.ShadowMapperPoint;
                shadowMapper.UpdateFromLightViewProjection(scene.Camera, light);
                shadowMapper.Bind(graphics, assigned);
                this.DrawShadowsContext.EyePosition = shadowMapper.LightPosition;
                this.DrawShadowsContext.ViewProjection = shadowMapper.ToShadowMatrix;
                this.DrawShadowComponents(this.DrawShadowsContext, cullIndex, shadowObjs);
                stopwatch.Stop();
                dict.Add($"DoPointShadowMapping {l} - Draw {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                //Assign light parameters
                light.ShadowMapIndex = assigned;

                assigned++;
            }

            gStopwatch.Stop();
            dict.Add($"DoPointShadowMapping TOTAL", gStopwatch.Elapsed.TotalMilliseconds);

            if (this.Game.CollectGameStatus)
            {
                this.Game.GameStatus.Add(dict);
            }
        }
        /// <summary>
        /// Draw spot light shadow maps
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="scene">Scene</param>
        /// <param name="cullIndex">Cull index</param>
        protected virtual void DoSpotShadowMapping(GameTime gameTime, Scene scene, ref int cullIndex)
        {
            Dictionary<string, double> dict = new Dictionary<string, double>();

            Stopwatch gStopwatch = new Stopwatch();
            gStopwatch.Start();

            //And there were lights
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var shadowCastingLights = scene.Lights.GetSpotShadowCastingLights(scene.Camera.Position);
            stopwatch.Stop();
            dict.Add($"DoSpotShadowMapping Getting lights", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowCastingLights.Any())
            {
                return;
            }

            //Draw components if drop shadow (opaque)
            stopwatch.Restart();
            var shadowObjs = scene.GetComponents().Where(c => c.Visible && c.CastShadow);
            stopwatch.Stop();
            dict.Add($"DoSpotShadowMapping Getting components", stopwatch.Elapsed.TotalMilliseconds);

            if (!shadowObjs.Any())
            {
                return;
            }

            var toCullShadowObjs = shadowObjs.OfType<ICullable>();

            //All objects suitable for culling
            bool allCullingObjects = shadowObjs.Count() == toCullShadowObjs.Count();

            var graphics = this.Game.Graphics;
            int assigned = 0;

            int l = 0;
            foreach (var light in shadowCastingLights)
            {
                light.ClearShadowParameters();

                if (assigned >= MaxCubicShadows)
                {
                    continue;
                }

                //Cull test
                stopwatch.Restart();
                var sph = new CullingVolumeSphere(light.Position, light.Radius);
                var doShadows = this.cullManager.Cull(sph, cullIndex, toCullShadowObjs);
                stopwatch.Stop();
                dict.Add($"DoSpotShadowMapping {l} - Cull {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                if (allCullingObjects && !doShadows)
                {
                    //All objects suitable for culling but no one pass the culling test
                    continue;
                }

                //Draw shadows
                stopwatch.Restart();
                var shadowMapper = this.DrawShadowsContext.ShadowMap = this.ShadowMapperSpot;
                shadowMapper.UpdateFromLightViewProjection(scene.Camera, light);
                shadowMapper.Bind(graphics, assigned);
                this.DrawShadowsContext.EyePosition = shadowMapper.LightPosition;
                this.DrawShadowsContext.ViewProjection = shadowMapper.ToShadowMatrix;
                this.DrawShadowComponents(this.DrawShadowsContext, cullIndex, shadowObjs);
                stopwatch.Stop();
                dict.Add($"DoSpotShadowMapping {l} - Draw {cullIndex}", stopwatch.Elapsed.TotalMilliseconds);

                //Assign light parameters
                light.FromLightVP = shadowMapper.FromLightViewProjectionArray;
                light.ShadowMapIndex = assigned;
                light.ShadowMapCount = 1;

                assigned++;

                cullIndex++;

                l++;
            }

            gStopwatch.Stop();
            dict.Add($"DoSpotShadowMapping TOTAL", gStopwatch.Elapsed.TotalMilliseconds);

            if (this.Game.CollectGameStatus)
            {
                this.Game.GameStatus.Add(dict);
            }
        }

        /// <summary>
        /// Draw components for shadow mapping
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="index">Culling index</param>
        /// <param name="components">Components to draw</param>
        protected void DrawShadowComponents(DrawContextShadows context, int index, IEnumerable<ISceneObject> components)
        {
            var graphics = this.Game.Graphics;

            var objects = components.Where(c => IsVisible(c, index)).ToList();
            if (objects.Any())
            {
                objects.Sort((c1, c2) => Sort(c1, c2, index));

                objects.ForEach((c) => DrawShadows(graphics, context, c));
            }
        }
        /// <summary>
        /// Gets if the specified object is not culled by the cull index
        /// </summary>
        /// <param name="c">Scene object</param>
        /// <param name="cullIndex">Cull index</param>
        /// <returns>Returns true if the object is not culled</returns>
        private bool IsVisible(ISceneObject c, int cullIndex)
        {
            if (!(c is Drawable)) return false;

            if (c is ICullable cull)
            {
                return !this.cullManager.GetCullValue(cullIndex, cull).Culled;
            }

            return true;
        }
        /// <summary>
        /// Sorts an object list by distance to culling point of view
        /// </summary>
        /// <param name="c1">Scene object one</param>
        /// <param name="c2">Scene object two</param>
        /// <param name="cullIndex">Cull index</param>
        /// <returns></returns>
        private int Sort(ISceneObject c1, ISceneObject c2, int cullIndex)
        {
            int res = c1.DepthEnabled.CompareTo(c2.DepthEnabled);
            if (res == 0)
            {
                float d1 = float.MaxValue;
                if (c1 is ICullable cull1)
                {
                    d1 = this.cullManager.GetCullValue(cullIndex, cull1).Distance;
                }

                float d2 = float.MaxValue;
                if (c2 is ICullable cull2)
                {
                    d2 = this.cullManager.GetCullValue(cullIndex, cull2).Distance;
                }

                res = -d1.CompareTo(d2);
            }

            if (res == 0)
            {
                res = c1.Order.CompareTo(c2.Order);
            }

            return res;
        }
        /// <summary>
        /// Draws the specified object shadows
        /// </summary>
        /// <param name="graphics">Graphics</param>
        /// <param name="context">Context</param>
        /// <param name="c">Scene object</param>
        private void DrawShadows(Graphics graphics, DrawContextShadows context, ISceneObject c)
        {
            if (c is IDrawable drawable)
            {
                graphics.SetRasterizerShadowMapping();
                graphics.SetDepthStencilShadowMapping();

                if (c.AlphaEnabled)
                {
                    graphics.SetBlendTransparent();
                }
                else
                {
                    graphics.SetBlendDefault();
                }

                drawable.DrawShadows(context);
            }
        }
    }
}
