using System.Collections.Generic;
using UnityEngine;

public class ChaikinCourbe : MonoBehaviour
{
   
    public List<Transform> controlPoints = new();

    [Range(0, 8)] public int iterations = 3;
    public bool closed = true;

 
    public bool drawControlPolygon = true;
    public bool drawSubdividedCurve = true;

    private void OnDrawGizmos()
    {
        if (controlPoints == null || controlPoints.Count < 2) return;

        var pts = new List<Vector3>(controlPoints.Count);
        foreach (var t in controlPoints)
            if (t != null) pts.Add(t.position);

        if (pts.Count < 2) return;

        if (drawControlPolygon)
        {
            Gizmos.color = Color.yellow;
            DrawPolyline(pts, closed);
        }

        if (!drawSubdividedCurve) return;

        var refined = Chaikin(pts, iterations, closed);

        Gizmos.color = Color.cyan;
        DrawPolyline(refined, closed);
    }

    static void DrawPolyline(List<Vector3> pts, bool closed)
    {
        for (int i = 0; i < pts.Count - 1; i++)
            Gizmos.DrawLine(pts[i], pts[i + 1]);

        if (closed && pts.Count > 2)
            Gizmos.DrawLine(pts[^1], pts[0]);
    }

    static List<Vector3> Chaikin(List<Vector3> input, int iters, bool closed)
    {
        var current = new List<Vector3>(input);

        for (int k = 0; k < iters; k++)
        {
            var next = new List<Vector3>();

            int n = current.Count;
            int last = closed ? n : n - 1;

            if (!closed)
                next.Add(current[0]); 

            for (int i = 0; i < last; i++)
            {
                Vector3 p0 = current[i];
                Vector3 p1 = current[(i + 1) % n];


                Vector3 q = 0.75f * p0 + 0.25f * p1;
                Vector3 r = 0.25f * p0 + 0.75f * p1;

                next.Add(q);
                next.Add(r);
            }

            if (!closed)
                next.Add(current[^1]); 

            current = next;
        }

        return current;
    }
}
