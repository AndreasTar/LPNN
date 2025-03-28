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

    [SerializeField] private List<Bounds> boundingVolumes;
    [NonSerialized] public Bounds bounds;
    [NonSerialized] public float voxelSize = 1;
    [NonSerialized] public bool vis_bounds = false;


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

    void OnDrawGizmosSelected()
    {
        if (vis_bounds) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
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

    }

    public void PlaceVoxels() {

        if (bounds.size == Vector3.zero) {
            Debug.LogError("Bounding volume is zero!"); // TODO info message on inspector instead?
            return;
        }

        Transform voxelParent = transform.Find("VoxelsParent");
        if ( voxelParent != null) DestroyImmediate(voxelParent.gameObject);

        voxelParent = new GameObject("VoxelsParent").transform;
        voxelParent.parent = transform;

        // calculate the amount of voxels needed
        Vector3Int voxelAmount = new(
            Mathf.RoundToInt(bounds.size.x+1 / voxelSize),
            Mathf.RoundToInt(bounds.size.y+1 / voxelSize),
            Mathf.RoundToInt(bounds.size.z+1 / voxelSize)
        );

        // calculate the offset needed to center the voxels
        // Vector3 offset = new(
        //     bounds.size.x / 2 - voxelAmount.x * voxelSize / 2,
        //     bounds.size.y / 2 - voxelAmount.y * voxelSize / 2,
        //     bounds.size.z / 2 - voxelAmount.z * voxelSize / 2
        // );

        // create the voxels
        for (int x = 0; x < voxelAmount.x; x++) {
            for (int y = 0; y < voxelAmount.y; y++) {
                for (int z = 0; z < voxelAmount.z; z++) {
                    GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxel.transform.position = new Vector3(
                        bounds.min.x + x * voxelSize,
                        bounds.min.y + y * voxelSize,
                        bounds.min.z + z * voxelSize
                    );
                    voxel.GetComponent<MeshRenderer>().enabled = false;
                    voxel.name = $"{x}_{y}_{z}";
                    voxel.transform.localScale = new Vector3(voxelSize, voxelSize, voxelSize);
                    //voxel.transform.localPosition += bounds.center;
                    voxel.transform.parent = voxelParent;
                }
            }
        }
    }


    // public void temp() {
    //     Bounds bounds = boundingVolumes[0].bounds;
    //     bounds.Encapsulate(boundingVolumes[1].bounds);
    // }


}

#region inspectorstuff
#if UNITY_EDITOR

[CustomEditor(typeof(LPNN_script))]
public class LPNN_Inspector: Editor {
    public override void OnInspectorGUI()
    {

        // TODO add all the tooltips
        // TODO maybe make enum choice between box voxels and arbitrary voxels so we can have equal size vs equal amount per axis

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
            Debug.Log("Bounds calculated");
            lpnn.CalculateBoundingVolume();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        EditorGUILayout.FloatField("Voxel Size (in meters)", 1); // HACK

        if (GUILayout.Button("Place Voxels")) {
            Debug.Log("Voxels placed");
            lpnn.PlaceVoxels();
        }
    }
}

#endif
#endregion