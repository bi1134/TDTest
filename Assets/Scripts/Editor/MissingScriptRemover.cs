#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MissingScriptRemover : EditorWindow
{
    [MenuItem("Tools/Remove Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptRemover>("Remove Missing Scripts");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Find and Remove Missing Scripts in Selection"))
        {
            RemoveMissingScriptsInSelection();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Note: Select GameObjects in the Scene or Project view first.");
    }

    private static void RemoveMissingScriptsInSelection()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int compCount = 0;
        int goCount = 0;
        
        foreach (GameObject go in selectedObjects)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (count > 0)
            {
                Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                compCount += count;
                goCount++;
            }
            
            // Recursive check for children
            foreach (Transform child in go.transform)
            {
                ProcessChild(child, ref compCount, ref goCount);
            }
        }
        
        Debug.Log($"Removed {compCount} missing scripts from {goCount} GameObjects.");
    }
    
    private static void ProcessChild(Transform t, ref int compCount, ref int goCount)
    {
        GameObject go = t.gameObject;
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (count > 0)
        {
            Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            compCount += count;
            goCount++;
        }
        
        foreach (Transform child in t)
        {
            ProcessChild(child, ref compCount, ref goCount);
        }
    }
}
#endif
