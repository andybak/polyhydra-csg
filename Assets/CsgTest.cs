using System;
using System.Collections.Generic;
using System.Linq;
using Csg;
using Polyhydra.Core;
using UnityEngine;
using Vertex = Csg.Vertex;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CsgTest : TestBase
{

    [Serializable]
    public enum CsgOps
    {
        Union,
        Subtract,
        SubtractAlt,
        Intersect,        
    }
    
    [Header("Shape 1 Parameters")]
    public RadialSolids.RadialPolyType Shape1Type;
    [Range(3, 64)] public int Shape1Sides = 3;
    public Vector3 Shape1Position;
    public Vector3 Shape1Rotation;
    public float Shape1Scale = 1;
    public Vector3 Shape1NUScale = Vector3.one;
    
    [Header("Shape 2 Parameters")]
    public RadialSolids.RadialPolyType Shape2Type;
    [Range(3, 64)] public int Shape2Sides = 5;
    public Vector3 Shape2Position;
    public Vector3 Shape2Rotation;
    public float Shape2Scale = 1;
    public Vector3 Shape2NUScale = Vector3.one;
    
    [Header("CSG")] public CsgOps CsgOp;
    public bool Weld;
    public bool MergeFaces;
    public override void Go()
    {
        var vertices = new List<Vector3>();
        var faces = new List<List<int>>();
        
        var poly1 = FromPoly(RadialSolids.Build(Shape1Type, Shape1Sides), Shape1Position, Shape1Rotation, Shape1NUScale * Shape1Scale);
        var poly2 = FromPoly(RadialSolids.Build(Shape2Type, Shape2Sides), Shape2Position, Shape2Rotation, Shape2NUScale * Shape2Scale);
        
        Solid result = null;
        
        switch (CsgOp)
        {
            case CsgOps.Union:
                result = poly1.Union(poly2);
                break;
            case CsgOps.Subtract:
                result = poly1.Substract(poly2);
                break;
            case CsgOps.SubtractAlt:
                result = poly2.Substract(poly1);
                break;
            case CsgOps.Intersect:
                result = poly1.Intersect(poly2);
                break;
        }

        for (var i = 0; i < result.Polygons.Count; i++)
        {
            var poly = result.Polygons[i];
            int firstVertIndex = vertices.Count;
            foreach (var v in poly.Vertices)
            {
                vertices.Add(new Vector3((float)v.Pos.x, (float)v.Pos.y, (float)v.Pos.z));
            }
            faces.Add(Enumerable.Range(firstVertIndex, poly.Vertices.Count).ToList());
        }

        poly = new PolyMesh(vertices, faces);
        if (Weld)
        {
            poly = poly.Weld(.01f);
        }

        if (MergeFaces)
        {
            poly.MergeCoplanarFaces(.1f);
        }
        Build();
    }
    
    static Vertex NoTexVertex (Vector3 pos) => new Vertex (pos, new Vector2 (0, 0));

    public Solid FromPoly(PolyMesh poly, Vector3 pos, Vector3 rot, Vector3 scale)
    {
        List<Polygon> polygons = new List<Polygon>();
        foreach (var face in poly.Faces)
        {
            Polygon polygon = new Polygon(
                face.GetVertices().Select(v=>
                {
                    return NoTexVertex(new Vector3(
                        v.Position.x + pos.x,
                        v.Position.y + pos.y,
                        v.Position.z + pos.z
                    ));
                }).ToArray());
            polygons.Add(polygon);
        }
        var result = Solid.FromPolygons(polygons);
        result = result.Transform(Matrix4x4.Rotate(Quaternion.Euler(rot.x, rot.y, rot.z)));
        result = result.Transform(Matrix4x4.Scale(new Vector3(scale.x, scale.y, scale.z)));
        return result;
    }
}
