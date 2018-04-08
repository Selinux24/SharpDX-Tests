﻿using SharpDX;
using System;
using System.Collections.Generic;

namespace Engine.PathFinding.RecastNavigation
{
    /// <summary>
    /// Provides the ability to perform pathfinding related queries against a navigation mesh.
    /// </summary>
    public class NavMeshQuery
    {
        /// <summary>
        /// Navmesh data.
        /// </summary>
        private NavMesh m_nav = null;
        /// <summary>
        /// Node pool.
        /// </summary>
        private NodePool m_nodePool = null;
        /// <summary>
        /// Small node pool.
        /// </summary>
        private NodePool m_tinyNodePool = null;
        /// <summary>
        /// Open list queue.
        /// </summary>
        private NodeQueue m_openList = null;
        /// <summary>
        /// Sliced query state.
        /// </summary>
        private QueryData m_query = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public NavMeshQuery()
        {
            m_query = new QueryData();
        }

        /// <summary>
        /// Initializes the query object.
        /// </summary>
        /// <param name="nav">Pointer to the dtNavMesh object to use for all queries.</param>
        /// <param name="maxNodes">Maximum number of search nodes.</param>
        /// <returns>The status flags for the query.</returns>
        public Status Init(NavMesh nav, int maxNodes)
        {
            if (maxNodes > Detour.DT_NULL_IDX || maxNodes > (1 << Detour.DT_NODE_PARENT_BITS) - 1)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nav = nav;

            if (m_nodePool == null || m_nodePool.GetMaxNodes() < maxNodes)
            {
                if (m_nodePool != null)
                {
                    Helper.Dispose(m_nodePool);
                    m_nodePool = null;
                }

                m_nodePool = new NodePool(maxNodes, Helper.NextPowerOfTwo(maxNodes / 4));
            }
            else
            {
                m_nodePool.Clear();
            }

            if (m_tinyNodePool == null)
            {
                m_tinyNodePool = new NodePool(64, 32);
            }
            else
            {
                m_tinyNodePool.Clear();
            }

            if (m_openList == null || m_openList.GetCapacity() < maxNodes)
            {
                if (m_openList != null)
                {
                    Helper.Dispose(m_openList);
                    m_openList = null;
                }

                m_openList = new NodeQueue(maxNodes);
            }
            else
            {
                m_openList.Clear();
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Finds a path from the start polygon to the end polygon.
        /// </summary>
        /// <param name="startRef">The refrence id of the start polygon.</param>
        /// <param name="endRef">The reference id of the end polygon.</param>
        /// <param name="startPos">A position within the start polygon.</param>
        /// <param name="endPos">A position within the end polygon.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="path">An ordered list of polygon references representing the path.</param>
        /// <param name="pathCount">The number of polygons returned in the path array.</param>
        /// <param name="maxPath">The maximum number of polygons the @p path array can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindPath(int startRef, int endRef, Vector3 startPos, Vector3 endPos, QueryFilter filter, out int[] path, out int pathCount, int maxPath)
        {
            path = new int[maxPath];
            pathCount = 0;

            // Validate input
            if (!m_nav.IsValidPolyRef(startRef) || !m_nav.IsValidPolyRef(endRef) || filter == null || maxPath <= 0)
            {
                return Status.DT_FAILURE;
            }

            if (startRef == endRef)
            {
                path[0] = startRef;
                pathCount = 1;
                return Status.DT_SUCCESS;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            var startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = startPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = Vector3.Distance(startPos, endPos) * Detour.H_SCALE;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            var lastBestNode = startNode;
            float lastBestNodeCost = startNode.total;

            bool outOfNodes = false;

            while (!m_openList.Empty())
            {
                // Remove node from open list and put it in closed list.
                var bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Reached the goal, stop searching.
                if (bestNode.id == endRef)
                {
                    lastBestNode = bestNode;
                    break;
                }

                // Get current poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(bestRef, out MeshTile bestTile, out Poly bestPoly);

                // Get parent poly and tile.
                int parentRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                if (bestNode.pidx != 0)
                {
                    parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
                }
                if (parentRef != 0)
                {
                    m_nav.GetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    int neighbourRef = bestTile.links[i].nref;

                    // Skip invalid ids and do not expand back to where we came from.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Get neighbour poly and tile.
                    // The API input has been cheked already, skip checking internal data.
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // deal explicitly with crossing tile boundaries
                    int crossSide = 0;
                    if (bestTile.links[i].side != 0xff)
                    {
                        crossSide = bestTile.links[i].side >> 1;
                    }

                    // get the node
                    var neighbourNode = m_nodePool.GetNode(neighbourRef, crossSide);
                    if (neighbourNode == null)
                    {
                        outOfNodes = true;
                        continue;
                    }

                    // If the node is visited the first time, calculate node position.
                    if (neighbourNode.flags == NodeFlags.DT_NODE_NONE)
                    {
                        GetEdgeMidPoint(
                            bestRef, bestPoly, bestTile,
                            neighbourRef, neighbourPoly, neighbourTile,
                            out neighbourNode.pos);
                    }

                    // Calculate cost and heuristic.
                    float cost = 0;
                    float heuristic = 0;

                    // Special case for last node.
                    if (neighbourRef == endRef)
                    {
                        // Cost
                        float curCost = filter.GetCost(
                            bestNode.pos, neighbourNode.pos,
                            parentRef, parentTile, parentPoly,
                            bestRef, bestTile, bestPoly,
                            neighbourRef, neighbourTile, neighbourPoly);
                        float endCost = filter.GetCost(
                            neighbourNode.pos, endPos,
                            bestRef, bestTile, bestPoly,
                            neighbourRef, neighbourTile, neighbourPoly,
                            0, null, null);

                        cost = bestNode.cost + curCost + endCost;
                        heuristic = 0;
                    }
                    else
                    {
                        // Cost
                        float curCost = filter.GetCost(
                            bestNode.pos, neighbourNode.pos,
                            parentRef, parentTile, parentPoly,
                            bestRef, bestTile, bestPoly,
                            neighbourRef, neighbourTile, neighbourPoly);
                        cost = bestNode.cost + curCost;
                        heuristic = Vector3.Distance(neighbourNode.pos, endPos) * Detour.H_SCALE;
                    }

                    float total = cost + heuristic;

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }
                    // The node is already visited and process, and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    // Add or update the node.
                    neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.id = neighbourRef;
                    neighbourNode.flags = (neighbourNode.flags & ~NodeFlags.DT_NODE_CLOSED);
                    neighbourNode.cost = cost;
                    neighbourNode.total = total;

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        // Already in open, update node location.
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        // Put the node in open list.
                        neighbourNode.flags |= NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }

                    // Update nearest node to target so far.
                    if (heuristic < lastBestNodeCost)
                    {
                        lastBestNodeCost = heuristic;
                        lastBestNode = neighbourNode;
                    }
                }
            }

            Status status = GetPathToNode(lastBestNode, out path, out pathCount, maxPath);

            if (lastBestNode.id != endRef)
            {
                status |= Status.DT_PARTIAL_RESULT;
            }

            if (outOfNodes)
            {
                status |= Status.DT_OUT_OF_NODES;
            }

            return status;
        }
        /// <summary>
        /// Finds the straight path from the start to the end position within the polygon corridor.
        /// </summary>
        /// <param name="startPos">Path start position.</param>
        /// <param name="endPos">Path end position.</param>
        /// <param name="path">An array of polygon references that represent the path corridor.</param>
        /// <param name="pathSize">The number of polygons in the path array.</param>
        /// <param name="straightPath">Points describing the straight path.</param>
        /// <param name="straightPathFlags">Flags describing each point.</param>
        /// <param name="straightPathRefs">The reference id of the polygon that is being entered at each point.</param>
        /// <param name="straightPathCount">The number of points in the straight path.</param>
        /// <param name="maxStraightPath">The maximum number of points the straight path arrays can hold.</param>
        /// <param name="options">Query options.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindStraightPath(Vector3 startPos, Vector3 endPos, int[] path, int pathSize, out Vector3[] straightPath, out StraightPathFlags[] straightPathFlags, out int[] straightPathRefs, out int straightPathCount, int maxStraightPath, StraightPathOptions options)
        {
            straightPath = new Vector3[maxStraightPath];
            straightPathFlags = new StraightPathFlags[maxStraightPath];
            straightPathRefs = new int[maxStraightPath];
            straightPathCount = 0;

            if (maxStraightPath == 0)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            if (path == null || path.Length == 0)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            Status stat = 0;

            // TODO: Should this be callers responsibility?
            if (ClosestPointOnPolyBoundary(path[0], startPos, out Vector3 closestStartPos).HasFlag(Status.DT_FAILURE))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            if (ClosestPointOnPolyBoundary(path[pathSize - 1], endPos, out Vector3 closestEndPos).HasFlag(Status.DT_FAILURE))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            // Add start point.
            stat = AppendVertex(
                closestStartPos, StraightPathFlags.DT_STRAIGHTPATH_START, path[0],
                ref straightPath, ref straightPathFlags, ref straightPathRefs,
                ref straightPathCount, maxStraightPath);
            if (stat != Status.DT_IN_PROGRESS)
            {
                return stat;
            }

            if (pathSize > 1)
            {
                Vector3 portalApex = closestStartPos;
                Vector3 portalLeft = portalApex;
                Vector3 portalRight = portalApex;
                int apexIndex = 0;
                int leftIndex = 0;
                int rightIndex = 0;

                PolyTypes leftPolyType = 0;
                PolyTypes rightPolyType = 0;

                int leftPolyRef = path[0];
                int rightPolyRef = path[0];

                for (int i = 0; i < pathSize; ++i)
                {
                    Vector3 left;
                    Vector3 right;
                    PolyTypes toType;

                    if (i + 1 < pathSize)
                    {
                        // Next portal.
                        if (GetPortalPoints(path[i], path[i + 1], out left, out right, out PolyTypes fromType, out toType).HasFlag(Status.DT_FAILURE))
                        {
                            // Failed to get portal points, in practice this means that path[i+1] is invalid polygon.
                            // Clamp the end point to path[i], and return the path so far.

                            if (ClosestPointOnPolyBoundary(path[i], endPos, out closestEndPos).HasFlag(Status.DT_FAILURE))
                            {
                                // This should only happen when the first polygon is invalid.
                                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
                            }

                            // Apeend portals along the current straight path segment.
                            if ((options & (StraightPathOptions.DT_STRAIGHTPATH_AREA_CROSSINGS | StraightPathOptions.DT_STRAIGHTPATH_ALL_CROSSINGS)) != 0)
                            {
                                // Ignore status return value as we're just about to return anyway.
                                AppendPortals(apexIndex, i, closestEndPos, path,
                                    ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                    ref straightPathCount, maxStraightPath, options);
                            }

                            // Ignore status return value as we're just about to return anyway.
                            AppendVertex(
                                closestEndPos, 0, path[i],
                                ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                ref straightPathCount, maxStraightPath);

                            return Status.DT_SUCCESS | Status.DT_PARTIAL_RESULT | ((straightPathCount >= maxStraightPath) ? Status.DT_BUFFER_TOO_SMALL : 0);
                        }

                        // If starting really close the portal, advance.
                        if (i == 0)
                        {
                            if (Detour.DistancePtSegSqr2D(portalApex, left, right, out float t) < (0.001f * 0.001f))
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // End of the path.
                        left = closestEndPos;
                        right = closestEndPos;

                        toType = PolyTypes.DT_POLYTYPE_GROUND;
                    }

                    // Right vertex.
                    if (Detour.TriArea2D(portalApex, portalRight, right) <= 0.0f)
                    {
                        if (Detour.Vequal(portalApex, portalRight) || Detour.TriArea2D(portalApex, portalLeft, right) > 0.0f)
                        {
                            portalRight = right;
                            rightPolyRef = (i + 1 < pathSize) ? path[i + 1] : 0;
                            rightPolyType = toType;
                            rightIndex = i;
                        }
                        else
                        {
                            // Append portals along the current straight path segment.
                            if ((options & (StraightPathOptions.DT_STRAIGHTPATH_AREA_CROSSINGS | StraightPathOptions.DT_STRAIGHTPATH_ALL_CROSSINGS)) != 0)
                            {
                                stat = AppendPortals(
                                    apexIndex, leftIndex, portalLeft, path,
                                    ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                    ref straightPathCount, maxStraightPath, options);
                                if (stat != Status.DT_IN_PROGRESS)
                                {
                                    return stat;
                                }
                            }

                            portalApex = portalLeft;
                            apexIndex = leftIndex;

                            StraightPathFlags flags = 0;
                            if (leftPolyRef == 0)
                            {
                                flags = StraightPathFlags.DT_STRAIGHTPATH_END;
                            }
                            else if (leftPolyType == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                            {
                                flags = StraightPathFlags.DT_STRAIGHTPATH_OFFMESH_CONNECTION;
                            }
                            int r = leftPolyRef;

                            // Append or update vertex
                            stat = AppendVertex(
                                portalApex, flags, r,
                                ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                ref straightPathCount, maxStraightPath);
                            if (stat != Status.DT_IN_PROGRESS)
                            {
                                return stat;
                            }

                            portalLeft = portalApex;
                            portalRight = portalApex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;

                            // Restart
                            i = apexIndex;

                            continue;
                        }
                    }

                    // Left vertex.
                    if (Detour.TriArea2D(portalApex, portalLeft, left) >= 0.0f)
                    {
                        if (Detour.Vequal(portalApex, portalLeft) || Detour.TriArea2D(portalApex, portalRight, left) < 0.0f)
                        {
                            portalLeft = left;
                            leftPolyRef = (i + 1 < pathSize) ? path[i + 1] : 0;
                            leftPolyType = toType;
                            leftIndex = i;
                        }
                        else
                        {
                            // Append portals along the current straight path segment.
                            if ((options & (StraightPathOptions.DT_STRAIGHTPATH_AREA_CROSSINGS | StraightPathOptions.DT_STRAIGHTPATH_ALL_CROSSINGS)) != 0)
                            {
                                stat = AppendPortals(
                                    apexIndex, rightIndex, portalRight, path,
                                    ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                    ref straightPathCount, maxStraightPath, options);
                                if (stat != Status.DT_IN_PROGRESS)
                                {
                                    return stat;
                                }
                            }

                            portalApex = portalRight;
                            apexIndex = rightIndex;

                            StraightPathFlags flags = 0;
                            if (rightPolyRef == 0)
                            {
                                flags = StraightPathFlags.DT_STRAIGHTPATH_END;
                            }
                            else if (rightPolyType == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                            {
                                flags = StraightPathFlags.DT_STRAIGHTPATH_OFFMESH_CONNECTION;
                            }
                            int r = rightPolyRef;

                            // Append or update vertex
                            stat = AppendVertex(
                                portalApex, flags, r,
                                ref straightPath, ref straightPathFlags, ref straightPathRefs,
                                ref straightPathCount, maxStraightPath);
                            if (stat != Status.DT_IN_PROGRESS)
                            {
                                return stat;
                            }

                            portalLeft = portalApex;
                            portalRight = portalApex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;

                            // Restart
                            i = apexIndex;

                            continue;
                        }
                    }
                }

                // Append portals along the current straight path segment.
                if ((options & (StraightPathOptions.DT_STRAIGHTPATH_AREA_CROSSINGS | StraightPathOptions.DT_STRAIGHTPATH_ALL_CROSSINGS)) != 0)
                {
                    stat = AppendPortals(
                        apexIndex, pathSize - 1, closestEndPos, path,
                        ref straightPath, ref straightPathFlags, ref straightPathRefs,
                        ref straightPathCount, maxStraightPath, options);
                    if (stat != Status.DT_IN_PROGRESS)
                    {
                        return stat;
                    }
                }
            }

            // Ignore status return value as we're just about to return anyway.
            AppendVertex(
                closestEndPos, StraightPathFlags.DT_STRAIGHTPATH_END, 0,
                ref straightPath, ref straightPathFlags, ref straightPathRefs,
                ref straightPathCount, maxStraightPath);

            return Status.DT_SUCCESS | ((straightPathCount >= maxStraightPath) ? Status.DT_BUFFER_TOO_SMALL : 0);
        }
        /// <summary>
        /// Intializes a sliced path query.
        /// </summary>
        /// <param name="startRef">The refrence id of the start polygon.</param>
        /// <param name="endRef">The reference id of the end polygon.</param>
        /// <param name="startPos">A position within the start polygon.</param>
        /// <param name="endPos">A position within the end polygon.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="options">Query options</param>
        /// <returns>The status flags for the query.</returns>
        /// <example>
        /// Common use case:
        /// -# Call InitSlicedFindPath() to initialize the sliced path query.
        /// -# Call UpdateSlicedFindPath() until it returns complete.
        /// -# Call FinalizeSlicedFindPath() to get the path.
        /// </example>
        public Status InitSlicedFindPath(int startRef, int endRef, Vector3 startPos, Vector3 endPos, QueryFilter filter, FindPathOptions options)
        {
            // Init path state.
            m_query = new QueryData
            {
                status = 0,
                startRef = startRef,
                endRef = endRef,
                startPos = startPos,
                endPos = endPos,
                filter = filter,
                options = options,
                raycastLimitSqr = float.MaxValue
            };

            if (startRef == 0 || endRef == 0)
            {
                return Status.DT_FAILURE;
            }

            // Validate input
            if (!m_nav.IsValidPolyRef(startRef) || !m_nav.IsValidPolyRef(endRef))
            {
                return Status.DT_FAILURE;
            }

            // trade quality with performance?
            if ((options & FindPathOptions.DT_FINDPATH_ANY_ANGLE) != 0)
            {
                // limiting to several times the character radius yields nice results. It is not sensitive 
                // so it is enough to compute it from the first tile.
                MeshTile tile = m_nav.GetTileByRef(startRef);
                float agentRadius = tile.header.walkableRadius;
                m_query.raycastLimitSqr = (float)Math.Pow(agentRadius * Detour.DT_RAY_CAST_LIMIT_PROPORTIONS, 2);
            }

            if (startRef == endRef)
            {
                m_query.status = Status.DT_SUCCESS;
                return Status.DT_SUCCESS;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            Node startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = startPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = Vector3.Distance(startPos, endPos) * Detour.H_SCALE;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            m_query.status = Status.DT_IN_PROGRESS;
            m_query.lastBestNode = startNode;
            m_query.lastBestNodeCost = startNode.total;

            return m_query.status;
        }
        /// <summary>
        /// Updates an in-progress sliced path query.
        /// </summary>
        /// <param name="maxIter">The maximum number of iterations to perform.</param>
        /// <param name="doneIters">The actual number of iterations completed.</param>
        /// <returns>The status flags for the query.</returns>
        public Status UpdateSlicedFindPath(int maxIter, out int doneIters)
        {
            doneIters = 0;

            if (!m_query.status.HasFlag(Status.DT_IN_PROGRESS))
            {
                return m_query.status;
            }

            // Make sure the request is still valid.
            if (!m_nav.IsValidPolyRef(m_query.startRef) || !m_nav.IsValidPolyRef(m_query.endRef))
            {
                m_query.status = Status.DT_FAILURE;
                return Status.DT_FAILURE;
            }

            int iter = 0;
            while (iter < maxIter && !m_openList.Empty())
            {
                iter++;

                // Remove node from open list and put it in closed list.
                Node bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Reached the goal, stop searching.
                if (bestNode.id == m_query.endRef)
                {
                    m_query.lastBestNode = bestNode;
                    Status details = m_query.status & Status.DT_STATUS_DETAIL_MASK;
                    m_query.status = Status.DT_SUCCESS | details;
                    doneIters = iter;
                    return m_query.status;
                }

                // Get current poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                if (!m_nav.GetTileAndPolyByRef(bestRef, out MeshTile bestTile, out Poly bestPoly))
                {
                    // The polygon has disappeared during the sliced query, fail.
                    m_query.status = Status.DT_FAILURE;
                    doneIters = iter;
                    return m_query.status;
                }

                // Get parent and grand parent poly and tile.
                int parentRef = 0;
                int grandpaRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                Node parentNode = null;
                if (bestNode.pidx != 0)
                {
                    parentNode = m_nodePool.GetNodeAtIdx(bestNode.pidx);
                    parentRef = parentNode.id;
                    if (parentNode.pidx != 0)
                    {
                        grandpaRef = m_nodePool.GetNodeAtIdx(parentNode.pidx).id;
                    }
                }
                if (parentRef != 0)
                {
                    bool invalidParent = !m_nav.GetTileAndPolyByRef(parentRef, out parentTile, out parentPoly);
                    if (invalidParent || (grandpaRef != 0 && !m_nav.IsValidPolyRef(grandpaRef)))
                    {
                        // The polygon has disappeared during the sliced query, fail.
                        m_query.status = Status.DT_FAILURE;
                        doneIters = iter;
                        return m_query.status;
                    }
                }

                // decide whether to test raycast to previous nodes
                bool tryLOS = false;
                if ((m_query.options & FindPathOptions.DT_FINDPATH_ANY_ANGLE) != 0)
                {
                    if ((parentRef != 0) && (Vector3.DistanceSquared(parentNode.pos, bestNode.pos) < m_query.raycastLimitSqr))
                    {
                        tryLOS = true;
                    }
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    int neighbourRef = bestTile.links[i].nref;

                    // Skip invalid ids and do not expand back to where we came from.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Get neighbour poly and tile.
                    // The API input has been cheked already, skip checking internal data.
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    if (!m_query.filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // get the neighbor node
                    Node neighbourNode = m_nodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        m_query.status |= Status.DT_OUT_OF_NODES;
                        continue;
                    }

                    // do not expand to nodes that were already visited from the same parent
                    if (neighbourNode.pidx != 0 && neighbourNode.pidx == bestNode.pidx)
                    {
                        continue;
                    }

                    // If the node is visited the first time, calculate node position.
                    if (neighbourNode.flags == NodeFlags.DT_NODE_NONE)
                    {
                        GetEdgeMidPoint(
                            bestRef, bestPoly, bestTile,
                            neighbourRef, neighbourPoly, neighbourTile,
                            out neighbourNode.pos);
                    }

                    // Calculate cost and heuristic.
                    float cost = 0;
                    float heuristic = 0;

                    // raycast parent
                    bool foundShortCut = false;

                    RaycastHit rayHit = new RaycastHit
                    {
                        maxPath = 0,
                        pathCost = 0,
                        t = 0,
                    };
                    if (tryLOS)
                    {
                        Raycast(parentRef, parentNode.pos, neighbourNode.pos, m_query.filter, RaycastOptions.DT_RAYCAST_USE_COSTS, 0, out rayHit, ref grandpaRef);
                        foundShortCut = rayHit.t >= 1.0f;
                    }

                    // update move cost
                    if (foundShortCut)
                    {
                        // shortcut found using raycast. Using shorter cost instead
                        cost = parentNode.cost + rayHit.pathCost;
                    }
                    else
                    {
                        // No shortcut found.
                        float curCost = m_query.filter.GetCost(
                            bestNode.pos, neighbourNode.pos,
                            parentRef, parentTile, parentPoly,
                            bestRef, bestTile, bestPoly,
                            neighbourRef, neighbourTile, neighbourPoly);

                        cost = bestNode.cost + curCost;
                    }

                    // Special case for last node.
                    if (neighbourRef == m_query.endRef)
                    {
                        float endCost = m_query.filter.GetCost(
                            neighbourNode.pos, m_query.endPos,
                            bestRef, bestTile, bestPoly,
                            neighbourRef, neighbourTile, neighbourPoly,
                            0, null, null);

                        cost = cost + endCost;
                        heuristic = 0;
                    }
                    else
                    {
                        heuristic = Vector3.Distance(neighbourNode.pos, m_query.endPos) * Detour.H_SCALE;
                    }

                    float total = cost + heuristic;

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }
                    // The node is already visited and process, and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    // Add or update the node.
                    neighbourNode.pidx = foundShortCut ? bestNode.pidx : m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.id = neighbourRef;
                    neighbourNode.flags = (neighbourNode.flags & ~(NodeFlags.DT_NODE_CLOSED | NodeFlags.DT_NODE_PARENT_DETACHED));
                    neighbourNode.cost = cost;
                    neighbourNode.total = total;
                    if (foundShortCut)
                    {
                        neighbourNode.flags = (neighbourNode.flags | NodeFlags.DT_NODE_PARENT_DETACHED);
                    }

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        // Already in open, update node location.
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        // Put the node in open list.
                        neighbourNode.flags |= NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }

                    // Update nearest node to target so far.
                    if (heuristic < m_query.lastBestNodeCost)
                    {
                        m_query.lastBestNodeCost = heuristic;
                        m_query.lastBestNode = neighbourNode;
                    }
                }
            }

            // Exhausted all nodes, but could not find path.
            if (m_openList.Empty())
            {
                Status details = m_query.status & Status.DT_STATUS_DETAIL_MASK;
                m_query.status = Status.DT_SUCCESS | details;
            }

            doneIters = iter;

            return m_query.status;
        }
        /// <summary>
        /// Finalizes and returns the results of a sliced path query.
        /// </summary>
        /// <param name="path">An ordered list of polygon references representing the path. (Start to end.)</param>
        /// <param name="pathCount">The number of polygons returned in the path array.</param>
        /// <param name="maxPath">The max number of polygons the path array can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FinalizeSlicedFindPath(out int[] path, out int pathCount, int maxPath)
        {
            path = null;
            pathCount = 0;

            List<int> pathList = new List<int>();

            if (m_query.status.HasFlag(Status.DT_FAILURE))
            {
                // Reset query.
                m_query = null;
                return Status.DT_FAILURE;
            }

            if (m_query.startRef == m_query.endRef)
            {
                // Special case: the search starts and ends at same poly.
                pathList.Add(m_query.startRef);
            }
            else
            {
                // Reverse the path.
                if (m_query.lastBestNode.id != m_query.endRef)
                {
                    m_query.status |= Status.DT_PARTIAL_RESULT;
                }

                Node prev = null;
                Node node = m_query.lastBestNode;
                NodeFlags prevRay = 0;
                do
                {
                    Node next = m_nodePool.GetNodeAtIdx(node.pidx);
                    node.pidx = m_nodePool.GetNodeIdx(prev);
                    prev = node;
                    NodeFlags nextRay = node.flags & NodeFlags.DT_NODE_PARENT_DETACHED; // keep track of whether parent is not adjacent (i.e. due to raycast shortcut)
                    node.flags = (node.flags & ~NodeFlags.DT_NODE_PARENT_DETACHED) | prevRay; // and store it in the reversed path's node
                    prevRay = nextRay;
                    node = next;
                }
                while (node != null);

                // Store path
                node = prev;
                do
                {
                    Node next = m_nodePool.GetNodeAtIdx(node.pidx);
                    Status status = 0;
                    if ((node.flags & NodeFlags.DT_NODE_PARENT_DETACHED) != 0)
                    {
                        status = Raycast(node.id, node.pos, next.pos, m_query.filter, out float t, out Vector3 normal, out int[] rpath, out int m, maxPath - pathList.Count);
                        if (status.HasFlag(Status.DT_SUCCESS))
                        {
                            pathList.AddRange(rpath);
                        }
                        // raycast ends on poly boundary and the path might include the next poly boundary.
                        if (pathList[pathList.Count - 1] == next.id)
                        {
                            pathList.RemoveAt(pathList.Count - 1); // remove to avoid duplicates
                        }
                    }
                    else
                    {
                        pathList.Add(node.id);
                        if (pathList.Count >= maxPath)
                        {
                            status = Status.DT_BUFFER_TOO_SMALL;
                        }
                    }

                    if ((status & Status.DT_STATUS_DETAIL_MASK) != 0)
                    {
                        m_query.status |= status & Status.DT_STATUS_DETAIL_MASK;
                        break;
                    }
                    node = next;
                }
                while (node != null);
            }

            Status details = m_query.status & Status.DT_STATUS_DETAIL_MASK;

            // Reset query.
            m_query = new QueryData();

            path = pathList.ToArray();
            pathCount = pathList.Count;

            return Status.DT_SUCCESS | details;
        }
        /// <summary>
        /// Finalizes and returns the results of an incomplete sliced path query, returning the path to the furthest polygon on the existing path that was visited during the search.
        /// </summary>
        /// <param name="existing">An array of polygon references for the existing path.</param>
        /// <param name="existingSize">The number of polygon in the existing array.</param>
        /// <param name="path">An ordered list of polygon references representing the path. (Start to end.)</param>
        /// <param name="pathCount">The number of polygons returned in the path array.</param>
        /// <param name="maxPath">The max number of polygons the @p path array can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FinalizeSlicedFindPathPartial(int[] existing, int existingSize, out int[] path, out int pathCount, int maxPath)
        {
            path = null;
            pathCount = 0;

            if (existingSize == 0)
            {
                return Status.DT_FAILURE;
            }

            if (m_query.status.HasFlag(Status.DT_FAILURE))
            {
                // Reset query.
                m_query = new QueryData();
                return Status.DT_FAILURE;
            }

            List<int> pathList = new List<int>();

            if (m_query.startRef == m_query.endRef)
            {
                // Special case: the search starts and ends at same poly.
                pathList.Add(m_query.startRef);
            }
            else
            {
                // Find furthest existing node that was visited.
                Node prev = null;
                Node node = null;
                for (int i = existingSize - 1; i >= 0; --i)
                {
                    m_nodePool.FindNodes(existing[i], out Node[] nodes, 1);
                    if (nodes != null)
                    {
                        node = nodes[0];
                        break;
                    }
                }

                if (node == null)
                {
                    m_query.status |= Status.DT_PARTIAL_RESULT;
                    node = m_query.lastBestNode;
                }

                // Reverse the path.
                NodeFlags prevRay = 0;
                do
                {
                    Node next = m_nodePool.GetNodeAtIdx(node.pidx);
                    node.pidx = m_nodePool.GetNodeIdx(prev);
                    prev = node;
                    NodeFlags nextRay = node.flags & NodeFlags.DT_NODE_PARENT_DETACHED; // keep track of whether parent is not adjacent (i.e. due to raycast shortcut)
                    node.flags = (node.flags & ~NodeFlags.DT_NODE_PARENT_DETACHED) | prevRay; // and store it in the reversed path's node
                    prevRay = nextRay;
                    node = next;
                }
                while (node != null);

                // Store path
                node = prev;
                do
                {
                    Node next = m_nodePool.GetNodeAtIdx(node.pidx);
                    Status status = 0;
                    if ((node.flags & NodeFlags.DT_NODE_PARENT_DETACHED) != 0)
                    {
                        status = Raycast(node.id, node.pos, next.pos, m_query.filter, out float t, out Vector3 normal, out int[] rpath, out int m, maxPath - pathList.Count);
                        if (status.HasFlag(Status.DT_SUCCESS))
                        {
                            pathList.AddRange(rpath);
                        }
                        // raycast ends on poly boundary and the path might include the next poly boundary.
                        if (pathList[pathList.Count - 1] == next.id)
                        {
                            pathList.RemoveAt(pathList.Count); // remove to avoid duplicates
                        }
                    }
                    else
                    {
                        pathList.Add(node.id);
                        if (pathList.Count >= maxPath)
                        {
                            status = Status.DT_BUFFER_TOO_SMALL;
                        }
                    }

                    if ((status & Status.DT_STATUS_DETAIL_MASK) != 0)
                    {
                        m_query.status |= status & Status.DT_STATUS_DETAIL_MASK;
                        break;
                    }
                    node = next;
                }
                while (node != null);
            }

            Status details = m_query.status & Status.DT_STATUS_DETAIL_MASK;

            // Reset query.
            m_query = new QueryData();

            path = pathList.ToArray();
            pathCount = pathList.Count;

            return Status.DT_SUCCESS | details;
        }
        /// <summary>
        /// Finds the polygons along the navigation graph that touch the specified circle.
        /// </summary>
        /// <param name="startRef">The reference id of the polygon where the search starts.</param>
        /// <param name="centerPos">The center of the search circle.</param>
        /// <param name="radius">The radius of the search circle.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="resultRef">The reference ids of the polygons touched by the circle.</param>
        /// <param name="resultParent">The reference ids of the parent polygons for each result. Zero if a result polygon has no parent.</param>
        /// <param name="resultCost">The search cost from centerPos to the polygon.</param>
        /// <param name="resultCount">The number of polygons found.</param>
        /// <param name="maxResult">The maximum number of polygons the result arrays can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindPolysAroundCircle(int startRef, Vector3 centerPos, float radius, QueryFilter filter, out int[] resultRef, out int[] resultParent, out float[] resultCost, out int resultCount, int maxResult)
        {
            resultRef = new int[maxResult];
            resultParent = new int[maxResult];
            resultCost = new float[maxResult];
            resultCount = 0;

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            Node startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = centerPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            Status status = Status.DT_SUCCESS;

            int n = 0;

            float radiusSqr = (radius * radius);

            while (!m_openList.Empty())
            {
                Node bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(bestRef, out MeshTile bestTile, out Poly bestPoly);

                // Get parent poly and tile.
                int parentRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                if (bestNode.pidx != 0)
                {
                    parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
                }
                if (parentRef != 0)
                {
                    m_nav.GetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);
                }

                if (n < maxResult)
                {
                    resultRef[n] = bestRef;
                    resultParent[n] = parentRef;
                    resultCost[n] = bestNode.total;
                    ++n;
                }
                else
                {
                    status |= Status.DT_BUFFER_TOO_SMALL;
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    Link link = bestTile.links[i];
                    int neighbourRef = link.nref;
                    // Skip invalid neighbours and do not follow back to parent.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Expand to neighbour
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    // Do not advance if the polygon is excluded by the filter.
                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // Find edge and calc distance to the edge.
                    if (GetPortalPoints(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, out Vector3 va, out Vector3 vb).HasFlag(Status.DT_FAILURE))
                    {
                        continue;
                    }

                    // If the circle is not touching the next polygon, skip it.
                    float distSqr = Detour.DistancePtSegSqr2D(centerPos, va, vb, out float tseg);
                    if (distSqr > radiusSqr)
                    {
                        continue;
                    }

                    Node neighbourNode = m_nodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        status |= Status.DT_OUT_OF_NODES;
                        continue;
                    }

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                    {
                        continue;
                    }

                    // Cost
                    if (neighbourNode.flags == 0)
                    {
                        neighbourNode.pos = Vector3.Lerp(va, vb, 0.5f);
                    }

                    float cost = filter.GetCost(
                        bestNode.pos, neighbourNode.pos,
                        parentRef, parentTile, parentPoly,
                        bestRef, bestTile, bestPoly,
                        neighbourRef, neighbourTile, neighbourPoly);

                    float total = bestNode.total + cost;

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    neighbourNode.id = neighbourRef;
                    neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.total = total;

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        neighbourNode.flags = NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }
                }
            }

            resultCount = n;

            return status;
        }
        /// <summary>
        /// Finds the polygons along the naviation graph that touch the specified convex polygon.
        /// </summary>
        /// <param name="startRef">The reference id of the polygon where the search starts.</param>
        /// <param name="verts">The vertices describing the convex polygon.</param>
        /// <param name="nverts">The number of vertices in the polygon.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="resultRef">The reference ids of the polygons touched by the search polygon.</param>
        /// <param name="resultParent">The reference ids of the parent polygons for each result. Zero if a result polygon has no parent.</param>
        /// <param name="resultCost">The search cost from the centroid point to the polygon.</param>
        /// <param name="resultCount">The number of polygons found.</param>
        /// <param name="maxResult">The maximum number of polygons the result arrays can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindPolysAroundShape(int startRef, Vector3[] verts, int nverts, QueryFilter filter, out int[] resultRef, out int[] resultParent, out float[] resultCost, out int resultCount, int maxResult)
        {
            resultRef = new int[maxResult];
            resultParent = new int[maxResult];
            resultCost = new float[maxResult];
            resultCount = 0;

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            Vector3 centerPos = Vector3.Zero;
            for (int i = 0; i < nverts; ++i)
            {
                centerPos += verts[i];
            }
            centerPos *= (1.0f / nverts);

            Node startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = centerPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            Status status = Status.DT_SUCCESS;

            int n = 0;

            while (!m_openList.Empty())
            {
                Node bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(bestRef, out MeshTile bestTile, out Poly bestPoly);

                // Get parent poly and tile.
                int parentRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                if (bestNode.pidx != 0)
                {
                    parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
                }
                if (parentRef != 0)
                {
                    m_nav.GetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);
                }

                if (n < maxResult)
                {
                    resultRef[n] = bestRef;
                    resultParent[n] = parentRef;
                    resultCost[n] = bestNode.total;
                    ++n;
                }
                else
                {
                    status |= Status.DT_BUFFER_TOO_SMALL;
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    Link link = bestTile.links[i];
                    int neighbourRef = link.nref;
                    // Skip invalid neighbours and do not follow back to parent.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Expand to neighbour
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    // Do not advance if the polygon is excluded by the filter.
                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // Find edge and calc distance to the edge.
                    if (GetPortalPoints(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, out Vector3 va, out Vector3 vb).HasFlag(Status.DT_FAILURE))
                    {
                        continue;
                    }

                    // If the poly is not touching the edge to the next polygon, skip the connection it.
                    if (!Detour.IntersectSegmentPoly2D(va, vb, verts, nverts, out float tmin, out float tmax, out int segMin, out int segMax))
                    {
                        continue;
                    }
                    if (tmin > 1.0f || tmax < 0.0f)
                    {
                        continue;
                    }

                    Node neighbourNode = m_nodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        status |= Status.DT_OUT_OF_NODES;
                        continue;
                    }

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                    {
                        continue;
                    }

                    // Cost
                    if (neighbourNode.flags == 0)
                    {
                        neighbourNode.pos = Vector3.Lerp(va, vb, 0.5f);
                    }

                    float cost = filter.GetCost(
                        bestNode.pos, neighbourNode.pos,
                        parentRef, parentTile, parentPoly,
                        bestRef, bestTile, bestPoly,
                        neighbourRef, neighbourTile, neighbourPoly);

                    float total = bestNode.total + cost;

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    neighbourNode.id = neighbourRef;
                    neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.total = total;

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        neighbourNode.flags = NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }
                }
            }

            resultCount = n;

            return status;
        }
        /// <summary>
        /// Gets a path from the explored nodes in the previous search.
        /// </summary>
        /// <param name="endRef">The reference id of the end polygon.</param>
        /// <param name="path">An ordered list of polygon references representing the path. (Start to end.)</param>
        /// <param name="pathCount">The number of polygons returned in the path array.</param>
        /// <param name="maxPath">The maximum number of polygons the path array can hold.</param>
        /// <returns>
        /// The status flags. Returns DT_FAILURE | DT_INVALID_PARAM if any parameter is wrong, or if
        /// endRef was not explored in the previous search. Returns DT_SUCCESS | DT_BUFFER_TOO_SMALL
        /// if path cannot contain the entire path. In this case it is filled to capacity with a partial path.
        /// Otherwise returns DT_SUCCESS.
        /// </returns>
        /// <remarks>
        /// The result of this function depends on the state of the query object. For that reason it should only
        /// be used immediately after one of the two Dijkstra searches, findPolysAroundCircle or findPolysAroundShape.
        /// </remarks>
        public Status GetPathFromDijkstraSearch(int endRef, out int[] path, out int pathCount, int maxPath)
        {
            path = null;
            pathCount = 0;

            if (!m_nav.IsValidPolyRef(endRef) || path == null || pathCount == 0 || maxPath < 0)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            if (m_nodePool.FindNodes(endRef, out Node[] endNodes, 1) != 1 || (endNodes[0].flags & NodeFlags.DT_NODE_CLOSED) == 0)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            return GetPathToNode(endNodes[0], out path, out pathCount, maxPath);
        }
        /// <summary>
        /// Finds the polygon nearest to the specified center point.
        /// </summary>
        /// <param name="center">The center of the search box.</param>
        /// <param name="halfExtents">The search distance along each axis.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="nearestRef">The reference id of the nearest polygon.</param>
        /// <param name="nearestPt">The nearest point on the polygon.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindNearestPoly(Vector3 center, Vector3 halfExtents, QueryFilter filter, out int nearestRef, out Vector3 nearestPt)
        {
            nearestRef = 0;
            nearestPt = Vector3.Zero;

            var query = new FindNearestPolyQuery(this, center);

            Status status = QueryPolygons(center, halfExtents, filter, query);
            if (status.HasFlag(Status.DT_FAILURE))
            {
                return status;
            }

            nearestRef = query.NearestRef();
            // Only override nearestPt if we actually found a poly so the nearest point is valid.
            if (nearestRef != 0)
            {
                nearestPt = query.NearestPoint();
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Finds polygons that overlap the search box.
        /// </summary>
        /// <param name="center">The center of the search box.</param>
        /// <param name="halfExtents">The search distance along each axis.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="polys">The reference ids of the polygons that overlap the query box.</param>
        /// <param name="polyCount">The number of polygons in the search result.</param>
        /// <param name="maxPolys">The maximum number of polygons the search result can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status QueryPolygons(Vector3 center, Vector3 halfExtents, QueryFilter filter, int[] polys, int polyCount, int maxPolys)
        {
            if (polys == null || polyCount == 0 || maxPolys < 0)
            {
                return Status.DT_FAILURE;
            }

            CollectPolysQuery collector = new CollectPolysQuery(polys, maxPolys);

            if (QueryPolygons(center, halfExtents, filter, collector).HasFlag(Status.DT_FAILURE))
            {
                return Status.DT_FAILURE;
            }

            polyCount = collector.NumCollected();

            return collector.Overflowed() ? Status.DT_SUCCESS | Status.DT_BUFFER_TOO_SMALL : Status.DT_SUCCESS;
        }
        /// <summary>
        /// Finds polygons that overlap the search box.
        /// </summary>
        /// <param name="center">The center of the search box.</param>
        /// <param name="halfExtents">The search distance along each axis.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="query">The query. Polygons found will be batched together and passed to this query.</param>
        /// <returns>The status flags for the query.</returns>
        public Status QueryPolygons(Vector3 center, Vector3 halfExtents, QueryFilter filter, IPolyQuery query)
        {
            Vector3 bmin = Vector3.Subtract(center, halfExtents);
            Vector3 bmax = Vector3.Add(center, halfExtents);

            // Find tiles the query touches.
            m_nav.CalcTileLoc(bmin, out int minx, out int miny);
            m_nav.CalcTileLoc(bmax, out int maxx, out int maxy);

            int MAX_NEIS = 32;
            MeshTile[] neis = new MeshTile[MAX_NEIS];

            for (int y = miny; y <= maxy; ++y)
            {
                for (int x = minx; x <= maxx; ++x)
                {
                    int nneis = m_nav.GetTilesAt(x, y, neis, MAX_NEIS);
                    for (int j = 0; j < nneis; ++j)
                    {
                        QueryPolygonsInTile(neis[j], bmin, bmax, filter, query);
                    }
                }
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Finds the non-overlapping navigation polygons in the local neighbourhood around the center position.
        /// </summary>
        /// <param name="startRef">The reference id of the polygon where the search starts.</param>
        /// <param name="centerPos">The center of the query circle.</param>
        /// <param name="radius">The radius of the query circle.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="resultRef">The reference ids of the polygons touched by the circle.</param>
        /// <param name="resultParent">The reference ids of the parent polygons for each result. Zero if a result polygon has no parent.</param>
        /// <param name="resultCount">The number of polygons found.</param>
        /// <param name="maxResult">The maximum number of polygons the result arrays can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindLocalNeighbourhood(int startRef, Vector3 centerPos, float radius, QueryFilter filter, out int[] resultRef, out int[] resultParent, out int resultCount, int maxResult)
        {
            resultRef = new int[maxResult];
            resultParent = new int[maxResult];
            resultCount = 0;

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            int MAX_STACK = 48;
            Node[] stack = new Node[MAX_STACK];
            int nstack = 0;

            m_tinyNodePool.Clear();

            Node startNode = m_tinyNodePool.GetNode(startRef, 0);
            startNode.pidx = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_CLOSED;
            stack[nstack++] = startNode;

            float radiusSqr = (radius * radius);

            Vector3[] pa = new Vector3[Detour.DT_VERTS_PER_POLYGON];
            Vector3[] pb = new Vector3[Detour.DT_VERTS_PER_POLYGON];

            Status status = Status.DT_SUCCESS;

            int n = 0;
            if (n < maxResult)
            {
                resultRef[n] = startNode.id;
                resultParent[n] = 0;
                ++n;
            }
            else
            {
                status |= Status.DT_BUFFER_TOO_SMALL;
            }

            while (nstack != 0)
            {
                // Pop front.
                Node curNode = stack[0];
                for (int i = 0; i < nstack - 1; ++i)
                {
                    stack[i] = stack[i + 1];
                }
                nstack--;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int curRef = curNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(curRef, out MeshTile curTile, out Poly curPoly);

                for (int i = curPoly.firstLink; i != Detour.DT_NULL_LINK; i = curTile.links[i].next)
                {
                    Link link = curTile.links[i];
                    int neighbourRef = link.nref;
                    // Skip invalid neighbours.
                    if (neighbourRef == 0)
                    {
                        continue;
                    }

                    // Skip if cannot alloca more nodes.
                    Node neighbourNode = m_tinyNodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        continue;
                    }
                    // Skip visited.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                    {
                        continue;
                    }

                    // Expand to neighbour
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    // Skip off-mesh connections.
                    if (neighbourPoly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    {
                        continue;
                    }

                    // Do not advance if the polygon is excluded by the filter.
                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // Find edge and calc distance to the edge.
                    if (GetPortalPoints(curRef, curPoly, curTile, neighbourRef, neighbourPoly, neighbourTile, out Vector3 va, out Vector3 vb).HasFlag(Status.DT_FAILURE))
                    {
                        continue;
                    }

                    // If the circle is not touching the next polygon, skip it.
                    float distSqr = Detour.DistancePtSegSqr2D(centerPos, va, vb, out float tseg);
                    if (distSqr > radiusSqr)
                    {
                        continue;
                    }

                    // Mark node visited, this is done before the overlap test so that
                    // we will not visit the poly again if the test fails.
                    neighbourNode.flags |= NodeFlags.DT_NODE_CLOSED;
                    neighbourNode.pidx = m_tinyNodePool.GetNodeIdx(curNode);

                    // Check that the polygon does not collide with existing polygons.

                    // Collect vertices of the neighbour poly.
                    int npa = neighbourPoly.vertCount;
                    for (int k = 0; k < npa; ++k)
                    {
                        pa[k] = neighbourTile.verts[neighbourPoly.verts[k]];
                    }

                    bool overlap = false;
                    for (int j = 0; j < n; ++j)
                    {
                        int pastRef = resultRef[j];

                        // Connected polys do not overlap.
                        bool connected = false;
                        for (int k = curPoly.firstLink; k != Detour.DT_NULL_LINK; k = curTile.links[k].next)
                        {
                            if (curTile.links[k].nref == pastRef)
                            {
                                connected = true;
                                break;
                            }
                        }
                        if (connected)
                        {
                            continue;
                        }

                        // Potentially overlapping.
                        m_nav.GetTileAndPolyByRefUnsafe(pastRef, out MeshTile pastTile, out Poly pastPoly);

                        // Get vertices and test overlap
                        int npb = pastPoly.vertCount;
                        for (int k = 0; k < npb; ++k)
                        {
                            pb[k] = pastTile.verts[pastPoly.verts[k]];
                        }

                        if (Detour.OverlapPolyPoly2D(pa, npa, pb, npb))
                        {
                            overlap = true;
                            break;
                        }
                    }
                    if (overlap)
                        continue;

                    // This poly is fine, store and advance to the poly.
                    if (n < maxResult)
                    {
                        resultRef[n] = neighbourRef;
                        resultParent[n] = curRef;
                        ++n;
                    }
                    else
                    {
                        status |= Status.DT_BUFFER_TOO_SMALL;
                    }

                    if (nstack < MAX_STACK)
                    {
                        stack[nstack++] = neighbourNode;
                    }
                }
            }

            resultCount = n;

            return status;
        }
        /// <summary>
        /// Moves from the start to the end position constrained to the navigation mesh.
        /// </summary>
        /// <param name="startRef">The reference id of the start polygon.</param>
        /// <param name="startPos">A position of the mover within the start polygon.</param>
        /// <param name="endPos">The desired end position of the mover.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="resultPos">The result position of the mover.</param>
        /// <param name="visited">The reference ids of the polygons visited during the move.</param>
        /// <param name="visitedCount">The number of polygons visited during the move.</param>
        /// <param name="maxVisitedSize">The maximum number of polygons the visited array can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status MoveAlongSurface(int startRef, Vector3 startPos, Vector3 endPos, QueryFilter filter, out Vector3 resultPos, out int[] visited, out int visitedCount, int maxVisitedSize)
        {
            resultPos = Vector3.Zero;
            visited = new int[maxVisitedSize];
            visitedCount = 0;

            // Validate input
            if (startRef == 0)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }
            if (!m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            Status status = Status.DT_SUCCESS;

            int MAX_STACK = 48;
            Node[] stack = new Node[MAX_STACK];
            int nstack = 0;

            m_tinyNodePool.Clear();

            Node startNode = m_tinyNodePool.GetNode(startRef, 0);
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_CLOSED;
            stack[nstack++] = startNode;

            Vector3 bestPos = startPos;
            float bestDist = float.MaxValue;
            Node bestNode = null;

            // Search constraints
            Vector3 searchPos = Vector3.Lerp(startPos, endPos, 0.5f);
            float searchRadSqr = (float)Math.Pow(Vector3.Distance(startPos, endPos) / 2.0f + 0.001f, 2);

            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON];

            while (nstack != 0)
            {
                // Pop front.
                Node curNode = stack[0];
                for (int i = 0; i < nstack - 1; ++i)
                {
                    stack[i] = stack[i + 1];
                }
                nstack--;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int curRef = curNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(curRef, out MeshTile curTile, out Poly curPoly);

                // Collect vertices.
                int nverts = curPoly.vertCount;
                for (int i = 0; i < nverts; ++i)
                {
                    verts[i] = curTile.verts[curPoly.verts[i]];
                }

                // If target is inside the poly, stop search.
                if (Detour.PointInPolygon(endPos, verts, nverts))
                {
                    bestNode = curNode;
                    bestPos = endPos;
                    break;
                }

                // Find wall edges and find nearest point inside the walls.
                for (int i = 0, j = curPoly.vertCount - 1; i < curPoly.vertCount; j = i++)
                {
                    // Find links to neighbours.
                    int MAX_NEIS = 8;
                    int nneis = 0;
                    int[] neis = new int[MAX_NEIS];

                    if ((curPoly.neis[j] & Detour.DT_EXT_LINK) != 0)
                    {
                        // Tile border.
                        for (int k = curPoly.firstLink; k != Detour.DT_NULL_LINK; k = curTile.links[k].next)
                        {
                            Link link = curTile.links[k];
                            if (link.edge == j)
                            {
                                if (link.nref != 0)
                                {
                                    m_nav.GetTileAndPolyByRefUnsafe(link.nref, out MeshTile neiTile, out Poly neiPoly);
                                    if (filter.PassFilter(link.nref, neiTile, neiPoly))
                                    {
                                        if (nneis < MAX_NEIS)
                                        {
                                            neis[nneis++] = link.nref;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (curPoly.neis[j] != 0)
                    {
                        int idx = (curPoly.neis[j] - 1);
                        int r = m_nav.GetPolyRefBase(curTile) | idx;
                        if (filter.PassFilter(r, curTile, curTile.polys[idx]))
                        {
                            // Internal edge, encode id.
                            neis[nneis++] = r;
                        }
                    }

                    if (nneis == 0)
                    {
                        // Wall edge, calc distance.
                        Vector3 vj = verts[j];
                        Vector3 vi = verts[i];
                        float distSqr = Detour.DistancePtSegSqr2D(endPos, vj, vi, out float tseg);
                        if (distSqr < bestDist)
                        {
                            // Update nearest distance.
                            bestPos = Vector3.Lerp(vj, vi, tseg);
                            bestDist = distSqr;
                            bestNode = curNode;
                        }
                    }
                    else
                    {
                        for (int k = 0; k < nneis; ++k)
                        {
                            // Skip if no node can be allocated.
                            Node neighbourNode = m_tinyNodePool.GetNode(neis[k], 0);
                            if (neighbourNode == null)
                            {
                                continue;
                            }
                            // Skip if already visited.
                            if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                            {
                                continue;
                            }

                            // Skip the link if it is too far from search constraint.
                            // TODO: Maybe should use getPortalPoints(), but this one is way faster.
                            Vector3 vj = verts[j];
                            Vector3 vi = verts[i];
                            float distSqr = Detour.DistancePtSegSqr2D(searchPos, vj, vi, out float tseg);
                            if (distSqr > searchRadSqr)
                            {
                                continue;
                            }

                            // Mark as the node as visited and push to queue.
                            if (nstack < MAX_STACK)
                            {
                                neighbourNode.pidx = m_tinyNodePool.GetNodeIdx(curNode);
                                neighbourNode.flags |= NodeFlags.DT_NODE_CLOSED;
                                stack[nstack++] = neighbourNode;
                            }
                        }
                    }
                }
            }

            int n = 0;
            if (bestNode != null)
            {
                // Reverse the path.
                Node prev = null;
                Node node = bestNode;
                do
                {
                    Node next = m_tinyNodePool.GetNodeAtIdx(node.pidx);
                    node.pidx = m_tinyNodePool.GetNodeIdx(prev);
                    prev = node;
                    node = next;
                }
                while (node != null);

                // Store result
                node = prev;
                do
                {
                    visited[n++] = node.id;
                    if (n >= maxVisitedSize)
                    {
                        status |= Status.DT_BUFFER_TOO_SMALL;
                        break;
                    }
                    node = m_tinyNodePool.GetNodeAtIdx(node.pidx);
                }
                while (node != null);
            }

            resultPos = bestPos;

            visitedCount = n;

            return status;
        }
        /// <summary>
        /// Casts a 'walkability' ray along the surface of the navigation mesh from the start position toward the end position.
        /// </summary>
        /// <param name="startRef">The reference id of the start polygon.</param>
        /// <param name="startPos">A position within the start polygon representing the start of the ray.</param>
        /// <param name="endPos">The position to cast the ray toward.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="t">The hit parameter. (FLT_MAX if no wall hit.)</param>
        /// <param name="hitNormal">The normal of the nearest wall hit.</param>
        /// <param name="path">The reference ids of the visited polygons.</param>
        /// <param name="pathCount">The number of visited polygons.</param>
        /// <param name="maxPath">The maximum number of polygons the path array can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status Raycast(int startRef, Vector3 startPos, Vector3 endPos, QueryFilter filter, out float t, out Vector3 hitNormal, out int[] path, out int pathCount, int maxPath)
        {
            int prevRef = 0;
            Status status = Raycast(startRef, startPos, endPos, filter, 0, maxPath, out RaycastHit hit, ref prevRef);

            t = hit.t;
            path = hit.path;
            hitNormal = hit.hitNormal;
            pathCount = hit.pathCount;

            return status;
        }
        /// <summary>
        /// Casts a 'walkability' ray along the surface of the navigation mesh from the start position toward the end position.
        /// </summary>
        /// <param name="startRef">The reference id of the start polygon.</param>
        /// <param name="startPos">A position within the start polygon representing the start of the ray.</param>
        /// <param name="endPos">The position to cast the ray toward.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="options">Govern how the raycast behaves. See dtRaycastOptions</param>
        /// <param name="maxPath">The maximum number of polygons the path array can hold.</param>
        /// <param name="hit">Pointer to a raycast hit structure which will be filled by the results.</param>
        /// <param name="prevRef">parent of start ref. Used during for cost calculation</param>
        /// <returns></returns>
        public Status Raycast(int startRef, Vector3 startPos, Vector3 endPos, QueryFilter filter, RaycastOptions options, int maxPath, out RaycastHit hit, ref int prevRef)
        {
            hit = new RaycastHit
            {
                maxPath = maxPath,
                t = 0,
                pathCount = 0,
                pathCost = 0
            };

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }
            if (prevRef == 0 && !m_nav.IsValidPolyRef(prevRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            Vector3 dir, curPos, lastPos;
            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON + 3];
            int n = 0;

            curPos = startPos;
            dir = Vector3.Subtract(endPos, startPos);
            hit.hitNormal = Vector3.Zero;

            Status status = Status.DT_SUCCESS;

            MeshTile prevTile, tile, nextTile;
            Poly prevPoly, poly, nextPoly;
            int curRef;

            // The API input has been checked already, skip checking internal data.
            curRef = startRef;
            tile = null;
            poly = null;
            m_nav.GetTileAndPolyByRefUnsafe(curRef, out tile, out poly);
            nextTile = prevTile = tile;
            nextPoly = prevPoly = poly;
            if (prevRef != 0)
            {
                m_nav.GetTileAndPolyByRefUnsafe(prevRef, out prevTile, out prevPoly);
            }

            while (curRef != 0)
            {
                // Cast ray against current polygon.

                // Collect vertices.
                int nv = 0;
                for (int i = 0; i < poly.vertCount; ++i)
                {
                    verts[nv] = tile.verts[poly.verts[i]];
                    nv++;
                }

                if (!Detour.IntersectSegmentPoly2D(startPos, endPos, verts, nv, out float tmin, out float tmax, out int segMin, out int segMax))
                {
                    // Could not hit the polygon, keep the old t and report hit.
                    hit.pathCount = n;
                    return status;
                }

                hit.hitEdgeIndex = segMax;

                // Keep track of furthest t so far.
                if (tmax > hit.t)
                {
                    hit.t = tmax;
                }

                // Store visited polygons.
                if (n < hit.maxPath)
                {
                    hit.path[n++] = curRef;
                }
                else
                {
                    status |= Status.DT_BUFFER_TOO_SMALL;
                }

                // Ray end is completely inside the polygon.
                if (segMax == -1)
                {
                    hit.t = float.MaxValue;
                    hit.pathCount = n;

                    // add the cost
                    if ((options & RaycastOptions.DT_RAYCAST_USE_COSTS) != 0)
                    {
                        hit.pathCost += filter.GetCost(curPos, endPos, prevRef, prevTile, prevPoly, curRef, tile, poly, curRef, tile, poly);
                    }

                    return status;
                }

                // Follow neighbours.
                int nextRef = 0;

                for (int i = poly.firstLink; i != Detour.DT_NULL_LINK; i = tile.links[i].next)
                {
                    Link link = tile.links[i];

                    // Find link which contains this edge.
                    if (link.edge != segMax)
                    {
                        continue;
                    }

                    // Get pointer to the next polygon.
                    m_nav.GetTileAndPolyByRefUnsafe(link.nref, out nextTile, out nextPoly);

                    // Skip off-mesh connections.
                    if (nextPoly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    {
                        continue;
                    }

                    // Skip links based on filter.
                    if (!filter.PassFilter(link.nref, nextTile, nextPoly))
                    {
                        continue;
                    }

                    // If the link is internal, just return the ref.
                    if (link.side == 0xff)
                    {
                        nextRef = link.nref;
                        break;
                    }

                    // If the link is at tile boundary,

                    // Check if the link spans the whole edge, and accept.
                    if (link.bmin == 0 && link.bmax == 255)
                    {
                        nextRef = link.nref;
                        break;
                    }

                    // Check for partial edge links.
                    int v0 = poly.verts[link.edge];
                    int v1 = poly.verts[(link.edge + 1) % poly.vertCount];
                    Vector3 left = tile.verts[v0];
                    Vector3 right = tile.verts[v1];

                    // Check that the intersection lies inside the link portal.
                    if (link.side == 0 || link.side == 4)
                    {
                        // Calculate link size.
                        const float s = 1.0f / 255.0f;
                        float lmin = left.Z + (right.Z - left.Z) * (link.bmin * s);
                        float lmax = left.Z + (right.Z - left.Z) * (link.bmax * s);
                        if (lmin > lmax)
                        {
                            Helper.Swap(ref lmin, ref lmax);
                        }

                        // Find Z intersection.
                        float z = startPos.Z + (endPos.Z - startPos.Z) * tmax;
                        if (z >= lmin && z <= lmax)
                        {
                            nextRef = link.nref;
                            break;
                        }
                    }
                    else if (link.side == 2 || link.side == 6)
                    {
                        // Calculate link size.
                        const float s = 1.0f / 255.0f;
                        float lmin = left.X + (right.X - left.X) * (link.bmin * s);
                        float lmax = left.X + (right.X - left.X) * (link.bmax * s);
                        if (lmin > lmax)
                        {
                            Helper.Swap(ref lmin, ref lmax);
                        }

                        // Find X intersection.
                        float x = startPos.X + (endPos.X - startPos.X) * tmax;
                        if (x >= lmin && x <= lmax)
                        {
                            nextRef = link.nref;
                            break;
                        }
                    }
                }

                // add the cost
                if ((options & RaycastOptions.DT_RAYCAST_USE_COSTS) != 0)
                {
                    // compute the intersection point at the furthest end of the polygon
                    // and correct the height (since the raycast moves in 2d)
                    lastPos = curPos;
                    curPos = Vector3.Add(startPos, dir) * hit.t;
                    var e1 = verts[segMax];
                    var e2 = verts[((segMax + 1) % nv)];
                    Vector3 eDir;
                    Vector3 diff;
                    eDir = Vector3.Subtract(e2, e1);
                    diff = Vector3.Subtract(curPos, e1);
                    float s = (eDir.X * eDir.X) > (eDir.Z * eDir.Z) ? diff.X / eDir.X : diff.Z / eDir.Z;
                    curPos.Y = e1.Y + eDir.Y * s;

                    hit.pathCost += filter.GetCost(lastPos, curPos, prevRef, prevTile, prevPoly, curRef, tile, poly, nextRef, nextTile, nextPoly);
                }

                if (nextRef == 0)
                {
                    // No neighbour, we hit a wall.

                    // Calculate hit normal.
                    int a = segMax;
                    int b = segMax + 1 < nv ? segMax + 1 : 0;
                    var va = verts[a];
                    var vb = verts[b];
                    float dx = vb.X - va.X;
                    float dz = vb.Z - va.Z;
                    hit.hitNormal.X = dz;
                    hit.hitNormal.Y = 0;
                    hit.hitNormal.Z = -dx;
                    hit.hitNormal.Normalize();
                    hit.pathCount = n;
                    return status;
                }

                // No hit, advance to neighbour polygon.
                prevRef = curRef;
                curRef = nextRef;
                prevTile = tile;
                tile = nextTile;
                prevPoly = poly;
                poly = nextPoly;
            }

            hit.pathCount = n;

            return status;
        }
        /// <summary>
        /// Finds the distance from the specified position to the nearest polygon wall.
        /// </summary>
        /// <param name="startRef">The reference id of the polygon containing centerPos.</param>
        /// <param name="centerPos">The center of the search circle.</param>
        /// <param name="maxRadius">The radius of the search circle.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="hitDist">The distance to the nearest wall from centerPos.</param>
        /// <param name="hitPos">The nearest position on the wall that was hit.</param>
        /// <param name="hitNormal">The normalized ray formed from the wall point to the source point.</param>
        /// <returns>The status flags for the query.</returns>
        public Status FindDistanceToWall(int startRef, Vector3 centerPos, float maxRadius, QueryFilter filter, out float hitDist, out Vector3 hitPos, out Vector3 hitNormal)
        {
            hitDist = 0;
            hitPos = Vector3.Zero;
            hitNormal = Vector3.Zero;

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            Node startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = centerPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            float radiusSqr = (maxRadius * maxRadius);

            Status status = Status.DT_SUCCESS;

            while (!m_openList.Empty())
            {
                Node bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(bestRef, out MeshTile bestTile, out Poly bestPoly);

                // Get parent poly and tile.
                int parentRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                if (bestNode.pidx != 0)
                {
                    parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
                }
                if (parentRef != 0)
                {
                    m_nav.GetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);
                }

                // Hit test walls.
                for (int i = 0, j = bestPoly.vertCount - 1; i < bestPoly.vertCount; j = i++)
                {
                    // Skip non-solid edges.
                    if ((bestPoly.neis[j] & Detour.DT_EXT_LINK) != 0)
                    {
                        // Tile border.
                        bool solid = true;
                        for (int k = bestPoly.firstLink; k != Detour.DT_NULL_LINK; k = bestTile.links[k].next)
                        {
                            Link link = bestTile.links[k];
                            if (link.edge == j)
                            {
                                if (link.nref != 0)
                                {
                                    m_nav.GetTileAndPolyByRefUnsafe(link.nref, out MeshTile neiTile, out Poly neiPoly);
                                    if (filter.PassFilter(link.nref, neiTile, neiPoly))
                                    {
                                        solid = false;
                                    }
                                }
                                break;
                            }
                        }
                        if (!solid)
                        {
                            continue;
                        }
                    }
                    else if (bestPoly.neis[j] != 0)
                    {
                        // Internal edge
                        int idx = bestPoly.neis[j] - 1;
                        int r = m_nav.GetPolyRefBase(bestTile) | idx;
                        if (filter.PassFilter(r, bestTile, bestTile.polys[idx]))
                        {
                            continue;
                        }
                    }

                    // Calc distance to the edge.
                    Vector3 vj = bestTile.verts[bestPoly.verts[j]];
                    Vector3 vi = bestTile.verts[bestPoly.verts[i]];
                    float distSqr = Detour.DistancePtSegSqr2D(centerPos, vj, vi, out float tseg);

                    // Edge is too far, skip.
                    if (distSqr > radiusSqr)
                    {
                        continue;
                    }

                    // Hit wall, update radius.
                    radiusSqr = distSqr;
                    // Calculate hit pos.
                    hitPos = vj + (vi - vj) * tseg;
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    Link link = bestTile.links[i];
                    int neighbourRef = link.nref;
                    // Skip invalid neighbours and do not follow back to parent.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Expand to neighbour.
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    // Skip off-mesh connections.
                    if (neighbourPoly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    {
                        continue;
                    }

                    // Calc distance to the edge.
                    Vector3 va = bestTile.verts[bestPoly.verts[link.edge]];
                    Vector3 vb = bestTile.verts[bestPoly.verts[(link.edge + 1) % bestPoly.vertCount]];
                    float distSqr = Detour.DistancePtSegSqr2D(centerPos, va, vb, out float tseg);

                    // If the circle is not touching the next polygon, skip it.
                    if (distSqr > radiusSqr)
                    {
                        continue;
                    }

                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    Node neighbourNode = m_nodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        status |= Status.DT_OUT_OF_NODES;
                        continue;
                    }

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                    {
                        continue;
                    }

                    // Cost
                    if (neighbourNode.flags == 0)
                    {
                        GetEdgeMidPoint(
                            bestRef, bestPoly, bestTile,
                            neighbourRef, neighbourPoly, neighbourTile,
                            out neighbourNode.pos);
                    }

                    float total = bestNode.total + Vector3.Distance(bestNode.pos, neighbourNode.pos);

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    neighbourNode.id = neighbourRef;
                    neighbourNode.flags = (neighbourNode.flags & ~NodeFlags.DT_NODE_CLOSED);
                    neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.total = total;

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        neighbourNode.flags |= NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }
                }
            }

            // Calc hit normal.
            hitNormal = Vector3.Subtract(centerPos, hitPos);
            hitNormal.Normalize();

            hitDist = (float)Math.Sqrt(radiusSqr);

            return status;
        }
        /// <summary>
        /// Returns the segments for the specified polygon, optionally including portals.
        /// </summary>
        /// <param name="r">The reference id of the polygon.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="segmentVerts">The segments.</param>
        /// <param name="segmentRefs">The reference ids of each segment's neighbor polygon. Or zero if the segment is a wall.</param>
        /// <param name="segmentCount">The number of segments returned.</param>
        /// <param name="maxSegments">The maximum number of segments the result arrays can hold.</param>
        /// <returns>The status flags for the query.</returns>
        public Status GetPolyWallSegments(int r, QueryFilter filter, out Vector3[] segmentVerts, out int[] segmentRefs, out int segmentCount, int maxSegments)
        {
            segmentVerts = new Vector3[maxSegments];
            segmentRefs = new int[maxSegments];
            segmentCount = 0;

            if (!m_nav.GetTileAndPolyByRef(r, out MeshTile tile, out Poly poly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            int n = 0;
            int MAX_INTERVAL = 16;
            SegInterval[] ints = new SegInterval[MAX_INTERVAL];
            int nints;

            bool storePortals = segmentRefs != null;

            Status status = Status.DT_SUCCESS;

            for (int i = 0, j = poly.vertCount - 1; i < poly.vertCount; j = i++)
            {
                // Skip non-solid edges.
                nints = 0;
                if ((poly.neis[j] & Detour.DT_EXT_LINK) != 0)
                {
                    // Tile border.
                    for (int k = poly.firstLink; k != Detour.DT_NULL_LINK; k = tile.links[k].next)
                    {
                        Link link = tile.links[k];
                        if (link.edge == j)
                        {
                            if (link.nref != 0)
                            {
                                m_nav.GetTileAndPolyByRefUnsafe(link.nref, out MeshTile neiTile, out Poly neiPoly);
                                if (filter.PassFilter(link.nref, neiTile, neiPoly))
                                {
                                    SegInterval.InsertInterval(ref ints, ref nints, MAX_INTERVAL, link.bmin, link.bmax, link.nref);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Internal edge
                    int neiRef = 0;
                    if (poly.neis[j] != 0)
                    {
                        int idx = (poly.neis[j] - 1);
                        neiRef = m_nav.GetPolyRefBase(tile) | idx;
                        if (!filter.PassFilter(neiRef, tile, tile.polys[idx]))
                        {
                            neiRef = 0;
                        }
                    }

                    // If the edge leads to another polygon and portals are not stored, skip.
                    if (neiRef != 0 && !storePortals)
                    {
                        continue;
                    }

                    if (n < maxSegments)
                    {
                        segmentVerts[n + 0] = tile.verts[poly.verts[j]];
                        segmentVerts[n + 1] = tile.verts[poly.verts[i]];
                        segmentRefs[n] = neiRef;
                        n++;
                    }
                    else
                    {
                        status |= Status.DT_BUFFER_TOO_SMALL;
                    }

                    continue;
                }

                // Add sentinels
                SegInterval.InsertInterval(ref ints, ref nints, MAX_INTERVAL, -1, 0, 0);
                SegInterval.InsertInterval(ref ints, ref nints, MAX_INTERVAL, 255, 256, 0);

                // Store segments.
                Vector3 vj = tile.verts[poly.verts[j]];
                Vector3 vi = tile.verts[poly.verts[i]];
                for (int k = 1; k < nints; ++k)
                {
                    // Portal segment.
                    if (storePortals && ints[k].r != 0)
                    {
                        float tmin = ints[k].tmin / 255.0f;
                        float tmax = ints[k].tmax / 255.0f;
                        if (n < maxSegments)
                        {
                            segmentVerts[n + 0] = Vector3.Lerp(vj, vi, tmin);
                            segmentVerts[n + 1] = Vector3.Lerp(vj, vi, tmax);
                            segmentRefs[n] = ints[k].r;
                            n++;
                        }
                        else
                        {
                            status |= Status.DT_BUFFER_TOO_SMALL;
                        }
                    }

                    // Wall segment.
                    int imin = ints[k - 1].tmax;
                    int imax = ints[k].tmin;
                    if (imin != imax)
                    {
                        float tmin = imin / 255.0f;
                        float tmax = imax / 255.0f;
                        if (n < maxSegments)
                        {
                            segmentVerts[n + 0] = Vector3.Lerp(vj, vi, tmin);
                            segmentVerts[n + 1] = Vector3.Lerp(vj, vi, tmax);
                            segmentRefs[n] = 0;
                            n++;
                        }
                        else
                        {
                            status |= Status.DT_BUFFER_TOO_SMALL;
                        }
                    }
                }
            }

            segmentCount = n;

            return status;
        }
        /// <summary>
        /// Returns random location on navmesh.
        /// </summary>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="frand">Function returning a random number [0..1).</param>
        /// <param name="randomRef">The reference id of the random location.</param>
        /// <param name="randomPt">The random location. </param>
        /// <returns>The status flags for the query.</returns>
        /// <remarks>
        /// Polygons are chosen weighted by area. The search runs in linear related to number of polygon.
        /// </remarks>
        public Status FindRandomPoint(QueryFilter filter, Random frand, int randomRef, Vector3 randomPt)
        {
            // Randomly pick one tile. Assume that all tiles cover roughly the same area.
            MeshTile tile = null;
            float tsum = 0.0f;
            for (int i = 0; i < m_nav.MaxTiles; i++)
            {
                MeshTile tl = m_nav.Tiles[i];
                if (tl == null || tl.header.magic != Detour.DT_NAVMESH_MAGIC)
                {
                    continue;
                }

                // Choose random tile using reservoi sampling.
                float area = 1.0f; // Could be tile area too.
                tsum += area;
                float u = frand.NextFloat(0, 1);
                if (u * tsum <= area)
                {
                    tile = tl;
                }
            }
            if (tile == null)
            {
                return Status.DT_FAILURE;
            }

            // Randomly pick one polygon weighted by polygon area.
            Poly poly = null;
            int polyRef = 0;
            int bse = m_nav.GetPolyRefBase(tile);

            float areaSum = 0.0f;
            for (int i = 0; i < tile.header.polyCount; ++i)
            {
                Poly p = tile.polys[i];
                // Do not return off-mesh connection polygons.
                if (p.Type != PolyTypes.DT_POLYTYPE_GROUND)
                {
                    continue;
                }
                // Must pass filter
                int r = bse | i;
                if (!filter.PassFilter(r, tile, p))
                {
                    continue;
                }

                // Calc area of the polygon.
                float polyArea = 0.0f;
                for (int j = 2; j < p.vertCount; ++j)
                {
                    var va = tile.verts[p.verts[0]];
                    var vb = tile.verts[p.verts[j - 1]];
                    var vc = tile.verts[p.verts[j]];
                    polyArea += Detour.TriArea2D(va, vb, vc);
                }

                // Choose random polygon weighted by area, using reservoi sampling.
                areaSum += polyArea;
                float u = frand.NextFloat(0, 1);
                if (u * areaSum <= polyArea)
                {
                    poly = p;
                    polyRef = r;
                }
            }

            if (poly == null)
            {
                return Status.DT_FAILURE;
            }

            // Randomly pick point on polygon.
            var v = tile.verts[poly.verts[0]];
            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON];
            verts[0] = v;
            for (int j = 1; j < poly.vertCount; ++j)
            {
                v = tile.verts[poly.verts[j]];
                verts[j] = v;
            }

            float s = frand.NextFloat(0, 1);
            float t = frand.NextFloat(0, 1);

            Detour.RandomPointInConvexPoly(verts, poly.vertCount, out float[] areas, s, t, out Vector3 pt);

            Status status = GetPolyHeight(polyRef, pt, out float h);
            if (status.HasFlag(Status.DT_FAILURE))
            {
                return status;
            }
            pt.Y = h;

            randomPt = pt;

            randomRef = polyRef;

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Returns random location on navmesh within the reach of specified location.
        /// </summary>
        /// <param name="startRef">The reference id of the polygon where the search starts.</param>
        /// <param name="centerPos">The center of the search circle.</param>
        /// <param name="maxRadius">The radius of the search circle.</param>
        /// <param name="filter">The polygon filter to apply to the query.</param>
        /// <param name="frand">Function returning a random number [0..1).</param>
        /// <param name="randomRef">The reference id of the random location.</param>
        /// <param name="randomPt">The random location.</param>
        /// <returns>The status flags for the query.</returns>
        /// <remarks>
        /// Polygons are chosen weighted by area. The search runs in linear related to number of polygon.
        /// The location is not exactly constrained by the circle, but it limits the visited polygons.
        /// </remarks>
        public Status FindRandomPointAroundCircle(int startRef, Vector3 centerPos, float maxRadius, QueryFilter filter, Random frand, out int randomRef, out Vector3 randomPt)
        {
            randomRef = 0;
            randomPt = new Vector3();

            // Validate input
            if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nav.GetTileAndPolyByRefUnsafe(startRef, out MeshTile startTile, out Poly startPoly);
            if (!filter.PassFilter(startRef, startTile, startPoly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            m_nodePool.Clear();
            m_openList.Clear();

            Node startNode = m_nodePool.GetNode(startRef, 0);
            startNode.pos = centerPos;
            startNode.pidx = 0;
            startNode.cost = 0;
            startNode.total = 0;
            startNode.id = startRef;
            startNode.flags = NodeFlags.DT_NODE_OPEN;
            m_openList.Push(startNode);

            Status status = Status.DT_SUCCESS;

            float radiusSqr = maxRadius * maxRadius;
            float areaSum = 0.0f;

            MeshTile randomTile = null;
            Poly randomPoly = null;
            int randomPolyRef = 0;

            while (!m_openList.Empty())
            {
                Node bestNode = m_openList.Pop();
                bestNode.flags &= ~NodeFlags.DT_NODE_OPEN;
                bestNode.flags |= NodeFlags.DT_NODE_CLOSED;

                // Get poly and tile.
                // The API input has been cheked already, skip checking internal data.
                int bestRef = bestNode.id;
                m_nav.GetTileAndPolyByRefUnsafe(bestRef, out MeshTile bestTile, out Poly bestPoly);

                // Place random locations on on ground.
                if (bestPoly.Type == PolyTypes.DT_POLYTYPE_GROUND)
                {
                    // Calc area of the polygon.
                    float polyArea = 0.0f;
                    for (int j = 2; j < bestPoly.vertCount; ++j)
                    {
                        var va = bestTile.verts[bestPoly.verts[0]];
                        var vb = bestTile.verts[bestPoly.verts[j - 1]];
                        var vc = bestTile.verts[bestPoly.verts[j]];
                        polyArea += Detour.TriArea2D(va, vb, vc);
                    }
                    // Choose random polygon weighted by area, using reservoi sampling.
                    areaSum += polyArea;
                    float u = frand.NextFloat(0, 1);
                    if (u * areaSum <= polyArea)
                    {
                        randomTile = bestTile;
                        randomPoly = bestPoly;
                        randomPolyRef = bestRef;
                    }
                }

                // Get parent poly and tile.
                int parentRef = 0;
                MeshTile parentTile = null;
                Poly parentPoly = null;
                if (bestNode.pidx != 0)
                {
                    parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
                }
                if (parentRef != 0)
                {
                    m_nav.GetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);
                }

                for (int i = bestPoly.firstLink; i != Detour.DT_NULL_LINK; i = bestTile.links[i].next)
                {
                    Link link = bestTile.links[i];
                    int neighbourRef = link.nref;
                    // Skip invalid neighbours and do not follow back to parent.
                    if (neighbourRef == 0 || neighbourRef == parentRef)
                    {
                        continue;
                    }

                    // Expand to neighbour
                    m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, out MeshTile neighbourTile, out Poly neighbourPoly);

                    // Do not advance if the polygon is excluded by the filter.
                    if (!filter.PassFilter(neighbourRef, neighbourTile, neighbourPoly))
                    {
                        continue;
                    }

                    // Find edge and calc distance to the edge.
                    if (GetPortalPoints(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, out Vector3 va, out Vector3 vb).HasFlag(Status.DT_FAILURE))
                    {
                        continue;
                    }

                    // If the circle is not touching the next polygon, skip it.
                    float distSqr = Detour.DistancePtSegSqr2D(centerPos, va, vb, out float tseg);
                    if (distSqr > radiusSqr)
                    {
                        continue;
                    }

                    var neighbourNode = m_nodePool.GetNode(neighbourRef, 0);
                    if (neighbourNode == null)
                    {
                        status |= Status.DT_OUT_OF_NODES;
                        continue;
                    }

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_CLOSED) != 0)
                    {
                        continue;
                    }

                    // Cost
                    if (neighbourNode.flags == NodeFlags.DT_NODE_NONE)
                    {
                        neighbourNode.pos = Vector3.Lerp(va, vb, 0.5f);
                    }

                    float total = bestNode.total + Vector3.Distance(bestNode.pos, neighbourNode.pos);

                    // The node is already in open list and the new result is worse, skip.
                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0 && total >= neighbourNode.total)
                    {
                        continue;
                    }

                    neighbourNode.id = neighbourRef;
                    neighbourNode.flags = (neighbourNode.flags & ~NodeFlags.DT_NODE_CLOSED);
                    neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
                    neighbourNode.total = total;

                    if ((neighbourNode.flags & NodeFlags.DT_NODE_OPEN) != 0)
                    {
                        m_openList.Modify(neighbourNode);
                    }
                    else
                    {
                        neighbourNode.flags = NodeFlags.DT_NODE_OPEN;
                        m_openList.Push(neighbourNode);
                    }
                }
            }

            if (randomPoly == null)
            {
                return Status.DT_FAILURE;
            }

            // Randomly pick point on polygon.
            var v = randomTile.verts[randomPoly.verts[0]];
            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON];
            verts[0] = v;
            for (int j = 1; j < randomPoly.vertCount; ++j)
            {
                v = randomTile.verts[randomPoly.verts[j]];
                verts[j] = v;
            }

            float s = frand.NextFloat(0, 1);
            float t = frand.NextFloat(0, 1);

            Detour.RandomPointInConvexPoly(verts, randomPoly.vertCount, out float[] areas, s, t, out Vector3 pt);

            Status stat = GetPolyHeight(randomPolyRef, pt, out float h);
            if (stat.HasFlag(Status.DT_FAILURE))
            {
                return stat;
            }
            pt.Y = h;

            randomPt = pt;
            randomRef = randomPolyRef;

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Finds the closest point on the specified polygon.
        /// </summary>
        /// <param name="r">The reference id of the polygon.</param>
        /// <param name="pos">The position to check.</param>
        /// <param name="closest">The closest point on the polygon.</param>
        /// <param name="posOverPoly">True of the position is over the polygon.</param>
        /// <returns>The status flags for the query.</returns>
        public Status ClosestPointOnPoly(int r, Vector3 pos, out Vector3 closest, out bool posOverPoly)
        {
            closest = Vector3.Zero;
            posOverPoly = false;

            if (!m_nav.GetTileAndPolyByRef(r, out MeshTile tile, out Poly poly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }
            if (tile == null)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            // Off-mesh connections don't have detail polygons.
            if (poly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
            {
                var v0 = tile.verts[poly.verts[0]];
                var v1 = tile.verts[poly.verts[1]];
                float d0 = Vector3.Distance(pos, v0);
                float d1 = Vector3.Distance(pos, v1);
                float u = d0 / (d0 + d1);

                closest = Vector3.Lerp(v0, v1, u);
                posOverPoly = false;
                return Status.DT_SUCCESS;
            }

            int ip = Array.IndexOf(tile.polys, poly);
            PolyDetail pd = tile.detailMeshes[ip];

            // Clamp point to be inside the polygon.
            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON];
            int nv = poly.vertCount;
            for (int i = 0; i < nv; ++i)
            {
                verts[i] = tile.verts[poly.verts[i]];
            }

            closest = pos;
            if (!Detour.DistancePtPolyEdgesSqr(pos, verts, nv, out float[] edged, out float[] edget))
            {
                // Point is outside the polygon, dtClamp to nearest edge.
                float dmin = edged[0];
                int imin = 0;
                for (int i = 1; i < nv; ++i)
                {
                    if (edged[i] < dmin)
                    {
                        dmin = edged[i];
                        imin = i;
                    }
                }
                var va = verts[imin];
                var vb = verts[((imin + 1) % nv)];
                closest = Vector3.Lerp(va, vb, edget[imin]);
                posOverPoly = false;
            }
            else
            {
                posOverPoly = true;
            }

            // Find height at the location.
            for (int j = 0; j < pd.triCount; ++j)
            {
                var t = tile.detailTris[(pd.triBase + j)];
                Vector3[] v = new Vector3[3];
                for (int k = 0; k < 3; ++k)
                {
                    if (t[k] < poly.vertCount)
                    {
                        v[k] = tile.verts[poly.verts[t[k]]];
                    }
                    else
                    {
                        v[k] = tile.detailVerts[(pd.vertBase + (t[k] - poly.vertCount))];
                    }
                }
                if (Detour.ClosestHeightPointTriangle(closest, v[0], v[1], v[2], out float h))
                {
                    closest.Y = h;
                    break;
                }
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Returns a point on the boundary closest to the source point if the source point is outside the polygon's xz-bounds.
        /// </summary>
        /// <param name="r">The reference id to the polygon.</param>
        /// <param name="pos">The position to check.</param>
        /// <param name="closest">The closest point.</param>
        /// <returns>The status flags for the query.</returns>
        public Status ClosestPointOnPolyBoundary(int r, Vector3 pos, out Vector3 closest)
        {
            closest = new Vector3();

            if (!m_nav.GetTileAndPolyByRef(r, out MeshTile tile, out Poly poly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            // Collect vertices.
            Vector3[] verts = new Vector3[Detour.DT_VERTS_PER_POLYGON];
            int nv = 0;
            for (int i = 0; i < poly.vertCount; ++i)
            {
                verts[nv] = tile.verts[poly.verts[i]];
                nv++;
            }

            bool inside = Detour.DistancePtPolyEdgesSqr(pos, verts, nv, out float[] edged, out float[] edget);
            if (inside)
            {
                // Point is inside the polygon, return the point.
                closest = pos;
            }
            else
            {
                // Point is outside the polygon, dtClamp to nearest edge.
                float dmin = edged[0];
                int imin = 0;
                for (int i = 1; i < nv; ++i)
                {
                    if (edged[i] < dmin)
                    {
                        dmin = edged[i];
                        imin = i;
                    }
                }
                var va = verts[imin];
                var vb = verts[((imin + 1) % nv)];
                closest = Vector3.Lerp(va, vb, edget[imin]);
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Gets the height of the polygon at the provided position using the height detail. (Most accurate.)
        /// </summary>
        /// <param name="r">The reference id of the polygon.</param>
        /// <param name="pos">A position within the xz-bounds of the polygon.</param>
        /// <param name="height">The height at the surface of the polygon.</param>
        /// <returns>The status flags for the query.</returns>
        public Status GetPolyHeight(int r, Vector3 pos, out float height)
        {
            height = 0;

            if (!m_nav.GetTileAndPolyByRef(r, out MeshTile tile, out Poly poly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            if (poly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
            {
                var v0 = tile.verts[poly.verts[0]];
                var v1 = tile.verts[poly.verts[1]];
                float d0 = Recast.VDist2(pos, v0);
                float d1 = Recast.VDist2(pos, v1);
                float u = d0 / (d0 + d1);
                height = v0.Y + (v1.Y - v0.Y) * u;
                return Status.DT_SUCCESS;
            }
            else
            {
                int ip = Array.IndexOf(tile.polys, poly);
                var pd = tile.detailMeshes[ip];
                for (int j = 0; j < pd.triCount; ++j)
                {
                    var t = tile.detailTris[(pd.triBase + j)];
                    Vector3[] v = new Vector3[3];
                    for (int k = 0; k < 3; ++k)
                    {
                        if (t[k] < poly.vertCount)
                        {
                            v[k] = tile.verts[poly.verts[t[k]]];
                        }
                        else
                        {
                            v[k] = tile.detailVerts[(pd.vertBase + (t[k] - poly.vertCount))];
                        }
                    }
                    if (Detour.ClosestHeightPointTriangle(pos, v[0], v[1], v[2], out float h))
                    {
                        height = h;
                        return Status.DT_SUCCESS;
                    }
                }
            }

            return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
        }
        /// <summary>
        /// Returns true if the polygon reference is valid and passes the filter restrictions.
        /// </summary>
        /// <param name="r">The polygon reference to check.</param>
        /// <param name="filter">The filter to apply.</param>
        /// <returns>Returns true if the polygon reference is valid and passes the filter restrictions.</returns>
        public bool IsValidPolyRef(int r, QueryFilter filter)
        {
            bool status = m_nav.GetTileAndPolyByRef(r, out MeshTile tile, out Poly poly);
            // If cannot get polygon, assume it does not exists and boundary is invalid.
            if (!status)
            {
                return false;
            }
            // If cannot pass filter, assume flags has changed and boundary is invalid.
            if (!filter.PassFilter(r, tile, poly))
            {
                return false;
            }
            return true;
        }
        /// <summary>
        ///  Returns true if the polygon reference is in the closed list. 
        /// </summary>
        /// <param name="r">The reference id of the polygon to check.</param>
        /// <returns>True if the polygon is in closed list.</returns>
        public bool IsInClosedList(int r)
        {
            if (m_nodePool == null) return false;

            int n = m_nodePool.FindNodes(r, out Node[] nodes, Detour.DT_MAX_STATES_PER_NODE);

            for (int i = 0; i < n; i++)
            {
                if ((nodes[i].flags & NodeFlags.DT_NODE_CLOSED) != 0)
                {
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Gets the node pool.
        /// </summary>
        /// <returns>The node pool.</returns>
        public NodePool GetNodePool() { return m_nodePool; }
        /// <summary>
        /// Gets the navigation mesh the query object is using.
        /// </summary>
        /// <returns>The navigation mesh the query object is using.</returns>
        public NavMesh GetAttachedNavMesh() { return m_nav; }

        /// <summary>
        ///  Queries polygons within a tile.
        /// </summary>
        private void QueryPolygonsInTile(MeshTile tile, Vector3 qmin, Vector3 qmax, QueryFilter filter, IPolyQuery query)
        {
            int batchSize = 32;
            int[] polyRefs = new int[batchSize];
            Poly[] polys = new Poly[batchSize];
            int n = 0;

            if (tile.bvTree != null)
            {
                int nodeIndex = 0;
                int endIndex = tile.header.bvNodeCount;
                var tbmin = tile.header.bmin;
                var tbmax = tile.header.bmax;
                float qfac = tile.header.bvQuantFactor;

                // Calculate quantized box
                // dtClamp query box to world box.
                float minx = MathUtil.Clamp(qmin.X, tbmin.X, tbmax.X) - tbmin.X;
                float miny = MathUtil.Clamp(qmin.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
                float minz = MathUtil.Clamp(qmin.Z, tbmin.Z, tbmax.Z) - tbmin.Z;
                float maxx = MathUtil.Clamp(qmax.X, tbmin.X, tbmax.X) - tbmin.X;
                float maxy = MathUtil.Clamp(qmax.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
                float maxz = MathUtil.Clamp(qmax.Z, tbmin.Z, tbmax.Z) - tbmin.Z;
                // Quantize
                Int3 bmin = new Int3();
                Int3 bmax = new Int3();
                bmin.X = (int)(qfac * minx) & 0xfffe;
                bmin.Y = (int)(qfac * miny) & 0xfffe;
                bmin.Z = (int)(qfac * minz) & 0xfffe;
                bmax.X = (int)(qfac * maxx + 1) | 1;
                bmax.Y = (int)(qfac * maxy + 1) | 1;
                bmax.Z = (int)(qfac * maxz + 1) | 1;

                // Traverse tree
                int bse = m_nav.GetPolyRefBase(tile);
                while (nodeIndex < endIndex)
                {
                    var node = nodeIndex < tile.bvTree.Length ? tile.bvTree[nodeIndex] : new BVNode();

                    bool overlap = Detour.OverlapQuantBounds(bmin, bmax, node.bmin, node.bmax);
                    bool isLeafNode = node.i >= 0;

                    if (isLeafNode && overlap)
                    {
                        int r = bse | node.i;
                        if (filter.PassFilter(r, tile, tile.polys[node.i]))
                        {
                            polyRefs[n] = r;
                            polys[n] = tile.polys[node.i];

                            if (n == batchSize - 1)
                            {
                                query.Process(tile, polys, polyRefs, batchSize);
                                n = 0;
                            }
                            else
                            {
                                n++;
                            }
                        }
                    }

                    if (overlap || isLeafNode)
                    {
                        nodeIndex++;
                    }
                    else
                    {
                        int escapeIndex = -node.i;
                        nodeIndex += escapeIndex;
                    }
                }
            }
            else
            {
                Vector3 bmin;
                Vector3 bmax;
                int bse = m_nav.GetPolyRefBase(tile);
                for (int i = 0; i < tile.header.polyCount; ++i)
                {
                    var p = tile.polys[i];
                    // Do not return off-mesh connection polygons.
                    if (p.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    {
                        continue;
                    }
                    // Must pass filter
                    int r = bse | i;
                    if (!filter.PassFilter(r, tile, p))
                    {
                        continue;
                    }
                    // Calc polygon bounds.
                    var v = tile.verts[p.verts[0]];
                    bmin = v;
                    bmax = v;
                    for (int j = 1; j < p.vertCount; ++j)
                    {
                        v = tile.verts[p.verts[j]];
                        bmin = Vector3.Min(bmin, v);
                        bmax = Vector3.Min(bmax, v);
                    }
                    if (Recast.OverlapBounds(qmin, qmax, bmin, bmax))
                    {
                        polyRefs[n] = r;
                        polys[n] = p;

                        if (n == batchSize - 1)
                        {
                            query.Process(tile, polys, polyRefs, batchSize);
                            n = 0;
                        }
                        else
                        {
                            n++;
                        }
                    }
                }
            }

            // Process the last polygons that didn't make a full batch.
            if (n > 0)
            {
                query.Process(tile, polys, polyRefs, n);
            }
        }
        /// <summary>
        /// Returns portal points between two polygons.
        /// </summary>
        private Status GetPortalPoints(int from, int to, out Vector3 left, out Vector3 right, out PolyTypes fromType, out PolyTypes toType)
        {
            left = new Vector3();
            right = new Vector3();
            fromType = PolyTypes.DT_POLYTYPE_GROUND;
            toType = PolyTypes.DT_POLYTYPE_GROUND;

            if (!m_nav.GetTileAndPolyByRef(from, out MeshTile fromTile, out Poly fromPoly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            fromType = fromPoly.Type;

            if (!m_nav.GetTileAndPolyByRef(to, out MeshTile toTile, out Poly toPoly))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            toType = toPoly.Type;

            return GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, out left, out right);
        }
        /// <summary>
        /// Returns portal points between two polygons.
        /// </summary>
        private Status GetPortalPoints(int from, Poly fromPoly, MeshTile fromTile, int to, Poly toPoly, MeshTile toTile, out Vector3 left, out Vector3 right)
        {
            left = new Vector3();
            right = new Vector3();

            // Find the link that points to the 'to' polygon.
            Link? link = null;
            for (int i = fromPoly.firstLink; i != Detour.DT_NULL_LINK; i = fromTile.links[i].next)
            {
                if (fromTile.links[i].nref == to)
                {
                    link = fromTile.links[i];
                    break;
                }
            }
            if (!link.HasValue)
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            // Handle off-mesh connections.
            if (fromPoly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
            {
                // Find link that points to first vertex.
                for (int i = fromPoly.firstLink; i != Detour.DT_NULL_LINK; i = fromTile.links[i].next)
                {
                    if (fromTile.links[i].nref == to)
                    {
                        int v = fromTile.links[i].edge;
                        left = fromTile.verts[fromPoly.verts[v]];
                        right = fromTile.verts[fromPoly.verts[v]];
                        return Status.DT_SUCCESS;
                    }
                }
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            if (toPoly.Type == PolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
            {
                for (int i = toPoly.firstLink; i != Detour.DT_NULL_LINK; i = toTile.links[i].next)
                {
                    if (toTile.links[i].nref == from)
                    {
                        int v = toTile.links[i].edge;
                        left = toTile.verts[toPoly.verts[v]];
                        right = toTile.verts[toPoly.verts[v]];
                        return Status.DT_SUCCESS;
                    }
                }
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            // Find portal vertices.
            int v0 = fromPoly.verts[link.Value.edge];
            int v1 = fromPoly.verts[(link.Value.edge + 1) % (int)fromPoly.vertCount];
            left = fromTile.verts[v0];
            right = fromTile.verts[v1];

            // If the link is at tile boundary, dtClamp the vertices to
            // the link width.
            if (link.Value.side != 0xff)
            {
                // Unpack portal limits.
                if (link.Value.bmin != 0 || link.Value.bmax != 255)
                {
                    float s = 1.0f / 255.0f;
                    float tmin = link.Value.bmin * s;
                    float tmax = link.Value.bmax * s;
                    left = Vector3.Lerp(fromTile.verts[v0], fromTile.verts[v1], tmin);
                    right = Vector3.Lerp(fromTile.verts[v0], fromTile.verts[v1], tmax);
                }
            }

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Returns edge mid point between two polygons.
        /// </summary>
        private Status GetEdgeMidPoint(int from, int to, out Vector3 mid)
        {
            mid = new Vector3();

            if (GetPortalPoints(from, to, out Vector3 left, out Vector3 right, out PolyTypes fromType, out PolyTypes toType).HasFlag(Status.DT_FAILURE))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            mid = (left + right) * 0.5f;

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Returns edge mid point between two polygons.
        /// </summary>
        private Status GetEdgeMidPoint(int from, Poly fromPoly, MeshTile fromTile, int to, Poly toPoly, MeshTile toTile, out Vector3 mid)
        {
            mid = new Vector3();

            if (GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, out Vector3 left, out Vector3 right).HasFlag(Status.DT_FAILURE))
            {
                return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
            }

            mid = (left + right) * 0.5f;

            return Status.DT_SUCCESS;
        }
        /// <summary>
        /// Appends vertex to a straight path
        /// </summary>
        private Status AppendVertex(Vector3 pos, StraightPathFlags flags, int r, ref Vector3[] straightPath, ref StraightPathFlags[] straightPathFlags, ref int[] straightPathRefs, ref int straightPathCount, int maxStraightPath)
        {
            if ((straightPathCount) > 0 && Detour.Vequal(straightPath[((straightPathCount) - 1)], pos))
            {
                // The vertices are equal, update flags and poly.
                if (straightPathFlags != null)
                {
                    straightPathFlags[(straightPathCount) - 1] = flags;
                }
                if (straightPathRefs != null)
                {
                    straightPathRefs[(straightPathCount) - 1] = r;
                }
            }
            else
            {
                // Append new vertex.
                straightPath[(straightPathCount)] = pos;
                if (straightPathFlags != null)
                {
                    straightPathFlags[(straightPathCount)] = flags;
                }
                if (straightPathRefs != null)
                {
                    straightPathRefs[(straightPathCount)] = r;
                }
                straightPathCount++;

                // If there is no space to append more vertices, return.
                if (straightPathCount >= maxStraightPath)
                {
                    return Status.DT_SUCCESS | Status.DT_BUFFER_TOO_SMALL;
                }

                // If reached end of path, return.
                if (flags == StraightPathFlags.DT_STRAIGHTPATH_END)
                {
                    return Status.DT_SUCCESS;
                }
            }
            return Status.DT_IN_PROGRESS;
        }
        /// <summary>
        /// Appends intermediate portal points to a straight path.
        /// </summary>
        private Status AppendPortals(int startIdx, int endIdx, Vector3 endPos, int[] path, ref Vector3[] straightPath, ref StraightPathFlags[] straightPathFlags, ref int[] straightPathRefs, ref int straightPathCount, int maxStraightPath, StraightPathOptions options)
        {
            Vector3 startPos = straightPath[straightPathCount - 1];
            // Append or update last vertex
            Status stat = 0;
            for (int i = startIdx; i < endIdx; i++)
            {
                // Calculate portal
                int from = path[i];
                if (!m_nav.GetTileAndPolyByRef(from, out MeshTile fromTile, out Poly fromPoly))
                {
                    return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
                }

                int to = path[i + 1];
                if (!m_nav.GetTileAndPolyByRef(to, out MeshTile toTile, out Poly toPoly))
                {
                    return Status.DT_FAILURE | Status.DT_INVALID_PARAM;
                }

                if (GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, out Vector3 left, out Vector3 right).HasFlag(Status.DT_FAILURE))
                {
                    break;
                }

                if (options.HasFlag(StraightPathOptions.DT_STRAIGHTPATH_AREA_CROSSINGS))
                {
                    // Skip intersection if only area crossings are requested.
                    if (fromPoly.Area == toPoly.Area)
                    {
                        continue;
                    }
                }

                // Append intersection
                if (Detour.IntersectSegSeg2D(startPos, endPos, left, right, out float s, out float t))
                {
                    Vector3 pt = Vector3.Lerp(left, right, t);

                    stat = AppendVertex(
                        pt, 0, path[i + 1],
                        ref straightPath, ref straightPathFlags, ref straightPathRefs,
                        ref straightPathCount, maxStraightPath);
                    if (stat != Status.DT_IN_PROGRESS)
                    {
                        return stat;
                    }
                }
            }
            return Status.DT_IN_PROGRESS;
        }
        /// <summary>
        /// Gets the path leading to the specified end node.
        /// </summary>
        private Status GetPathToNode(Node endNode, out int[] path, out int pathCount, int maxPath)
        {
            path = new int[maxPath];

            // Find the length of the entire path.
            Node curNode = endNode;
            int length = 0;
            do
            {
                length++;
                curNode = m_nodePool.GetNodeAtIdx(curNode.pidx);
            } while (curNode != null);

            // If the path cannot be fully stored then advance to the last node we will be able to store.
            curNode = endNode;
            int writeCount;
            for (writeCount = length; writeCount > maxPath; writeCount--)
            {
                curNode = m_nodePool.GetNodeAtIdx(curNode.pidx);
            }

            // Write path
            for (int i = writeCount - 1; i >= 0; i--)
            {
                path[i] = curNode.id;
                curNode = m_nodePool.GetNodeAtIdx(curNode.pidx);
            }

            pathCount = Math.Min(length, maxPath);

            if (length > maxPath)
            {
                return Status.DT_SUCCESS | Status.DT_BUFFER_TOO_SMALL;
            }

            return Status.DT_SUCCESS;
        }
    }
}