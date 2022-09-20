using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Csg
{
    public class Solid
    {
        struct Vertex2DInterpolation
        {
            public float Result;
            public Vector2 Tex;
        }

        /// <summary>
        /// Used for tesselating co-planar polygons to keep
        /// track of texture coordinates.
        /// </summary>
        struct Vertex2D
        {
            public Vector2 Pos;
            public Vector2 Tex;
            public Vertex2D(Vector2 pos, Vector2 tex)
            {
                Pos = pos;
                Tex = tex;
            }
            public Vertex2D(float x, float y, Vector2 tex)
            {
                Pos = new Vector2(x, y);
                Tex = tex;
            }
        }

        class RetesselateActivePolygon
        {
            public int polygonindex;
            public int leftvertexindex;
            public int rightvertexindex;
            public Vertex2D topleft;
            public Vertex2D topright;
            public Vertex2D bottomleft;
            public Vertex2D bottomright;
            public Line2D leftline;
            public bool leftlinecontinues;
            public Line2D rightline;
            public bool rightlinecontinues;
            public RetesselateOutPolygon outpolygon;
        }

        class RetesselateOutPolygon
        {
            public readonly List<Vertex2D> leftpoints = new List<Vertex2D>();
            public readonly List<Vertex2D> rightpoints = new List<Vertex2D>();
        }

        public List<Polygon> Polygons;

        public bool IsCanonicalized;
        public bool IsRetesselated;

        public const int DefaultResolution2D = 32;
        public const int DefaultResolution3D = 12;

        Bounds cachedBoundingBox;
        private bool isBoundingBoxCached = false;

        public Solid()
        {
            Polygons = new List<Polygon>();
            IsCanonicalized = true;
            IsRetesselated = true;
        }

        public static Solid FromPolygons(List<Polygon> polygons)
        {
            Solid csg = new Solid();
            csg.Polygons = polygons;
            csg.IsCanonicalized = false;
            csg.IsRetesselated = false;
            return csg;
        }

        public Solid Union(Solid other)
        {
            return this.UnionSub(other, false, false).Retesselated().Canonicalized();
        }

        public Solid UnionMany(params Solid[] others)
        {
            List<Solid> csgs = new List<Solid>();
            csgs.Add(this);
            csgs.AddRange(others);
            int i = 1;
            for (; i < csgs.Count; i += 2)
            {
                Solid n = csgs[i - 1].UnionSub(csgs[i], false, false);
                csgs.Add(n);
            }
            return csgs[i - 1].Retesselated().Canonicalized();
        }

        Solid UnionSub(Solid csg, bool retesselate, bool canonicalize)
        {
            if (!MayOverlap(csg))
            {
                return UnionForNonIntersecting(csg);
            }
            else
            {
                Tree a = new Tree(Polygons);
                Tree b = new Tree(csg.Polygons);

                a.ClipTo(b, false);
                b.ClipTo(a);
                b.Invert();
                b.ClipTo(a);
                b.Invert();

                Polygon[] ArrayPolygonA = a.AllPolygons().ToArray();
                Polygon[] ArrayPolygonB = b.AllPolygons().ToArray();
                Polygon[] ArrayNewPolygon = new Polygon[ArrayPolygonA.Length + ArrayPolygonB.Length];
                Array.Copy(ArrayPolygonA, 0, ArrayNewPolygon, 0, ArrayPolygonA.Length);
                Array.Copy(ArrayPolygonB, 0, ArrayNewPolygon, ArrayPolygonA.Length, ArrayPolygonB.Length);

                Solid result = Solid.FromPolygons(ArrayNewPolygon.ToList());
                if (retesselate)
                    result = result.Retesselated();
                if (canonicalize)
                    result = result.Canonicalized();
                return result;
            }
        }

        Solid UnionForNonIntersecting(Solid csg)
        {
            Polygon[] ArrayPolygonA = Polygons.ToArray();
            Polygon[] ArrayPolygonB = csg.Polygons.ToArray();
            Polygon[] ArrayNewPolygon = new Polygon[ArrayPolygonA.Length + ArrayPolygonB.Length];
            Array.Copy(ArrayPolygonA, 0, ArrayNewPolygon, 0, ArrayPolygonA.Length);
            Array.Copy(ArrayPolygonB, 0, ArrayNewPolygon, ArrayPolygonA.Length, ArrayPolygonB.Length);

            Solid result = Solid.FromPolygons(ArrayNewPolygon.ToList());
            result.IsCanonicalized = IsCanonicalized && csg.IsCanonicalized;
            result.IsRetesselated = IsRetesselated && csg.IsRetesselated;
            return result;
        }

        public Solid Substract(params Solid[] csgs)
        {
            Solid result = this;
            for (var i = 0; i < csgs.Length; i++)
            {
                bool islast = (i == (csgs.Length - 1));
                result = result.SubtractSub(csgs[i], islast, islast);
            }
            return result;
        }

        Solid SubtractSub(Solid csg, bool retesselate, bool canonicalize)
        {
            Tree a = new Tree(Polygons);
            Tree b = new Tree(csg.Polygons);

            a.Invert();
            a.ClipTo(b);
            b.ClipTo(a, true);
            a.AddPolygons(b.AllPolygons());
            a.Invert();

            Solid result = Solid.FromPolygons(a.AllPolygons());
            if (retesselate)
                result = result.Retesselated();
            if (canonicalize)
                result = result.Canonicalized();
            return result;
        }

        public Solid Intersect(params Solid[] csgs)
        {
            Solid result = this;
            for (var i = 0; i < csgs.Length; i++)
            {
                bool islast = (i == (csgs.Length - 1));
                result = result.IntersectSub(csgs[i], islast, islast);
            }
            return result;
        }

        Solid IntersectSub(Solid csg, bool retesselate, bool canonicalize)
        {
            Tree a = new Tree(Polygons);
            Tree b = new Tree(csg.Polygons);

            a.Invert();
            b.ClipTo(a);
            b.Invert();
            a.ClipTo(b);
            b.ClipTo(a);
            a.AddPolygons(b.AllPolygons());
            a.Invert();

            Solid result = Solid.FromPolygons(a.AllPolygons());
            if (retesselate)
                result = result.Retesselated();
            if (canonicalize)
                result = result.Canonicalized();
            return result;
        }

        public Solid Transform(Matrix4x4 matrix4x4)
        {
            Dictionary<Vertex, Vertex> transformedvertices = new Dictionary<Vertex, Vertex>();
            Dictionary<Plane, Plane> transformedplanes = new Dictionary<Plane, Plane>();
            Polygon[] ArrayNewPolygon = new Polygon[Polygons.Count];

            for (int i = 0; i < Polygons.Count; i++)
            {
                Polygon ActivePolygon = Polygons[i];
                Plane newplane;
                Plane plane = ActivePolygon.Plane;
                if (transformedplanes.ContainsKey(plane))
                {
                    newplane = transformedplanes[plane];
                }
                else
                {
                    newplane = plane.Project(matrix4x4);
                    transformedplanes[plane] = newplane;
                }

               Vertex[] ArrayNewVertex = new Vertex[ActivePolygon.Vertices.Length];

                for (int V = 0; V < ActivePolygon.Vertices.Length; ++V)
                {
                    Vertex ActiveVertex = ActivePolygon.Vertices[V];
                    Vertex newvertex;

                    if (transformedvertices.ContainsKey(ActiveVertex))
                    {
                        newvertex = transformedvertices[ActiveVertex];
                    }
                    else
                    {
                        newvertex = ActiveVertex.Transform(matrix4x4);
                        transformedvertices[ActiveVertex] = newvertex;
                    }
                    ArrayNewVertex[V] = newvertex;
                }
                ArrayNewPolygon[i] = new Polygon(ActivePolygon.PolygonColor, ArrayNewVertex, newplane);
            }
            Solid result = Solid.FromPolygons(ArrayNewPolygon.ToList());
            result.IsRetesselated = this.IsRetesselated;
            result.IsCanonicalized = this.IsCanonicalized;
            return result;
        }

        public Solid Translate(Vector3 offset)
        {
            return Transform(Matrix4x4.Translate(offset));
        }

        public Solid Translate(float x = 0, float y = 0, float z = 0)
        {
            return Transform(Matrix4x4.Translate(new Vector3(x, y, z)));
        }

        public Solid Scale(Vector3 scale)
        {
            return Transform(Matrix4x4.Scale(scale));
        }

        public Solid Scale(float scale)
        {
            return Transform(Matrix4x4.Scale(new Vector3(scale, scale, scale)));
        }

        public Solid Scale(float x, float y, float z)
        {
            return Transform(Matrix4x4.Scale(new Vector3(x, y, z)));
        }

        public Solid Canonicalized()
        {
            if (IsCanonicalized)
            {
                return this;
            }
            else
            {
                var factory = new FuzzyCsgFactory();
                var result = factory.GetCsg(this);
                result.IsCanonicalized = true;
                result.IsRetesselated = IsRetesselated;
                return result;
            }
        }

        public Solid Retesselated()
        {
            if (IsRetesselated)
            {
                return this;
            }
            else
            {
                Solid csg = this;
                Dictionary<Plane, List<Polygon>> polygonsPerPlane = new Dictionary<Plane, List<Polygon>>();

                foreach (Polygon polygon in csg.Polygons)
                {
                    Plane plane = polygon.Plane;

                    List<Polygon> ppp;
                    if (polygonsPerPlane.TryGetValue(plane, out ppp))
                    {
                        ppp.Add(polygon);
                    }
                    else
                    {
                        ppp = new List<Polygon>(1);
                        ppp.Add(polygon);
                        polygonsPerPlane.Add(plane, ppp);
                    }
                }
                var destpolygons = new List<Polygon>();

                foreach (KeyValuePair<Plane, List<Polygon>> planetag in polygonsPerPlane)
                {
                    var sourcepolygons = planetag.Value;
                    if (sourcepolygons.Count < 2)
                    {
                        destpolygons.AddRange(sourcepolygons);
                    }
                    else
                    {
                        List<Polygon> retesselatedpolygons = new List<Polygon>(sourcepolygons.Count);
                        Solid.RetesselateCoplanarPolygons(sourcepolygons, retesselatedpolygons);
                        destpolygons.AddRange(retesselatedpolygons);
                    }
                }

                Solid result = Solid.FromPolygons(destpolygons);
                result.IsRetesselated = true;
                return result;
            }
        }

        Bounds Boundss
        {
            get
            {
                if (!isBoundingBoxCached)
                {
                    Vector3 minpoint = new Vector3(0, 0, 0);
                    Vector3 maxpoint = new Vector3(0, 0, 0);
                    List<Polygon> polygons = this.Polygons;
                    int numpolygons = polygons.Count;

                    for (int i = 0; i < numpolygons; i++)
                    {
                        Polygon polygon = polygons[i];
                        Bounds bounds = polygon.BoundingBox;
                        if (i == 0)
                        {
                            minpoint = bounds.min;
                            maxpoint = bounds.max;
                        }
                        else
                        {
                            minpoint = minpoint.Min(bounds.min);
                            maxpoint = maxpoint.Max(bounds.max);
                        }
                    }
                    cachedBoundingBox = new Bounds(minpoint, maxpoint);
                    isBoundingBoxCached = true;
                }
                return cachedBoundingBox;
            }
        }

        bool MayOverlap(Solid csg)
        {
            if ((this.Polygons.Count == 0) || (csg.Polygons.Count == 0))
            {
                return false;
            }
            else
            {
                Bounds mybounds = Boundss;
                Bounds otherbounds = csg.Boundss;

                if (mybounds.max.x < otherbounds.min.x)
                    return false;
                if (mybounds.min.x > otherbounds.max.x)
                    return false;
                if (mybounds.max.y < otherbounds.min.y)
                    return false;
                if (mybounds.min.y > otherbounds.max.y)
                    return false;
                if (mybounds.max.z < otherbounds.min.z)
                    return false;
                if (mybounds.min.z > otherbounds.max.z)
                    return false;
                return true;
            }
        }

        static void RetesselateCoplanarPolygons(List<Polygon> sourcepolygons, List<Polygon> destpolygons)
        {
            var EPS = 1e-5;

            int numpolygons = sourcepolygons.Count;
            if (numpolygons > 0)
            {
                Plane plane = sourcepolygons[0].Plane;
                Color PolygonColor = sourcepolygons[0].PolygonColor;
                OrthoNormalBasis orthobasis = new OrthoNormalBasis(plane);
                List<List<Vertex2D>> polygonvertices2d = new List<List<Vertex2D>>(); // array of array of Vertex2Ds
                List<int> polygontopvertexindexes = new List<int>(); // array of indexes of topmost vertex per polygon
                Dictionary<float, List<int>> topy2polygonindexes = new Dictionary<float, List<int>>();
                Dictionary<float, HashSet<int>> ycoordinatetopolygonindexes = new Dictionary<float, HashSet<int>>();

                //var xcoordinatebins = new Dictionary<double, double>();
                Dictionary<float, float> ycoordinatebins = new Dictionary<float, float>();

                // convert all polygon vertices to 2D
                // Make a list of all encountered y coordinates
                // And build a map of all polygons that have a vertex at a certain y coordinate:
                double ycoordinateBinningFactor = 1.0 / EPS * 10;
                for (int polygonindex = 0; polygonindex < numpolygons; polygonindex++)
                {
                    Polygon poly3d = sourcepolygons[polygonindex];
                    List<Vertex2D> vertices2d = new List<Vertex2D>();
                    int numvertices = poly3d.Vertices.Length;
                    int minindex = -1;
                    if (numvertices > 0)
                    {
                        float miny = 0, maxy = 0;
                        //int maxindex;
                        for (int i = 0; i < numvertices; i++)
                        {
                            Vector2 pos2d = orthobasis.To2D(poly3d.Vertices[i].Pos);
                            // perform binning of y coordinates: If we have multiple vertices very
                            // close to each other, give them the same y coordinate:
                            float ycoordinatebin = (float)Math.Floor(pos2d.y * ycoordinateBinningFactor);
                            float newy;
                            if (ycoordinatebins.ContainsKey(ycoordinatebin))
                            {
                                newy = ycoordinatebins[ycoordinatebin];
                            }
                            else if (ycoordinatebins.ContainsKey(ycoordinatebin + 1))
                            {
                                newy = ycoordinatebins[ycoordinatebin + 1];
                            }
                            else if (ycoordinatebins.ContainsKey(ycoordinatebin - 1))
                            {
                                newy = ycoordinatebins[ycoordinatebin - 1];
                            }
                            else
                            {
                                newy = pos2d.y;
                                ycoordinatebins[ycoordinatebin] = pos2d.y;
                            }
                            pos2d = new Vector2(pos2d.x, newy);
                            vertices2d.Add(new Vertex2D(pos2d, poly3d.Vertices[i].Tex));
                            float y = pos2d.y;
                            if ((i == 0) || (y < miny))
                            {
                                miny = y;
                                minindex = i;
                            }
                            if ((i == 0) || (y > maxy))
                            {
                                maxy = y;
                                //maxindex = i;
                            }
                            if (!(ycoordinatetopolygonindexes.ContainsKey(y)))
                            {
                                ycoordinatetopolygonindexes[y] = new HashSet<int>();
                            }
                            ycoordinatetopolygonindexes[y].Add(polygonindex);
                        }
                        if (miny >= maxy)
                        {
                            // degenerate polygon, all vertices have same y coordinate. Just ignore it from now:
                            vertices2d = new List<Vertex2D>();
                            numvertices = 0;
                            minindex = -1;
                        }
                        else
                        {
                            if (!(topy2polygonindexes.ContainsKey(miny)))
                            {
                                topy2polygonindexes[miny] = new List<int>();
                            }
                            topy2polygonindexes[miny].Add(polygonindex);
                        }
                    } // if(numvertices > 0)
                      // reverse the vertex order:
                    vertices2d.Reverse();
                    minindex = numvertices - minindex - 1;
                    polygonvertices2d.Add(vertices2d);
                    polygontopvertexindexes.Add(minindex);
                }
                List<float> ycoordinates = new List<float>();
                foreach (KeyValuePair<float, HashSet<int>> ycoordinate in ycoordinatetopolygonindexes)
                {
                    ycoordinates.Add(ycoordinate.Key);
                }
                ycoordinates.Sort();

                // Now we will iterate over all y coordinates, from lowest to highest y coordinate
                // activepolygons: source polygons that are 'active', i.e. intersect with our y coordinate
                //   Is sorted so the polygons are in left to right order
                // Each element in activepolygons has these properties:
                //        polygonindex: the index of the source polygon (i.e. an index into the sourcepolygons
                //                      and polygonvertices2d arrays)
                //        leftvertexindex: the index of the vertex at the left side of the polygon (lowest x)
                //                         that is at or just above the current y coordinate
                //        rightvertexindex: dito at right hand side of polygon
                //        topleft, bottomleft: coordinates of the left side of the polygon crossing the current y coordinate
                //        topright, bottomright: coordinates of the right hand side of the polygon crossing the current y coordinate
                List<RetesselateActivePolygon> activepolygons = new List<RetesselateActivePolygon>();
                List<RetesselateActivePolygon> prevoutpolygonrow = new List<RetesselateActivePolygon>();

                for (var yindex = 0; yindex < ycoordinates.Count; yindex++)
                {
                    var newoutpolygonrow = new List<RetesselateActivePolygon>();
                    var ycoordinate = ycoordinates[yindex];
                    //var ycoordinate_as_string = ycoordinates + "";

                    // update activepolygons for this y coordinate:
                    // - Remove any polygons that end at this y coordinate
                    // - update leftvertexindex and rightvertexindex (which point to the current vertex index
                    //   at the the left and right side of the polygon
                    // Iterate over all polygons that have a corner at this y coordinate:
                    HashSet<int> polygonindexeswithcorner = ycoordinatetopolygonindexes[ycoordinate];
                    for (int activepolygonindex = 0; activepolygonindex < activepolygons.Count; ++activepolygonindex)
                    {
                        RetesselateActivePolygon activepolygon = activepolygons[activepolygonindex];
                        int polygonindex = activepolygon.polygonindex;
                        if (polygonindexeswithcorner.Contains(polygonindex))
                        {
                            // this active polygon has a corner at this y coordinate:
                            List<Vertex2D> vertices2d = polygonvertices2d[polygonindex];
                            int numvertices = vertices2d.Count;
                            int newleftvertexindex = activepolygon.leftvertexindex;
                            int newrightvertexindex = activepolygon.rightvertexindex;
                            // See if we need to increase leftvertexindex or decrease rightvertexindex:
                            while (true)
                            {
                                int nextleftvertexindex = newleftvertexindex + 1;
                                if (nextleftvertexindex >= numvertices)
                                    nextleftvertexindex = 0;
                                if (vertices2d[nextleftvertexindex].Pos.y != ycoordinate)
                                    break;
                                newleftvertexindex = nextleftvertexindex;
                            }
                            int nextrightvertexindex = newrightvertexindex - 1;
                            if (nextrightvertexindex < 0)
                                nextrightvertexindex = numvertices - 1;
                            if (vertices2d[nextrightvertexindex].Pos.y == ycoordinate)
                            {
                                newrightvertexindex = nextrightvertexindex;
                            }
                            if ((newleftvertexindex != activepolygon.leftvertexindex) && (newleftvertexindex == newrightvertexindex))
                            {
                                // We have increased leftvertexindex or decreased rightvertexindex, and now they point to the same vertex
                                // This means that this is the bottom point of the polygon. We'll remove it:
                                activepolygons.RemoveAt(activepolygonindex);
                                --activepolygonindex;
                            }
                            else
                            {
                                activepolygon.leftvertexindex = newleftvertexindex;
                                activepolygon.rightvertexindex = newrightvertexindex;
                                activepolygon.topleft = vertices2d[newleftvertexindex];
                                activepolygon.topright = vertices2d[newrightvertexindex];

                                int nextleftvertexindex = newleftvertexindex + 1;
                                if (nextleftvertexindex >= numvertices)
                                {
                                    nextleftvertexindex = 0;
                                }

                                activepolygon.bottomleft = vertices2d[nextleftvertexindex];
                                nextrightvertexindex = newrightvertexindex - 1;
                                if (nextrightvertexindex < 0)
                                {
                                    nextrightvertexindex = numvertices - 1;
                                }

                                activepolygon.bottomright = vertices2d[nextrightvertexindex];
                            }
                        } // if polygon has corner here
                    } // for activepolygonindex
                    float nextycoordinate;
                    if (yindex >= ycoordinates.Count - 1)
                    {
                        // last row, all polygons must be finished here:
                        activepolygons = new List<RetesselateActivePolygon>();
                        nextycoordinate = 0.0f;
                    }
                    else // yindex < ycoordinates.length-1
                    {
                        nextycoordinate = ycoordinates[yindex + 1];
                        float middleycoordinate = 0.5f * (ycoordinate + nextycoordinate);
                        // update activepolygons by adding any polygons that start here:
                        List<int> startingpolygonindexes;
                        if (topy2polygonindexes.TryGetValue(ycoordinate, out startingpolygonindexes))
                        {
                            foreach (int polygonindex in startingpolygonindexes)
                            {
                                List<Vertex2D> vertices2d = polygonvertices2d[polygonindex];
                                int numvertices = vertices2d.Count;
                                int topvertexindex = polygontopvertexindexes[polygonindex];
                                // the top of the polygon may be a horizontal line. In that case topvertexindex can point to any point on this line.
                                // Find the left and right topmost vertices which have the current y coordinate:
                                int topleftvertexindex = topvertexindex;
                                while (true)
                                {
                                    int i = topleftvertexindex + 1;
                                    if (i >= numvertices)
                                        i = 0;
                                    if (vertices2d[i].Pos.y != ycoordinate)
                                        break;
                                    if (i == topvertexindex)
                                        break; // should not happen, but just to prevent endless loops
                                    topleftvertexindex = i;
                                }
                                int toprightvertexindex = topvertexindex;
                                while (true)
                                {
                                    int i = toprightvertexindex - 1;
                                    if (i < 0)
                                        i = numvertices - 1;
                                    if (vertices2d[i].Pos.y != ycoordinate)
                                        break;
                                    if (i == topleftvertexindex)
                                        break; // should not happen, but just to prevent endless loops
                                    toprightvertexindex = i;
                                }
                                int nextleftvertexindex = topleftvertexindex + 1;
                                if (nextleftvertexindex >= numvertices)
                                {
                                    nextleftvertexindex = 0;
                                }

                                int nextrightvertexindex = toprightvertexindex - 1;
                                if (nextrightvertexindex < 0)
                                {
                                    nextrightvertexindex = numvertices - 1;
                                }

                                RetesselateActivePolygon newactivepolygon = new RetesselateActivePolygon
                                {
                                    polygonindex = polygonindex,
                                    leftvertexindex = topleftvertexindex,
                                    rightvertexindex = toprightvertexindex,
                                    topleft = vertices2d[topleftvertexindex],
                                    topright = vertices2d[toprightvertexindex],
                                    bottomleft = vertices2d[nextleftvertexindex],
                                    bottomright = vertices2d[nextrightvertexindex],
                                };

                                InsertSorted(activepolygons, newactivepolygon, (el1, el2) =>
                                {
                                    Vertex2DInterpolation x1 = InterpolateBetween2DPointsForY(
                                        el1.topleft, el1.bottomleft, middleycoordinate);
                                    Vertex2DInterpolation x2 = InterpolateBetween2DPointsForY(
                                        el2.topleft, el2.bottomleft, middleycoordinate);
                                    if (x1.Result > x2.Result)
                                        return 1;
                                    if (x1.Result < x2.Result)
                                        return -1;
                                    return 0;
                                });
                            } // for(var polygonindex in startingpolygonindexes)
                        }
                    } //  yindex < ycoordinates.length-1
                      //if( (yindex == ycoordinates.length-1) || (nextycoordinate - ycoordinate > EPS) )
                    if (true)
                    {
                        // Now activepolygons is up to date
                        // Build the output polygons for the next row in newoutpolygonrow:
                        for (int activepolygon_key = 0; activepolygon_key < activepolygons.Count; activepolygon_key++)
                        {
                            RetesselateActivePolygon activepolygon = activepolygons[activepolygon_key];
                            int polygonindex = activepolygon.polygonindex;
                            List<Vertex2D> vertices2d = polygonvertices2d[polygonindex];
                            int numvertices = vertices2d.Count;

                            Vertex2DInterpolation x = InterpolateBetween2DPointsForY(activepolygon.topleft, activepolygon.bottomleft, ycoordinate);
                            Vertex2D topleft = new Vertex2D(x.Result, ycoordinate, x.Tex);
                            x = InterpolateBetween2DPointsForY(activepolygon.topright, activepolygon.bottomright, ycoordinate);
                            Vertex2D topright = new Vertex2D(x.Result, ycoordinate, x.Tex);
                            x = InterpolateBetween2DPointsForY(activepolygon.topleft, activepolygon.bottomleft, nextycoordinate);
                            Vertex2D bottomleft = new Vertex2D(x.Result, nextycoordinate, x.Tex);
                            x = InterpolateBetween2DPointsForY(activepolygon.topright, activepolygon.bottomright, nextycoordinate);
                            Vertex2D bottomright = new Vertex2D(x.Result, nextycoordinate, x.Tex);
                            RetesselateActivePolygon outpolygon = new RetesselateActivePolygon
                            {
                                topleft = topleft,
                                topright = topright,
                                bottomleft = bottomleft,
                                bottomright = bottomright,
                                leftline = Line2D.FromPoints(topleft.Pos, bottomleft.Pos),
                                rightline = Line2D.FromPoints(bottomright.Pos, topright.Pos)
                            };
                            if (newoutpolygonrow.Count > 0)
                            {
                                RetesselateActivePolygon prevoutpolygon = newoutpolygonrow[newoutpolygonrow.Count - 1];
                                double d1 = outpolygon.topleft.Pos.DistanceTo(prevoutpolygon.topright.Pos);
                                double d2 = outpolygon.bottomleft.Pos.DistanceTo(prevoutpolygon.bottomright.Pos);
                                if ((d1 < EPS) && (d2 < EPS))
                                {
                                    // we can join this polygon with the one to the left:
                                    outpolygon.topleft = prevoutpolygon.topleft;
                                    outpolygon.leftline = prevoutpolygon.leftline;
                                    outpolygon.bottomleft = prevoutpolygon.bottomleft;
                                    newoutpolygonrow.RemoveAt(newoutpolygonrow.Count - 1);
                                }
                            }
                            newoutpolygonrow.Add(outpolygon);
                        } // for(activepolygon in activepolygons)
                        if (yindex > 0)
                        {
                            // try to match the new polygons against the previous row:
                            HashSet<int> prevcontinuedindexes = new HashSet<int>();
                            HashSet<int> matchedindexes = new HashSet<int>();
                            for (int i = 0; i < newoutpolygonrow.Count; i++)
                            {
                                var thispolygon = newoutpolygonrow[i];
                                if (thispolygon.leftline != null && thispolygon.rightline != null)
                                {
                                    for (int ii = 0; ii < prevoutpolygonrow.Count; ii++)
                                    {
                                        if (!matchedindexes.Contains(ii)) // not already processed?
                                        {
                                            // We have a match if the sidelines are equal or if the top coordinates
                                            // are on the sidelines of the previous polygon
                                            RetesselateActivePolygon prevpolygon = prevoutpolygonrow[ii];
                                            if (prevpolygon.leftline != null && prevpolygon.rightline != null && prevpolygon.bottomleft.Pos.DistanceTo(thispolygon.topleft.Pos) < EPS)
                                            {
                                                if (prevpolygon.bottomright.Pos.DistanceTo(thispolygon.topright.Pos) < EPS)
                                                {
                                                    // Yes, the top of this polygon matches the bottom of the previous:
                                                    matchedindexes.Add(ii);
                                                    // Now check if the joined polygon would remain convex:
                                                    float d1 = thispolygon.leftline.Direction.x - prevpolygon.leftline.Direction.x;
                                                    float d2 = thispolygon.rightline.Direction.x - prevpolygon.rightline.Direction.x;
                                                    bool leftlinecontinues = Math.Abs(d1) < EPS;
                                                    bool rightlinecontinues = Math.Abs(d2) < EPS;
                                                    bool leftlineisconvex = leftlinecontinues || (d1 >= 0);
                                                    bool rightlineisconvex = rightlinecontinues || (d2 >= 0);
                                                    if (leftlineisconvex && rightlineisconvex)
                                                    {
                                                        // yes, both sides have convex corners:
                                                        // This polygon will continue the previous polygon
                                                        thispolygon.outpolygon = prevpolygon.outpolygon;
                                                        thispolygon.leftlinecontinues = leftlinecontinues;
                                                        thispolygon.rightlinecontinues = rightlinecontinues;
                                                        prevcontinuedindexes.Add(ii);
                                                    }
                                                    break;
                                                }
                                            }
                                        } // if(!prevcontinuedindexes[ii])
                                    } // for ii
                                }
                            } // for i
                            for (var ii = 0; ii < prevoutpolygonrow.Count; ii++)
                            {
                                if (!prevcontinuedindexes.Contains(ii))
                                {
                                    // polygon ends here
                                    // Finish the polygon with the last point(s):
                                    RetesselateActivePolygon prevpolygon = prevoutpolygonrow[ii];
                                    if (prevpolygon.outpolygon == null)
                                        continue;
                                    prevpolygon.outpolygon.rightpoints.Add(prevpolygon.bottomright);
                                    if (prevpolygon.bottomright.Pos.DistanceTo(prevpolygon.bottomleft.Pos) > EPS)
                                    {
                                        // polygon ends with a horizontal line:
                                        prevpolygon.outpolygon.leftpoints.Add(prevpolygon.bottomleft);
                                    }
                                    // reverse the left half so we get a counterclockwise circle:
                                    prevpolygon.outpolygon.leftpoints.Reverse();
                                    List<Vertex2D> points2d = new List<Vertex2D>(prevpolygon.outpolygon.rightpoints);
                                    points2d.AddRange(prevpolygon.outpolygon.leftpoints);
                                    Vertex[] vertices3d = new Vertex[points2d.Count];
                                    for (int i = 0; i < points2d.Count; i++)
                                    {
                                        Vertex2D point2d = points2d[i];
                                        Vector3 point3d = orthobasis.To3D(point2d.Pos);
                                        Vertex vertex3d = new Vertex(point3d, point2d.Tex);
                                        vertices3d[i] = vertex3d;
                                    }
                                    Polygon polygon = new Polygon(PolygonColor, vertices3d, plane);
                                    destpolygons.Add(polygon);
                                }
                            }
                        } // if(yindex > 0)
                        for (int i = 0; i < newoutpolygonrow.Count; i++)
                        {
                            RetesselateActivePolygon thispolygon = newoutpolygonrow[i];
                            if (thispolygon.outpolygon == null)
                            {
                                // polygon starts here:
                                thispolygon.outpolygon = new RetesselateOutPolygon();
                                thispolygon.outpolygon.leftpoints.Add(thispolygon.topleft);
                                if (thispolygon.topleft.Pos.DistanceTo(thispolygon.topright.Pos) > EPS)
                                {
                                    // we have a horizontal line at the top:
                                    thispolygon.outpolygon.rightpoints.Add(thispolygon.topright);
                                }
                            }
                            else
                            {
                                // continuation of a previous row
                                if (!thispolygon.leftlinecontinues)
                                {
                                    thispolygon.outpolygon.leftpoints.Add(thispolygon.topleft);
                                }
                                if (!thispolygon.rightlinecontinues)
                                {
                                    thispolygon.outpolygon.rightpoints.Add(thispolygon.topright);
                                }
                            }
                        }
                        prevoutpolygonrow = newoutpolygonrow;
                    }
                } // for yindex
            } // if(numpolygons > 0)
        }

        static void InsertSorted<T>(List<T> array, T element, Func<T, T, int> comparefunc)
        {
            int leftbound = 0;
            int rightbound = array.Count;
            while (rightbound > leftbound)
            {
                int testindex = (leftbound + rightbound) / 2;
                T testelement = array[testindex];
                int compareresult = comparefunc(element, testelement);
                if (compareresult > 0) // element > testelement
                {
                    leftbound = testindex + 1;
                }
                else
                {
                    rightbound = testindex;
                }
            }
            array.Insert(leftbound, element);
        }

        static Vertex2DInterpolation InterpolateBetween2DPointsForY(Vertex2D vertex1, Vertex2D vertex2, float y)
        {
            Vector2 point1 = vertex1.Pos;
            Vector2 point2 = vertex2.Pos;
            float f1 = y - point1.y;
            float f2 = point2.y - point1.y;
            if (f2 < 0)
            {
                f1 = -f1;
                f2 = -f2;
            }
            float t;
            if (f1 <= 0)
            {
                t = 0.0f;
            }
            else if (f1 >= f2)
            {
                t = 1.0f;
            }
            else if (f2 < 1e-10)
            {
                t = 0.5f;
            }
            else
            {
                t = f1 / f2;
            }
            float result = point1.x + t * (point2.x - point1.x);
            return new Vertex2DInterpolation
            {
                Result = result,
                Tex = vertex1.Tex + (vertex2.Tex - vertex1.Tex) * t,
            };
        }
    }

    class FuzzyCsgFactory
    {
        public Polygon GetPolygon(Polygon sourcepolygon)
        {
            Plane newplane = sourcepolygon.Plane;
            List<Vertex> newvertices = new List<Vertex>(sourcepolygon.Vertices);

            // two vertices that were originally very close may now have become
            // truly identical (referring to the same CSG.Vertex object).
            // Remove duplicate vertices:
            Vertex[] newvertices_dedup = new Vertex[newvertices.Count];
            int ArrayNewVertexIndex = 0;
            if (newvertices.Count > 0)
            {
                Vertex prevvertextag = newvertices[newvertices.Count - 1];
                foreach (Vertex vertex in newvertices)
                {
                    Vertex vertextag = vertex;
                    if (vertextag.Pos != prevvertextag.Pos || vertextag.Tex != prevvertextag.Tex)
                    {
                        newvertices_dedup[ArrayNewVertexIndex++] = vertex;
                    }
                    prevvertextag = vertextag;
                }
            }
            // If it's degenerate, remove all vertices:
            if (ArrayNewVertexIndex < 3)
            {
                return new Polygon(sourcepolygon.PolygonColor, new Vertex[0], newplane);
            }
            return new Polygon(sourcepolygon.PolygonColor, newvertices_dedup.Take(ArrayNewVertexIndex).ToArray(), newplane);
        }

        public Solid GetCsg(Solid sourcecsg)
        {
            List<Polygon> newpolygons = new List<Polygon>();
            foreach (Polygon polygon in sourcecsg.Polygons)
            {
                Polygon newpolygon = GetPolygon(polygon);
                if (newpolygon.Vertices.Length >= 3)
                {
                    newpolygons.Add(newpolygon);
                }
            }
            return Solid.FromPolygons(newpolygons);
        }
    }
}
