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

    private void Awake()
    {
        // if list not empty
        //     get its bounding box
        //     return
        // if child exists
        //     get its bounding box
        //     return
        // error

        boundingVolumes ??= new List<BoxCollider>();

        if (boundingVolumes.Count > 0)
        {
            boundingVolumes = new List<BoxCollider>();
        }
        GameObject boundingChildren;
        if ( boundingChildren = transform.Find("BoundingVolumes").gameObject){
            foreach (Transform child in boundingChildren.transform)
            {
                if (child.TryGetComponent<BoxCollider>(out var boxCollider))
                {
                    boundingVolumes.Add(boxCollider);
                }
            }
        }
        else
        {
            Debug.LogError("No bounding volumes found");
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
        base.OnInspectorGUI();
        EditorGUILayout.LabelField("This is a custom inspector");
    }
}

#endif
#endregion