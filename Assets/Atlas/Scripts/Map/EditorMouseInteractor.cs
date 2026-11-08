using UnityEngine;
using UnityEngine.EventSystems;

namespace Atlas.Map
{
    /// <summary>
    /// Mouse fallback so everything works in the editor without a headset:
    /// click a node to select it (click again or click empty space to clear), drag to move it.
    /// </summary>
    public class EditorMouseInteractor : MonoBehaviour
    {
        [Tooltip("Turn off on device builds if you like; harmless either way.")]
        public bool enableInBuilds = false;
        public float dragThresholdPixels = 6f;

        NodeView pressed;
        Vector2 pressPos;
        bool dragging;
        Plane dragPlane;
        Vector3 grabOffset;

        void Update()
        {
            if (!Application.isEditor && !enableInBuilds) return;
            if (!InputCompat.HasMouse) return;
            var cam = Camera.main;
            if (cam == null) return;
            var mouse = InputCompat.MousePosition;

            if (InputCompat.MouseDown)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // clicking UI
                pressed = null; dragging = false;
                if (Physics.Raycast(cam.ScreenPointToRay(mouse), out var hit, 10f))
                    pressed = hit.collider.GetComponentInParent<NodeView>();
                pressPos = mouse;
                if (pressed != null)
                {
                    dragPlane = new Plane(Vector3.up, pressed.transform.position);
                    if (dragPlane.Raycast(cam.ScreenPointToRay(mouse), out var d))
                        grabOffset = pressed.transform.position - cam.ScreenPointToRay(mouse).GetPoint(d);
                }
                else
                {
                    AtlasServices.Backend?.SelectNode(null);
                }
            }

            if (pressed != null && InputCompat.MouseHeld)
            {
                if (!dragging && (mouse - pressPos).magnitude > dragThresholdPixels)
                {
                    dragging = true;
                    pressed.BeginDrag();
                }
                if (dragging && dragPlane.Raycast(cam.ScreenPointToRay(mouse), out var d))
                    pressed.transform.position = cam.ScreenPointToRay(mouse).GetPoint(d) + grabOffset;
            }

            if (pressed != null && InputCompat.MouseUp)
            {
                if (dragging) pressed.EndDrag();
                else
                {
                    var b = AtlasServices.Backend;
                    if (b != null)
                    {
                        bool already = b.GetSelection(b.LocalIdentity) == pressed.Data.id;
                        b.SelectNode(already ? null : pressed.Data.id);
                    }
                }
                pressed = null; dragging = false;
            }
        }
    }
}
