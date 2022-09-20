using System.Collections.Generic;
using UnityEngine;

namespace Csg
{
    //https://github.com/jscad/OpenJSCAD.org/blob/master/packages/web/src/ui/viewer/jscad-viewer-lightgl.js
    public class Convertor
    {
        public static List<Mesh> csgToMeshesWithCache(Solid initial_csg)
        {
            Solid csg = initial_csg.Canonicalized();
            var mesh = new Mesh();
            var meshes = new List<Mesh>() { mesh };
            var vertexTag2Index = new List<Vertex>();
            var vertices = new List<Vector3>();
            var colors = new List<List<double>>();
            var triangles = new List<int[]>();
            // set to true if we want to use interpolated vertex normals
            // this creates nice round spheres but does not represent the shape of
            // the actual model
            var smoothlighting = true;
            List<Polygon> polygons = csg.Polygons;
            var numpolygons = polygons.Count;

            for (var j = 0; j < numpolygons; j++)
            {

                var polygon = polygons[j];
                List<double> color = new List<double>() { polygon.PolygonColor.r, polygon.PolygonColor.g, polygon.PolygonColor.b, polygon.PolygonColor.a };  // default color

                List<int> indices = new List<int>();

                foreach (Vertex vertex in polygon.Vertices)
                {

                    var vertextag = vertex;
                    int vertexindex = 0;
                    List<double> prevcolor = null;
                    if (vertexTag2Index.Contains(vertextag))
                    {

                        vertexindex = vertexTag2Index.IndexOf(vertextag);
                        prevcolor = colors[vertexindex];
                    }
                    if (smoothlighting && vertexTag2Index.Contains(vertextag) && prevcolor != null &&
                       (prevcolor[0] == color[0]) &&
                       (prevcolor[1] == color[1]) &&
                       (prevcolor[2] == color[2]))
                    {

                        vertexindex = vertexTag2Index.IndexOf(vertextag);
                    }
                    else
                    {

                        vertexindex = vertices.Count;
                        vertexTag2Index.Add(vertex);
                        vertices.Add(vertex.Pos);
                        colors.Add(color);
                    }
                    indices.Add(vertexindex);
                }
                for (var i = 2; i < indices.Count; i++)
                {
                    triangles.Add(new int[] { indices[0], indices[i - 1], indices[i] });
                }
                // if too many vertices, start a new mesh;
                if (vertices.Count > 65000)
                {
                    // finalize the old mesh
                    mesh.triangles = triangles;
                    mesh.vertices = vertices;
                    mesh.colors = colors;

                    if (mesh.vertices.Count > 0)
                    {
                        meshes.Add(mesh);
                    }

                    // start a new mesh
                    mesh = new Mesh();
                    triangles = new List<int[]>();
                    colors = new List<List<double>>();
                    vertices = new List<Vector3>();
                }
            }
            // finalize last mesh
            mesh.triangles = triangles;
            mesh.vertices = vertices;
            mesh.colors = colors;

            if (mesh.vertices.Count > 0)
            {
                meshes.Add(mesh);
            }

            return meshes;
        }
    }
}
