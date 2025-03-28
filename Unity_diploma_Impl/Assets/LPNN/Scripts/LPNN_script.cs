using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

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

    private List<Bounds> boundingVolumes;
    private List<Vector3> evalPoints;


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

            Gizmos.color = Color.magenta;
            foreach (var bound in boundingVolumes)
            {
                Gizmos.DrawWireCube(bound.center, bound.size);
            }
            Gizmos.color = Color.yellow;
            foreach (var point in evalPoints)
            {
                Gizmos.DrawSphere(point, 0.05f);
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

    private bool IsPointInsideBounds(Vector3 point) {
        foreach (var bound in boundingVolumes)
        {
            if (bound.Contains(point) || bound.Intersects(new Bounds(point, new Vector3(voxelSize,voxelSize,voxelSize)))) return true;
        }
        return false;
    }

    public void PlaceVoxels() {

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

        // create the voxels
        for (int x = 0; x < voxelAmount.x; x++) {
            for (int y = 0; y < voxelAmount.y; y++) {
                for (int z = 0; z < voxelAmount.z; z++) {
                    var point = new Vector3(
                        bounds.min.x + x * voxelSize,
                        bounds.min.y + y * voxelSize,
                        bounds.min.z + z * voxelSize
                    );
                    if (!IsPointInsideBounds(point)) continue;
                    evalPoints.Add(point);
                    
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

}

#region inspectorstuff
#if UNITY_EDITOR

[CustomEditor(typeof(LPNN_script))]
public class LPNN_Inspector: Editor {
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
            if (size > 0.1f) lpnn.PlaceVoxels();
        }

        if (GUILayout.Button("Place Voxels")) {
            lpnn.PlaceVoxels();
            Debug.Log("Voxels placed");
        }
    }
}

#endif
#endregion