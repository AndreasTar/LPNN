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
    private List<Vector3Int> epIndexes;
    [NonSerialized] public Vector3Int voxelAmountperDir;

    private Dictionary<Vector3, float[][]> features; // = {vec3i, float[f1, f2]}

    System.Diagnostics.Stopwatch stopwatch = new();


    private void Awake()
    {
        // on awake, we need to remove the object itself and only keep the light probes        
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
            Debug.LogError("No bounding volumes found. Make sure to add them.");
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
            Debug.LogError("Bounding volume is zero! Did you set the shape properly?"); // TODO info message on inspector instead?
            return;
        }
        evalPoints ??= new List<Vector3>();
        evalPoints.Clear();

        epIndexes ??= new List<Vector3Int>();
        epIndexes.Clear();

        stopwatch.Restart();
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

        voxelAmountperDir = voxelAmount;

        int xi=0, yi=0, zi=0;
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
                    epIndexes.Add(new Vector3Int(xi, yi, zi));

                    zi ++;
                }
                zi = 0;
                yi ++;
            }
            yi = 0;
            xi ++;
        }
        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public void BakeGI() {
        Lightmapping.BakeAsync();
    }

    public void CalculateFeatures() {
        if (evalPoints == null || evalPoints.Count == 0) {
            Debug.LogError("No evaluation points found! Please place them first!");
            return;
        }

        features ??= new Dictionary<Vector3, float[][]>();
        features.Clear();

        stopwatch.Restart();
        foreach (var p in evalPoints) {
            features.Add(p, new float[5][]);

            features[p][0] = EvaluateSH(p);
            features[p][1] = CalcLightVar(p);
            features[p][2] = CalcNormalVar(p);
            features[p][3] = CalcOcclFactor(p);
            features[p][4] = CalcRGBVar(p);

        }

        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public void SaveFeatures(bool append = false) {
        stopwatch.Restart();
        if (features == null || features.Count == 0) {
            Debug.LogError("No features found! Please calculate them first!");
            return;
        }

        if (!Utils.WriteFeaturesToFile(features, append)) {
            Debug.LogError("Failed to save features to file!");
            return;
        }
        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
        Debug.Log($"Results saved to {Utils.filepath+"features.txt"}. Total: {features.Count} points.");
    }

    float[] EvaluateSH(Vector3 point) {

        Color [] c = new Color[6];
        SphericalHarmonicsL2 sh = new();

        LightProbes.GetInterpolatedProbe(point, null, out sh);
        sh.Evaluate(Utils.FixedDirections, c);

        float[] temp = new float[24];
        for(int i = 0; i < 6; i++){
            temp[i*4] = c[i].r;
            temp[i*4+1] = c[i].g;
            temp[i*4+2] = c[i].b;
            temp[i*4+3] = c[i].a;
        }
        return temp;
    }

    float[] CalcLightVar(Vector3 point) {

        float sampleRadius = 1f;
        int sampleCount = 10;
        List<float> luminances = new();

        for (int i = 0; i < sampleCount; i++){

            Vector3 offset = UnityEngine.Random.onUnitSphere * sampleRadius;
            Vector3 samplePos = point + offset;

            LightProbes.GetInterpolatedProbe(samplePos, null, out SphericalHarmonicsL2 sh);

            Color[] col = new Color[1];
            Vector3[] dirs = new Vector3[]{UnityEngine.Random.onUnitSphere};

            sh.Evaluate(dirs.ToArray(), col); // sample in up direction or multiple if needed
            float luminance = col[0].r * 0.2126f + col[0].g * 0.7152f + col[0].b * 0.0722f;
            
            luminances.Add(luminance);
        }

        float mean = 0f;
        foreach (var lum in luminances) mean += lum;
        mean /= sampleCount;

        float variance = 0f;
        foreach (var lum in luminances) variance += (lum - mean) * (lum - mean);
        variance /= sampleCount;

        return new float[]{variance};
    }

    float[] CalcNormalVar(Vector3 point) {

        float sampleRadius = 1f;
        int sampleCount = 10;
        float rayDistance = 5f;

        List<Vector3> normals = new();

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            Ray ray = new(point + dir * sampleRadius, -dir);

            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                normals.Add(hit.normal);
            }
        }
        
        if (normals.Count == 0) return new float[]{0f};

        Vector3 meanNormal = Vector3.zero;
        foreach (var n in normals) meanNormal += n;
        meanNormal.Normalize();

        float variance = 0f;
        foreach (var n in normals) variance += 1f - Vector3.Dot(n, meanNormal);
        variance /= normals.Count;

        return new float[]{variance}; // Higher value -> more complex local geometry
    }

    float[] CalcOcclFactor(Vector3 point) {

        int rayCount = 50;
        float rayDistance = 10f;

        int hitCount = 0;

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            if (Physics.Raycast(point, dir, rayDistance))
            {
                hitCount++;
            }
        }

        float occlusionFactor = (float)hitCount / rayCount;

        return new float[]{occlusionFactor};
    }

    float[] CalcRGBVar(Vector3 point) {

        float sampleRadius = 1f;
        int sampleCount = 10;
        List<Color> colors = new();

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 offset = UnityEngine.Random.onUnitSphere * sampleRadius;
            Vector3 samplePos = point + offset;

            LightProbes.GetInterpolatedProbe(samplePos, null, out SphericalHarmonicsL2 sh);

            Color[] col = new Color[1];
            Vector3[] dirs = new Vector3[]{UnityEngine.Random.onUnitSphere};

            sh.Evaluate(dirs.ToArray(), col); // sample in up direction or multiple if needed
            colors.Add(col[0]);
        }

        float meanR = 0f, meanG = 0f, meanB = 0f;
        foreach (var c in colors) {
            meanR += c.r;
            meanG += c.g;
            meanB += c.b;
        }
        meanR /= sampleCount;
        meanG /= sampleCount;
        meanB /= sampleCount;

        float varianceR = 0f, varianceG = 0f, varianceB = 0f;
        foreach (var c in colors) {
            varianceR += (c.r - meanR) * (c.r - meanR);
            varianceG += (c.g - meanG) * (c.g - meanG);
            varianceB += (c.b - meanB) * (c.b - meanB);
        }
        varianceR /= sampleCount;
        varianceG /= sampleCount;
        varianceB /= sampleCount;

        return new float[]{varianceR, varianceG, varianceB};
    }

    public void CalculateLabels(bool append = false) {
        if (predef_lightProbes == null) {
            Debug.LogError("No LightProbeGroup found! Please assign one!");
            return;
        }

        CompareLPGroup(append);
    }
    void CompareLPGroup(bool append = false) {
        if (predef_lightProbes == null) {
            Debug.LogError("No LightProbeGroup found! Please assign one!");
            return;
        }

        Vector3[] pLP_positions = predef_lightProbes.probePositions;
        string s = "";
        string destination = Application.dataPath + "/LPNN/Results/comparisons.txt";

        Debug.Log($"{evalPoints.Count} {pLP_positions.Length}");

        stopwatch.Restart();

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

        if (append) {
            File.AppendAllText(destination, s);
        } else {
            File.WriteAllText(destination, s);
        }
        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
        Debug.Log($"Results saved to {destination}. Total: {evalPoints.Count} lines. {count} Trues, {evalPoints.Count - count} Falses.");
    }

    public void EvaluateModel() {
        LightProbeAI modelScript = GetComponent<LightProbeAI>();

        stopwatch.Restart();
        float[] res;
        try
        {
            res = modelScript.Predict(features);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Model evaluation failed: {e.Message}");
            return;
        }
        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
        Debug.Log($"Model evaluated. Result: {res.Length} values.");
        string destination = Application.dataPath + "/LPNN/Results/model_evals.txt";
        File.WriteAllText(destination, res.ToLineSeparatedString());
        Debug.Log($"Results saved to {destination}. Total: {res.Length} lines.");
    }

    public void PlacePredictions(float threshold){
        
        gameObject.GetOrAddComponent<LightProbeGroup>();

        List<Vector3> positions = new();

        stopwatch.Restart();

        string[] predictions = File.ReadAllLines(Application.dataPath + "/LPNN/Results/model_evals.txt"); 
        List<float> pred = new();
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var p in predictions.ToList()) {
            float val = float.Parse(p);
            if (val < min) min = val;
            if (val > max) max = val;
            pred.Add(val);
        }

        for (int i = 0; i < pred.Count; i++){
            pred[i] = Utils.map(pred[i], min, max, 0, 1);
        }
        
        for (int i = 0; i < pred.Count; i++){
            if (pred[i] > threshold) {
                positions.Add(evalPoints[i]);
            }
        }
        gameObject.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
        stopwatch.Stop();
        Debug.Log($"Finished in {stopwatch.ElapsedMilliseconds} ms.");
        Debug.Log($"Placed {positions.Count} predicted light probes.");
        Debug.Log($"Min: {min}, Max: {max}, Average: {min+pred.Average()}");
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
        // TODO add chrominance too with luminance
        // TODO rework the tooltip layout

        base.OnInspectorGUI();
        LPNN_script lpnn = (LPNN_script)target;

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

        EditorGUILayout.LabelField($"Voxel Count: x: {lpnn.voxelAmountperDir.x}, y: {lpnn.voxelAmountperDir.y}, z: {lpnn.voxelAmountperDir.z} Total: {lpnn.voxelAmountperDir.x * lpnn.voxelAmountperDir.y * lpnn.voxelAmountperDir.z}");

        if (GUILayout.Button("Place Evaluation Points")) {
            lpnn.PlaceEvalPoints();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Bake Global Illumination")) {
            lpnn.BakeGI();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Get Features")) {
            lpnn.CalculateFeatures();
            Debug.Log("Evaluated");
        }

        if (GUILayout.Button("Save Features to File")) {
            lpnn.SaveFeatures();
            Debug.Log("Saved");
        }

        if (GUILayout.Button("Append Features to File")) {
            lpnn.SaveFeatures(true);
            Debug.Log("Appended");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Get Labels & Save")) {
            lpnn.CalculateLabels();
        }
        if (GUILayout.Button("Get Labels & Append")) {
            lpnn.CalculateLabels(true);
        }


        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        if (GUILayout.Button("Evaluate with model")) {
            lpnn.EvaluateModel();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        threshold = EditorGUILayout.Slider(threshold, 0f, 1f);

        if (GUILayout.Button("Place Predicted LP")) {
            lpnn.PlacePredictions(threshold);
        }
    }
}

#endif
#endregion