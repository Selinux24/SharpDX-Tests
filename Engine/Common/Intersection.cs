﻿using SharpDX;
using System;
using System.Collections.Generic;

namespace Engine.Common
{
    /// <summary>
    /// Intersections
    /// </summary>
    public static class Intersection
    {
        /// <summary>
        /// Containment test between this <see cref="BoundingFrustum"/> and specified <see cref="BoundingFrustum"/>.
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="frustum">A <see cref="BoundingFrustum"/> for testing.</param>
        /// <returns>Result of testing for containment between this <see cref="BoundingFrustum"/> and specified <see cref="BoundingFrustum"/>.</returns>
        public static ContainmentType FrustumContainsFrustum(this BoundingFrustum instance, BoundingFrustum frustum)
        {
            if (instance == frustum)
            {
                return ContainmentType.Contains;
            }

            var intersects = false;
            for (var i = 0; i < 6; ++i)
            {
                var plane = instance.GetPlane(i);
                frustum.Intersects(ref plane, out PlaneIntersectionType planeIntersectionType);
                if (planeIntersectionType == PlaneIntersectionType.Back)
                {
                    return ContainmentType.Disjoint;
                }
                else if (planeIntersectionType == PlaneIntersectionType.Intersecting)
                {
                    intersects = true;
                    break;
                }
            }

            return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
        }
        /// <summary>
        /// Determines whether a BoundingBox contains a BoundingBox.
        /// </summary>
        /// <param name="box1">The first box to test</param>
        /// <param name="box2">The second box to test</param>
        /// <returns>The type of containment the two objects have</returns>
        public static ContainmentType BoxContainsBox(ref BoundingBox box1, ref BoundingBox box2)
        {
            return Collision.BoxContainsBox(ref box1, ref box2);
        }
        /// <summary>
        /// Determines whether there is an intersection between a Ray and a BoundingBox
        /// </summary>
        /// <param name="ray">The ray to test</param>
        /// <param name="box">The box to test</param>
        /// <param name="distance">When the method completes, contains the distance of the intersection, or 0 if there was no intersection</param>
        /// <returns>Whether the two objects intersected</returns>
        public static bool RayIntersectsBox(Ray ray, BoundingBox box, out float distance)
        {
            return Collision.RayIntersectsBox(ref ray, ref box, out distance);
        }
        /// <summary>
        /// Determines whether there is an intersection between a <see cref="Ray"/> and a triangle.
        /// </summary>
        /// <param name="ray">The ray to test.</param>
        /// <param name="vertex1">The first vertex of the triangle to test.</param>
        /// <param name="vertex2">The second vertex of the triangle to test.</param>
        /// <param name="vertex3">The third vertex of the triangle to test.</param>
        /// <param name="point">When the method completes, contains the point of intersection, or <see cref="Vector3.Zero"/> if there was no intersection.</param>
        /// <param name="distance">Distance to point</param>
        /// <returns>Whether the two objects intersected.</returns>
        public static bool RayIntersectsTriangle(ref Ray ray, ref Vector3 vertex1, ref Vector3 vertex2, ref Vector3 vertex3, out Vector3 point, out float distance)
        {
            if (!Collision.RayIntersectsTriangle(ref ray, ref vertex1, ref vertex2, ref vertex3, out float d))
            {
                point = Vector3.Zero;
                distance = float.MaxValue;
                return false;
            }

            point = ray.Position + (ray.Direction * d);
            distance = d;
            return true;
        }
        /// <summary>
        /// Finds the distance between a point and triangle ABC.
        /// </summary>
        /// <param name="p">A point.</param>
        /// <param name="a">The first vertex of the triangle.</param>
        /// <param name="b">The second vertex of the triangle.</param>
        /// <param name="c">The third vertex of the triangle.</param>
        /// <param name="distance">The distance between the point and the triangle.</param>
        /// <returns>A value indicating whether the point is contained within the triangle.</returns>
        public static bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            Vector3 v0 = c - a;
            Vector3 v1 = b - a;
            Vector3 v2 = p - a;

            Vector2 v20 = new Vector2(v0.X, v0.Z);
            Vector2 v21 = new Vector2(v1.X, v1.Z);
            Vector2 v22 = new Vector2(v2.X, v2.Z);

            Vector2.Dot(ref v20, ref v20, out float dot00);
            Vector2.Dot(ref v20, ref v21, out float dot01);
            Vector2.Dot(ref v20, ref v22, out float dot02);
            Vector2.Dot(ref v21, ref v21, out float dot11);
            Vector2.Dot(ref v21, ref v22, out float dot12);

            //compute barycentric coordinates
            float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            const float EPS = 1E-4f;

            //if point lies inside triangle, return interpolated y-coordinate
            if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
            {
                var h = a.Y + v0.Y * u + v1.Y * v;
                distance = Math.Abs(h - p.Y);
                return true;
            }

            distance = float.MaxValue;
            return false;
        }

        /// <summary>
        /// Performs intersection test with ray and ray intersectable item list
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="items">Ray intersectable item list</param>
        /// <param name="facingOnly">Select only items facing to ray origin</param>
        /// <param name="position">Result picked position</param>
        /// <param name="item">Result picked ray intersectable item</param>
        /// <param name="distance">Result distance to picked position</param>
        /// <returns>Returns first intersection if exists</returns>
        public static bool IntersectFirst<T>(Ray ray, IEnumerable<T> items, bool facingOnly, out Vector3 position, out T item, out float distance) where T : IRayIntersectable
        {
            position = Vector3.Zero;
            item = default(T);
            distance = float.MaxValue;

            foreach (var cItem in items)
            {
                if (cItem.Intersects(ray, facingOnly, out Vector3 pos, out float d))
                {
                    position = pos;
                    item = cItem;
                    distance = d;

                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Performs intersection test with ray and ray intersectable item list
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="items">Triangle list</param>
        /// <param name="facingOnly">Select only items facing to ray origin</param>
        /// <param name="position">Result picked position</param>
        /// <param name="item">Result picked ray intersectable item</param>
        /// <param name="distance">Result distance to picked position</param>
        /// <returns>Returns nearest intersection if exists</returns>
        public static bool IntersectNearest<T>(Ray ray, IEnumerable<T> items, bool facingOnly, out Vector3 position, out T item, out float distance) where T : IRayIntersectable
        {
            position = Vector3.Zero;
            item = default(T);
            distance = float.MaxValue;

            if (IntersectAll(ray, items, facingOnly, out Vector3[] pickedPositions, out T[] pickedTriangles, out float[] pickedDistances))
            {
                float distanceMin = float.MaxValue;

                for (int i = 0; i < pickedPositions.Length; i++)
                {
                    float dist = pickedDistances[i];
                    if (dist < distanceMin)
                    {
                        distanceMin = dist;
                        position = pickedPositions[i];
                        item = pickedTriangles[i];
                        distance = pickedDistances[i];
                    }
                }

                return true;
            }

            return false;
        }
        /// <summary>
        /// Performs intersection test with ray and ray intersectable item list
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="items">Triangle list</param>
        /// <param name="facingOnly">Select only items facing to ray origin</param>
        /// <param name="pickedPositions">Picked position list</param>
        /// <param name="pickedItems">Picked ray intersectable item list</param>
        /// <param name="pickedDistances">Distances to picked positions</param>
        /// <returns>Returns all intersections if exists</returns>
        public static bool IntersectAll<T>(Ray ray, IEnumerable<T> items, bool facingOnly, out Vector3[] pickedPositions, out T[] pickedItems, out float[] pickedDistances) where T : IRayIntersectable
        {
            SortedDictionary<float, Vector3> pickedPositionList = new SortedDictionary<float, Vector3>();
            SortedDictionary<float, T> pickedTriangleList = new SortedDictionary<float, T>();
            SortedDictionary<float, float> pickedDistancesList = new SortedDictionary<float, float>();

            foreach (T t in items)
            {
                //Avoid duplicate picked positions
                var intersects = t.Intersects(ray, facingOnly, out Vector3 pos, out float d);
                if (intersects && !pickedPositionList.ContainsValue(pos))
                {
                    float k = d;
                    while (pickedPositionList.ContainsKey(k))
                    {
                        //Avoid duplicate distance keys
                        k += 0.001f;
                    }

                    pickedPositionList.Add(k, pos);
                    pickedTriangleList.Add(k, t);
                    pickedDistancesList.Add(k, d);
                }
            }

            if (pickedPositionList.Values.Count > 0)
            {
                pickedPositions = new Vector3[pickedPositionList.Values.Count];
                pickedItems = new T[pickedTriangleList.Values.Count];
                pickedDistances = new float[pickedDistancesList.Values.Count];

                pickedPositionList.Values.CopyTo(pickedPositions, 0);
                pickedTriangleList.Values.CopyTo(pickedItems, 0);
                pickedDistancesList.Values.CopyTo(pickedDistances, 0);

                return true;
            }
            else
            {
                pickedPositions = null;
                pickedItems = null;
                pickedDistances = null;

                return false;
            }
        }

        /// <summary>
        /// Gets the nearest point in a ray from a specified point
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="point">Point</param>
        /// <returns>Returns the resulting point along the ray</returns>
        public static Vector3 NearestPointOnLine(Ray ray, Vector3 point)
        {
            Vector3 origin = ray.Position;
            Vector3 dir = Vector3.Normalize(ray.Direction);

            //Vector from origin to point
            var v = origin - point;

            //Project v over direction vector and get the distance magnitude over dir
            var d = Vector3.Dot(v, dir);

            //Move from origin towards the resulting distance
            return origin + (dir * d);
        }
        /// <summary>
        /// Gets the distance from the nearest point in a ray from a specified point
        /// </summary>
        /// <param name="ray">Ray</param>
        /// <param name="point">Point</param>
        /// <returns>Returns the resulting minimum distance from point to ray</returns>
        public static float DistanceFromPointToLine(Ray ray, Vector3 point)
        {
            var linePoint = NearestPointOnLine(ray, point);

            return Vector3.Distance(linePoint, point);
        }
    }
}
