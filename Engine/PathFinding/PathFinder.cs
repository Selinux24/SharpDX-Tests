﻿using SharpDX;
using System;
using System.Collections.Generic;

namespace Engine.PathFinding
{
    using Engine.Collections;

    /// <summary>
    /// Path finder class
    /// </summary>
    public static class PathFinder
    {
        /// <summary>
        /// Constant for second diagonal distance method
        /// </summary>
        private static readonly float ChebisevCnt = (float)Math.Sqrt(2) - 2f;

        /// <summary>
        /// Cached paths
        /// </summary>
        private static List<PathCache> Cache = new List<PathCache>(10);

        /// <summary>
        /// Gets the path from start to end
        /// </summary>
        /// <param name="grid">Grid</param>
        /// <param name="startPosition">Start point</param>
        /// <param name="endPosition">End point</param>
        /// <param name="heuristicMethod">Heuristic metod (Diagonal distance 2 by default)</param>
        /// <param name="heuristicEstimateValue">Heuristic estimate value (8 by default)</param>
        /// <returns>Returns the path from start to end</returns>
        public static PathFinderPath FindPath(IGraph grid, Vector3 startPosition, Vector3 endPosition, HeuristicMethods heuristicMethod = HeuristicMethods.DiagonalDistance2, int heuristicEstimateValue = 8)
        {
            IGraphNode start = grid.FindNode(startPosition);
            IGraphNode end = grid.FindNode(endPosition);
            if (start != null && end != null)
            {
                PathCache cachedPath = Cache.Find(p => p.Start == start && p.End == end);
                if (cachedPath != null)
                {
                    //Return path
                    return new PathFinderPath(startPosition, endPosition, cachedPath.Path.ReturnPath.ToArray());
                }
                else
                {
                    //Calculate return path
                    IGraphNode[] solvedList = CalcReturnPath(start, end, heuristicMethod, heuristicEstimateValue);
                    if (solvedList != null && solvedList.Length > 0)
                    {
                        //Generate path
                        var path = new PathFinderPath(startPosition, endPosition, solvedList);

                        //Update queue
                        if (Cache.Count >= 10) Cache.RemoveAt(0);

                        //Add path to caché
                        Cache.Add(new PathCache()
                        {
                            Path = path,
                            Start = start,
                            End = end,
                        });

                        return path;
                    }
                }
            }

            return null;
        }
        /// <summary>
        /// Gets the path from start to end
        /// </summary>
        /// <param name="start">Start node</param>
        /// <param name="end">End node</param>
        /// <param name="heuristicMethod">Heuristic metod</param>
        /// <param name="heuristicEstimateValue">Heuristic estimate value</param>
        /// <returns>Returns the path from start to end</returns>
        private static IGraphNode[] CalcReturnPath(IGraphNode start, IGraphNode end, HeuristicMethods heuristicMethod, int heuristicEstimateValue)
        {
            //New queue
            PriorityQueue<IGraphNode, float> openPathsQueue = new PriorityQueue<IGraphNode, float>();
            //Data dictionary
            Dictionary<IGraphNode, PathFinderData> nodesData = new Dictionary<IGraphNode, PathFinderData>();

            //Add first node
            openPathsQueue.Enqueue(start, 1);
            nodesData.Add(start, new PathFinderData());

            bool nodeFound = false;
            while (openPathsQueue.Count > 0)
            {
                //Dequeue the node with lower priority
                PriorityQueueItem<IGraphNode, float> item = openPathsQueue.Dequeue();

                IGraphNode currentNode = item.Value;
                PathFinderData currentNodeData = nodesData[currentNode];

                //If the node is not closed to continue the process
                if (currentNodeData.State != GraphNodeStates.Closed)
                {
                    //Set the node status Closed
                    currentNodeData.State = GraphNodeStates.Closed;

                    //If the current node is the destination node has found the way
                    if (currentNode == end)
                    {
                        currentNodeData.State = GraphNodeStates.Closed;
                        nodeFound = true;

                        break;
                    }
                    else
                    {
                        //Search every possible direction from the current node
                        for (int i = 1; i < currentNode.Connections.Length; i++)
                        {
                            IGraphNode nextNode = currentNode[i];
                            if (nextNode != null)
                            {
                                if (!nodesData.ContainsKey(nextNode))
                                {
                                    nodesData.Add(nextNode, new PathFinderData());
                                }

                                PathFinderData nextNodeData = nodesData[nextNode];

                                if (nextNode.State == GraphNodeStates.Closed)
                                {
                                    //Impassable node
                                    continue;
                                }

                                if (nextNodeData.State == GraphNodeStates.Closed)
                                {
                                    //Closed node
                                    continue;
                                }

                                float newGone = currentNode.Cost + ((int)nextNodeData.State);

                                if (nextNodeData.State == GraphNodeStates.Clear)
                                {
                                    if (nextNode.Cost < newGone)
                                    {
                                        continue;
                                    }
                                }

                                nextNodeData.NextNode = currentNode;
                                nextNodeData.Cost = newGone;
                                nextNodeData.State = GraphNodeStates.Clear;

                                //Calculate priority from next to end
                                float heuristicValue = CalcHeuristic(
                                    nextNode.Center,
                                    end.Center,
                                    heuristicMethod);

                                openPathsQueue.Enqueue(nextNode, newGone + (heuristicEstimateValue * heuristicValue));
                            }
                        }
                    }
                }
            }

            if (nodeFound)
            {
                //We found a valid path
                List<IGraphNode> solvedList = new List<IGraphNode>();

                IGraphNode node = end;
                while (node != null)
                {
                    solvedList.Insert(0, node);

                    node = nodesData[node].NextNode;
                }

                return solvedList.ToArray();
            }
            else
            {
                //If no result...
                return null;
            }
        }
        /// <summary>
        /// Calculate the heuristic value as the start and end positions
        /// </summary>
        /// <param name="start">Start position</param>
        /// <param name="end">End position</param>
        /// <param name="heuristicMethod">Calculation method</param>
        /// <returns>Returns the heuristic value according to the start and end positions</returns>
        private static float CalcHeuristic(Vector3 start, Vector3 end, HeuristicMethods heuristicMethod)
        {
            if (heuristicMethod == HeuristicMethods.Euclidean)
            {
                float dx = (end.X - start.X);
                float dz = (end.Z - start.Z);
                float h = (float)Math.Sqrt(dx * dx + dz * dz);

                return h;
            }
            else if (heuristicMethod == HeuristicMethods.Manhattan)
            {
                float dx = Math.Abs(start.X - end.X);
                float dz = Math.Abs(start.Z - end.Z);
                float h = dx + dz;

                return h;
            }
            else if (heuristicMethod == HeuristicMethods.DiagonalDistance1)
            {
                float dx = Math.Abs(start.X - end.X);
                float dz = Math.Abs(start.Z - end.Z);
                float h = Math.Max(dx, dz);

                return h;
            }
            else if (heuristicMethod == HeuristicMethods.DiagonalDistance2)
            {
                float dx = Math.Abs(start.X - end.X);
                float dz = Math.Abs(start.Z - end.Z);
                float h = (dx + dz) + ChebisevCnt * Math.Min(dx, dz);

                return h;
            }
            else if (heuristicMethod == HeuristicMethods.HexDistance)
            {
                float dx = start.X - end.X;
                float dy = start.Y - end.Y;
                float dz = dx - dy;
                float h = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));

                return h;
            }
            else
            {
                throw new ArgumentException(string.Format("Calculation method {0} not valid.", heuristicMethod));
            }
        }

        /// <summary>
        /// Cached path
        /// </summary>
        class PathCache
        {
            /// <summary>
            /// Start node
            /// </summary>
            public IGraphNode Start;
            /// <summary>
            /// End node
            /// </summary>
            public IGraphNode End;
            /// <summary>
            /// Path
            /// </summary>
            public PathFinderPath Path;
        }
    }
}
