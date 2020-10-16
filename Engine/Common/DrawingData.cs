﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Engine.Common
{
    using Engine.Animation;
    using Engine.Content;

    /// <summary>
    /// Mesh data
    /// </summary>
    public class DrawingData : IDisposable
    {
        /// <summary>
        /// Volume mesh triangle list
        /// </summary>
        private readonly List<Triangle> volumeMesh = new List<Triangle>();
        /// <summary>
        /// Light list
        /// </summary>
        private readonly List<ISceneLight> lights = new List<ISceneLight>();

        /// <summary>
        /// Game instance
        /// </summary>
        protected readonly Game Game = null;
        /// <summary>
        /// Description
        /// </summary>
        protected readonly DrawingDataDescription Description;

        /// <summary>
        /// Materials dictionary
        /// </summary>
        public MaterialDictionary Materials { get; set; } = new MaterialDictionary();
        /// <summary>
        /// Texture dictionary
        /// </summary>
        public TextureDictionary Textures { get; set; } = new TextureDictionary();
        /// <summary>
        /// Meshes
        /// </summary>
        public MeshDictionary Meshes { get; set; } = new MeshDictionary();
        /// <summary>
        /// Volume mesh
        /// </summary>
        public IEnumerable<Triangle> VolumeMesh
        {
            get
            {
                return volumeMesh.ToArray();
            }
        }
        /// <summary>
        /// Datos de animación
        /// </summary>
        public SkinningData SkinningData { get; set; } = null;
        /// <summary>
        /// Lights collection
        /// </summary>
        public IEnumerable<ISceneLight> Lights
        {
            get
            {
                return lights.ToArray();
            }
        }

        /// <summary>
        /// Model initialization
        /// </summary>
        /// <param name="game">Game</param>
        /// <param name="name">Owner name</param>
        /// <param name="modelContent">Model content</param>
        /// <param name="description">Data description</param>
        /// <param name="instancingBuffer">Instancing buffer descriptor</param>
        /// <returns>Returns the generated drawing data objects</returns>
        public static async Task<DrawingData> Build(Game game, string name, ModelContent modelContent, DrawingDataDescription description, BufferDescriptor instancingBuffer = null)
        {
            DrawingData res = null;

            await Task.Run(()=>
            {
                res = new DrawingData(game, description);

                //Animation
                if (description.LoadAnimation)
                {
                    InitializeSkinningData(res, modelContent);
                }

                //Images
                InitializeTextures(res, game, modelContent, description.TextureCount);

                //Materials
                InitializeMaterials(res, modelContent);

                //Skins & Meshes
                InitializeGeometry(res, modelContent, description);

                //Update meshes into device
                InitializeMeshes(res, game, name, description.DynamicBuffers, instancingBuffer);

                //Lights
                InitializeLights(res, modelContent);
            });

            return res;
        }
        /// <summary>
        /// Initialize textures
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="game">Game</param>
        /// <param name="modelContent">Model content</param>
        /// <param name="textureCount">Texture count</param>
        private static void InitializeTextures(DrawingData drw, Game game, ModelContent modelContent, int textureCount)
        {
            if (modelContent.Images != null)
            {
                foreach (string images in modelContent.Images.Keys)
                {
                    var info = modelContent.Images[images];

                    var view = game.ResourceManager.RequestResource(info);
                    if (view != null)
                    {
                        drw.Textures.Add(images, view);

                        //Set the maximum texture index in the model
                        if (info.Count > textureCount) textureCount = info.Count;
                    }
                }
            }
        }
        /// <summary>
        /// Initialize materials
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="modelContent">Model content</param>
        private static void InitializeMaterials(DrawingData drw, ModelContent modelContent)
        {
            foreach (string mat in modelContent.Materials?.Keys)
            {
                var effectInfo = modelContent.Materials[mat];

                MeshMaterial meshMaterial = new MeshMaterial()
                {
                    Material = new Material(effectInfo),
                    EmissionTexture = drw.Textures[effectInfo.EmissionTexture],
                    AmbientTexture = drw.Textures[effectInfo.AmbientTexture],
                    DiffuseTexture = drw.Textures[effectInfo.DiffuseTexture],
                    SpecularTexture = drw.Textures[effectInfo.SpecularTexture],
                    ReflectiveTexture = drw.Textures[effectInfo.ReflectiveTexture],
                    NormalMap = drw.Textures[effectInfo.NormalMapTexture],
                };

                drw.Materials.Add(mat, meshMaterial);
            }
        }
        /// <summary>
        /// Initilize geometry
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="modelContent">Model content</param>
        /// <param name="description">Description</param>
        private static void InitializeGeometry(DrawingData drw, ModelContent modelContent, DrawingDataDescription description)
        {
            foreach (string meshName in modelContent.Geometry.Keys)
            {
                InitializeGeometryMesh(drw, modelContent, description, meshName);
            }
        }
        /// <summary>
        /// Initialize geometry mesh
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="modelContent">Model content</param>
        /// <param name="description">Description</param>
        /// <param name="meshName">Mesh name</param>
        private static void InitializeGeometryMesh(DrawingData drw, ModelContent modelContent, DrawingDataDescription description, string meshName)
        {
            //Get skinning data
            var isSkinned = ReadSkinningData(
                description, modelContent, meshName,
                out var bindShapeMatrix, out var weights, out var jointNames);

            //Process the mesh geometry material by material
            var dictGeometry = modelContent.Geometry[meshName];

            foreach (string material in dictGeometry.Keys)
            {
                var geometry = dictGeometry[material];

                if (geometry.IsVolume)
                {
                    //If volume, store position only
                    drw.volumeMesh.AddRange(geometry.GetTriangles());

                    continue;
                }

                //Get vertex type
                var vertexType = GetVertexType(geometry.VertexType, isSkinned, description.LoadNormalMaps, drw.Materials, material);

                //Process the vertex data
                geometry.ProcessVertexData(
                    vertexType,
                    description.Constraint,
                    out var vertices, out var indices);

                if (!bindShapeMatrix.IsIdentity)
                {
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = vertices[i].Transform(bindShapeMatrix);
                    }
                }

                //Convert the vertex data to final mesh data
                var vertexList = VertexData.Convert(
                    vertexType,
                    vertices,
                    weights,
                    jointNames);

                if (vertexList.Any())
                {
                    //Create and store the mesh into the drawing data
                    Mesh nMesh = new Mesh(
                        meshName,
                        geometry.Topology,
                        geometry.Transform,
                        vertexList,
                        indices);

                    drw.Meshes.Add(meshName, geometry.Material, nMesh);
                }
            }
        }
        /// <summary>
        /// Get vertex type from geometry
        /// </summary>
        /// <param name="vertexType">Vertex type</param>
        /// <param name="isSkinned">Sets wether the current geometry has skinning data or not</param>
        /// <param name="loadNormalMaps">Load normal maps flag</param>
        /// <param name="materials">Material dictionary</param>
        /// <param name="material">Material name</param>
        /// <returns>Returns the vertex type</returns>
        private static VertexTypes GetVertexType(VertexTypes vertexType, bool isSkinned, bool loadNormalMaps, MaterialDictionary materials, string material)
        {
            var res = vertexType;
            if (isSkinned)
            {
                //Get skinned equivalent
                res = VertexData.GetSkinnedEquivalent(res);
            }

            if (!loadNormalMaps)
            {
                return res;
            }

            if (VertexData.IsTextured(res) && !VertexData.IsTangent(res))
            {
                var meshMaterial = materials[material];
                if (meshMaterial?.NormalMap != null)
                {
                    //Get tangent equivalent
                    res = VertexData.GetTangentEquivalent(res);
                }
            }

            return res;
        }
        /// <summary>
        /// Reads skinning data
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="modelContent">Model content</param>
        /// <param name="meshName">Mesh name</param>
        /// <param name="bindShapeMatrix">Resulting bind shape matrix</param>
        /// <param name="weights">Resulting weights</param>
        /// <param name="jointNames">Resulting joints</param>
        /// <returns>Returns true if the model has skinnging data</returns>
        private static bool ReadSkinningData(DrawingDataDescription description, ModelContent modelContent, string meshName, out Matrix bindShapeMatrix, out Weight[] weights, out string[] jointNames)
        {
            bindShapeMatrix = Matrix.Identity;
            weights = null;
            jointNames = null;

            if (description.LoadAnimation && modelContent.Controllers != null && modelContent.SkinningInfo != null)
            {
                var cInfo = modelContent.Controllers.GetControllerForMesh(meshName);
                if (cInfo != null)
                {
                    //Apply shape matrix if controller exists but we are not loading animation info
                    bindShapeMatrix = cInfo.BindShapeMatrix;
                    weights = cInfo.Weights;

                    //Find skeleton for controller
                    var sInfo = modelContent.SkinningInfo[cInfo.Armature];
                    jointNames = sInfo.Skeleton.GetJointNames();

                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Initialize skinning data
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="modelContent">Model content</param>
        private static void InitializeSkinningData(DrawingData drw, ModelContent modelContent)
        {
            if (modelContent.SkinningInfo?.Count > 0)
            {
                //Use the definition to read animation data into a clip dictionary
                foreach (var sInfo in modelContent.SkinningInfo.Values)
                {
                    if (drw.SkinningData != null)
                    {
                        continue;
                    }

                    drw.SkinningData = new SkinningData(sInfo.Skeleton);

                    var animations = InitializeJoints(modelContent, sInfo.Skeleton.Root, sInfo.Controllers);

                    drw.SkinningData.Initialize(
                        animations,
                        modelContent.Animations.Definition);
                }
            }
        }
        /// <summary>
        /// Initialize skeleton data
        /// </summary>
        /// <param name="modelContent">Model content</param>
        /// <param name="joint">Joint to initialize</param>
        /// <param name="animations">Animation list to feed</param>
        private static JointAnimation[] InitializeJoints(ModelContent modelContent, Joint joint, string[] skinController)
        {
            List<JointAnimation> animations = new List<JointAnimation>();

            List<JointAnimation> boneAnimations = new List<JointAnimation>();

            //Find keyframes for current bone
            var c = FindJointKeyframes(joint.Name, modelContent.Animations);
            if (c != null && c.Length > 0)
            {
                //Set bones
                Array.ForEach(c, (a) =>
                {
                    boneAnimations.Add(new JointAnimation(a.Joint, a.Keyframes));
                });
            }

            if (boneAnimations.Count > 0)
            {
                //Only one bone animation (for now)
                animations.Add(boneAnimations[0]);
            }

            foreach (string controllerName in skinController)
            {
                var controller = modelContent.Controllers[controllerName];

                Matrix ibm = Matrix.Identity;

                if (controller.InverseBindMatrix.ContainsKey(joint.Name))
                {
                    ibm = controller.InverseBindMatrix[joint.Name];
                }

                joint.Offset = ibm;
            }

            if (joint.Childs?.Length > 0)
            {
                foreach (var child in joint.Childs)
                {
                    var ja = InitializeJoints(modelContent, child, skinController);

                    animations.AddRange(ja);
                }
            }

            return animations.ToArray();
        }
        /// <summary>
        /// Initialize mesh buffers in the graphics device
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="game">Game</param>
        /// <param name="name">Owner name</param>
        /// <param name="dynamicBuffers">Create dynamic buffers</param>
        /// <param name="instancingBuffer">Instancing buffer descriptor</param>
        private static void InitializeMeshes(DrawingData drw, Game game, string name, bool dynamicBuffers, BufferDescriptor instancingBuffer)
        {
            foreach (var dictionary in drw.Meshes.Values)
            {
                foreach (var mesh in dictionary.Values)
                {
                    //Vertices
                    mesh.VertexBuffer = game.BufferManager.AddVertexData($"{name}.{mesh.Name}", dynamicBuffers, mesh.Vertices, instancingBuffer);

                    if (mesh.Indexed)
                    {
                        //Indices
                        mesh.IndexBuffer = game.BufferManager.AddIndexData($"{name}.{mesh.Name}", dynamicBuffers, mesh.Indices);
                    }
                }
            }
        }
        /// <summary>
        /// Find keyframes for a joint
        /// </summary>
        /// <param name="jointName">Joint name</param>
        /// <param name="animations">Animation dictionary</param>
        /// <returns>Returns joint's animation content</returns>
        private static AnimationContent[] FindJointKeyframes(string jointName, Dictionary<string, AnimationContent[]> animations)
        {
            foreach (string key in animations.Keys)
            {
                if (Array.Exists(animations[key], a => a.Joint == jointName))
                {
                    return Array.FindAll(animations[key], a => a.Joint == jointName);
                }
            }

            return new AnimationContent[] { };
        }
        /// <summary>
        /// Initialize lights
        /// </summary>
        /// <param name="drw">Drawing data</param>
        /// <param name="modelContent">Model content</param>
        private static void InitializeLights(DrawingData drw, ModelContent modelContent)
        {
            foreach (var key in modelContent.Lights.Keys)
            {
                var l = modelContent.Lights[key];

                if (l.LightType == LightContentTypes.Point)
                {
                    drw.lights.Add(l.CreatePointLight());
                }
                else if (l.LightType == LightContentTypes.Spot)
                {
                    drw.lights.Add(l.CreateSpotLight());
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        /// <param name="description">Description</param>
        public DrawingData(Game game, DrawingDataDescription description)
        {
            Game = game;
            Description = description;
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~DrawingData()
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
                foreach (var item in Meshes?.Values)
                {
                    foreach (var mesh in item.Values)
                    {
                        //Remove data from buffer manager
                        Game.BufferManager?.RemoveVertexData(mesh.VertexBuffer);

                        if (mesh.IndexBuffer != null)
                        {
                            Game.BufferManager?.RemoveIndexData(mesh.IndexBuffer);
                        }

                        //Dispose the mesh
                        mesh.Dispose();
                    }
                }
                Meshes?.Clear();
                Meshes = null;

                Materials?.Clear();
                Materials = null;

                //Don't dispose textures!
                Textures?.Clear();
                Textures = null;

                SkinningData = null;
            }
        }

        /// <summary>
        /// Gets the drawing data's point list
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's point list</returns>
        public IEnumerable<Vector3> GetPoints(bool refresh = false)
        {
            return GetPoints(Matrix.Identity, refresh);
        }
        /// <summary>
        /// Gets the drawing data's point list
        /// </summary>
        /// <param name="transform">Transform to apply</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's point list</returns>
        public IEnumerable<Vector3> GetPoints(Matrix transform, bool refresh = false)
        {
            List<Vector3> points = new List<Vector3>();

            var meshMaterialList = Meshes.Values.ToArray();

            foreach (var dictionary in meshMaterialList)
            {
                var meshList = dictionary.Values.ToArray();

                foreach (var mesh in meshList)
                {
                    var meshPoints = mesh.GetPoints(refresh);
                    if (meshPoints.Any())
                    {
                        var trnPoints = meshPoints.ToArray();
                        Vector3.TransformCoordinate(trnPoints, ref transform, trnPoints);
                        points.AddRange(trnPoints);
                    }
                }
            }

            return points.ToArray();
        }
        /// <summary>
        /// Gets the drawing data's point list
        /// </summary>
        /// <param name="boneTransforms">Bone transforms list</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's point list</returns>
        public IEnumerable<Vector3> GetPoints(Matrix[] boneTransforms, bool refresh = false)
        {
            return GetPoints(Matrix.Identity, boneTransforms, refresh);
        }
        /// <summary>
        /// Gets the drawing data's point list
        /// </summary>
        /// <param name="transform">Global transform</param>
        /// <param name="boneTransforms">Bone transforms list</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's point list</returns>
        public IEnumerable<Vector3> GetPoints(Matrix transform, Matrix[] boneTransforms, bool refresh = false)
        {
            List<Vector3> points = new List<Vector3>();

            var meshMaterialList = Meshes.Values.ToArray();

            foreach (var dictionary in meshMaterialList)
            {
                var meshList = dictionary.Values.ToArray();

                foreach (var mesh in meshList)
                {
                    var meshPoints = mesh.GetPoints(boneTransforms, refresh);
                    if (meshPoints.Any())
                    {
                        var trnPoints = meshPoints.ToArray();
                        Vector3.TransformCoordinate(trnPoints, ref transform, trnPoints);
                        points.AddRange(trnPoints);
                    }
                }
            }

            return points.ToArray();
        }
        /// <summary>
        /// Gets the drawing data's triangle list
        /// </summary>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's triangle list</returns>
        public IEnumerable<Triangle> GetTriangles(bool refresh = false)
        {
            return GetTriangles(Matrix.Identity, refresh);
        }
        /// <summary>
        /// Gets the drawing data's triangle list
        /// </summary>
        /// <param name="transform">Transform to apply</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's triangle list</returns>
        public IEnumerable<Triangle> GetTriangles(Matrix transform, bool refresh = false)
        {
            List<Triangle> triangles = new List<Triangle>();

            var meshMaterialList = Meshes.Values.ToArray();

            foreach (var dictionary in meshMaterialList)
            {
                var meshList = dictionary.Values.ToArray();

                foreach (var mesh in meshList)
                {
                    var meshTriangles = mesh.GetTriangles(refresh);
                    triangles.AddRange(Triangle.Transform(meshTriangles, transform));
                }
            }

            return triangles.ToArray();
        }
        /// <summary>
        /// Gets the drawing data's triangle list
        /// </summary>
        /// <param name="boneTransforms">Bone transforms list</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's triangle list</returns>
        public IEnumerable<Triangle> GetTriangles(Matrix[] boneTransforms, bool refresh = false)
        {
            return GetTriangles(Matrix.Identity, boneTransforms, refresh);
        }
        /// <summary>
        /// Gets the drawing data's triangle list
        /// </summary>
        /// <param name="transform">Transform to apply</param>
        /// <param name="boneTransforms">Bone transforms list</param>
        /// <param name="refresh">Sets if the cache must be refresehd or not</param>
        /// <returns>Returns the drawing data's triangle list</returns>
        public IEnumerable<Triangle> GetTriangles(Matrix transform, Matrix[] boneTransforms, bool refresh = false)
        {
            List<Triangle> triangles = new List<Triangle>();

            var meshMaterialList = Meshes.Values.ToArray();

            foreach (var dictionary in meshMaterialList)
            {
                var meshList = dictionary.Values.ToArray();

                foreach (var mesh in meshList)
                {
                    var meshTriangles = mesh.GetTriangles(boneTransforms, refresh);
                    triangles.AddRange(Triangle.Transform(meshTriangles, transform));
                }
            }

            return triangles.ToArray();
        }

        /// <summary>
        /// Gets the first mesh by mesh name
        /// </summary>
        /// <param name="name">Name</param>
        public Mesh GetMeshByName(string name)
        {
            if (!Meshes.ContainsKey(name))
            {
                return null;
            }

            return Meshes[name].Values.FirstOrDefault();
        }
    }
}
