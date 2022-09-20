using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Csg
{
    public class Tree
    {
        PolygonTreeNode PolygonTree;
        Node _RootNode;

        public Node RootNode => _RootNode;

        public Tree(List<Polygon> polygons)
        {
            PolygonTree = new PolygonTreeNode();
            _RootNode = new Node(null);
            if (polygons != null)
                AddPolygons(polygons);
        }

        public void Invert()
        {
            PolygonTree.Invert();
            _RootNode.Invert();
        }

        public void ClipTo(Tree tree, bool alsoRemoveCoplanarFront = false)
        {
            _RootNode.ClipTo(tree, alsoRemoveCoplanarFront);
        }

        public List<Polygon> AllPolygons()
        {
            return PolygonTree.GetPolygons();
        }

        public void AddPolygons(List<Polygon> polygons)
        {
            int n = polygons.Count;

            PolygonTreeNode[] ArrayAddedChildren = PolygonTree.AddChildren(polygons.ToArray());

            _RootNode.AddPolygonTreeNodes(ArrayAddedChildren);
        }
    }

    public class Node
    {
        private Plane _ActivePlane;
        public Node Front;
        public Node Back;
        public List<PolygonTreeNode> PolygonTreeNodes = new List<PolygonTreeNode>();
        public readonly Node Parent;
        private static readonly Plane EmptyPlane = new Plane();
        private bool HasActivePlane;

        public Node(Node parent)
        {
            Parent = parent;
        }

        public Plane ActivePlane
        {
            get => _ActivePlane;
            set
            {
                HasActivePlane = true;
                _ActivePlane = value;
            }
        }

        public void Invert()
        {
            Queue<Node> ActiveQueue = new Queue<Node>();
            Node ActiveNode = this;

            while (true)
            {
                if (ActiveNode.HasActivePlane)
                {
                    ActiveNode.ActivePlane = ActiveNode.ActivePlane.Flipped();
                }

                if (ActiveNode.Front != null)
                {
                    ActiveQueue.Enqueue(ActiveNode.Front);
                }

                if (ActiveNode.Back != null)
                {
                    ActiveQueue.Enqueue(ActiveNode.Back);
                }

                Node TempNode = ActiveNode.Front;
                ActiveNode.Front = ActiveNode.Back;
                ActiveNode.Back = TempNode;

                if (ActiveQueue.Count > 0)
                    ActiveNode = ActiveQueue.Dequeue();
                else
                    break;
            }
        }

        public void ClipPolygons(List<PolygonTreeNode> clippolygontreenodes, bool alsoRemoveCoplanarFront)
        {
            Args args = new Args(node: this, polygonTreeNodes: clippolygontreenodes);
            Stack<Args> stack = new Stack<Args>();

            while (true)
            {
                var clippingNode = args.Node;
                var polygontreenodes = args.PolygonTreeNodes;

                if (clippingNode.HasActivePlane)
                {
                    List<PolygonTreeNode> ListBackNodes = new List<PolygonTreeNode>();
                    PolygonTreeNode BackNode;
                    List<PolygonTreeNode> ListFrontNodes = new List<PolygonTreeNode>();
                    PolygonTreeNode FrontNode;
                    PolygonTreeNode CoplanarBackNode;

                    Plane plane = clippingNode.ActivePlane;
                    int numpolygontreenodes = polygontreenodes.Count;

                    for (var i = 0; i < numpolygontreenodes; i++)
                    {
                        PolygonTreeNode polyNode = polygontreenodes[i];

                        if (!polyNode.IsRemoved)
                        {
                            if (alsoRemoveCoplanarFront)
                            {
                                polyNode.SplitByPlane(plane, ref ListBackNodes, out CoplanarBackNode, out FrontNode, out BackNode);
                            }
                            else
                            {
                                polyNode.SplitByPlane(plane, ref ListFrontNodes, out CoplanarBackNode, out FrontNode, out BackNode);
                            }

                            if (FrontNode != null)
                            {
                                ListFrontNodes.Add(FrontNode);
                            }

                            if (BackNode != null)
                            {
                                ListBackNodes.Add(BackNode);
                            }

                            if (CoplanarBackNode != null)
                            {
                                ListBackNodes.Add(CoplanarBackNode);
                            }
                        }
                    }

                    if (clippingNode.Front != null && ListFrontNodes.Count > 0)
                    {
                        stack.Push(new Args(node: clippingNode.Front, polygonTreeNodes: ListFrontNodes));
                    }

                    int numbacknodes = ListBackNodes.Count;

                    if (clippingNode.Back != null && numbacknodes > 0)
                    {
                        stack.Push(new Args(node: clippingNode.Back, polygonTreeNodes: ListBackNodes));
                    }
                    else if (numbacknodes > 0)
                    {
                        // there's nothing behind this plane. Delete the nodes behind this plane:
                        for (int ii = 0; ii < numbacknodes; ii++)
                        {
                            ListBackNodes[ii].Remove();
                        }
                    }
                }
                if (stack.Count > 0)
                    args = stack.Pop();
                else
                    break;
            }
        }

        public void ClipTo(Tree clippingTree, bool alsoRemoveCoplanarFront)
        {
            Node node = this;
            Stack<Node> stack = new Stack<Node>();
            while (node != null)
            {
                if (node.PolygonTreeNodes.Count > 0)
                {
                    clippingTree.RootNode.ClipPolygons(node.PolygonTreeNodes, alsoRemoveCoplanarFront);
                }
                if (node.Front != null)
                {
                    stack.Push(node.Front);
                }
                if (node.Back != null)
                {
                    stack.Push(node.Back);
                }
                node = (stack.Count > 0) ? stack.Pop() : null;
            }
        }

        public void AddPolygonTreeNodes(PolygonTreeNode[] addpolygontreenodes)
        {
            Args args = new Args(node: this, polygonTreeNodes: addpolygontreenodes.ToList());
            Stack<Args> stack = new Stack<Args>();
            while (true)
            {
                Node node = args.Node;
                List<PolygonTreeNode> polygontreenodes = args.PolygonTreeNodes;

                if (polygontreenodes.Count == 0)
                {
                    // Nothing to do
                }
                else
                {
                    Node _this = node;
                    Plane _thisPlane = _this.ActivePlane;
                    if (_thisPlane.Equals(EmptyPlane))
                    {
                        Plane bestplane = polygontreenodes[0].GetPolygon().Plane;
                        node.ActivePlane = bestplane;
                        _thisPlane = bestplane;
                    }

                    List<PolygonTreeNode> ListFrontNodes = new List<PolygonTreeNode>();
                    List<PolygonTreeNode> ListBackNodes = new List<PolygonTreeNode>();
                    PolygonTreeNode FrontNode;
                    PolygonTreeNode BackNode;
                    PolygonTreeNode CoplanarBackNode;

                    for (int i = 0, n = polygontreenodes.Count; i < n; i++)
                    {
                        polygontreenodes[i].SplitByPlane(_thisPlane, ref _this.PolygonTreeNodes, out CoplanarBackNode, out FrontNode, out BackNode);
                        if (FrontNode != null)
                        {
                            ListFrontNodes.Add(FrontNode);
                        }
                        if (BackNode != null)
                        {
                            ListBackNodes.Add(BackNode);
                        }
                        if (CoplanarBackNode != null)
                        {
                            ListBackNodes.Add(CoplanarBackNode);
                        }
                    }

                    if (ListFrontNodes.Count > 0)
                    {
                        if (node.Front == null)
                            node.Front = new Node(node);

                        stack.Push(new Args(node: node.Front, polygonTreeNodes: ListFrontNodes));
                    }
                    if (ListBackNodes.Count > 0)
                    {
                        if (node.Back == null)
                            node.Back = new Node(node);

                        stack.Push(new Args(node: node.Back, polygonTreeNodes: ListBackNodes));
                    }
                }

                if (stack.Count > 0)
                    args = stack.Pop();
                else
                    break;
            }
        }

        struct Args
        {
            public Node Node;
            public List<PolygonTreeNode> PolygonTreeNodes;

            public Args(Node node, List<PolygonTreeNode> polygonTreeNodes)
            {
                Node = node;
                PolygonTreeNodes = polygonTreeNodes;
            }
        }
    }

    public class PolygonTreeNode
    {
        PolygonTreeNode Parent;
        readonly List<PolygonTreeNode> Children = new List<PolygonTreeNode>();
        Polygon Polygon;
        bool Removed;
        
        public void Remove()
        {
            if (!Removed)
            {
                Removed = true;

                if (Parent != null)
                {
                    // remove ourselves from the parent's children list:
                    Parent.Children.Remove(this);

                    // invalidate the parent's polygon, and of all parents above it:
                    Parent.RecursivelyInvalidatePolygon();
                }
            }
        }

        public bool IsRemoved => Removed;

        public bool IsRootNode => Parent == null;

        public void Invert()
        {
            if (!IsRootNode)
                throw new InvalidOperationException("Only the root nodes are invertable.");
            InvertSub();
        }

        public Polygon GetPolygon()
        {
            if (Polygon == null)
                throw new InvalidOperationException("Node is not associated with a polygon.");
            return this.Polygon;
        }

        public List<Polygon> GetPolygons()
        {
            List<Polygon> result = new List<Polygon>();
            Queue<List<PolygonTreeNode>> queue = new Queue<List<PolygonTreeNode>>();
            queue.Enqueue(new List<PolygonTreeNode>() { this });

            while (queue.Count > 0)
            {
                List<PolygonTreeNode> children = queue.Dequeue();
                int l = children.Count;

                for (int j = 0; j < l; j++)
                {
                    PolygonTreeNode node = children[j];
                    if (node.Polygon != null)
                    {
                        result.Add(node.Polygon);
                    }
                    else
                    {
                        queue.Enqueue(node.Children);
                    }
                }
            }

            return result;
        }

        public void SplitByPlane(Plane plane,
            ref List<PolygonTreeNode> coplanarfrontnodes, out PolygonTreeNode CoplanarBackNode,
            out PolygonTreeNode FrontNode, out PolygonTreeNode BackNode)
        {
            CoplanarBackNode = null;
            FrontNode = null;
            BackNode = null;

            if (Children.Count > 0)
            {
                Queue<List<PolygonTreeNode>> QueueNodeList = null;
                List<PolygonTreeNode> ActiveNodeList = Children;
                while (true)
                {
                    int l = ActiveNodeList.Count;
                    for (int j = 0; j < l; j++)
                    {
                        PolygonTreeNode node = ActiveNodeList[j];
                        if (node.Children.Count > 0)
                        {
                            if (QueueNodeList == null)
                                QueueNodeList = new Queue<List<PolygonTreeNode>>(node.Children.Count);
                            QueueNodeList.Enqueue(node.Children);
                        }
                        else
                        {
                            node.SplitPolygonByPlane(plane, ref coplanarfrontnodes, out CoplanarBackNode, out FrontNode, out BackNode);
                        }
                    }
                    if (QueueNodeList != null && QueueNodeList.Count > 0)
                        ActiveNodeList = QueueNodeList.Dequeue();
                    else
                        break;
                }
            }
            else
            {
                SplitPolygonByPlane(plane, ref coplanarfrontnodes, out CoplanarBackNode, out FrontNode, out BackNode);
            }
        }

        void SplitPolygonByPlane(Plane plane,
            ref List<PolygonTreeNode> CoplanarFrontNodes, out PolygonTreeNode CoplanarBackNode,
            out PolygonTreeNode FrontNode, out PolygonTreeNode BackNode)
        {
            Polygon ActivePolygon = this.Polygon;
            CoplanarBackNode = null;
            FrontNode = null;
            BackNode = null;

            if (ActivePolygon != null)
            {
                BoundingSphere Sphere = ActivePolygon.BoundingSphere;
                double SphereRadius = Sphere.radius + 1.0e-4;
                Vector3 PlaneNormal = plane.normal;
                Vector3 SphereCenter = Sphere.position;
                float d = PlaneNormal.Dot(SphereCenter) - plane.distance;

                if (d > SphereRadius)
                {
                    FrontNode = this;
                }
                else if (d < -SphereRadius)
                {
                    BackNode = this;
                }
                else
                {
                    SplitPolygonResult splitresult;
                    plane.SplitPolygon(ActivePolygon, out splitresult);
                    switch (splitresult.Type)
                    {
                        case 0:
                            CoplanarFrontNodes.Add(this);
                            break;
                        case 1:
                            CoplanarBackNode = this;
                            break;
                        case 2:
                            FrontNode = this;
                            break;
                        case 3:
                            BackNode = this;
                            break;
                        default:
                            if (splitresult.Front != null)
                            {
                                FrontNode = AddChild(splitresult.Front);
                            }
                            if (splitresult.Back != null)
                            {
                                BackNode = AddChild(splitresult.Back);
                            }
                            break;
                    }
                }
            }
        }

        public PolygonTreeNode AddChild(Polygon polygon)
        {
            PolygonTreeNode NewChildNode = new PolygonTreeNode();
            NewChildNode.Parent = this;
            NewChildNode.Polygon = polygon;
            Children.Add(NewChildNode);
            return NewChildNode;
        }

        public PolygonTreeNode[] AddChildren(Polygon[] ArrayNewPolygon)
        {
            PolygonTreeNode[] ArrayAddedNode = new PolygonTreeNode[ArrayNewPolygon.Length];

            for (int P = 0; P < ArrayNewPolygon.Length; P++)
            {
                PolygonTreeNode NewChildNode = new PolygonTreeNode();
                NewChildNode.Parent = this;
                NewChildNode.Polygon = ArrayNewPolygon[P];
                Children.Add(NewChildNode);
                ArrayAddedNode[P] = NewChildNode;
            }
            return ArrayAddedNode;
        }

        void InvertSub()
        {
            Queue<List<PolygonTreeNode>> queue = new Queue<List<PolygonTreeNode>>();
            queue.Enqueue(new List<PolygonTreeNode>() { this });

            while (queue.Count > 0)
            {
                List<PolygonTreeNode> children = queue.Dequeue();
                int l = children.Count;

                for (int j = 0; j < l; j++)
                {
                    PolygonTreeNode node = children[j];
                    if (node.Polygon != null)
                    {
                        node.Polygon = node.Polygon.Flipped();
                    }
                    queue.Enqueue(node.Children);
                }
            }
        }

        void RecursivelyInvalidatePolygon()
        {
            PolygonTreeNode node = this;
            while (node.Polygon != null)
            {
                node.Polygon = null;
                if (node.Parent != null)
                {
                    node = node.Parent;
                }
            }
        }
    }
}
