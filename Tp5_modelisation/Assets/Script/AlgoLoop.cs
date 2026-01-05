using System;
using System.Collections.Generic;
using UnityEngine;

public class AlgoLoop : MonoBehaviour
{
    public MeshFilter meshFilter;
    [Range(0, 4)] public int subdivisions = 1;

    [ContextMenu("Apply Loop Subdivision (overwrite mesh)")]
    public void ApplyOnce()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        var m = Instantiate(meshFilter.sharedMesh);
        m.name = meshFilter.sharedMesh.name + "_LoopSubdiv";

        for (int i = 0; i < subdivisions; i++)
            m = SubdivideLoop(m);

        meshFilter.sharedMesh = m;
    }

    struct EdgeKey : IEquatable<EdgeKey>
    {
        public int a, b;
        public EdgeKey(int i0, int i1)
        {
            if (i0 < i1) { a = i0; b = i1; }
            else { a = i1; b = i0; }
        }
        public bool Equals(EdgeKey other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => (a * 73856093) ^ (b * 19349663);
    }

    class EdgeInfo
    {
        public int v0, v1;          
        public int opp0 = -1, opp1 = -1;
        public int newIndex = -1;   
    }

    static Mesh SubdivideLoop(Mesh mesh)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;


        var edges = new Dictionary<EdgeKey, EdgeInfo>(tris.Length);
        var neighbors = new List<HashSet<int>>(verts.Length);
        for (int i = 0; i < verts.Length; i++) neighbors.Add(new HashSet<int>());

        void AddNeighbor(int i0, int i1)
        {
            neighbors[i0].Add(i1);
            neighbors[i1].Add(i0);
        }

        void RegisterEdge(int a, int b, int opp)
        {
            var key = new EdgeKey(a, b);
            if (!edges.TryGetValue(key, out var info))
            {
                info = new EdgeInfo { v0 = key.a, v1 = key.b, opp0 = opp, opp1 = -1 };
                edges[key] = info;
            }
            else
            {

                if (info.opp1 == -1) info.opp1 = opp;
            }
        }

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t];
            int i1 = tris[t + 1];
            int i2 = tris[t + 2];

            AddNeighbor(i0, i1);
            AddNeighbor(i1, i2);
            AddNeighbor(i2, i0);


            RegisterEdge(i0, i1, i2);

            RegisterEdge(i1, i2, i0);
            RegisterEdge(i2, i0, i1);
        }

        var newVerts = new List<Vector3>(verts.Length + edges.Count);

        for (int i = 0; i < verts.Length; i++)
        {
            int n = neighbors[i].Count; 
            if (n < 3)
            {
         
                newVerts.Add(verts[i]);
                continue;
            }

            float alpha = Alpha(n);
            Vector3 sum = Vector3.zero;
            foreach (int nb in neighbors[i]) sum += verts[nb];

            Vector3 vNew = (1f - n * alpha) * verts[i] + alpha * sum; 
            newVerts.Add(vNew);
        }

        foreach (var kv in edges)
        {
            var e = kv.Value;
            Vector3 v0 = verts[e.v0];
            Vector3 v1 = verts[e.v1];

            Vector3 eNew;

            bool isBoundary = (e.opp0 == -1 || e.opp1 == -1);
            if (isBoundary)
            {
                eNew = 0.5f * (v0 + v1);
            }
            else
            {
                Vector3 vl = verts[e.opp0];
                Vector3 vr = verts[e.opp1];
                eNew = (3f / 8f) * (v0 + v1) + (1f / 8f) * (vl + vr); 
            }

            e.newIndex = newVerts.Count;
            newVerts.Add(eNew);
        }


        int EdgePoint(int a, int b) => edges[new EdgeKey(a, b)].newIndex;

        var newTris = new List<int>(tris.Length * 4);

        for (int t = 0; t < tris.Length; t += 3)
        {
            int x1 = tris[t];
            int x2 = tris[t + 1];
            int x3 = tris[t + 2];

            int x1x2 = EdgePoint(x1, x2);
            int x2x3 = EdgePoint(x2, x3);
            int x3x1 = EdgePoint(x3, x1);

            newTris.Add(x1); newTris.Add(x1x2); newTris.Add(x3x1);
            newTris.Add(x2); newTris.Add(x2x3); newTris.Add(x1x2);
            newTris.Add(x3); newTris.Add(x3x1); newTris.Add(x2x3);
            newTris.Add(x1x2); newTris.Add(x2x3); newTris.Add(x3x1);
        }

        var outMesh = new Mesh();
        outMesh.indexFormat = (newVerts.Count > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        outMesh.vertices = newVerts.ToArray();
        outMesh.triangles = newTris.ToArray();
        outMesh.RecalculateNormals();
        outMesh.RecalculateBounds();

        return outMesh;
    }

    static float Alpha(int n)
    {
        if (n == 3) return 3f / 16f;

        float nn = n;
        float c = Mathf.Cos(2f * Mathf.PI / nn);
        float term = (3f / 8f) + (1f / 4f) * c;
        return (1f / nn) * (5f / 8f - term * term);
    }
}
