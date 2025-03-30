using System.Collections.Generic;
using UnityEngine;

public class Utils {

    public static readonly Vector3[] FixedDirections = new Vector3[] {
            new Vector3( 1.0f, 0.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3( 0.0f, 1.0f, 0.0f),
            new Vector3( 0.0f,-1.0f, 0.0f),
            new Vector3( 0.0f, 0.0f, 1.0f),
            new Vector3( 0.0f, 0.0f,-1.0f)
    };

    public static bool IsPointInsideBounds(Vector3 point, ref List<Bounds> bounds, float voxelSize) {
        foreach (var bound in bounds)
        {
            if (bound.Contains(point) || bound.Intersects(new Bounds(point, new Vector3(voxelSize,voxelSize,voxelSize)))) return true;
        }
        return false;
    }

    public static void GenerateUniformSphereSampling(out List<Vector3> directions, int numDirections) {
        directions = new List<Vector3>(numDirections);
        for (int i = 0; i < numDirections; i++) {
            Vector2 r = new(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
            float phi = r.x * 2.0f * Mathf.PI;
            float cosTheta = 1.0f - 2.0f * r.y;
            float sinTheta = Mathf.Sqrt(1.0f - cosTheta * cosTheta);
            Vector3 vec = new(Mathf.Cos(phi) * sinTheta, Mathf.Sin(phi) * sinTheta, cosTheta);
            vec.Normalize();
            directions.Add(vec);
        }
    }
}
