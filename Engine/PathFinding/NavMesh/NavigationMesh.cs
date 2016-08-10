﻿using System;
using SharpDX;

namespace Engine.PathFinding.NavMesh
{
    using Engine.Common;

    /// <summary>
    /// Navigation Mesh
    /// </summary>
    public class NavigationMesh : IGraph
    {
        /// <summary>
        /// 
        /// </summary>
        protected TiledNavigationMesh TiledNavigationMesh = null;
        /// <summary>
        /// 
        /// </summary>
        protected NavigationMeshQuery Query = null;
        /// <summary>
        /// 
        /// </summary>
        protected NavigationMeshNode[] Nodes = null;

        /// <summary>
        /// Navigation Mesh Build
        /// </summary>
        /// <param name="vertices">Vertices</param>
        /// <param name="indices">Indices</param>
        /// <param name="settings">Generation settings</param>
        /// <returns>Returns a navigation mesh</returns>
        public static NavigationMesh Build(VertexData[] vertices, uint[] indices, NavigationMeshGenerationSettings settings)
        {
            int tris = indices.Length / 3;

            Triangle[] triangles = new Triangle[tris];

            int index = 0;
            for (int i = 0; i < tris; i++)
            {
                triangles[i] = new Triangle(
                    vertices[indices[index++]].Position.Value,
                    vertices[indices[index++]].Position.Value,
                    vertices[indices[index++]].Position.Value);
            }

            return Build(triangles, settings);
        }
        /// <summary>
        /// Navigation Mesh Build
        /// </summary>
        /// <param name="triangles">List of triangles</param>
        /// <param name="settings">Generation settings</param>
        /// <returns>Returns a navigation mesh</returns>
        public static NavigationMesh Build(Triangle[] triangles, NavigationMeshGenerationSettings settings)
        {
            BoundingBox bbox = BoundingBox.FromPoints(triangles[0].GetCorners());
            Array.ForEach(triangles, tri => bbox = BoundingBox.Merge(bbox, BoundingBox.FromPoints(tri.GetCorners())));

            var fh = new HeightField(bbox, settings.CellSize, settings.CellHeight);
            fh.RasterizeTriangles(triangles, Area.Default);
            fh.FilterLedgeSpans(settings.VoxelAgentHeight, settings.VoxelMaxClimb);
            fh.FilterLowHangingWalkableObstacles(settings.VoxelMaxClimb);
            fh.FilterWalkableLowHeightSpans(settings.VoxelAgentHeight);

            var ch = new CompactHeightField(fh, settings.VoxelAgentHeight, settings.VoxelMaxClimb);
            ch.Erode(settings.VoxelAgentRadius);
            ch.BuildDistanceField();
            ch.BuildRegions(0, settings.MinRegionSize, settings.MergedRegionSize);

            var cs = ch.BuildContourSet(settings.MaxEdgeError, settings.MaxEdgeLength, settings.ContourFlags);

            var pm = new PolyMesh(cs, settings.CellSize, settings.CellHeight, 0, settings.VertsPerPoly);

            var pmd = new PolyMeshDetail(pm, ch, settings.SampleDistance, settings.MaxSampleError);

            var builder = new NavigationMeshBuilder(
                pm,
                pmd,
                new OffMeshConnection[0],
                settings.CellSize,
                settings.CellHeight,
                settings.VertsPerPoly,
                settings.MaxClimb,
                settings.BuildBoundingVolumeTree,
                settings.AgentHeight,
                settings.AgentRadius);

            var nm = new NavigationMesh();
            nm.TiledNavigationMesh = new TiledNavigationMesh(builder);
            nm.Query = new NavigationMeshQuery(nm.TiledNavigationMesh, 2048);
            nm.Nodes = new NavigationMeshNode[pmd.MeshCount];

            for (int i = 0; i < pmd.MeshCount; i++)
            {
                var mesh = pmd.Meshes[i];

                nm.Nodes[i] = new NavigationMeshNode(nm, new Polygon(mesh.VertexCount));
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    nm.Nodes[i].Poly.Points[v] = pmd.Verts[mesh.VertexIndex + v];
                }
            }

            return nm;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IGraphNode[] GetNodes()
        {
            return Array.ConvertAll(this.Nodes, (n) => { return (IGraphNode)n; });
        }
        /// <summary>
        /// Finds a path over the navigation mesh
        /// </summary>
        /// <param name="from">From position</param>
        /// <param name="to">To position</param>
        /// <returns>Returns path between the specified points if exists</returns>
        public PathFindingPath FindPath(Vector3 from, Vector3 to)
        {
            Vector3[] path;
            if (this.Query.FindPath(from, to, out path))
            {
                return new PathFindingPath(path);
            }

            return null;
        }
        /// <summary>
        /// Gets text representation of instance
        /// </summary>
        /// <returns>Returns text representation</returns>
        public override string ToString()
        {
            return "NavMesh";
        }
    }
}
