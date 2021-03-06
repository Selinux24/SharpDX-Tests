﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Engine.Common
{
    /// <summary>
    /// Geometry utilities
    /// </summary>
    public static class GeometryUtil
    {
        /// <summary>
        /// Generates a index for a triangle soup quad with the specified shape
        /// </summary>
        /// <param name="bufferShape">Buffer shape</param>
        /// <param name="triangles">Triangle count</param>
        /// <returns>Returns the generated index list</returns>
        public static IEnumerable<uint> GenerateIndices(IndexBufferShapes bufferShape, int triangles)
        {
            return GenerateIndices(LevelOfDetail.High, bufferShape, triangles);
        }
        /// <summary>
        /// Generates a index for a triangle soup quad with the specified shape
        /// </summary>
        /// <param name="lod">Level of detail</param>
        /// <param name="bufferShape">Buffer shape</param>
        /// <param name="triangles">Triangle count</param>
        /// <returns>Returns the generated index list</returns>
        public static IEnumerable<uint> GenerateIndices(LevelOfDetail lod, IndexBufferShapes bufferShape, int triangles)
        {
            uint offset = (uint)lod;
            uint fullSide = (uint)Math.Sqrt(triangles / 2f);

            int tris = triangles / (int)Math.Pow(offset, 2);

            int nodes = tris / 2;
            uint side = (uint)Math.Sqrt(nodes);
            uint sideLoss = side / 2;

            bool topSide =
                bufferShape == IndexBufferShapes.CornerTopLeft ||
                bufferShape == IndexBufferShapes.CornerTopRight ||
                bufferShape == IndexBufferShapes.SideTop;

            bool bottomSide =
                bufferShape == IndexBufferShapes.CornerBottomLeft ||
                bufferShape == IndexBufferShapes.CornerBottomRight ||
                bufferShape == IndexBufferShapes.SideBottom;

            bool leftSide =
                bufferShape == IndexBufferShapes.CornerBottomLeft ||
                bufferShape == IndexBufferShapes.CornerTopLeft ||
                bufferShape == IndexBufferShapes.SideLeft;

            bool rightSide =
                bufferShape == IndexBufferShapes.CornerBottomRight ||
                bufferShape == IndexBufferShapes.CornerTopRight ||
                bufferShape == IndexBufferShapes.SideRight;

            uint totalTriangles = (uint)tris;
            if (topSide) totalTriangles -= sideLoss;
            if (bottomSide) totalTriangles -= sideLoss;
            if (leftSide) totalTriangles -= sideLoss;
            if (rightSide) totalTriangles -= sideLoss;

            List<uint> indices = new List<uint>((int)totalTriangles * 3);

            for (uint y = 1; y < side; y += 2)
            {
                for (uint x = 1; x < side; x += 2)
                {
                    uint indexPRow = (((y - 1) * offset) * (fullSide + 1)) + (x * offset);
                    uint indexCRow = (((y + 0) * offset) * (fullSide + 1)) + (x * offset);
                    uint indexNRow = (((y + 1) * offset) * (fullSide + 1)) + (x * offset);

                    bool firstRow = y == 1;
                    bool lastRow = y == side - 1;
                    bool firstColumn = x == 1;
                    bool lastColumn = x == side - 1;

                    //Top side
                    var top = ComputeTopSide(firstRow, topSide, offset, indexPRow, indexCRow);

                    //Bottom side
                    var bottom = ComputeBottomSide(lastRow, bottomSide, offset, indexCRow, indexNRow);

                    //Left side
                    var left = ComputeLeftSide(firstColumn, leftSide, offset, indexPRow, indexCRow, indexNRow);

                    //Right side
                    var right = ComputeRightSide(lastColumn, rightSide, offset, indexPRow, indexCRow, indexNRow);

                    indices.AddRange(top);
                    indices.AddRange(bottom);
                    indices.AddRange(left);
                    indices.AddRange(right);
                }
            }

            return indices.ToArray();
        }
        /// <summary>
        /// Computes the top side indexes for triangle soup quad
        /// </summary>
        /// <param name="firstRow">It's the first row</param>
        /// <param name="topSide">It's the top side</param>
        /// <param name="offset">Index offset</param>
        /// <param name="indexPRow">P index</param>
        /// <param name="indexCRow">C index</param>
        /// <returns>Returns the indexes list</returns>
        private static IEnumerable<uint> ComputeTopSide(bool firstRow, bool topSide, uint offset, uint indexPRow, uint indexCRow)
        {
            if (firstRow && topSide)
            {
                return new[]
                {
                    //Top
                    indexCRow,
                    indexPRow - (1 * offset),
                    indexPRow + (1 * offset),
                };
            }
            else
            {
                return new[]
                {
                    //Top left
                    indexCRow,
                    indexPRow - (1 * offset),
                    indexPRow,
                    //Top right
                    indexCRow,
                    indexPRow,
                    indexPRow + (1 * offset),
                };
            }
        }
        /// <summary>
        /// Computes the bottom side indexes for triangle soup quad
        /// </summary>
        /// <param name="lastRow">It's the last row</param>
        /// <param name="bottomSide">It's the bottom side</param>
        /// <param name="offset">Index offset</param>
        /// <param name="indexCRow">C index</param>
        /// <param name="indexNRow">N index</param>
        /// <returns>Returns the indexes list</returns>
        private static IEnumerable<uint> ComputeBottomSide(bool lastRow, bool bottomSide, uint offset, uint indexCRow, uint indexNRow)
        {
            if (lastRow && bottomSide)
            {
                return new[]
                {
                    //Bottom only
                    indexCRow,
                    indexNRow + (1 * offset),
                    indexNRow - (1 * offset),
                };
            }
            else
            {
                return new[]
                {
                    //Bottom left
                    indexCRow,
                    indexNRow,
                    indexNRow - (1 * offset),
                    //Bottom right
                    indexCRow,
                    indexNRow + (1 * offset),
                    indexNRow,
                };
            }
        }
        /// <summary>
        /// Computes the left side indexes for triangle soup quad
        /// </summary>
        /// <param name="firstColumn">It's the first column</param>
        /// <param name="leftSide">It's the left side</param>
        /// <param name="offset">Index offset</param>
        /// <param name="indexPRow">P index</param>
        /// <param name="indexCRow">C index</param>
        /// <param name="indexNRow">N index</param>
        /// <returns>Returns the indexes list</returns>
        private static IEnumerable<uint> ComputeLeftSide(bool firstColumn, bool leftSide, uint offset, uint indexPRow, uint indexCRow, uint indexNRow)
        {
            if (firstColumn && leftSide)
            {
                return new[]
                {
                    //Left only
                    indexCRow,
                    indexNRow - (1 * offset),
                    indexPRow - (1 * offset),
                };
            }
            else
            {
                return new[]
                {
                    //Left top
                    indexCRow,
                    indexCRow - (1 * offset),
                    indexPRow - (1 * offset),
                    //Left bottom
                    indexCRow,
                    indexNRow - (1 * offset),
                    indexCRow - (1 * offset),
                };
            }
        }
        /// <summary>
        /// Computes the right side indexes for triangle soup quad
        /// </summary>
        /// <param name="lastColumn">It's the last column</param>
        /// <param name="rightSide">It's the right side</param>
        /// <param name="offset">Index offset</param>
        /// <param name="indexPRow">P index</param>
        /// <param name="indexCRow">C index</param>
        /// <param name="indexNRow">N index</param>
        /// <returns>Returns the indexes list</returns>
        private static IEnumerable<uint> ComputeRightSide(bool lastColumn, bool rightSide, uint offset, uint indexPRow, uint indexCRow, uint indexNRow)
        {
            if (lastColumn && rightSide)
            {
                return new[]
                {
                    //Right only
                    indexCRow,
                    indexPRow + (1 * offset),
                    indexNRow + (1 * offset),
                };
            }
            else
            {
                return new[]
                {
                    //Right top
                    indexCRow,
                    indexPRow + (1 * offset),
                    indexCRow + (1 * offset),
                    //Right bottom
                    indexCRow,
                    indexCRow + (1 * offset),
                    indexNRow + (1 * offset),
                };
            }
        }
        /// <summary>
        /// Toggle coordinates from left-handed to right-handed and vice versa
        /// </summary>
        /// <typeparam name="T">Index type</typeparam>
        /// <param name="indices">Indices in a triangle list topology</param>
        /// <returns>Returns a new array</returns>
        public static IEnumerable<T> ChangeCoordinate<T>(IEnumerable<T> indices)
        {
            var idx = indices.ToArray();

            T[] res = new T[idx.Length];

            for (int i = 0; i < idx.Length; i += 3)
            {
                res[i + 0] = idx[i + 0];
                res[i + 1] = idx[i + 2];
                res[i + 2] = idx[i + 1];
            }

            return res;
        }

        /// <summary>
        /// Creates a new UV map from parameters
        /// </summary>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <param name="texU">Texture U</param>
        /// <param name="texV">Texture V</param>
        /// <param name="texWidth">Texture total width</param>
        /// <param name="texHeight">Texture total height</param>
        /// <returns>Returns the new UP map for the sprite</returns>
        public static Vector4 CreateUVMap(float width, float height, float texU, float texV, float texWidth, float texHeight)
        {
            //Texture map
            float u0 = texWidth > 0 ? (texU) / texWidth : 0;
            float v0 = texHeight > 0 ? (texV) / texHeight : 0;
            float u1 = texWidth > 0 ? (texU + width) / texWidth : 1;
            float v1 = texHeight > 0 ? (texV + height) / texHeight : 1;

            return new Vector4(u0, v0, u1, v1);
        }

        /// <summary>
        /// Creates a line list
        /// </summary>
        /// <param name="lines">Line list</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateLineList(IEnumerable<Line3D> lines)
        {
            List<Vector3> data = new List<Vector3>();

            foreach (var line in lines)
            {
                data.Add(line.Point1);
                data.Add(line.Point2);
            }

            return new GeometryDescriptor()
            {
                Vertices = data.ToArray()
            };
        }
        /// <summary>
        /// Creates a line list
        /// </summary>
        /// <param name="lines">Line list</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateIndexedLineList(IEnumerable<Line3D> lines)
        {
            List<Vector3> vData = new List<Vector3>();
            List<uint> iData = new List<uint>();

            foreach (var line in lines)
            {
                var p1 = line.Point1;
                var p2 = line.Point2;

                var i1 = vData.IndexOf(p1);
                var i2 = vData.IndexOf(p2);

                if (i1 >= 0)
                {
                    iData.Add((uint)i1);
                }
                else
                {
                    vData.Add(p1);
                    iData.Add((uint)vData.Count - 1);
                }

                if (i2 >= 0)
                {
                    iData.Add((uint)i2);
                }
                else
                {
                    vData.Add(p2);
                    iData.Add((uint)vData.Count - 1);
                }
            }

            return new GeometryDescriptor()
            {
                Vertices = vData.ToArray(),
                Indices = iData.ToArray(),
            };
        }
        /// <summary>
        /// Creates a triangle list
        /// </summary>
        /// <param name="triangles">Triangle list</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateTriangleList(IEnumerable<Triangle> triangles)
        {
            List<Vector3> vData = new List<Vector3>();

            foreach (var triangle in triangles)
            {
                vData.Add(triangle.Point1);
                vData.Add(triangle.Point2);
                vData.Add(triangle.Point3);
            }

            return new GeometryDescriptor()
            {
                Vertices = vData.ToArray(),
            };
        }
        /// <summary>
        /// Creates a triangle list
        /// </summary>
        /// <param name="triangles">Triangle list</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateIndexedTriangleList(IEnumerable<Triangle> triangles)
        {
            List<Vector3> vData = new List<Vector3>();
            List<uint> iData = new List<uint>();

            foreach (var triangle in triangles)
            {
                var p1 = triangle.Point1;
                var p2 = triangle.Point2;
                var p3 = triangle.Point3;

                var i1 = vData.IndexOf(p1);
                var i2 = vData.IndexOf(p2);
                var i3 = vData.IndexOf(p3);

                if (i1 >= 0)
                {
                    iData.Add((uint)i1);
                }
                else
                {
                    vData.Add(p1);
                    iData.Add((uint)vData.Count - 1);
                }

                if (i2 >= 0)
                {
                    iData.Add((uint)i2);
                }
                else
                {
                    vData.Add(p2);
                    iData.Add((uint)vData.Count - 1);
                }

                if (i3 >= 0)
                {
                    iData.Add((uint)i3);
                }
                else
                {
                    vData.Add(p3);
                    iData.Add((uint)vData.Count - 1);
                }
            }

            return new GeometryDescriptor()
            {
                Vertices = vData.ToArray(),
                Indices = iData.ToArray(),
            };
        }
        /// <summary>
        /// Creates a unit sprite
        /// </summary>
        /// <returns>Returns a geometry descriptor</returns>
        /// <remarks>Unit size with then center in X=0.5;Y=0.5</remarks>
        public static GeometryDescriptor CreateUnitSprite()
        {
            return CreateSprite(new Vector2(-0.5f, 0.5f), 1, 1, 0, 0);
        }
        /// <summary>
        /// Creates a unit sprite
        /// </summary>
        /// <param name="uvMap">UV map</param>
        /// <returns>Returns a geometry descriptor</returns>
        /// <remarks>Unit size with then center in X=0.5;Y=0.5</remarks>
        public static GeometryDescriptor CreateUnitSprite(Vector4 uvMap)
        {
            return CreateSprite(new Vector2(-0.5f, 0.5f), 1, 1, 0, 0, uvMap);
        }
        /// <summary>
        /// Creates a sprite
        /// </summary>
        /// <param name="position">Sprite position</param>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSprite(Vector2 position, float width, float height)
        {
            return CreateSprite(position, width, height, 0, 0, new Vector4(0, 0, 1, 1));
        }
        /// <summary>
        /// Creates a sprite
        /// </summary>
        /// <param name="position">Sprite position</param>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <param name="formWidth">Render form width</param>
        /// <param name="formHeight">Render form height</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSprite(Vector2 position, float width, float height, float formWidth, float formHeight)
        {
            return CreateSprite(position, width, height, formWidth, formHeight, new Vector4(0, 0, 1, 1));
        }
        /// <summary>
        /// Creates a sprite
        /// </summary>
        /// <param name="position">Sprite position</param>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <param name="formWidth">Render form width</param>
        /// <param name="formHeight">Render form height</param>
        /// <param name="uvMap">UV map</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSprite(Vector2 position, float width, float height, float formWidth, float formHeight, Vector4 uvMap)
        {
            Vector3[] vertices = new Vector3[4];
            Vector2[] uvs = new Vector2[4];

            float left = (formWidth * 0.5f * -1f) + position.X;
            float right = left + width;
            float top = (formHeight * 0.5f) + position.Y;
            float bottom = top - height;

            //Texture map
            float u0 = uvMap.X;
            float v0 = uvMap.Y;
            float u1 = uvMap.Z;
            float v1 = uvMap.W;

            vertices[0] = new Vector3(left, top, 0.0f);
            uvs[0] = new Vector2(u0, v0);

            vertices[1] = new Vector3(right, bottom, 0.0f);
            uvs[1] = new Vector2(u1, v1);

            vertices[2] = new Vector3(left, bottom, 0.0f);
            uvs[2] = new Vector2(u0, v1);

            vertices[3] = new Vector3(right, top, 0.0f);
            uvs[3] = new Vector2(u1, v0);

            uint[] indices = new uint[6];

            indices[0] = 0;
            indices[1] = 1;
            indices[2] = 2;

            indices[3] = 0;
            indices[4] = 3;
            indices[5] = 1;

            return new GeometryDescriptor()
            {
                Vertices = vertices,
                Indices = indices,
                Uvs = uvs,
            };
        }
        /// <summary>
        /// Creates a screen
        /// </summary>
        /// <param name="form">Form</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateScreen(EngineForm form)
        {
            return CreateScreen(form.RenderWidth, form.RenderHeight);
        }
        /// <summary>
        /// Creates a screen
        /// </summary>
        /// <param name="renderWidth">Render area width</param>
        /// <param name="renderHeight">Render area height</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateScreen(int renderWidth, int renderHeight)
        {
            Vector3[] vertices = new Vector3[4];
            Vector2[] uvs = new Vector2[4];
            uint[] indices = new uint[6];

            float width = renderWidth;
            float height = renderHeight;

            float left = ((width / 2) * -1);
            float right = left + width;
            float top = (height / 2);
            float bottom = top - height;

            vertices[0] = new Vector3(left, top, 0.0f);
            uvs[0] = new Vector2(0.0f, 0.0f);

            vertices[1] = new Vector3(right, bottom, 0.0f);
            uvs[1] = new Vector2(1.0f, 1.0f);

            vertices[2] = new Vector3(left, bottom, 0.0f);
            uvs[2] = new Vector2(0.0f, 1.0f);

            vertices[3] = new Vector3(right, top, 0.0f);
            uvs[3] = new Vector2(1.0f, 0.0f);

            indices[0] = 0;
            indices[1] = 1;
            indices[2] = 2;

            indices[3] = 0;
            indices[4] = 3;
            indices[5] = 1;

            return new GeometryDescriptor()
            {
                Vertices = vertices,
                Indices = indices,
                Uvs = uvs,
            };
        }
        /// <summary>
        /// Creates a box
        /// </summary>
        /// <param name="bbox">Bounding box</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateBox(BoundingBox bbox)
        {
            return CreateBox(bbox.Center, bbox.Width, bbox.Height, bbox.Depth);
        }
        /// <summary>
        /// Creates a box
        /// </summary>
        /// <param name="obb">Oriented bounding box</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateBox(OrientedBoundingBox obb)
        {
            return CreateBox(obb.Center, obb.Extents.X * 2, obb.Extents.Y * 2, obb.Extents.Z * 2);
        }
        /// <summary>
        /// Creates a box
        /// </summary>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <param name="depth">Depth</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateBox(float width, float height, float depth)
        {
            return CreateBox(Vector3.Zero, width, height, depth);
        }
        /// <summary>
        /// Creates a box
        /// </summary>
        /// <param name="center">Box center</param>
        /// <param name="width">Width</param>
        /// <param name="height">Height</param>
        /// <param name="depth">Depth</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateBox(Vector3 center, float width, float height, float depth)
        {
            Vector3[] vertices = new Vector3[24];
            uint[] indices = new uint[36];

            float w2 = 0.5f * width;
            float h2 = 0.5f * height;
            float d2 = 0.5f * depth;

            // Fill in the front face vertex data.
            vertices[0] = new Vector3(-w2, -h2, -d2);
            vertices[1] = new Vector3(-w2, +h2, -d2);
            vertices[2] = new Vector3(+w2, +h2, -d2);
            vertices[3] = new Vector3(+w2, -h2, -d2);

            // Fill in the back face vertex data.
            vertices[4] = new Vector3(-w2, -h2, +d2);
            vertices[5] = new Vector3(+w2, -h2, +d2);
            vertices[6] = new Vector3(+w2, +h2, +d2);
            vertices[7] = new Vector3(-w2, +h2, +d2);

            // Fill in the top face vertex data.
            vertices[8] = new Vector3(-w2, +h2, -d2);
            vertices[9] = new Vector3(-w2, +h2, +d2);
            vertices[10] = new Vector3(+w2, +h2, +d2);
            vertices[11] = new Vector3(+w2, +h2, -d2);

            // Fill in the bottom face vertex data.
            vertices[12] = new Vector3(-w2, -h2, -d2);
            vertices[13] = new Vector3(+w2, -h2, -d2);
            vertices[14] = new Vector3(+w2, -h2, +d2);
            vertices[15] = new Vector3(-w2, -h2, +d2);

            // Fill in the left face vertex data.
            vertices[16] = new Vector3(-w2, -h2, +d2);
            vertices[17] = new Vector3(-w2, +h2, +d2);
            vertices[18] = new Vector3(-w2, +h2, -d2);
            vertices[19] = new Vector3(-w2, -h2, -d2);

            // Fill in the right face vertex data.
            vertices[20] = new Vector3(+w2, -h2, -d2);
            vertices[21] = new Vector3(+w2, +h2, -d2);
            vertices[22] = new Vector3(+w2, +h2, +d2);
            vertices[23] = new Vector3(+w2, -h2, +d2);

            // Fill in the front face index data
            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 0; indices[4] = 2; indices[5] = 3;

            // Fill in the back face index data
            indices[6] = 4; indices[7] = 5; indices[8] = 6;
            indices[9] = 4; indices[10] = 6; indices[11] = 7;

            // Fill in the top face index data
            indices[12] = 8; indices[13] = 9; indices[14] = 10;
            indices[15] = 8; indices[16] = 10; indices[17] = 11;

            // Fill in the bottom face index data
            indices[18] = 12; indices[19] = 13; indices[20] = 14;
            indices[21] = 12; indices[22] = 14; indices[23] = 15;

            // Fill in the left face index data
            indices[24] = 16; indices[25] = 17; indices[26] = 18;
            indices[27] = 16; indices[28] = 18; indices[29] = 19;

            // Fill in the right face index data
            indices[30] = 20; indices[31] = 21; indices[32] = 22;
            indices[33] = 20; indices[34] = 22; indices[35] = 23;

            if (center != Vector3.Zero)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] += center;
                }
            }

            return new GeometryDescriptor()
            {
                Vertices = vertices,
                Indices = indices,
            };
        }
        /// <summary>
        /// Creates a cone
        /// </summary>
        /// <param name="radius">The base radius</param>
        /// <param name="sliceCount">The base slice count</param>
        /// <param name="height">Cone height</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateCone(float radius, uint sliceCount, float height)
        {
            List<Vector3> vertList = new List<Vector3>();
            List<uint> indexList = new List<uint>();

            vertList.Add(new Vector3(0.0f, 0.0f, 0.0f));

            vertList.Add(new Vector3(0.0f, -height, 0.0f));

            float thetaStep = MathUtil.TwoPi / (float)sliceCount;

            for (int sl = 0; sl < sliceCount; sl++)
            {
                float theta = sl * thetaStep;

                Vector3 position;
                Vector3 normal;
                Vector3 tangent;
                Vector2 texture;

                // spherical to cartesian
                position.X = radius * (float)Math.Sin(MathUtil.PiOverTwo) * (float)Math.Cos(theta);
                position.Y = -height;
                position.Z = radius * (float)Math.Sin(MathUtil.PiOverTwo) * (float)Math.Sin(theta);

                normal = position;
                normal.Normalize();

                // Partial derivative of P with respect to theta
                tangent.X = -radius * (float)Math.Sin(MathUtil.PiOverTwo) * (float)Math.Sin(theta);
                tangent.Y = 0.0f;
                tangent.Z = +radius * (float)Math.Sin(MathUtil.PiOverTwo) * (float)Math.Cos(theta);
                tangent.Normalize();

                texture.X = theta / MathUtil.TwoPi;
                texture.Y = 1f;

                vertList.Add(position);
            }

            for (uint index = 0; index < sliceCount; index++)
            {
                indexList.Add(0);
                indexList.Add(index == sliceCount - 1 ? 2 : index + 3);
                indexList.Add(index + 2);

                indexList.Add(1);
                indexList.Add(index + 2);
                indexList.Add(index == sliceCount - 1 ? 2 : index + 3);
            }

            return new GeometryDescriptor()
            {
                Vertices = vertList.ToArray(),
                Indices = indexList.ToArray(),
            };
        }
        /// <summary>
        /// Creates a sphere
        /// </summary>
        /// <param name="sphere">Sphere</param>
        /// <param name="sliceCount">Slice count</param>
        /// <param name="stackCount">Stack count</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSphere(BoundingSphere sphere, uint sliceCount, uint stackCount)
        {
            return CreateSphere(sphere.Center, sphere.Radius, sliceCount, stackCount);
        }
        /// <summary>
        /// Creates a sphere
        /// </summary>
        /// <param name="radius">Radius</param>
        /// <param name="sliceCount">Slice count</param>
        /// <param name="stackCount">Stack count</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSphere(float radius, uint sliceCount, uint stackCount)
        {
            return CreateSphere(Vector3.Zero, radius, sliceCount, stackCount);
        }
        /// <summary>
        /// Creates a sphere
        /// </summary>
        /// <param name="center">Sphere center</param>
        /// <param name="radius">Radius</param>
        /// <param name="sliceCount">Slice count</param>
        /// <param name="stackCount">Stack count</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateSphere(Vector3 center, float radius, uint sliceCount, uint stackCount)
        {
            List<Vector3> vertList = new List<Vector3>();
            List<Vector3> normList = new List<Vector3>();
            List<Vector3> tangList = new List<Vector3>();
            List<Vector3> binmList = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();

            sliceCount--;
            stackCount++;

            #region Positions

            //North pole
            vertList.Add(new Vector3(0.0f, radius, 0.0f) + center);
            normList.Add(new Vector3(0.0f, 1.0f, 0.0f));
            tangList.Add(new Vector3(0.0f, 0.0f, 1.0f));
            binmList.Add(new Vector3(1.0f, 0.0f, 0.0f));
            uvList.Add(new Vector2(0.0f, 0.0f));

            float phiStep = MathUtil.Pi / stackCount;
            float thetaStep = 2.0f * MathUtil.Pi / sliceCount;

            for (int st = 1; st <= stackCount - 1; ++st)
            {
                float phi = st * phiStep;

                for (int sl = 0; sl <= sliceCount; ++sl)
                {
                    float theta = sl * thetaStep;

                    float x = (float)Math.Sin(phi) * (float)Math.Cos(theta);
                    float y = (float)Math.Cos(phi);
                    float z = (float)Math.Sin(phi) * (float)Math.Sin(theta);

                    float tX = -(float)Math.Sin(phi) * (float)Math.Sin(theta);
                    float tY = 0.0f;
                    float tZ = +(float)Math.Sin(phi) * (float)Math.Cos(theta);

                    Vector3 position = radius * new Vector3(x, y, z);
                    Vector3 normal = new Vector3(x, y, z);
                    Vector3 tangent = Vector3.Normalize(new Vector3(tX, tY, tZ));
                    Vector3 binormal = Vector3.Cross(normal, tangent);

                    float u = theta / MathUtil.Pi * 2f;
                    float v = phi / MathUtil.Pi;

                    Vector2 texture = new Vector2(u, v);

                    vertList.Add(position + center);
                    normList.Add(normal);
                    tangList.Add(tangent);
                    binmList.Add(binormal);
                    uvList.Add(texture);
                }
            }

            //South pole
            vertList.Add(new Vector3(0.0f, -radius, 0.0f) + center);
            normList.Add(new Vector3(0.0f, -1.0f, 0.0f));
            tangList.Add(new Vector3(0.0f, 0.0f, -1.0f));
            binmList.Add(new Vector3(-1.0f, 0.0f, 0.0f));
            uvList.Add(new Vector2(0.0f, 1.0f));

            #endregion

            List<uint> indexList = new List<uint>();

            #region Indexes

            for (uint index = 1; index <= sliceCount; ++index)
            {
                indexList.Add(0);
                indexList.Add(index + 1);
                indexList.Add(index);
            }

            uint baseIndex = 1;
            uint ringVertexCount = sliceCount + 1;
            for (uint st = 0; st < stackCount - 2; ++st)
            {
                for (uint sl = 0; sl < sliceCount; ++sl)
                {
                    indexList.Add(baseIndex + st * ringVertexCount + sl);
                    indexList.Add(baseIndex + st * ringVertexCount + sl + 1);
                    indexList.Add(baseIndex + (st + 1) * ringVertexCount + sl);

                    indexList.Add(baseIndex + (st + 1) * ringVertexCount + sl);
                    indexList.Add(baseIndex + st * ringVertexCount + sl + 1);
                    indexList.Add(baseIndex + (st + 1) * ringVertexCount + sl + 1);
                }
            }

            uint southPoleIndex = (uint)vertList.Count - 1;

            baseIndex = southPoleIndex - ringVertexCount;

            for (uint index = 0; index < sliceCount; ++index)
            {
                indexList.Add(southPoleIndex);
                indexList.Add(baseIndex + index);
                indexList.Add(baseIndex + index + 1);
            }

            #endregion

            return new GeometryDescriptor()
            {
                Vertices = vertList.ToArray(),
                Normals = normList.ToArray(),
                Tangents = tangList.ToArray(),
                Binormals = binmList.ToArray(),
                Uvs = uvList.ToArray(),
                Indices = indexList.ToArray(),
            };
        }
        /// <summary>
        /// Creates a hemispheric
        /// </summary>
        /// <param name="radius">Radius</param>
        /// <param name="sliceCount">Slices (vertical)</param>
        /// <param name="stackCount">Stacks (horizontal)</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateHemispheric(float radius, uint sliceCount, uint stackCount)
        {
            return CreateHemispheric(Vector3.Zero, radius, sliceCount, stackCount);
        }
        /// <summary>
        /// Creates a hemispheric
        /// </summary>
        /// <param name="center">Center</param>
        /// <param name="radius">Radius</param>
        /// <param name="sliceCount">Slices (vertical)</param>
        /// <param name="stackCount">Stacks (horizontal)</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateHemispheric(Vector3 center, float radius, uint sliceCount, uint stackCount)
        {
            List<Vector3> vertList = new List<Vector3>();
            List<Vector3> normList = new List<Vector3>();
            List<Vector3> tangList = new List<Vector3>();
            List<Vector3> binmList = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();

            sliceCount--;
            stackCount++;

            #region Positions

            float phiStep = MathUtil.PiOverTwo / stackCount;
            float thetaStep = MathUtil.TwoPi / sliceCount;
            float halfStep = thetaStep / MathUtil.TwoPi / 2f;

            for (int st = 0; st <= stackCount; st++)
            {
                float phi = st * phiStep;

                for (int sl = 0; sl <= sliceCount; sl++)
                {
                    float theta = sl * thetaStep;

                    float sinPhi = (float)Math.Sin(phi);
                    float cosPhi = (float)Math.Cos(phi);
                    float sinTheta = (float)Math.Sin(theta);
                    float cosTheta = (float)Math.Cos(theta);

                    float x = sinPhi * cosTheta;
                    float y = cosPhi;
                    float z = sinPhi * sinTheta;

                    float tX = -sinPhi * sinTheta;
                    float tY = 0.0f;
                    float tZ = +sinPhi * cosTheta;

                    Vector3 position = radius * new Vector3(x, y, z);
                    Vector3 normal = new Vector3(x, y, z);
                    Vector3 tangent = Vector3.Normalize(new Vector3(tX, tY, tZ));
                    Vector3 binormal = Vector3.Cross(normal, tangent);

                    float u = theta / MathUtil.TwoPi;
                    float v = phi / MathUtil.PiOverTwo;

                    if (st == 0)
                    {
                        u -= halfStep;
                    }

                    Vector2 texture = new Vector2(u, v);

                    vertList.Add(position + center);
                    normList.Add(normal);
                    tangList.Add(tangent);
                    binmList.Add(binormal);
                    uvList.Add(texture);
                }
            }

            #endregion

            List<uint> indexList = new List<uint>();

            #region Indexes

            uint ringVertexCount = sliceCount + 1;
            for (uint st = 0; st < stackCount; st++)
            {
                for (uint sl = 0; sl < sliceCount; sl++)
                {
                    indexList.Add((st + 1) * ringVertexCount + sl + 0);
                    indexList.Add((st + 0) * ringVertexCount + sl + 1);
                    indexList.Add((st + 1) * ringVertexCount + sl + 1);

                    if (st == 0)
                    {
                        continue;
                    }

                    indexList.Add((st + 0) * ringVertexCount + sl + 0);
                    indexList.Add((st + 0) * ringVertexCount + sl + 1);
                    indexList.Add((st + 1) * ringVertexCount + sl + 0);
                }
            }

            #endregion

            return new GeometryDescriptor()
            {
                Vertices = vertList.ToArray(),
                Normals = normList.ToArray(),
                Tangents = tangList.ToArray(),
                Binormals = binmList.ToArray(),
                Uvs = uvList.ToArray(),
                Indices = indexList.ToArray(),
            };
        }
        /// <summary>
        /// Creates a XZ plane
        /// </summary>
        /// <param name="size">Plane size</param>
        /// <param name="height">Plane height</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateXZPlane(float size, float height)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-size*0.5f, +height, -size*0.5f),
                new Vector3(-size*0.5f, +height, +size*0.5f),
                new Vector3(+size*0.5f, +height, -size*0.5f),
                new Vector3(+size*0.5f, +height, +size*0.5f),
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.Up,
                Vector3.Up,
                Vector3.Up,
                Vector3.Up,
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, size),
                new Vector2(size, 0.0f),
                new Vector2(size, size),
            };

            uint[] indices = new uint[]
            {
                0, 1, 2,
                1, 3, 2,
            };

            return new GeometryDescriptor()
            {
                Vertices = vertices,
                Normals = normals,
                Uvs = uvs,
                Indices = indices,
            };
        }
        /// <summary>
        /// Creates a curve plane
        /// </summary>
        /// <param name="size">Quad size</param>
        /// <param name="textureRepeat">Texture repeat</param>
        /// <param name="planeWidth">Plane width</param>
        /// <param name="planeTop">Plane top</param>
        /// <param name="planeBottom">Plane bottom</param>
        /// <returns>Returns a geometry descriptor</returns>
        public static GeometryDescriptor CreateCurvePlane(int size, int textureRepeat, float planeWidth, float planeTop, float planeBottom)
        {
            Vector3[] vertices = new Vector3[(size + 1) * (size + 1)];
            Vector2[] uvs = new Vector2[(size + 1) * (size + 1)];

            // Determine the size of each quad on the sky plane.
            float quadSize = planeWidth / (float)size;

            // Calculate the radius of the sky plane based on the width.
            float radius = planeWidth * 0.5f;

            // Calculate the height constant to increment by.
            float constant = (planeTop - planeBottom) / (radius * radius);

            // Calculate the texture coordinate increment value.
            float textureDelta = (float)textureRepeat / (float)size;

            // Loop through the sky plane and build the coordinates based on the increment values given.
            for (int j = 0; j <= size; j++)
            {
                for (int i = 0; i <= size; i++)
                {
                    // Calculate the vertex coordinates.
                    float positionX = (-0.5f * planeWidth) + ((float)i * quadSize);
                    float positionZ = (-0.5f * planeWidth) + ((float)j * quadSize);
                    float positionY = planeTop - (constant * ((positionX * positionX) + (positionZ * positionZ)));

                    // Calculate the texture coordinates.
                    float tu = (float)i * textureDelta;
                    float tv = (float)j * textureDelta;

                    // Calculate the index into the sky plane array to add this coordinate.
                    int ix = j * (size + 1) + i;

                    // Add the coordinates to the sky plane array.
                    vertices[ix] = new Vector3(positionX, positionY, positionZ);
                    uvs[ix] = new Vector2(tu, tv);
                }
            }


            // Create the index array.
            List<uint> indexList = new List<uint>((size + 1) * (size + 1) * 6);

            // Load the vertex and index array with the sky plane array data.
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    int index1 = j * (size + 1) + i;
                    int index2 = j * (size + 1) + (i + 1);
                    int index3 = (j + 1) * (size + 1) + i;
                    int index4 = (j + 1) * (size + 1) + (i + 1);

                    // Triangle 1 - Upper Left
                    indexList.Add((uint)index1);

                    // Triangle 1 - Upper Right
                    indexList.Add((uint)index2);

                    // Triangle 1 - Bottom Left
                    indexList.Add((uint)index3);

                    // Triangle 2 - Bottom Left
                    indexList.Add((uint)index3);

                    // Triangle 2 - Upper Right
                    indexList.Add((uint)index2);

                    // Triangle 2 - Bottom Right
                    indexList.Add((uint)index4);
                }
            }

            return new GeometryDescriptor()
            {
                Vertices = vertices,
                Uvs = uvs,
                Indices = indexList.ToArray(),
            };
        }

        /// <summary>
        /// Compute normal of three points in the same plane
        /// </summary>
        /// <param name="p1">Point 1</param>
        /// <param name="p2">point 2</param>
        /// <param name="p3">point 3</param>
        /// <returns>Returns a normal descriptor</returns>
        public static NormalDescriptor ComputeNormal(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var p = new Plane(p1, p2, p3);

            return new NormalDescriptor()
            {
                Normal = p.Normal,
            };
        }
        /// <summary>
        /// Calculate tangent, normal and binormals of triangle vertices
        /// </summary>
        /// <param name="p1">Point 1</param>
        /// <param name="p2">Point 2</param>
        /// <param name="p3">Point 3</param>
        /// <param name="uv1">Texture uv 1</param>
        /// <param name="uv2">Texture uv 2</param>
        /// <param name="uv3">Texture uv 3</param>
        /// <returns>Returns a normal descriptor</returns>
        public static NormalDescriptor ComputeNormals(Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            // Calculate the two vectors for the face.
            Vector3 vector1 = p2 - p1;
            Vector3 vector2 = p3 - p1;

            // Calculate the tu and tv texture space vectors.
            Vector2 tuVector = new Vector2(uv2.X - uv1.X, uv3.X - uv1.X);
            Vector2 tvVector = new Vector2(uv2.Y - uv1.Y, uv3.Y - uv1.Y);

            // Calculate the denominator of the tangent / binormal equation.
            var den = 1.0f / (tuVector[0] * tvVector[1] - tuVector[1] * tvVector[0]);

            // Calculate the cross products and multiply by the coefficient to get the tangent and binormal.
            Vector3 tangent = new Vector3
            {
                X = (tvVector[1] * vector1.X - tvVector[0] * vector2.X) * den,
                Y = (tvVector[1] * vector1.Y - tvVector[0] * vector2.Y) * den,
                Z = (tvVector[1] * vector1.Z - tvVector[0] * vector2.Z) * den
            };

            Vector3 binormal = new Vector3
            {
                X = (tuVector[0] * vector2.X - tuVector[1] * vector1.X) * den,
                Y = (tuVector[0] * vector2.Y - tuVector[1] * vector1.Y) * den,
                Z = (tuVector[0] * vector2.Z - tuVector[1] * vector1.Z) * den
            };

            tangent.Normalize();
            binormal.Normalize();

            // Calculate the cross product of the tangent and binormal which will give the normal vector.
            Vector3 normal = Vector3.Cross(tangent, binormal);

            return new NormalDescriptor()
            {
                Normal = normal,
                Tangent = tangent,
                Binormal = binormal,
            };
        }

        /// <summary>
        /// Generates a bounding box from a vertex list item list
        /// </summary>
        /// <param name="vertexListItems">Vertex list item list</param>
        /// <returns>Returns the minimum bounding box that contains all the specified vertex list item list</returns>
        public static BoundingBox CreateBoundingBox<T>(IEnumerable<T> vertexListItems) where T : IVertexList
        {
            var bbox = new BoundingBox();
            bool initialized = false;

            foreach (var item in vertexListItems)
            {
                var tbox = BoundingBox.FromPoints(item.GetVertices().ToArray());

                if (!initialized)
                {
                    bbox = tbox;
                    initialized = true;
                }
                else
                {
                    bbox = BoundingBox.Merge(bbox, tbox);
                }
            }

            return bbox;
        }
        /// <summary>
        /// Generates a bounding sphere from a vertex list item list
        /// </summary>
        /// <param name="vertexListItems">Vertex list item list</param>
        /// <returns>Returns the minimum bounding sphere that contains all the specified vertex list item list</returns>
        public static BoundingSphere CreateBoundingSphere<T>(IEnumerable<T> vertexListItems) where T : IVertexList
        {
            BoundingSphere bsph = new BoundingSphere();
            bool initialized = false;

            foreach (var vertexItem in vertexListItems)
            {
                BoundingSphere tsph = BoundingSphere.FromPoints(vertexItem.GetVertices().ToArray());

                if (!initialized)
                {
                    bsph = tsph;
                    initialized = true;
                }
                else
                {
                    bsph = BoundingSphere.Merge(bsph, tsph);
                }
            }

            return bsph;
        }

        /// <summary>
        /// Computes constraints into vertices
        /// </summary>
        /// <param name="constraint">Constraint</param>
        /// <param name="vertices">Vertices</param>
        /// <param name="res">Resulting vertices</param>
        public static void ConstraintVertices(BoundingBox constraint, IEnumerable<VertexData> vertices, out IEnumerable<VertexData> res)
        {
            List<VertexData> tmpVertices = new List<VertexData>();

            for (int i = 0; i < vertices.Count(); i += 3)
            {
                if (constraint.Contains(vertices.ElementAt(i + 0).Position.Value) != ContainmentType.Disjoint ||
                    constraint.Contains(vertices.ElementAt(i + 1).Position.Value) != ContainmentType.Disjoint ||
                    constraint.Contains(vertices.ElementAt(i + 2).Position.Value) != ContainmentType.Disjoint)
                {
                    tmpVertices.Add(vertices.ElementAt(i + 0));
                    tmpVertices.Add(vertices.ElementAt(i + 1));
                    tmpVertices.Add(vertices.ElementAt(i + 2));
                }
            }

            res = tmpVertices;
        }
        /// <summary>
        /// Computes constraints into vertices
        /// </summary>
        /// <param name="constraint">Constraint</param>
        /// <param name="vertices">Vertices</param>
        /// <returns>Resulting vertices</returns>
        public static async Task<IEnumerable<VertexData>> ConstraintVerticesAsync(BoundingBox constraint, IEnumerable<VertexData> vertices)
        {
            return await Task.Factory.StartNew(() =>
            {
                ConstraintVertices(constraint, vertices, out var tres);

                return tres;
            },
            TaskCreationOptions.LongRunning);
        }
        /// <summary>
        /// Computes constraints into vertices and indices
        /// </summary>
        /// <param name="constraint">Constraint</param>
        /// <param name="vertices">Vertices</param>
        /// <param name="indices">Indices</param>
        /// <param name="resVertices">Resulting vertices</param>
        /// <param name="resIndices">Resulting indices</param>
        public static void ConstraintIndices(BoundingBox constraint, IEnumerable<VertexData> vertices, IEnumerable<uint> indices, out IEnumerable<VertexData> resVertices, out IEnumerable<uint> resIndices)
        {
            List<uint> tmpIndices = new List<uint>();

            // Gets all triangle indices into the constraint
            for (int i = 0; i < indices.Count(); i += 3)
            {
                var i0 = indices.ElementAt(i + 0);
                var i1 = indices.ElementAt(i + 1);
                var i2 = indices.ElementAt(i + 2);

                var v0 = vertices.ElementAt((int)i0);
                var v1 = vertices.ElementAt((int)i1);
                var v2 = vertices.ElementAt((int)i2);

                if (constraint.Contains(v0.Position.Value) != ContainmentType.Disjoint ||
                    constraint.Contains(v1.Position.Value) != ContainmentType.Disjoint ||
                    constraint.Contains(v2.Position.Value) != ContainmentType.Disjoint)
                {
                    tmpIndices.Add(i0);
                    tmpIndices.Add(i1);
                    tmpIndices.Add(i2);
                }
            }

            List<VertexData> tmpVertices = new List<VertexData>();
            List<Tuple<uint, uint>> dict = new List<Tuple<uint, uint>>();

            // Adds all the selected vertices for each unique index, and create a index traductor for the new vertext list
            foreach (uint index in tmpIndices.Distinct())
            {
                tmpVertices.Add(vertices.ElementAt((int)index));
                dict.Add(new Tuple<uint, uint>(index, (uint)tmpVertices.Count - 1));
            }

            // Set the new index values
            for (int i = 0; i < tmpIndices.Count; i++)
            {
                uint newIndex = dict.Find(d => d.Item1 == tmpIndices[i]).Item2;

                tmpIndices[i] = newIndex;
            }

            resVertices = tmpVertices;
            resIndices = tmpIndices;
        }
        /// <summary>
        /// Computes constraints into vertices and indices
        /// </summary>
        /// <param name="constraint">Constraint</param>
        /// <param name="vertices">Vertices</param>
        /// <param name="indices">Indices</param>
        /// <returns>Resulting vertices and indices</returns>
        public static async Task<(IEnumerable<VertexData> Vertices, IEnumerable<uint> Indices)> ConstraintIndicesAsync(BoundingBox constraint, IEnumerable<VertexData> vertices, IEnumerable<uint> indices)
        {
            return await Task.Factory.StartNew(() =>
            {
                ConstraintIndices(constraint, vertices, indices, out var tvertices, out var tindices);

                return (tvertices, tindices);
            },
            TaskCreationOptions.LongRunning);
        }
    }
}
