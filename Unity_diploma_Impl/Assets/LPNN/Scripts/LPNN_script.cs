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

    [SerializeField] private List<BoxCollider> boundingVolumes;
    [NonSerialized] public Bounds bounds;
    [NonSerialized] public float voxelSize = 1;

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

    public void CalculateBoundingVolume() {

        boundingVolumes ??= new List<BoxCollider>();

        if (boundingVolumes.Count > 0)
        {
            boundingVolumes = new List<BoxCollider>();
        }
        GameObject boundingChildren;
        try
        {
            if ( boundingChildren = transform.Find("BoundingVolumes").gameObject ) {
                foreach (Transform child in boundingChildren.transform){
                    if (child.TryGetComponent<BoxCollider>(out var boxCollider)){
                        boundingVolumes.Add(boxCollider);
                    }
                }
            }
        }
        catch
        {
            Debug.LogError("No bounding volumes found");
        }
    }

    public void PlaceVoxels() {
        
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
        EditorGUILayout.Toggle(content, false); // HACK same with this and everything tbh

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
            Debug.Log("Voxels places");
            lpnn.PlaceVoxels();
        }
    }
}

#endif
#endregion