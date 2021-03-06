﻿using SharpDX;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Collections
{
    /// <summary>
    /// Quadtree node
    /// </summary>
    public class QuadTreeNode
    {
        /// <summary>
        /// Recursive partition creation
        /// </summary>
        /// <param name="quadTree">Quadtree</param>
        /// <param name="parent">Parent node</param>
        /// <param name="bbox">Parent bounding box</param>
        /// <param name="items">All items</param>
        /// <param name="maxDepth">Maximum depth</param>
        /// <param name="treeDepth">Current depth</param>
        /// <param name="nodeCount">Node count</param>
        /// <returns>Returns new node</returns>
        public static QuadTreeNode CreatePartitions(
            QuadTree quadTree, QuadTreeNode parent,
            BoundingBox bbox,
            int maxDepth,
            int treeDepth,
            ref int nodeCount)
        {
            if (treeDepth <= maxDepth)
            {
                var node = new QuadTreeNode(quadTree, parent)
                {
                    Id = -1,
                    Level = treeDepth,
                    BoundingBox = bbox,
                };

                bool haltByDepth = treeDepth == maxDepth;
                if (haltByDepth)
                {
                    node.Id = nodeCount++;
                }
                else
                {
                    // Initialize node partitions
                    IntializeNode(quadTree, node, bbox, maxDepth, treeDepth + 1, ref nodeCount);
                }

                return node;
            }

            return null;
        }
        /// <summary>
        /// Initializes node partitinos
        /// </summary>
        /// <param name="quadTree">Quadtree</param>
        /// <param name="node">Current node</param>
        /// <param name="bbox">Bounding box</param>
        /// <param name="items">Items into the node</param>
        /// <param name="maxDepth">Maximum depth</param>
        /// <param name="nextTreeDepth">Next depth</param>
        /// <param name="nodeCount">Node count</param>
        private static void IntializeNode(
            QuadTree quadTree, QuadTreeNode node,
            BoundingBox bbox,
            int maxDepth,
            int nextTreeDepth,
            ref int nodeCount)
        {
            Vector3 M = bbox.Maximum;
            Vector3 c = (bbox.Maximum + bbox.Minimum) * 0.5f;
            Vector3 m = bbox.Minimum;

            //-1-1-1   +0+1+0   -->   mmm    cMc
            BoundingBox topLeftBox = new BoundingBox(new Vector3(m.X, m.Y, m.Z), new Vector3(c.X, M.Y, c.Z));
            //-1-1+0   +0+1+1   -->   mmc    cMM
            BoundingBox topRightBox = new BoundingBox(new Vector3(m.X, m.Y, c.Z), new Vector3(c.X, M.Y, M.Z));
            //+0-1-1   +1+1+0   -->   cmm    MMc
            BoundingBox bottomLeftBox = new BoundingBox(new Vector3(c.X, m.Y, m.Z), new Vector3(M.X, M.Y, c.Z));
            //+0-1+0   +1+1+1   -->   cmc    MMM
            BoundingBox bottomRightBox = new BoundingBox(new Vector3(c.X, m.Y, c.Z), new Vector3(M.X, M.Y, M.Z));

            var topLeftChild = CreatePartitions(quadTree, node, topLeftBox, maxDepth, nextTreeDepth, ref nodeCount);
            var topRightChild = CreatePartitions(quadTree, node, topRightBox, maxDepth, nextTreeDepth, ref nodeCount);
            var bottomLeftChild = CreatePartitions(quadTree, node, bottomLeftBox, maxDepth, nextTreeDepth, ref nodeCount);
            var bottomRightChild = CreatePartitions(quadTree, node, bottomRightBox, maxDepth, nextTreeDepth, ref nodeCount);

            List<QuadTreeNode> childList = new List<QuadTreeNode>();

            if (topLeftChild != null) childList.Add(topLeftChild);
            if (topRightChild != null) childList.Add(topRightChild);
            if (bottomLeftChild != null) childList.Add(bottomLeftChild);
            if (bottomRightChild != null) childList.Add(bottomRightChild);

            if (childList.Count > 0)
            {
                node.Children.AddRange(childList);
                node.TopLeftChild = topLeftChild;
                node.TopRightChild = topRightChild;
                node.BottomLeftChild = bottomLeftChild;
                node.BottomRightChild = bottomRightChild;
            }
        }

        /// <summary>
        /// Parent
        /// </summary>
        public QuadTree QuadTree { get; private set; }
        /// <summary>
        /// Parent node
        /// </summary>
        public QuadTreeNode Parent { get; private set; }
        /// <summary>
        /// Gets the child node al top lef position (from above)
        /// </summary>
        public QuadTreeNode TopLeftChild { get; private set; }
        /// <summary>
        /// Gets the child node al top right position (from above)
        /// </summary>
        public QuadTreeNode TopRightChild { get; private set; }
        /// <summary>
        /// Gets the child node al bottom lef position (from above)
        /// </summary>
        public QuadTreeNode BottomLeftChild { get; private set; }
        /// <summary>
        /// Gets the child node al bottom right position (from above)
        /// </summary>
        public QuadTreeNode BottomRightChild { get; private set; }

        /// <summary>
        /// Gets the neighbor at top position (from above)
        /// </summary>
        public QuadTreeNode TopNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at bottom position (from above)
        /// </summary>
        public QuadTreeNode BottomNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at left position (from above)
        /// </summary>
        public QuadTreeNode LeftNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at right position (from above)
        /// </summary>
        public QuadTreeNode RightNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at top left position (from above)
        /// </summary>
        public QuadTreeNode TopLeftNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at top right position (from above)
        /// </summary>
        public QuadTreeNode TopRightNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at bottom left position (from above)
        /// </summary>
        public QuadTreeNode BottomLeftNeighbor { get; private set; }
        /// <summary>
        /// Gets the neighbor at bottom right position (from above)
        /// </summary>
        public QuadTreeNode BottomRightNeighbor { get; private set; }

        /// <summary>
        /// Node Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Depth level
        /// </summary>
        public int Level { get; set; }
        /// <summary>
        /// Bounding box
        /// </summary>
        public BoundingBox BoundingBox { get; set; }
        /// <summary>
        /// Gets the node center position
        /// </summary>
        public Vector3 Center
        {
            get
            {
                return this.BoundingBox.GetCenter();
            }
        }
        /// <summary>
        /// Children list
        /// </summary>
        public List<QuadTreeNode> Children { get; private set; } = new List<QuadTreeNode>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="quadTree">Quadtree</param>
        /// <param name="parent">Parent node</param>
        public QuadTreeNode(QuadTree quadTree, QuadTreeNode parent)
        {
            this.QuadTree = quadTree;
            this.Parent = parent;
        }
        /// <summary>
        /// Connect nodes in the grid
        /// </summary>
        public void ConnectNodes()
        {
            this.TopNeighbor = this.FindNeighborNodeAtTop();
            this.BottomNeighbor = this.FindNeighborNodeAtBottom();
            this.LeftNeighbor = this.FindNeighborNodeAtLeft();
            this.RightNeighbor = this.FindNeighborNodeAtRight();

            this.TopLeftNeighbor = this.TopNeighbor?.FindNeighborNodeAtLeft();
            this.TopRightNeighbor = this.TopNeighbor?.FindNeighborNodeAtRight();
            this.BottomLeftNeighbor = this.BottomNeighbor?.FindNeighborNodeAtLeft();
            this.BottomRightNeighbor = this.BottomNeighbor?.FindNeighborNodeAtRight();

            if (this.Children?.Count > 0)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    this.Children[i].ConnectNodes();
                }
            }
        }
        /// <summary>
        /// Searchs for the neighbor node at top position (from above)
        /// </summary>
        /// <returns>Returns the neighbor node at top position if exists.</returns>
        private QuadTreeNode FindNeighborNodeAtTop()
        {
            if (this.Parent != null)
            {
                if (this == this.Parent.TopLeftChild)
                {
                    var node = this.Parent.FindNeighborNodeAtTop();
                    if (node != null)
                    {
                        return node.BottomLeftChild;
                    }
                }
                else if (this == this.Parent.TopRightChild)
                {
                    var node = this.Parent.FindNeighborNodeAtTop();
                    if (node != null)
                    {
                        return node.BottomRightChild;
                    }
                }
                else if (this == this.Parent.BottomLeftChild)
                {
                    return this.Parent.TopLeftChild;
                }
                else if (this == this.Parent.BottomRightChild)
                {
                    return this.Parent.TopRightChild;
                }
            }

            return null;
        }
        /// <summary>
        /// Searchs for the neighbor node at bottom position (from above)
        /// </summary>
        /// <returns>Returns the neighbor node at bottom position if exists.</returns>
        private QuadTreeNode FindNeighborNodeAtBottom()
        {
            if (this.Parent != null)
            {
                if (this == this.Parent.TopLeftChild)
                {
                    return this.Parent.BottomLeftChild;
                }
                else if (this == this.Parent.TopRightChild)
                {
                    return this.Parent.BottomRightChild;
                }
                else if (this == this.Parent.BottomLeftChild)
                {
                    var node = this.Parent.FindNeighborNodeAtBottom();
                    if (node != null)
                    {
                        return node.TopLeftChild;
                    }
                }
                else if (this == this.Parent.BottomRightChild)
                {
                    var node = this.Parent.FindNeighborNodeAtBottom();
                    if (node != null)
                    {
                        return node.TopRightChild;
                    }
                }
            }

            return null;
        }
        /// <summary>
        /// Searchs for the neighbor node at right position(from above)
        /// </summary>
        /// <returns>Returns the neighbor node at top position if exists.</returns>
        private QuadTreeNode FindNeighborNodeAtRight()
        {
            if (this.Parent != null)
            {
                if (this == this.Parent.TopLeftChild)
                {
                    return this.Parent.TopRightChild;
                }
                else if (this == this.Parent.TopRightChild)
                {
                    var node = this.Parent.FindNeighborNodeAtRight();
                    if (node != null)
                    {
                        return node.TopLeftChild;
                    }
                }
                else if (this == this.Parent.BottomLeftChild)
                {
                    return this.Parent.BottomRightChild;
                }
                else if (this == this.Parent.BottomRightChild)
                {
                    var node = this.Parent.FindNeighborNodeAtRight();
                    if (node != null)
                    {
                        return node.BottomLeftChild;
                    }
                }
            }

            return null;
        }
        /// <summary>
        /// Searchs for the neighbor node at left position (from above)
        /// </summary>
        /// <returns>Returns the neighbor node at left position if exists.</returns>
        private QuadTreeNode FindNeighborNodeAtLeft()
        {
            if (this.Parent != null)
            {
                if (this == this.Parent.TopLeftChild)
                {
                    var node = this.Parent.FindNeighborNodeAtLeft();
                    if (node != null)
                    {
                        return node.TopRightChild;
                    }
                }
                else if (this == this.Parent.TopRightChild)
                {
                    return this.Parent.TopLeftChild;
                }
                else if (this == this.Parent.BottomLeftChild)
                {
                    var node = this.Parent.FindNeighborNodeAtLeft();
                    if (node != null)
                    {
                        return node.BottomRightChild;
                    }
                }
                else if (this == this.Parent.BottomRightChild)
                {
                    return this.Parent.BottomLeftChild;
                }
            }

            return null;
        }

        /// <summary>
        /// Get bounding boxes of specified level
        /// </summary>
        /// <param name="maxDepth">Maximum depth (if zero there is no limit)</param>
        /// <returns>Returns bounding boxes of specified depth</returns>
        public IEnumerable<BoundingBox> GetBoundingBoxes(int maxDepth = 0)
        {
            List<BoundingBox> bboxes = new List<BoundingBox>();

            if (this.Children?.Any() == true)
            {
                if (maxDepth > 0 && this.Level == maxDepth)
                {
                    this.Children.ForEach((c) =>
                    {
                        bboxes.Add(c.BoundingBox);
                    });
                }
                else
                {
                    this.Children.ForEach((c) =>
                    {
                        bboxes.AddRange(c.GetBoundingBoxes(maxDepth));
                    });
                }
            }
            else
            {
                bboxes.Add(this.BoundingBox);
            }

            return bboxes.ToArray();
        }
        /// <summary>
        /// Gets maximum level value
        /// </summary>
        /// <returns></returns>
        public int GetMaxLevel()
        {
            int level = 0;

            if (this.Children?.Any() == true)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    int cLevel = this.Children[i].GetMaxLevel();

                    if (cLevel > level) level = cLevel;
                }
            }
            else
            {
                level = this.Level;
            }

            return level;
        }

        /// <summary>
        /// Gets the leaf nodes contained into the specified frustum
        /// </summary>
        /// <param name="frustum">Bounding frustum</param>
        /// <returns>Returns the leaf nodes contained into the frustum</returns>
        public IEnumerable<QuadTreeNode> GetNodesInVolume(ref BoundingFrustum frustum)
        {
            List<QuadTreeNode> nodes = new List<QuadTreeNode>();

            if (this.Children?.Any() == true)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    var childNodes = this.Children[i].GetNodesInVolume(ref frustum);
                    if (childNodes.Any())
                    {
                        nodes.AddRange(childNodes);
                    }
                }
            }
            else
            {
                if (frustum.Contains(this.BoundingBox) != ContainmentType.Disjoint)
                {
                    nodes.Add(this);
                }
            }

            return nodes.ToArray();
        }
        /// <summary>
        /// Gets the leaf nodes contained into the specified bounding box
        /// </summary>
        /// <param name="bbox">Bounding box</param>
        /// <returns>Returns the leaf nodes contained into the bounding box</returns>
        public IEnumerable<QuadTreeNode> GetNodesInVolume(ref BoundingBox bbox)
        {
            List<QuadTreeNode> nodes = new List<QuadTreeNode>();

            if (this.Children?.Any() == true)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    var childNodes = this.Children[i].GetNodesInVolume(ref bbox);
                    if (childNodes.Any())
                    {
                        nodes.AddRange(childNodes);
                    }
                }
            }
            else
            {
                if (bbox.Contains(this.BoundingBox) != ContainmentType.Disjoint)
                {
                    nodes.Add(this);
                }
            }

            return nodes.ToArray();
        }
        /// <summary>
        /// Gets the leaf nodes contained into the specified bounding sphere
        /// </summary>
        /// <param name="sphere">Bounding sphere</param>
        /// <returns>Returns the leaf nodes contained into the bounding sphere</returns>
        public IEnumerable<QuadTreeNode> GetNodesInVolume(ref BoundingSphere sphere)
        {
            List<QuadTreeNode> nodes = new List<QuadTreeNode>();

            if (this.Children?.Any() == true)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    var childNodes = this.Children[i].GetNodesInVolume(ref sphere);
                    if (childNodes.Any())
                    {
                        nodes.AddRange(childNodes);
                    }
                }
            }
            else
            {
                var bbox = this.BoundingBox;
                if (sphere.Contains(ref bbox) != ContainmentType.Disjoint)
                {
                    nodes.Add(this);
                }
            }

            return nodes.ToArray();
        }
        /// <summary>
        /// Gets all leaf nodes
        /// </summary>
        /// <returns>Returns all leaf nodes</returns>
        public IEnumerable<QuadTreeNode> GetLeafNodes()
        {
            List<QuadTreeNode> nodes = new List<QuadTreeNode>();

            if (this.Children?.Any() == true)
            {
                var leafNodes = this.Children.SelectMany(c => c.GetLeafNodes());
                nodes.AddRange(leafNodes);
            }
            else
            {
                nodes.Add(this);
            }

            return nodes.ToArray();
        }
        /// <summary>
        /// Gets node at position
        /// </summary>
        /// <param name="position">Position</param>
        /// <returns>Returns the leaf node wich contains the specified position</returns>
        public QuadTreeNode GetNode(Vector3 position)
        {
            if (this.Children == null)
            {
                if (this.BoundingBox.Contains(position) != ContainmentType.Disjoint)
                {
                    return this;
                }
            }
            else
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    var childNode = this.Children[i].GetNode(position);
                    if (childNode != null)
                    {
                        return childNode;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the text representation of the instance
        /// </summary>
        /// <returns>Returns the text representation of the instance</returns>
        public override string ToString()
        {
            if (this.Children == null)
            {
                //Leaf node
                return string.Format("QuadTreeNode {0}; Depth {1}", this.Id, this.Level);
            }
            else
            {
                //Node
                return string.Format("QuadTreeNode {0}; Depth {1}; Childs {2}", this.Id, this.Level, this.Children.Count);
            }
        }
    }
}
