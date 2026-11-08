using UnityEngine;
using UnityEngine.EventSystems;
using Atlas.Map;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Atlas.UI
{
    /// <summary>
    /// Creates every panel and arranges them around the table, relative to where the map was placed.
    /// Before join: only the role picker. After join: reasoning panel (left), question buttons (right),
    /// checklist and layer switcher (behind the map), orb + caption above the map, info card on the
    /// selected node. Tweak the offsets in the inspector (x = right, y = up, z = away from you, meters).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class AtlasUIRig : MonoBehaviour
    {
        [Header("Panels (created automatically if empty)")]
        public RolePickerPanel rolePicker;
        public InfoCardPanel infoCard;
        public ReasoningPanel reasoning;
        public ChecklistPanel checklist;
        public QuestionButtonsPanel questions;
        public LayerSwitcherPanel layerSwitcher;
        public CaptionPanel caption;
        public AgentOrb orb;

        [Header("Layout around the table center")]
        public Vector3 rolePickerOffset = new Vector3(0f, 0.3f, 0.05f);
        public Vector3 reasoningOffset = new Vector3(-0.52f, 0.0f, 0.02f);
        public Vector3 questionsOffset = new Vector3(0.55f, 0.0f, -0.06f);
        public Vector3 checklistOffset = new Vector3(-0.24f, 0.06f, 0.38f);
        public Vector3 layerSwitcherOffset = new Vector3(0.24f, 0.14f, 0.38f);
        public Vector3 orbOffset = new Vector3(0f, 0.3f, 0.05f);
        public Vector3 captionOffset = new Vector3(0f, 0.36f, 0.05f);

        [Tooltip("Skip the role picker in the editor and join as this role (faster iteration).")]
        public bool autoJoinInEditor = false;
        public Role autoJoinRole = Role.Engineer;

        void Awake()
        {
            BuildPanels();
            EnsureEventSystem();
        }

        void Start()
        {
            var map = MapBuilder.Instance;
            if (map != null) map.Placed += Layout;
            Layout();

            rolePicker.SetVisible(true);
            AtlasServices.WhenReady(b =>
            {
                b.LocalUserJoined += _ => ShowWorkspace();
                if (b.LocalUser != null) ShowWorkspace();
                else if (autoJoinInEditor && Application.isEditor) b.Join("Editor", autoJoinRole);
            });
        }

        void ShowWorkspace()
        {
            rolePicker.SetVisible(false);
            reasoning.SetVisible(true);
            checklist.SetVisible(true);
            questions.SetVisible(true);
            layerSwitcher.SetVisible(true);
            orb.gameObject.SetActive(true);
        }

        /// <summary>Snaps the rig to the table center and faces it away from the user.</summary>
        public void Layout()
        {
            var map = MapBuilder.Instance;
            if (map != null) transform.SetPositionAndRotation(map.transform.position, map.transform.rotation);

            rolePicker.transform.localPosition = rolePickerOffset;
            reasoning.transform.localPosition = reasoningOffset;
            questions.transform.localPosition = questionsOffset;
            checklist.transform.localPosition = checklistOffset;
            layerSwitcher.transform.localPosition = layerSwitcherOffset;
            caption.transform.localPosition = captionOffset;
            orb.SetHome(orbOffset);
            // Info card positions itself above the selected node.
        }

        [ContextMenu("Build UI Panels Now")]
        public void BuildPanels()
        {
            rolePicker = Ensure(rolePicker, "Role Picker");
            infoCard = Ensure(infoCard, "Info Card");
            reasoning = Ensure(reasoning, "Reasoning Panel");
            checklist = Ensure(checklist, "Checklist Panel");
            questions = Ensure(questions, "Question Buttons");
            layerSwitcher = Ensure(layerSwitcher, "Layer Switcher");
            caption = Ensure(caption, "Caption");
            if (orb == null)
            {
                orb = GetComponentInChildren<AgentOrb>(true);
                if (orb == null)
                {
                    var go = new GameObject("Agent Orb");
                    go.transform.SetParent(transform, false);
                    orb = go.AddComponent<AgentOrb>();
                }
            }
            orb.caption = caption;
            if (Application.isPlaying) orb.gameObject.SetActive(false);
        }

        T Ensure<T>(T existing, string name) where T : AtlasPanel
        {
            if (existing == null) existing = GetComponentInChildren<T>(true);
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                existing = go.AddComponent<T>();
            }
            existing.EnsureBuilt();
            return existing;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
