using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Csg
{
    public struct SplitPolygonResult
    {
        public int Type;
        public Polygon Front;
        public Polygon Back;
    }

    class OrthoNormalBasis
    {
        public readonly Vector3 U;
        public readonly Vector3 V;
        public readonly Plane Plane;
        public readonly Vector3 PlaneOrigin;

        public OrthoNormalBasis(Plane plane)
        {
            var rightvector = plane.normal.RandomNonParallelVector();
            V = Vector3.Cross(plane.normal, rightvector).Unit();
            U = Vector3.Cross(V, plane.normal);
            Plane = plane;
            PlaneOrigin = plane.normal * plane.distance;
        }

        public Vector2 To2D(Vector3 vec3)
        {
            return new Vector2(Vector3.Dot(vec3, U), Vector3.Dot(vec3, V));
        }
        public Vector3 To3D(Vector2 vec2)
        {
            return PlaneOrigin + U * vec2.x + V * vec2.y;
        }
    }

    class Line2D
    {
        readonly Vector2 normal;

        //readonly double w;
        public Line2D(Vector2 normal, double w)
        {
            var l = normal.magnitude;
            w *= l;
            normal = normal * (1.0f / l);
            this.normal = normal;
            //this.w = w;
        }
        public Vector2 Direction => normal.Normal();
        public static Line2D FromPoints(Vector2 p1, Vector2 p2)
        {
            var direction = p2 - (p1);
            var normal = direction.Normal().Negated().Unit();
            var w = Vector2.Dot(p1, normal);
            return new Line2D(normal, w);
        }
    }

    public static class CSGHelper
    {
        const double EPSILON = 0.00001d;

        public static Vector2 Negated(this Vector2 ActiveVector2)
        {
            return new Vector2(-ActiveVector2.x, -ActiveVector2.y);
        }

        public static Vector2 Normal(this Vector2 ActiveVector2)
        {
            return new Vector2(ActiveVector2.y, -ActiveVector2.x);
        }

        public static Vector2 Unit(this Vector2 ActiveVector2)
        {
            var d = ActiveVector2.magnitude;
            return new Vector2(ActiveVector2.x / d, ActiveVector2.y / d);
        }

        public static double DistanceTo(this Vector2 ActiveVector2, Vector2 a)
        {
            var dx = ActiveVector2.x - a.x;
            var dy = ActiveVector2.y - a.y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vector3 Negated(this Vector3 ActiveVector3)
        {
            return new Vector3(-ActiveVector3.x, -ActiveVector3.y, -ActiveVector3.z);
        }

        public static Vector3 Unit(this Vector3 ActiveVector3)
        {
            var d = ActiveVector3.magnitude;
            return new Vector3(ActiveVector3.x / d, ActiveVector3.y / d, ActiveVector3.z / d);
        }

        public static Vector3 Abs(this Vector3 ActiveVector3)
        {
            return new Vector3(Math.Abs(ActiveVector3.x), Math.Abs(ActiveVector3.y), Math.Abs(ActiveVector3.z));
        }

        public static Vector3 Min(this Vector3 ActiveVector3, Vector3 OtherVector3)
        {
            return Vector3.Min(ActiveVector3, OtherVector3);
        }

        public static Vector3 Max(this Vector3 ActiveVector3, Vector3 OtherVector3)
        {
            return Vector3.Max(ActiveVector3, OtherVector3);
        }

        public static float Dot(this Vector3 ActiveVector3, Vector3 OtherVector3)
        {
            return Vector3.Dot(ActiveVector3, OtherVector3);
        }

        public static Vector3 Cross(this Vector3 ActiveVector3, Vector3 OtherVector3)
        {
            return Vector3.Cross(ActiveVector3, OtherVector3);
        }

        public static double DistanceToSquared(this Vector3 ActiveVector3, Vector3 a)
        {
            var dx = ActiveVector3.x - a.x;
            var dy = ActiveVector3.y - a.y;
            var dz = ActiveVector3.z - a.z;
            return dx * dx + dy * dy + dz * dz;
        }

        public static Vector3 RandomNonParallelVector(this Vector3 ActiveVector3)
        {
            var abs = ActiveVector3.Abs();
            if ((abs.x <= abs.y) && (abs.x <= abs.z))
            {
                return new Vector3(1, 0, 0);
            }
            else if ((abs.y <= abs.x) && (abs.y <= abs.z))
            {
                return new Vector3(0, 1, 0);
            }
            else
            {
                return new Vector3(0, 0, 1);
            }
        }

        public static Plane Flipped(this Plane ActivePlane)
        {
            return new Plane(-ActivePlane.normal, -ActivePlane.distance);
        }

        public static Plane FromVector3s(Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).Unit();
            return new Plane(n, Vector3.Dot(n, a));
        }

        public static Plane Project(this Plane ActivePlane, Matrix4x4 matrix4x4)
        {
            // get two vectors in the plane:
            Vector3 r = ActivePlane.normal.RandomNonParallelVector();
            Vector3 u = Vector3.Cross(ActivePlane.normal, r);
            Vector3 v = Vector3.Cross(ActivePlane.normal, u);
            // get 3 points in the plane:
            Vector3 point1 = ActivePlane.normal * (ActivePlane.distance);
            Vector3 point2 = point1 + (u);
            Vector3 point3 = point1 + (v);
            // transform the points:
            point1 = matrix4x4 * point1;
            point2 = matrix4x4 * point2;
            point3 = matrix4x4 * point3;
            // and create a new plane from the transformed points:
            var newplane = FromVector3s(point1, point2, point3);
            return newplane;
        }

        public unsafe static void SplitPolygon(this Plane ActivePlane, Polygon ActivePolygon, out SplitPolygonResult result)
        {
            result = new SplitPolygonResult();
            Vector3 planenormal = ActivePlane.normal;
            Vertex[] vertices = ActivePolygon.Vertices;
            int numvertices = vertices.Length;

            if (ActivePolygon.Plane.Equals(ActivePlane))
            {
                result.Type = 0;
            }
            else
            {
                double EPS = EPSILON;
                float thisw = ActivePlane.distance;
                bool hasfront = false;
                bool hasback = false;
                bool* vertexIsBack = stackalloc bool[numvertices];
                double MINEPS = -EPS;

                for (var i = 0; i < numvertices; i++)
                {
                    float t = Vector3.Dot(planenormal, vertices[i].Pos) - thisw;
                    bool isback = (t < 0);
                    vertexIsBack[i] = isback;
                    if (t > EPS)
                        hasfront = true;
                    if (t < MINEPS)
                        hasback = true;
                }
                if ((!hasfront) && (!hasback))
                {
                    // all points coplanar
                    float t = Vector3.Dot(planenormal, ActivePolygon.Plane.normal);
                    result.Type = (t >= 0) ? 0 : 1;
                }
                else if (!hasback)
                {
                    result.Type = 2;
                }
                else if (!hasfront)
                {
                    result.Type = 3;
                }
                else
                {
                    // spanning
                    result.Type = 4;
                    Vertex[] ArrayFrontVertex = new Vertex[numvertices * 2];
                    int ArrayFrontVertexIndex = 0;
                    Vertex LastFrontVertex = vertices[numvertices - 1];
                    Vertex[] ArrayBackVertex = new Vertex[numvertices * 2];
                    int ArrayBackVertexIndex = 0;

                    bool isback = vertexIsBack[0];
                    double EPS_SQUARED = EPSILON * EPSILON;

                    for (int vertexindex = 0; vertexindex < numvertices; vertexindex++)
                    {
                        Vertex ActiveVertex = vertices[vertexindex];
                        int NextVertexIndex = vertexindex + 1;
                        if (NextVertexIndex >= numvertices)
                            NextVertexIndex = 0;
                        bool nextisback = vertexIsBack[NextVertexIndex];

                        if (isback == nextisback)
                        {
                            // line segment is on one side of the plane:
                            if (isback)
                            {
                                if (ArrayBackVertexIndex == 0 || ActiveVertex.Pos.DistanceToSquared(ArrayBackVertex[ArrayBackVertexIndex].Pos) >= EPS_SQUARED)
                                {
                                    ArrayBackVertex[ArrayBackVertexIndex++] = ActiveVertex;
                                }
                            }
                            else
                            {
                                if (ArrayFrontVertexIndex == 0 || ActiveVertex.Pos.DistanceToSquared(LastFrontVertex.Pos) >= EPS_SQUARED)
                                {
                                    ArrayFrontVertex[ArrayFrontVertexIndex++] = ActiveVertex;
                                }
                                LastFrontVertex = ActiveVertex;
                            }
                        }
                        else
                        {
                            // line segment intersects plane:
                            Vertex intersectionvertex = ActivePlane.SplitLineBetweenVertices(ActiveVertex, vertices[NextVertexIndex]);

                            if (isback)
                            {
                                if (ArrayBackVertexIndex == 0 || ActiveVertex.Pos.DistanceToSquared(ArrayBackVertex[ArrayBackVertexIndex].Pos) >= EPS_SQUARED)
                                {
                                    ArrayBackVertex[ArrayBackVertexIndex++] = ActiveVertex;
                                }
                                if (ArrayBackVertexIndex == 0 || intersectionvertex.Pos.DistanceToSquared(ArrayBackVertex[ArrayBackVertexIndex].Pos) >= EPS_SQUARED)
                                {
                                    ArrayBackVertex[ArrayBackVertexIndex++] = intersectionvertex;
                                }
                                if (ArrayFrontVertexIndex == 0 || intersectionvertex.Pos.DistanceToSquared(LastFrontVertex.Pos) >= EPS_SQUARED)
                                {
                                    ArrayFrontVertex[ArrayFrontVertexIndex++] = intersectionvertex;
                                }
                                LastFrontVertex = intersectionvertex;
                            }
                            else
                            {
                                if (ArrayFrontVertexIndex == 0 || ActiveVertex.Pos.DistanceToSquared(LastFrontVertex.Pos) >= EPS_SQUARED)
                                {
                                    ArrayFrontVertex[ArrayFrontVertexIndex++] = ActiveVertex;
                                }
                                LastFrontVertex = ActiveVertex;
                                if (ArrayFrontVertexIndex == 0 || intersectionvertex.Pos.DistanceToSquared(LastFrontVertex.Pos) >= EPS_SQUARED)
                                {
                                    ArrayFrontVertex[ArrayFrontVertexIndex++] = intersectionvertex;
                                }
                                LastFrontVertex = intersectionvertex;
                                if (intersectionvertex.Pos.DistanceToSquared(ArrayBackVertex[ArrayBackVertexIndex].Pos) >= EPS_SQUARED)
                                {
                                    ArrayBackVertex[ArrayBackVertexIndex++] = intersectionvertex;
                                }
                            }
                        }
                        isback = nextisback;
                    }

                    if (ArrayBackVertex[0].Pos.DistanceToSquared(ArrayBackVertex[ArrayBackVertexIndex - 1].Pos) < EPS_SQUARED)
                    {
                        --ArrayBackVertexIndex;
                    }

                    if (ArrayFrontVertex[0].Pos.DistanceToSquared(ArrayFrontVertex[ArrayFrontVertexIndex - 1].Pos) < EPS_SQUARED)
                    {
                        --ArrayFrontVertexIndex;
                    }

                    if (ArrayFrontVertexIndex >= 3)
                    {
                        result.Front = new Polygon(ActivePolygon.PolygonColor, ArrayFrontVertex.Take(ArrayFrontVertexIndex).ToArray(), ActivePolygon.Plane);
                    }
                    if (ArrayBackVertexIndex >= 3)
                    {
                        result.Back = new Polygon(ActivePolygon.PolygonColor, ArrayBackVertex.Take(ArrayBackVertexIndex).ToArray(), ActivePolygon.Plane);
                    }
                }
            }
        }

        static Vertex SplitLineBetweenVertices(this Plane ActivePlane, Vertex v1, Vertex v2)
        {
            Vector3 p1 = v1.Pos;
            Vector3 p2 = v2.Pos;
            Vector3 direction = p2 - (p1);
            float u = (ActivePlane.distance - Vector3.Dot(ActivePlane.normal, p1)) / Vector3.Dot(ActivePlane.normal, direction);

            if (double.IsNaN(u))
                u = 0;
            if (u > 1)
                u = 1;
            if (u < 0)
                u = 0;
            Vector3 result = p1 + (direction * u);
            Vector2 tresult = v1.Tex + (v2.Tex - v1.Tex) * u;
            return new Vertex(result, tresult);
        }
    }
}
