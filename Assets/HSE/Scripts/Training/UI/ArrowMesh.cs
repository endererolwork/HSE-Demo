using System.Collections.Generic;
using UnityEngine;

namespace HSE.Training.UI
{
    /// <summary>Procedural 3D arrow (round shaft + cone head) pointing along +Z, tip at the origin.</summary>
    public static class ArrowMesh
    {
        public static Mesh Create(float shaftRadius = 0.012f, float shaftLength = 0.07f, float headRadius = 0.03f, float headLength = 0.05f, int segments = 20)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            float total = shaftLength + headLength;

            // Shaft side (z from -total to -headLength).
            Ring(v, n, tri, shaftRadius, -total, shaftRadius, -headLength, segments);
            // Shaft back cap.
            Disc(v, n, tri, shaftRadius, -total, Vector3.back, segments);
            // Head underside (annulus from shaft to head radius) and cone.
            Disc(v, n, tri, headRadius, -headLength, Vector3.back, segments);
            Ring(v, n, tri, headRadius, -headLength, 0f, 0f, segments);

            var mesh = new Mesh { name = "GuideArrow" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Ring(List<Vector3> v, List<Vector3> n, List<int> tri, float r0, float z0, float r1, float z1, int seg)
        {
            int start = v.Count;
            float slope = (r0 - r1) / Mathf.Max(1e-5f, z1 - z0);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var normal = (dir + Vector3.forward * slope).normalized;
                v.Add(dir * r0 + Vector3.forward * z0); n.Add(normal);
                v.Add(dir * r1 + Vector3.forward * z1); n.Add(normal);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
        }

        static void Disc(List<Vector3> v, List<Vector3> n, List<int> tri, float r, float z, Vector3 normal, int seg)
        {
            int center = v.Count;
            v.Add(Vector3.forward * z); n.Add(normal);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z)); n.Add(normal);
            }
            for (int i = 0; i < seg; i++) tri.AddRange(new[] { center, center + 2 + i, center + 1 + i });
        }
    }
}
