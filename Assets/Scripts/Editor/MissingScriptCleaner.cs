using UnityEngine;
using UnityEditor;
using System.Linq;

namespace BranchingLEDAnimator.Editor
{
    /// <summary>
    /// Editor utility to clean up missing script references
    /// </summary>
    public class MissingScriptCleaner : EditorWindow
    {
        [MenuItem("Tools/LED Animation System/Clean Missing Scripts")]
        public static void ShowWindow()
        {
            GetWindow<MissingScriptCleaner>("Missing Script Cleaner");
        }
        
        [MenuItem("Tools/LED Animation System/Quick Clean Missing Scripts")]
        public static void QuickClean()
        {
            CleanMissingScripts();
        }
        
        void OnGUI()
        {
            GUILayout.Label("Missing Script Cleaner", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("This tool will remove all missing script references from:");
            GUILayout.Label("• All GameObjects in the current scene");
            GUILayout.Label("• All prefabs in the project");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Clean Missing Scripts", GUILayout.Height(30)))
            {
                CleanMissingScripts();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Clean Scene Only", GUILayout.Height(25)))
            {
                CleanSceneOnly();
            }
            
            if (GUILayout.Button("Clean Prefabs Only", GUILayout.Height(25)))
            {
                CleanPrefabsOnly();
            }
        }
        
        public static void CleanMissingScripts()
        {
            int sceneCount = CleanSceneOnly();
            int prefabCount = CleanPrefabsOnly();
            
            Debug.Log($"✓ Cleaned {sceneCount} missing scripts from scene and {prefabCount} from prefabs");
            EditorUtility.DisplayDialog("Missing Scripts Cleaned", 
                $"Removed {sceneCount} missing scripts from scene and {prefabCount} from prefabs.", "OK");
        }
        
        public static int CleanSceneOnly()
        {
            int count = 0;
            
            // Find all GameObjects in the scene
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.isLoaded) // Only scene objects, not prefabs
                .ToArray();
            
            foreach (GameObject go in allObjects)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                count += removed;
                
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    Debug.Log($"Removed {removed} missing scripts from: {go.name}");
                }
            }
            
            if (count > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
            
            Debug.Log($"✓ Cleaned {count} missing scripts from scene");
            return count;
        }
        
        public static int CleanPrefabsOnly()
        {
            int count = 0;
            
            // Find all prefab assets
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);
                    count += removed;
                    
                    if (removed > 0)
                    {
                        EditorUtility.SetDirty(prefab);
                        Debug.Log($"Removed {removed} missing scripts from prefab: {path}");
                    }
                }
            }
            
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log($"✓ Cleaned {count} missing scripts from prefabs");
            return count;
        }
        
        [MenuItem("GameObject/Remove Missing Scripts", false, 0)]
        public static void RemoveMissingScriptsFromSelected()
        {
            int count = 0;
            
            foreach (GameObject go in Selection.gameObjects)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                count += removed;
                
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    Debug.Log($"Removed {removed} missing scripts from: {go.name}");
                }
            }
            
            if (count > 0)
            {
                Debug.Log($"✓ Removed {count} missing scripts from selected objects");
            }
            else
            {
                Debug.Log("No missing scripts found on selected objects");
            }
        }
    }
}
