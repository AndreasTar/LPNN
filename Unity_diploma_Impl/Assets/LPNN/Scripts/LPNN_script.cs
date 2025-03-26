using UnityEditor;
using UnityEngine;

public class LPNN_script : MonoBehaviour
{
    #region rightclickmenu
    // Add a menu item named "LPNN Object" to MyMenu in the menu bar.
    [MenuItem("GameObject/Light/LPNN Object", false)]
    static void CreateLPNNObject(MenuCommand menuCommand)
    {
        // Create a custom game object
        GameObject go = new GameObject("LPNN Parent");
        go.AddComponent<LPNN_script>();

        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }
    #endregion


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