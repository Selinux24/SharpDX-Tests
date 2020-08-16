﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Engine
{
    using Engine.Animation;
    using Engine.Common;
    using Engine.Effects;

    /// <summary>
    /// Basic Model
    /// </summary>
    public class Model : BaseModel, ITransformable3D, IRayPickable<Triangle>, IIntersectable, ICullable
    {
        /// <summary>
        /// Update point cache flag
        /// </summary>
        private bool updatePoints = true;
        /// <summary>
        /// Update triangle cache flag
        /// </summary>
        private bool updateTriangles = true;
        /// <summary>
        /// Points cache
        /// </summary>
        private Vector3[] positionCache = null;
        /// <summary>
        /// Triangle list cache
        /// </summary>
        private Triangle[] triangleCache = null;
        /// <summary>
        /// Coarse bounding sphere
        /// </summary>
        private BoundingSphere? coarseBoundingSphere = null;
        /// <summary>
        /// Bounding sphere
        /// </summary>
        private BoundingSphere? boundingSphere = null;
        /// <summary>
        /// Bounding box
        /// </summary>
        private BoundingBox? boundingBox = null;
        /// <summary>
        /// Level of detail
        /// </summary>
        private LevelOfDetail levelOfDetail = LevelOfDetail.None;

        /// <summary>
        /// Current drawing data
        /// </summary>
        protected DrawingData DrawingData { get; private set; }
        /// <summary>
        /// Model parts collection
        /// </summary>
        protected List<ModelPart> ModelParts = new List<ModelPart>();
        /// <summary>
        /// Gets if model has volumes
        /// </summary>
        protected bool HasVolumes
        {
            get
            {
                return this.positionCache?.Any() == true;
            }
        }

        /// <summary>
        /// Model manipulator
        /// </summary>
        public Manipulator3D Manipulator { get; private set; }
        /// <summary>
        /// Animation controller
        /// </summary>
        public AnimationController AnimationController { get; set; } = new AnimationController();
        /// <summary>
        /// Texture index
        /// </summary>
        public uint TextureIndex { get; set; }
        /// <summary>
        /// Animation palette offset
        /// </summary>
        public uint AnimationOffset { get; set; }
        /// <summary>
        /// Level of detail
        /// </summary>
        public LevelOfDetail LevelOfDetail
        {
            get
            {
                return this.levelOfDetail;
            }
            set
            {
                this.levelOfDetail = this.GetLODNearest(value);
                this.DrawingData = this.GetDrawingData(this.levelOfDetail);
            }
        }
        /// <summary>
        /// Gets the current model lights collection
        /// </summary>
        public IEnumerable<ISceneLight> Lights { get; protected set; } = new ISceneLight[] { };
        /// <summary>
        /// Gets the model part by name
        /// </summary>
        /// <param name="name">Part name</param>
        /// <returns>Returns the model part name</returns>
        public ModelPart this[string name]
        {
            get
            {
                return this.ModelParts.Find(p => p.Name == name);
            }
        }
        /// <summary>
        /// Gets the model part count
        /// </summary>
        public int ModelPartCount
        {
            get
            {
                return this.ModelParts.Count;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Description</param>
        public Model(Scene scene, ModelDescription description)
            : base(scene, description)
        {
            this.TextureIndex = description.TextureIndex;

            if (description.TransformDependences?.Any() == true)
            {
                var parents = Array.FindAll(description.TransformDependences, i => i == -1);
                if (parents == null || parents.Length != 1)
                {
                    throw new EngineException("Model with transform dependences must have one (and only one) parent mesh identified by -1");
                }

                for (int i = 0; i < description.TransformNames.Length; i++)
                {
                    this.ModelParts.Add(new ModelPart(description.TransformNames[i]));
                }

                for (int i = 0; i < description.TransformNames.Length; i++)
                {
                    var thisName = description.TransformNames[i];
                    var thisMan = this[thisName].Manipulator;
                    thisMan.Updated += new EventHandler(ManipulatorUpdated);

                    var parentIndex = description.TransformDependences[i];
                    if (parentIndex >= 0)
                    {
                        var parentName = description.TransformNames[parentIndex];

                        thisMan.Parent = this[parentName].Manipulator;
                    }
                    else
                    {
                        this.Manipulator = thisMan;
                    }
                }
            }
            else
            {
                this.Manipulator = new Manipulator3D();
                this.Manipulator.Updated += new EventHandler(ManipulatorUpdated);
            }

            var drawData = this.GetDrawingData(LevelOfDetail.High);
            this.Lights = drawData?.Lights.Select(l => l.Clone()).ToArray() ?? new ISceneLight[] { };
        }

        /// <inheritdoc/>
        public override void Update(UpdateContext context)
        {
            if (this.DrawingData?.SkinningData != null)
            {
                if (this.AnimationController.Playing)
                {
                    this.InvalidateCache();
                }

                this.AnimationController.Update(context.GameTime.ElapsedSeconds, this.DrawingData.SkinningData);
                this.AnimationOffset = this.AnimationController.GetAnimationOffset(this.DrawingData.SkinningData);
            }

            if (this.ModelParts.Count > 0)
            {
                this.ModelParts.ForEach(p => p.Manipulator.Update(context.GameTime));
            }
            else
            {
                this.Manipulator.Update(context.GameTime);
            }

            if (this.Lights.Any())
            {
                foreach (var light in this.Lights)
                {
                    light.ParentTransform = this.Manipulator.LocalTransform;
                }
            }
        }
        /// <inheritdoc/>
        public override void DrawShadows(DrawContextShadows context)
        {
            if (!Visible)
            {
                return;
            }

            if (this.DrawingData == null)
            {
                return;
            }

            var effect = context.ShadowMap.GetEffect();
            if (effect == null)
            {
                return;
            }

            int count = 0;
            foreach (string meshName in this.DrawingData.Meshes.Keys)
            {
                count += DrawMeshShadow(context, effect, meshName);
            }
        }
        /// <inheritdoc/>
        public override void Draw(DrawContext context)
        {
            if (!Visible)
            {
                return;
            }

            if (this.DrawingData == null)
            {
                return;
            }

            var effect = this.GetEffect(context.DrawerMode);
            if (effect == null)
            {
                return;
            }

            int count = 0;
            foreach (string meshName in this.DrawingData.Meshes.Keys)
            {
                count += this.DrawMesh(context, effect, meshName);
            }

            Counters.InstancesPerFrame++;
            Counters.PrimitivesPerFrame += count;
        }
        /// <summary>
        /// Draws a mesh shadow
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="effect">Effect</param>
        /// <param name="meshName">Mesh name</param>
        /// <returns>Returns the number of drawn triangles</returns>
        private int DrawMeshShadow(DrawContextShadows context, IShadowMapDrawer effect, string meshName)
        {
            int count = 0;

            var graphics = this.Game.Graphics;

            var meshDict = this.DrawingData.Meshes[meshName];

            var localTransform = this.GetTransformByName(meshName);

            effect.UpdatePerFrame(localTransform, context);

            foreach (string materialName in meshDict.Keys)
            {
                var mesh = meshDict[materialName];
                if (!mesh.Ready)
                {
                    continue;
                }

                var material = this.DrawingData.Materials[materialName];

                effect.UpdatePerObject(this.AnimationOffset, material, this.TextureIndex);

                this.BufferManager.SetIndexBuffer(mesh.IndexBuffer);

                var technique = effect.GetTechnique(mesh.VertextType, false, material.Material.IsTransparent);
                this.BufferManager.SetInputAssembler(technique, mesh.VertexBuffer, mesh.Topology);

                count += mesh.Count;

                for (int p = 0; p < technique.PassCount; p++)
                {
                    graphics.EffectPassApply(technique, p, 0);

                    mesh.Draw(graphics);
                }
            }

            return count;
        }
        /// <summary>
        /// Draws a mesh
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="effect">Effect</param>
        /// <param name="meshName">Mesh name</param>
        /// <returns>Returns the number of drawn triangles</returns>
        private int DrawMesh(DrawContext context, IGeometryDrawer effect, string meshName)
        {
            int count = 0;

            var graphics = this.Game.Graphics;

            var meshDict = this.DrawingData.Meshes[meshName];

            var localTransform = this.GetTransformByName(meshName);

            effect.UpdatePerFrameFull(localTransform, context);

            foreach (string materialName in meshDict.Keys)
            {
                var mesh = meshDict[materialName];
                if (!mesh.Ready)
                {
                    continue;
                }

                var material = this.DrawingData.Materials[materialName];

                bool draw = context.ValidateDraw(this.BlendMode, material.Material.IsTransparent);
                if (!draw)
                {
                    continue;
                }

                effect.UpdatePerObject(this.AnimationOffset, material, this.TextureIndex, this.UseAnisotropicFiltering);

                this.BufferManager.SetIndexBuffer(mesh.IndexBuffer);

                var technique = effect.GetTechnique(mesh.VertextType, false);
                this.BufferManager.SetInputAssembler(technique, mesh.VertexBuffer, mesh.Topology);

                count += mesh.Count;

                for (int p = 0; p < technique.PassCount; p++)
                {
                    graphics.EffectPassApply(technique, p, 0);

                    mesh.Draw(graphics);
                }
            }

            return count;
        }

        /// <inheritdoc/>
        public override bool Cull(IIntersectionVolume volume, out float distance)
        {
            bool cull;
            distance = float.MaxValue;

            if (this.HasVolumes)
            {
                if (this.coarseBoundingSphere.HasValue)
                {
                    cull = volume.Contains(this.coarseBoundingSphere.Value) == ContainmentType.Disjoint;
                }
                else if (this.SphericVolume)
                {
                    cull = volume.Contains(this.GetBoundingSphere()) == ContainmentType.Disjoint;
                }
                else
                {
                    cull = volume.Contains(this.GetBoundingBox()) == ContainmentType.Disjoint;
                }
            }
            else
            {
                cull = false;
            }

            if (!cull)
            {
                var eyePosition = volume.Position;

                distance = Vector3.DistanceSquared(this.Manipulator.Position, eyePosition);

                this.LevelOfDetail = GameEnvironment.GetLOD(
                    eyePosition,
                    this.coarseBoundingSphere,
                    this.Manipulator.LocalTransform);
            }

            return cull;
        }

        /// <summary>
        /// Sets a new manipulator to this instance
        /// </summary>
        /// <param name="manipulator">Manipulator</param>
        public void SetManipulator(Manipulator3D manipulator)
        {
            this.Manipulator.Updated -= ManipulatorUpdated;
            this.Manipulator = null;

            this.Manipulator = manipulator;
            this.Manipulator.Updated += ManipulatorUpdated;
        }
        /// <summary>
        /// Occurs when manipulator transform updated
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event arguments</param>
        private void ManipulatorUpdated(object sender, EventArgs e)
        {
            this.InvalidateCache();

            this.coarseBoundingSphere = this.GetBoundingSphere();
        }

        /// <summary>
        /// Gets the transform by transform name
        /// </summary>
        /// <param name="name">Transform name</param>
        /// <returns>Retusn the transform of the specified transform name</returns>
        public Matrix GetTransformByName(string name)
        {
            var part = this.ModelParts.Find(p => p.Name == name);
            if (part != null)
            {
                return part.Manipulator.FinalTransform;
            }
            else
            {
                return this.Manipulator.FinalTransform;
            }
        }

        /// <summary>
        /// Invalidates the internal cache
        /// </summary>
        private void InvalidateCache()
        {
            this.updatePoints = true;
            this.updateTriangles = true;

            this.boundingSphere = null;
            this.boundingBox = null;
        }
        /// <summary>
        /// Gets point list of mesh if the vertex type has position channel
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns null or position list</returns>
        public IEnumerable<Vector3> GetPoints(bool refresh = false)
        {
            if (refresh || this.updatePoints)
            {
                IEnumerable<Vector3> cache;

                var drawingData = this.GetDrawingData(this.GetLODMinimum());
                if (drawingData == null)
                {
                    return new Vector3[] { };
                }

                if (drawingData.SkinningData != null)
                {
                    cache = drawingData.GetPoints(
                        this.Manipulator.LocalTransform,
                        this.AnimationController.GetCurrentPose(drawingData.SkinningData),
                        refresh);
                }
                else
                {
                    cache = drawingData.GetPoints(
                        this.Manipulator.LocalTransform,
                        refresh);
                }

                this.positionCache = cache.ToArray();

                this.updatePoints = false;
            }

            return this.positionCache?.ToArray() ?? new Vector3[] { };
        }
        /// <summary>
        /// Gets triangle list of mesh if the vertex type has position channel
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns null or triangle list</returns>
        public IEnumerable<Triangle> GetTriangles(bool refresh = false)
        {
            if (refresh || this.updateTriangles)
            {
                IEnumerable<Triangle> cache;

                var drawingData = this.GetDrawingData(this.GetLODMinimum());
                if (drawingData == null)
                {
                    return new Triangle[] { };
                }

                if (drawingData.SkinningData != null)
                {
                    cache = drawingData.GetTriangles(
                        this.Manipulator.LocalTransform,
                        this.AnimationController.GetCurrentPose(drawingData.SkinningData),
                        refresh);
                }
                else
                {
                    cache = drawingData.GetTriangles(
                        this.Manipulator.LocalTransform,
                        refresh);
                }

                this.triangleCache = cache.ToArray();

                this.updateTriangles = false;
            }

            return this.triangleCache?.ToArray() ?? new Triangle[] { };
        }
        /// <summary>
        /// Gets bounding sphere
        /// </summary>
        /// <returns>Returns bounding sphere. Empty if the vertex type hasn't position channel</returns>
        public BoundingSphere GetBoundingSphere()
        {
            return this.GetBoundingSphere(false);
        }
        /// <summary>
        /// Gets bounding sphere
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns bounding sphere. Empty if the vertex type hasn't position channel</returns>
        public BoundingSphere GetBoundingSphere(bool refresh)
        {
            if (refresh || this.boundingSphere == null)
            {
                var points = this.GetPoints(refresh);
                if (points.Any())
                {
                    this.boundingSphere = BoundingSphere.FromPoints(points.ToArray());
                }
            }

            return this.boundingSphere ?? new BoundingSphere(this.Manipulator.Position, 0f);
        }
        /// <summary>
        /// Gets bounding box
        /// </summary>
        /// <returns>Returns bounding box. Empty if the vertex type hasn't position channel</returns>
        public BoundingBox GetBoundingBox()
        {
            return this.GetBoundingBox(false);
        }
        /// <summary>
        /// Gets bounding box
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns bounding box. Empty if the vertex type hasn't position channel</returns>
        public BoundingBox GetBoundingBox(bool refresh)
        {
            if (refresh || this.boundingBox == null)
            {
                var points = this.GetPoints(refresh);
                if (points.Any())
                {
                    this.boundingBox = BoundingBox.FromPoints(points.ToArray());
                }
            }

            return this.boundingBox ?? new BoundingBox(this.Manipulator.Position, this.Manipulator.Position);
        }

        /// <summary>
        /// Gets nearest picking position of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="result">Picking result</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickNearest(Ray ray, out PickingResult<Triangle> result)
        {
            return PickNearest(ray, RayPickingParams.Default, out result);
        }
        /// <summary>
        /// Gets nearest picking position of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="rayPickingParams">Ray picking params</param>
        /// <param name="result">Picking result</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickNearest(Ray ray, RayPickingParams rayPickingParams, out PickingResult<Triangle> result)
        {
            result = new PickingResult<Triangle>()
            {
                Distance = float.MaxValue,
            };

            var bsph = this.GetBoundingSphere();
            if (bsph.Intersects(ref ray))
            {
                bool facingOnly = rayPickingParams.HasFlag(RayPickingParams.FacingOnly);
                var triangles = this.GetVolume(rayPickingParams.HasFlag(RayPickingParams.Geometry));

                if (triangles.Any() && Intersection.IntersectNearest(ray, triangles, facingOnly, out Vector3 pos, out Triangle tri, out float d))
                {
                    result.Position = pos;
                    result.Item = tri;
                    result.Distance = d;

                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Gets first picking position of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="result">Picking result</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickFirst(Ray ray, out PickingResult<Triangle> result)
        {
            return PickFirst(ray, RayPickingParams.Default, out result);
        }
        /// <summary>
        /// Gets first picking position of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="rayPickingParams">Ray picking params</param>
        /// <param name="result">Picking result</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickFirst(Ray ray, RayPickingParams rayPickingParams, out PickingResult<Triangle> result)
        {
            result = new PickingResult<Triangle>()
            {
                Distance = float.MaxValue,
            };

            var bsph = this.GetBoundingSphere();
            if (bsph.Intersects(ref ray))
            {
                bool facingOnly = rayPickingParams.HasFlag(RayPickingParams.FacingOnly);
                var triangles = this.GetVolume(rayPickingParams.HasFlag(RayPickingParams.Geometry));

                if (triangles.Any() && Intersection.IntersectFirst(ray, triangles, facingOnly, out Vector3 pos, out Triangle tri, out float d))
                {
                    result.Position = pos;
                    result.Item = tri;
                    result.Distance = d;

                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Get all picking positions of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="results">Picking results</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickAll(Ray ray, out PickingResult<Triangle>[] results)
        {
            return PickAll(ray, RayPickingParams.Default, out results);
        }
        /// <summary>
        /// Gets all picking position of giving ray
        /// </summary>
        /// <param name="ray">Picking ray</param>
        /// <param name="rayPickingParams">Ray picking params</param>
        /// <param name="results">Picking results</param>
        /// <returns>Returns true if ground position found</returns>
        public bool PickAll(Ray ray, RayPickingParams rayPickingParams, out PickingResult<Triangle>[] results)
        {
            results = null;

            var bsph = this.GetBoundingSphere();
            if (bsph.Intersects(ref ray))
            {
                bool facingOnly = rayPickingParams.HasFlag(RayPickingParams.FacingOnly);
                var triangles = this.GetVolume(rayPickingParams.HasFlag(RayPickingParams.Geometry));

                if (triangles.Any() && Intersection.IntersectAll(ray, triangles, facingOnly, out Vector3[] pos, out Triangle[] tri, out float[] ds))
                {
                    results = new PickingResult<Triangle>[pos.Length];

                    for (int i = 0; i < results.Length; i++)
                    {
                        results[i] = new PickingResult<Triangle>
                        {
                            Position = pos[i],
                            Item = tri[i],
                            Distance = ds[i]
                        };
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets internal volume
        /// </summary>
        /// <param name="full"></param>
        /// <returns>Returns internal volume</returns>
        public IEnumerable<Triangle> GetVolume(bool full)
        {
            if (!full && this.DrawingData?.VolumeMesh?.Any() == true)
            {
                return Triangle.Transform(this.DrawingData.VolumeMesh, this.Manipulator.LocalTransform);
            }

            return this.GetTriangles(true);
        }

        /// <summary>
        /// Gets whether the sphere intersects with the current object
        /// </summary>
        /// <param name="sphere">Sphere</param>
        /// <param name="result">Picking results</param>
        /// <returns>Returns true if intersects</returns>
        public bool Intersects(IntersectionVolumeSphere sphere, out PickingResult<Triangle> result)
        {
            result = new PickingResult<Triangle>()
            {
                Distance = float.MaxValue,
            };

            var bsph = this.GetBoundingSphere();
            if (bsph.Intersects(sphere))
            {
                var mesh = GetVolume(false);
                if (Intersection.SphereIntersectsMesh(sphere, mesh, out Triangle tri, out Vector3 position, out float distance))
                {
                    result.Distance = distance;
                    result.Position = position;
                    result.Item = tri;

                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Model extensions
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Adds a component to the scene
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="description">Description</param>
        /// <param name="usage">Component usage</param>
        /// <param name="order">Processing order</param>
        /// <returns>Returns the created component</returns>
        public static async Task<Model> AddComponentModel(this Scene scene, ModelDescription description, SceneObjectUsages usage = SceneObjectUsages.None, int order = 0)
        {
            Model component = null;

            await Task.Run(() =>
            {
                component = new Model(scene, description);

                scene.AddComponent(component, usage, order);
            });

            return component;
        }
    }
}
