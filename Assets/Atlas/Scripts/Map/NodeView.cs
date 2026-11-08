using TMPro;
using UnityEngine;
using Atlas.UI;

namespace Atlas.Map
{
    /// <summary>
    /// One box on the map. Owns its look (kind color, highlight, dimming, selection glow) and
    /// glides to its layout position. For Meta hand grab, wire the grabbable's select/unselect
    /// events to NotifyGrabbed / NotifyReleased, and a pinch/poke to NotifySelected.
    /// </summary>
    public class NodeView : MonoBehaviour
    {
        public NodeData Data { get; private set; }
        public Vector3 HomeLocal;            // where the current layout wants it (Content-local)
        public bool Held { get; private set; }
        public Vector3 Size { get; private set; }

        Renderer body;
        Material bodyMat;
        Transform shell;
        Material shellMat;
        TextMeshProUGUI label;
        Color kindColor;
        Color? emphasis;
        bool dimmed;
        Color? selection;
        Color shown;

        public void Init(NodeData data, Vector3 size, GameObject prefab)
        {
            Data = data;
            Size = size;
            name = "Node: " + data.id;
            kindColor = AtlasTheme.KindColor(data.kind);
            shown = kindColor;

            if (prefab != null)
            {
                var vis = Instantiate(prefab, transform, false);
                body = vis.GetComponentInChildren<Renderer>();
                if (GetComponentInChildren<Collider>() == null) gameObject.AddComponent<BoxCollider>().size = size;
            }
            else
            {
                var cube = AtlasMaterials.Primitive(PrimitiveType.Cube, "Body", transform, keepCollider: true);
                cube.transform.localScale = size;
                body = cube.GetComponent<Renderer>();
            }
            if (body != null) { bodyMat = AtlasMaterials.Lit(kindColor); body.sharedMaterial = bodyMat; }

            // Selection glow: a slightly larger translucent shell.
            var s = AtlasMaterials.Primitive(PrimitiveType.Cube, "SelectionGlow", transform);
            shell = s.transform;
            shell.localScale = size * 1.28f;
            shellMat = AtlasMaterials.Glow(Color.clear);
            s.GetComponent<Renderer>().sharedMaterial = shellMat;
            s.SetActive(false);

            label = WorldLabel.Create(transform, data.name, new Vector3(0, size.y / 2 + 0.016f, 0), 13f);
        }

        public void SetEmphasis(Color? color, bool dim)
        {
            emphasis = color;
            dimmed = dim && color == null;
        }

        public void SetSelection(Color? color)
        {
            selection = color;
            shell.gameObject.SetActive(color != null);
        }

        public void SetLabelVisible(bool v) { if (label != null) label.canvas.enabled = v; }

        public void NotifyGrabbed() => Held = true;

        public void NotifyReleased()
        {
            Held = false;
            MapBuilder.Instance?.OnNodeReleased(this);
        }

        public void NotifySelected() => AtlasServices.Backend?.SelectNode(Data.id);

        /// <summary>Used by the mouse fallback to move the node directly.</summary>
        public void BeginDrag() => Held = true;
        public void EndDrag() => NotifyReleased();

        void Update()
        {
            if (!Held)
                transform.localPosition = Vector3.Lerp(transform.localPosition, HomeLocal, 1 - Mathf.Exp(-9f * Time.deltaTime));

            var target = emphasis ?? (dimmed ? Color.Lerp(kindColor, new Color(0.12f, 0.14f, 0.18f), 0.7f) : kindColor);
            shown = Color.Lerp(shown, target, 1 - Mathf.Exp(-10f * Time.deltaTime));
            AtlasMaterials.SetColor(bodyMat, shown);
            if (label != null) label.alpha = dimmed ? 0.45f : 1f;

            if (selection is Color sc)
            {
                float a = 0.28f + 0.12f * Mathf.Sin(Time.time * 4f);
                shellMat.color = new Color(sc.r, sc.g, sc.b, a);
            }
        }
    }
}
