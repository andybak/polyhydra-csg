using System.Collections.Generic;
using UnityEngine;

namespace Csg
{
    public class Mesh
    {
        public List<int[]> triangles;
        public List<Vector3> vertices;
        public List<List<double>> colors;

        public Mesh()
        {
        }
    }
}
