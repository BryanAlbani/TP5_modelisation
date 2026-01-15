using System;
using System.Collections.Generic;
using UnityEngine;

public class AlgoLoop : MonoBehaviour
{
    [Range(1, 6)] public int iterations = 1;

    void Start()
    {

            Apply(iterations);
    }

    // Ici, on récupere le mesh, on fussionne les sommets dupliqué et on applique la subdivision un nombre de fois égale à iters
    public void Apply(int iters)
    {
        var mf = GetComponent<MeshFilter>();
        if (!mf || !mf.sharedMesh) return;


        Mesh mesh = Instantiate(mf.sharedMesh);
        mesh.name = mf.sharedMesh.name + "_LoopSubdiv";

        mesh = WeldVertices(mesh, 1e-6f);
        for (int i = 0; i < iters; i++)
            mesh = SubdivideOnce(mesh);

        mf.sharedMesh = mesh;
    }

    // Une struct pour identifier une arete et la stocker dans un dictionnaire
    struct EdgeKey : IEquatable<EdgeKey>
    {
        public int a, b; 
        public EdgeKey(int v0, int v1)
        {
            if (v0 < v1) { a = v0; b = v1; }
            else { a = v1; b = v0; }
        }
        public bool Equals(EdgeKey other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (a * 73856093) ^ (b * 19349663); }
        }
    }

    // Struct contenant les info topologiques d'une arete
    class EdgeInfo
    {
        public int v0, v1;     
        public int opp0 = -1;  
        public int opp1 = -1;  
        public int newIndex = -1;
        public bool IsBoundary => opp1 == -1;
    }

    //Ici , les sommets identiques sont fusionnés
    static Mesh WeldVertices(Mesh mesh, float tolerance = 1e-6f)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var uv = mesh.uv;
        bool hasUV = uv != null && uv.Length == verts.Length;

    
        int Quant(float x) => Mathf.RoundToInt(x / tolerance);

        var map = new Dictionary<(int, int, int), int>(verts.Length);
        var newVerts = new List<Vector3>(verts.Length);
        var newUV = hasUV ? new List<Vector2>(verts.Length) : null;
        var remap = new int[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];
            var key = (Quant(p.x), Quant(p.y), Quant(p.z));

            if (!map.TryGetValue(key, out int newIndex))
            {
                newIndex = newVerts.Count;
                map[key] = newIndex;
                newVerts.Add(p);
                if (hasUV) newUV!.Add(uv[i]);
            }

            remap[i] = newIndex;
        }


        var newTris = new int[tris.Length];
        for (int i = 0; i < tris.Length; i++)
            newTris[i] = remap[tris[i]];

        var outMesh = new Mesh();
        outMesh.indexFormat = (newVerts.Count > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        outMesh.vertices = newVerts.ToArray();
        outMesh.triangles = newTris;
        if (hasUV) outMesh.uv = newUV!.ToArray();

        outMesh.RecalculateBounds();
        outMesh.RecalculateNormals();
        return outMesh;
    }


    // Applique une itération de la subdivision, calcule des nouveaux points, maj  des positions de sommets, création des nouvelles faces
    static Mesh SubdivideOnce(Mesh mesh)
    {
        var oldVerts = mesh.vertices;
        var oldTris = mesh.triangles;
        var oldUV = mesh.uv;
        bool hasUV = oldUV != null && oldUV.Length == oldVerts.Length;

        int vCount = oldVerts.Length;

  
        var neighbors = new List<HashSet<int>>(vCount);
        for (int i = 0; i < vCount; i++) neighbors.Add(new HashSet<int>());

        var edges = new Dictionary<EdgeKey, EdgeInfo>(oldTris.Length);

        void AddEdge(int a, int b, int opp)
        {
            var key = new EdgeKey(a, b);
            if (!edges.TryGetValue(key, out var info))
            {
                info = new EdgeInfo { v0 = key.a, v1 = key.b, opp0 = opp, opp1 = -1 };
                edges.Add(key, info);
            }
            else
            {
              
                if (info.opp1 == -1) info.opp1 = opp;
               
            }
        }

        for (int t = 0; t < oldTris.Length; t += 3)
        {
            int a = oldTris[t];
            int b = oldTris[t + 1];
            int c = oldTris[t + 2];

            neighbors[a].Add(b); neighbors[a].Add(c);
            neighbors[b].Add(a); neighbors[b].Add(c);
            neighbors[c].Add(a); neighbors[c].Add(b);

            AddEdge(a, b, c);
            AddEdge(b, c, a);
            AddEdge(c, a, b);
        }

       
        var isBoundaryVertex = new bool[vCount];
        var boundaryNeighbors = new List<int>[vCount];
        for (int i = 0; i < vCount; i++) boundaryNeighbors[i] = new List<int>(2);

        foreach (var kv in edges)
        {
            var e = kv.Value;
            if (e.IsBoundary)
            {
                isBoundaryVertex[e.v0] = true;
                isBoundaryVertex[e.v1] = true;
                boundaryNeighbors[e.v0].Add(e.v1);
                boundaryNeighbors[e.v1].Add(e.v0);
            }
        }

      
        var newVerts = new List<Vector3>(vCount + edges.Count);
        var newUV = hasUV ? new List<Vector2>(vCount + edges.Count) : null;

        
        for (int i = 0; i < vCount; i++)
        {
            Vector3 v = oldVerts[i];

            if (isBoundaryVertex[i])
            {
               
                if (boundaryNeighbors[i].Count >= 2)
                {
                    Vector3 n0 = oldVerts[boundaryNeighbors[i][0]];
                    Vector3 n1 = oldVerts[boundaryNeighbors[i][1]];
                    v = (3f / 4f) * v + (1f / 8f) * (n0 + n1);
                }
            }
            else
            {
                int n = neighbors[i].Count;
                float alpha = ComputeAlpha(n);
                Vector3 sum = Vector3.zero;
                foreach (int nb in neighbors[i]) sum += oldVerts[nb];
                v = (1f - n * alpha) * v + alpha * sum;
            }

            newVerts.Add(v);

            if (hasUV)
            {
                Vector2 uv = oldUV[i];
                if (isBoundaryVertex[i] && boundaryNeighbors[i].Count >= 2)
                {
                    Vector2 n0 = oldUV[boundaryNeighbors[i][0]];
                    Vector2 n1 = oldUV[boundaryNeighbors[i][1]];
                    uv = (3f / 4f) * uv + (1f / 8f) * (n0 + n1);
                }
                else if (!isBoundaryVertex[i])
                {
                    int n = neighbors[i].Count;
                    float alpha = ComputeAlpha(n);
                    Vector2 sum = Vector2.zero;
                    foreach (int nb in neighbors[i]) sum += oldUV[nb];
                    uv = (1f - n * alpha) * uv + alpha * sum;
                }
                newUV!.Add(uv);
            }
        }

        foreach (var kv in edges)
        {
            var e = kv.Value;
            Vector3 v0 = oldVerts[e.v0];
            Vector3 v1 = oldVerts[e.v1];

            Vector3 newPos;
            if (e.IsBoundary)
            {
             
                newPos = 0.5f * (v0 + v1);
            }
            else
            {
     
                Vector3 vl = oldVerts[e.opp0];
                Vector3 vr = oldVerts[e.opp1];
                newPos = (3f / 8f) * (v0 + v1) + (1f / 8f) * (vl + vr);
            }

            e.newIndex = newVerts.Count;
            newVerts.Add(newPos);

            if (hasUV)
            {
                Vector2 uv0 = oldUV[e.v0];
                Vector2 uv1 = oldUV[e.v1];
                Vector2 newUv;
                if (e.IsBoundary)
                {
                    newUv = 0.5f * (uv0 + uv1);
                }
                else
                {
                    Vector2 uvl = oldUV[e.opp0];
                    Vector2 uvr = oldUV[e.opp1];
                    newUv = (3f / 8f) * (uv0 + uv1) + (1f / 8f) * (uvl + uvr);
                }
                newUV!.Add(newUv);
            }
        }

        var newTris = new List<int>(oldTris.Length * 4);

        int GetEdgeNewIndex(int a, int b)
        {
            var key = new EdgeKey(a, b);
            return edges[key].newIndex;
        }

        for (int t = 0; t < oldTris.Length; t += 3)
        {
            int x1 = oldTris[t];
            int x2 = oldTris[t + 1];
            int x3 = oldTris[t + 2];

            int x12 = GetEdgeNewIndex(x1, x2);
            int x23 = GetEdgeNewIndex(x2, x3);
            int x31 = GetEdgeNewIndex(x3, x1);


            newTris.Add(x1); newTris.Add(x12); newTris.Add(x31);
            newTris.Add(x2); newTris.Add(x23); newTris.Add(x12);
            newTris.Add(x3); newTris.Add(x31); newTris.Add(x23);
            newTris.Add(x12); newTris.Add(x23); newTris.Add(x31);
        }

        var outMesh = new Mesh();
        outMesh.indexFormat = (newVerts.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        outMesh.vertices = newVerts.ToArray();
        outMesh.triangles = newTris.ToArray();
        if (hasUV) outMesh.uv = newUV!.ToArray();

        outMesh.RecalculateNormals();
        outMesh.RecalculateBounds();


        return outMesh;
    }

    //Calcule la valeur, pour le repositionnement des sommets
    static float ComputeAlpha(int n)
    {
        if (n <= 0) return 0f;
        if (n == 3) return 3f / 16f;

        float nn = n;
        float cos = Mathf.Cos(2f * Mathf.PI / nn);
        float term = (3f / 8f) + (1f / 4f) * cos;
        float alpha = (1f / nn) * (5f / 8f - term * term);
        return alpha;
    }
}
