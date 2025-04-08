using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

public class LPNN_script : MonoBehaviour
{
    #region rightclickmenu
    // Add a menu item named "LPNN Object" to MyMenu in the menu bar.
    [MenuItem("GameObject/Light/LPNN Object", false)]
    static void CreateLPNNObject(MenuCommand menuCommand)
    {
        // Create a custom game object
        GameObject go = new("LPNN Parent");
        go.AddComponent<LPNN_script>();

        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }
    #endregion

    [NonSerialized] public Bounds bounds;
    [NonSerialized] public float voxelSize = 1f;
    [NonSerialized] public bool vis_bounds = true;

    public LightProbeGroup predef_lightProbes;

    private List<Bounds> boundingVolumes;
    private List<Vector3> evalPoints;

    private float[,,,,] features; // = new float[1, 11, 3, 9, 24]


    private void Awake()
    {
        // on awake, we need to remove the object itself and only keep the light probes

        // if list not empty
        //     get its bounding box
        //     return
        // if child exists
        //     get its bounding box
        //     return
        // error

        
    }

    void OnDrawGizmos()
    {
        if (vis_bounds) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (boundingVolumes != null) {
                Gizmos.color = Color.magenta;
                foreach (var bound in boundingVolumes){
                    Gizmos.DrawWireCube(bound.center, bound.size);
                }
            }
            if (evalPoints != null) {
                Gizmos.color = Color.yellow;
                foreach (var point in evalPoints){
                    Gizmos.DrawSphere(point, 0.05f);
                }
            }
            
        }
    }

    public void CalculateBoundingVolume() {

        boundingVolumes ??= new List<Bounds>();
        boundingVolumes.Clear();

        Transform boundingChildren;
        try
        {
            if ( boundingChildren = transform.Find("BoundingVolumes") ) {
                foreach (Transform child in boundingChildren){
                    if (child.TryGetComponent<Collider>(out var collider)){
                        boundingVolumes.Add(collider.bounds);
                    }
                }
            }
        }
        catch
        {
            Debug.LogError("No bounding volumes found");
        }

        bounds = boundingVolumes[0]; 
        foreach (var bound in boundingVolumes)
        {
            bounds.Encapsulate(bound);
        }

        HandleUtility.Repaint();

    }

    public void PlaceEvalPoints() {

        if (bounds.size == Vector3.zero) {
            Debug.LogError("Bounding volume is zero!"); // TODO info message on inspector instead?
            return;
        }
        evalPoints ??= new List<Vector3>();
        evalPoints.Clear();

        Transform voxelParent = transform.Find("VoxelsParent");
        if ( voxelParent != null) DestroyImmediate(voxelParent.gameObject);

        voxelParent = new GameObject("VoxelsParent").transform;
        voxelParent.parent = transform;

        // we add +1 and round so we wrap around the bounds even if we overshoot, its better
        Vector3Int voxelAmount = new(
            Mathf.RoundToInt((bounds.size.x+1) / voxelSize),
            Mathf.RoundToInt((bounds.size.y+1) / voxelSize),
            Mathf.RoundToInt((bounds.size.z+1) / voxelSize)
        );

        features = new float[1, voxelAmount.x, voxelAmount.y, voxelAmount.z, 24]; // TODO make this dynamic

        // create the voxels
        for (int x = 0; x < voxelAmount.x; x++) {
            for (int y = 0; y < voxelAmount.y; y++) {
                for (int z = 0; z < voxelAmount.z; z++) {
                    var point = new Vector3(
                        bounds.min.x + x * voxelSize,
                        bounds.min.y + y * voxelSize,
                        bounds.min.z + z * voxelSize
                    );
                    if (!Utils.IsPointInsideBounds(point, ref boundingVolumes, voxelSize)) continue;
                    evalPoints.Add(point);
                    
                    // TODO toggle to show voxels? is it needed?
                    // GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    // voxel.transform.position = point;
                    // voxel.GetComponent<MeshRenderer>().enabled = false;
                    // voxel.name = $"{x}_{y}_{z}";
                    // voxel.transform.localScale = new Vector3(voxelSize, voxelSize, voxelSize);
                    // voxel.transform.parent = voxelParent;
                }
            }
        }
    }

    public void EvaluateSH() {

        List<Color[]> results = new();
        if (evalPoints == null || evalPoints.Count == 0) {
            Debug.LogError("No evaluation points found! Please place them first.");
            return;
        }

        foreach (var p in evalPoints){
            Color [] c = new Color[6];
            SphericalHarmonicsL2 sh = new();

            LightProbes.GetInterpolatedProbe(p, null, out sh);
            sh.Evaluate(Utils.FixedDirections, c);
            results.Add(c);

            for(int i = 0; i < 24; i+=4){
                features[0, (int)p.x, (int)p.y, (int)p.z, i  ] = c[i].r;
                features[0, (int)p.x, (int)p.y, (int)p.z, i+1] = c[i].g;
                features[0, (int)p.x, (int)p.y, (int)p.z, i+2] = c[i].b;
                features[0, (int)p.x, (int)p.y, (int)p.z, i+3] = c[i].a;
            }

        }

        string destination = Application.dataPath + "/LPNN/Results/evals.txt";

        string s = "";
        foreach (var r in results){
            foreach (var c in r) {
                s += c.ToString("F4").Replace("RGBA(", "").Replace(")", "").Replace(",", " ");
                s += "\n";
            }
            s += "\n";
        }
        File.WriteAllText(destination, s);

        Debug.Log($"Results saved to {destination}. Total: {results.Count} points.");
        

    }

    public void CompareLPGroup() {
        if (predef_lightProbes == null) {
            Debug.LogError("No LightProbeGroup found! Please assign one.");
            return;
        }

        // TODO compare the results with the predefined light probes
        // TODO save the results to a file

        Vector3[] pLP_positions = predef_lightProbes.probePositions;
        string s = "";
        string destination = Application.dataPath + "/LPNN/Results/comparisons.txt";

        Debug.Log($"{evalPoints.Count} {pLP_positions.Length}");

        bool flag = false;
        int count = 0;
        foreach (var ep in evalPoints) {
            foreach (var p in pLP_positions) {
                if (Utils.IsPointInsideVoxel(p, ep, voxelSize)){
                    s += $"{true}\n";
                    flag = true;
                    count++;
                    break;
                }
            }

            if (!flag) {
                s += $"{false}\n";
            }
            flag = false;
        }

        File.WriteAllText(destination, s);
        Debug.Log($"Results saved to {destination}. Total: {evalPoints.Count} lines. {count} Trues, {evalPoints.Count - count} Falses.");
    }

    public void EvaluateModel() {
        LightProbeAI modelScript = GetComponent<LightProbeAI>();
        
        float[] res = modelScript.Predict(features);
        Debug.Log($"Model evaluated. Result: {res.Length} values.");
        string destination = Application.dataPath + "/LPNN/Results/model_evals.txt";
        File.WriteAllText(destination, res.ToLineSeparatedString());
        Debug.Log($"Results saved to {destination}. Total: {res.Length} lines.");
    }

    public void PlacePredictions(float threshold){
        
        gameObject.GetOrAddComponent<LightProbeGroup>();

        List<Vector3> positions = new();

        String[] predictions = File.ReadAllLines(Application.dataPath + "/LPNN/Results/model_evals.txt"); 
        List<float> pred = new();
        foreach (var p in predictions.ToList()) {
            Debug.Log($"{p} {float.Parse(p)}");
            pred.Add(float.Parse(p));
        }

        Debug.Log($"predicted {pred.Count} : {pred.ToLineSeparatedString()}");

        for (int i = 0; i < pred.Count; i++){
            if (pred[i] > threshold) {
                positions.Add(evalPoints[i]);
            }
        }
        gameObject.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
        Debug.Log($"Placed {positions.Count} predicted light probes.");
        
    }

}

#region inspectorstuff
#if UNITY_EDITOR

[CustomEditor(typeof(LPNN_script))]
public class LPNN_Inspector: Editor {

    private float threshold = 0.5f;

    public override void OnInspectorGUI()
    {

        // TODO add all the tooltips
        // TODO maybe make enum choice between box voxels and arbitrary voxels so we can have equal size vs equal amount per axis
        // TODO bounds is square, what if we want arbitrary size? like a Π shape or something?

        base.OnInspectorGUI();
        LPNN_script lpnn = (LPNN_script)target;
        EditorGUILayout.LabelField("This is a custom inspector");


        GUIContent content = new("Bounds", "The Bounds that will be used to place the evaluation volumes.");
        EditorGUILayout.BoundsField(content, lpnn.bounds); // HACK this may need to be stored

        content = new("Visualise Bounds", "Show a Rectangle in the Scene View that represents the Bounds.");

        bool toggle = EditorGUILayout.Toggle(content, lpnn.vis_bounds); // HACK same with this and everything tbh
        if (toggle != lpnn.vis_bounds) {
            lpnn.vis_bounds = toggle;
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Calculate Bounds")) {
            lpnn.CalculateBoundingVolume();
            Debug.Log("Bounds calculated");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        float size = EditorGUILayout.FloatField("Voxel Size (in meters)", lpnn.voxelSize);
        size = Mathf.Clamp(size, 0.05f, 100f);
        if (size != lpnn.voxelSize) {
            lpnn.voxelSize = size;
            serializedObject.ApplyModifiedProperties();
            if (size > 0.1f) lpnn.PlaceEvalPoints();
        }

        if (GUILayout.Button("Place Evaluation Points")) {
            lpnn.PlaceEvalPoints();
            Debug.Log("EVs placed");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Evaluate Spherical Harmonics")) {
            lpnn.EvaluateSH();
            Debug.Log("Evaluated");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Compare to Predefined LPGroup")) {
            lpnn.CompareLPGroup();
            Debug.Log("Compared");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Evaluate with model")) {
            lpnn.EvaluateModel();
            Debug.Log("evaluated with model");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        threshold = EditorGUILayout.Slider(threshold, 0f, 1f);

        if (GUILayout.Button("Place Predicted LP")) {
            lpnn.PlacePredictions(threshold);
            Debug.Log("placed lp");
        }
    }
}

#endif
#endregion