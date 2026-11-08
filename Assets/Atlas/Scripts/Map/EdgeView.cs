using UnityEngine;
using Atlas.UI;

namespace Atlas.Map
{
    /// <summary>A line between two nodes that follows them as they move.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class EdgeView : MonoBehaviour
    {
        public EdgeData Data { get; private set; }
        public NodeView From { get; private set; }
        public NodeView To { get; private set; }

        LineRenderer line;
        Color? emphasis;
        bool dimmed;
        Color shown = AtlasTheme.Edge;
        float width = 0.0025f;

        public void Init(EdgeData data, NodeView from, NodeView to)
        {
            Data = data; From = from; To = to;
            name = $"Edge: {data.from} → {data.to}";
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 3;
            line.sharedMaterial = AtlasMaterials.Glow(Color.white);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        public bool Connects(string a, string b) =>
            (Data.from == a && Data.to == b) || (Data.from == b && Data.to == a);

        public void SetEmphasis(Color? color, bool dim) { emphasis = color; dimmed = dim && color == null; }

        void LateUpdate()
        {
            if (From == null || To == null) return;
            line.SetPosition(0, From.transform.position);
            line.SetPosition(1, To.transform.position);

            var target = emphasis ?? (dimmed ? new Color(0.6f, 0.65f, 0.75f, 0.15f) : AtlasTheme.Edge);
            shown = Color.Lerp(shown, target, 1 - Mathf.Exp(-10f * Time.deltaTime));
            line.startColor = line.endColor = shown;
            float w = emphasis != null ? 0.005f : 0.0025f;
            width = Mathf.Lerp(width, w, 1 - Mathf.Exp(-10f * Time.deltaTime));
            line.widthMultiplier = width * Mathf.Max(0.0001f, transform.lossyScale.x);
        }
    }
}
