using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class FindBrokenPhysicsEditors
{
    [MenuItem("Tools/Diagnostics/Scan Broken (Open Scenes)")]
    public static void ScanOpenScenes()
    {
        int nullComponentSlots = 0, badMeshCollider = 0, missingScripts = 0;

        var allGos = GetAllGameObjectsInOpenScenes(includeInactive: true);

        foreach (var go in allGos)
        {
            if (!go) continue;

            // 1) 任意“空组件槽位”（不仅仅是Missing Script）
            // GetComponents<Component>() 会把空槽位返回为 null
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    nullComponentSlots++;
                    Debug.LogError($"[Null Component Slot] {GetFullPath(go)}", go);
                    break; // 一个对象报一次就够了
                }
            }

            // 2) Missing Script（MonoBehaviour缺失）
            int ms = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (ms > 0)
            {
                missingScripts += ms;
                Debug.LogWarning($"[Missing Script x{ms}] {GetFullPath(go)}", go);
            }

            // 3) MeshCollider sharedMesh 为空
            var mcs = go.GetComponents<MeshCollider>();
            foreach (var mc in mcs)
            {
                if (mc && mc.sharedMesh == null)
                {
                    badMeshCollider++;
                    Debug.LogError($"[Broken MeshCollider] sharedMesh == null → {GetFullPath(go)}", go);
                }
            }
        }

        Debug.Log($"[Scene Scan Finished] Null Component Slots:{nullComponentSlots}  Broken MeshCollider:{badMeshCollider}  Missing Scripts:{missingScripts}");
    }

    [MenuItem("Tools/Diagnostics/Scan Broken (All Prefabs in Project)")]
    public static void ScanAllPrefabsInProject()
    {
        int nullComponentSlots = 0, badMeshCollider = 0, missingScripts = 0;
        int scanned = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (!root) continue;
                scanned++;

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t) continue;
                    var go = t.gameObject;

                    // 任意空组件槽位
                    var comps = go.GetComponents<Component>();
                    for (int i = 0; i < comps.Length; i++)
                    {
                        if (comps[i] == null)
                        {
                            nullComponentSlots++;
                            Debug.LogError($"[Null Component Slot] (Prefab) {path} :: {GetTransformPath(t)}");
                            break;
                        }
                    }

                    // Missing Script
                    int ms = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (ms > 0)
                    {
                        missingScripts += ms;
                        Debug.LogWarning($"[Missing Script x{ms}] (Prefab) {path} :: {GetTransformPath(t)}");
                    }

                    // MeshCollider
                    foreach (var mc in go.GetComponents<MeshCollider>())
                    {
                        if (mc && mc.sharedMesh == null)
                        {
                            badMeshCollider++;
                            Debug.LogError($"[Broken MeshCollider] (Prefab) sharedMesh == null → {path} :: {GetTransformPath(t)}");
                        }
                    }
                }
            }
            finally
            {
                if (root)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[Prefab Scan Finished] Scanned:{scanned}  Null Component Slots:{nullComponentSlots}  Broken MeshCollider:{badMeshCollider}  Missing Scripts:{missingScripts}");
    }

    // ------------ helpers ------------

    static List<GameObject> GetAllGameObjectsInOpenScenes(bool includeInactive)
    {
        var list = new List<GameObject>(4096);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                var trs = root.GetComponentsInChildren<Transform>(includeInactive);
                foreach (var tr in trs)
                    if (tr) list.Add(tr.gameObject);
            }
        }
        return list;
    }

    static string GetFullPath(GameObject go)
    {
        if (!go) return "<null>";
        string path = go.name;
        var t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return go.scene.name + " :: " + path;
    }

    static string GetTransformPath(Transform t)
    {
        if (!t) return "<null>";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
