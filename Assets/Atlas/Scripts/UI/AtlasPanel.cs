using UnityEngine;

namespace Atlas.UI
{
    /// <summary>
    /// Base for every floating panel. Builds its own UI from code (in Awake, or in edit mode via
    /// the Atlas menu so Meta's "Add Ray/Poke Interaction to Canvas" quick actions can be used on it),
    /// fades in and out, and turns to face the user.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class AtlasPanel : MonoBehaviour
    {
        [SerializeField, HideInInspector] bool built;
        [Tooltip("Turn to face the user (yaw only).")]
        public bool faceUser = true;
        [Tooltip("Visible when the scene starts.")]
        public bool startVisible = false;

        protected Canvas canvas;
        protected CanvasGroup group;
        float targetAlpha;
        float popT = 1f;

        /// <summary>Canvas size in pixels (= millimetres).</summary>
        protected abstract Vector2 CanvasSize { get; }
        protected abstract void Build();

        protected IAtlasBackend B => AtlasServices.Backend;
        public bool Visible => targetAlpha > 0.5f;

        public void EnsureBuilt()
        {
            if (built && transform.childCount > 0)
            {
                canvas = GetComponent<Canvas>();
                group = GetComponent<CanvasGroup>();
                if (canvas != null && group != null) return;
            }
            UIFactory.ClearChildren(transform);
            canvas = UIFactory.SetupWorldCanvas(gameObject, CanvasSize);
            group = UIFactory.GetOrAdd<CanvasGroup>(gameObject);
            Build();
            built = true;
        }

        protected virtual void Awake()
        {
            EnsureBuilt();
            targetAlpha = startVisible ? 1 : 0;
            group.alpha = targetAlpha;
            group.interactable = group.blocksRaycasts = startVisible;
            if (faceUser) UIFactory.GetOrAdd<FaceCamera>(gameObject);
        }

        protected virtual void Start()
        {
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            Wire();
            AtlasServices.WhenReady(OnBackend);
        }

        /// <summary>Add button listeners here (runtime listeners aren't saved with edit-mode builds).</summary>
        protected virtual void Wire() { }

        /// <summary>Subscribe to backend events here.</summary>
        protected virtual void OnBackend(IAtlasBackend backend) { }

        public void SetVisible(bool visible)
        {
            if (visible && !Visible) popT = 0;
            targetAlpha = visible ? 1 : 0;
            group.interactable = group.blocksRaycasts = visible;
        }

        protected virtual void Update()
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 6f);
            if (popT < 1f)
            {
                popT = Mathf.Min(1f, popT + Time.unscaledDeltaTime * 5f);
                float s = Mathf.Lerp(0.92f, 1f, 1 - (1 - popT) * (1 - popT));
                transform.localScale = Vector3.one * AtlasTheme.PixelsToMeters * s;
            }
        }
    }
}
