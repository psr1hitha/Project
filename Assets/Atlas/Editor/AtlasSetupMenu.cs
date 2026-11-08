#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Atlas.Map;
using Atlas.UI;

namespace Atlas.EditorTools
{
    /// <summary>
    /// Atlas → Create Atlas Rig In Scene: adds the backend, map, renderer, push-to-talk and the full UI
    /// to the open scene in one click. The panels are built in edit mode so you can select each
    /// canvas and run Meta's "Interaction SDK → Add Ray Interaction to Canvas" (or Poke) quick action.
    /// </summary>
    public static class AtlasSetupMenu
    {
        [MenuItem("Atlas/Create Atlas Rig In Scene")]
        public static void CreateRig()
        {
            var root = new GameObject("Atlas");
            Undo.RegisterCreatedObjectUndo(root, "Create Atlas Rig");

            var backend = new GameObject("Backend (Local Fake)");
            backend.transform.SetParent(root.transform, false);
            backend.AddComponent<LocalFakeBackend>();

            var map = new GameObject("Atlas Map");
            map.transform.SetParent(root.transform, false);
            map.AddComponent<MapBuilder>();
            map.AddComponent<SceneCommandRenderer>();
            map.AddComponent<EditorMouseInteractor>();

            var voice = new GameObject("Push To Talk");
            voice.transform.SetParent(root.transform, false);
            voice.AddComponent<PushToTalkController>();

            var ui = new GameObject("Atlas UI");
            ui.transform.SetParent(root.transform, false);
            var rig = ui.AddComponent<AtlasUIRig>();
            rig.BuildPanels();
            LayOutForEditing(rig);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[Atlas] Rig created. Press Play: pick a role, click nodes, use the question buttons, keys 0–6 and T fake agent events.");
        }

        [MenuItem("Atlas/Rebuild UI Panels")]
        public static void RebuildPanels()
        {
            var rig = Object.FindFirstObjectByType<AtlasUIRig>();
            if (rig == null) { Debug.LogWarning("[Atlas] No AtlasUIRig in the scene. Use Atlas → Create Atlas Rig In Scene."); return; }
            foreach (var p in rig.GetComponentsInChildren<AtlasPanel>(true))
            {
                var so = new SerializedObject(p);
                var built = so.FindProperty("built");
                if (built != null) { built.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                p.EnsureBuilt();
                EditorUtility.SetDirty(p);
            }
            LayOutForEditing(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }

        [MenuItem("Atlas/Copy Sample architecture.json To Resources")]
        public static void WriteSampleJson()
        {
            var dir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Resources");
            var path = dir + "/architecture.json";
            if (System.IO.File.Exists(path) && !EditorUtility.DisplayDialog("Atlas", "Overwrite Assets/Resources/architecture.json?", "Overwrite", "Cancel")) return;
            System.IO.File.WriteAllText(path, SampleGraph.Json);
            AssetDatabase.ImportAsset(path);
            Debug.Log("[Atlas] Wrote " + path + ". Replace it with B2's data/architecture.json when ready.");
        }

        // Spread panels out in the Scene view so they can be inspected; runtime layout overrides this.
        static void LayOutForEditing(AtlasUIRig rig)
        {
            rig.rolePicker.transform.localPosition = rig.rolePickerOffset + Vector3.up * 1.2f;
            rig.reasoning.transform.localPosition = rig.reasoningOffset + Vector3.up * 1.2f;
            rig.questions.transform.localPosition = rig.questionsOffset + Vector3.up * 1.2f;
            rig.checklist.transform.localPosition = rig.checklistOffset + Vector3.up * 1.2f;
            rig.layerSwitcher.transform.localPosition = rig.layerSwitcherOffset + Vector3.up * 1.2f;
            rig.caption.transform.localPosition = rig.captionOffset + Vector3.up * 1.2f;
            rig.infoCard.transform.localPosition = new Vector3(0, 1.2f, -0.1f);
        }
    }
}
#endif
