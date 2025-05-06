using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Utils {


    
    public static float map(float value, float from1, float to1, float from2, float to2) {
        return from2 + (to2 - from2) * ((value - from1) / (to1 - from1));
    }

    public static readonly Vector3[] FixedDirections = new Vector3[] {
            new( 1.0f, 0.0f, 0.0f),
            new(-1.0f, 0.0f, 0.0f),
            new( 0.0f, 1.0f, 0.0f),
            new( 0.0f,-1.0f, 0.0f),
            new( 0.0f, 0.0f, 1.0f),
            new( 0.0f, 0.0f,-1.0f)
    };

    public static bool IsPointInsideBounds(Vector3 point, ref List<Bounds> bounds, float voxelSize) {
        foreach (var bound in bounds)
        {
            if (IsPointInsideBound(point, bound, voxelSize)) return true;
        }
        return false;
    }

    public static bool IsPointInsideBound(Vector3 point, Bounds bound, float voxelSize) {
        return bound.Contains(point) || bound.Intersects(new Bounds(point, new Vector3(voxelSize,voxelSize,voxelSize)));
    }

    public static bool IsPointInsideVoxel(Vector3 pointCenter, Vector3 pointTo, float voxelSize) {
        Bounds bound = new(pointCenter, new Vector3(voxelSize,voxelSize,voxelSize));
        return bound.Contains(pointTo);
    }

    public static void GenerateUniformSphereSampling(out List<Vector3> directions, int numDirections) {
        directions = new List<Vector3>(numDirections);
        for (int i = 0; i < numDirections; i++) {
            Vector2 r = new(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
            float phi = r.x * 2.0f * Mathf.PI;
            float cosTheta = 1.0f - 2.0f * r.y;
            float sinTheta = Mathf.Sqrt(1.0f - cosTheta * cosTheta);
            Vector3 vec = new(Mathf.Cos(phi) * sinTheta, Mathf.Sin(phi) * sinTheta, cosTheta);
            vec.Normalize();
            directions.Add(vec);
        }
    }

    public static readonly string filepath = Application.dataPath + "/LPNN/Results/";
    public static bool WriteFeaturesToFile(Dictionary<Vector3, float[][]> features, Vector3Int dims) {

        File.WriteAllText(filepath+"features.txt", $"{dims.x} {dims.y} {dims.z} {features.First().Value.SelectMany(x => x).ToArray().Length}\n"); // Clear the file

        foreach (var kvp in features)
        {
            Vector3 key = kvp.Key; // May be needed
            string block = "";
            foreach (var f in kvp.Value)
            {
                block += f.ToSeparatedString(" ");
                block += "\n";
            }
            block += "\n";
            File.AppendAllText(filepath+"features.txt", block);
        }

        return true;        
    }
}
