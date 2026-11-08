using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Atlas.UI;

namespace Atlas.Map
{
    /// <summary>
    /// Listens for new scene_command rows and draws them:
    /// highlight_nodes / highlight_path, animate_flow, show_blast_radius, set_layer, focus_node,
    /// show_ghost_architecture and clear. Effects stack until "clear".
    /// </summary>
    public class SceneCommandRenderer : MonoBehaviour
    {
        [Header("Flow animation")]
        public float flowSpeed = 0.12f;       // meters per second
        public float flowSpacing = 0.06f;     // meters between dots
        public float dotSize = 0.012f;

        [Header("Ghost architecture")]
        [Tooltip("Optional prepared prefabs by example name. Without one, a ghost is built from the hints below.")]
        public List<GhostPrefab> ghostPrefabs = new List<GhostPrefab>();
        public GhostExample billingExample = new GhostExample
        {
            example = "billing_service",
            title = "Billing service (proposed)",
            moveHints = new[] { "payment", "posting", "interest" },
            connectHints = new[] { "accounts_file", "transactions_file" },
            offset = new Vector3(0.42f, 0.06f, 0.16f),
        };

        [Serializable] public class GhostPrefab { public string example; public GameObject prefab; }

        [Serializable]
        public class GhostExample
        {
            public string example;
            public string title;
            [Tooltip("Node ids (or parts of ids/names) that move into the new service.")]
            public string[] moveHints;
            [Tooltip("Nodes the new service still talks to.")]
            public string[] connectHints;
            [Tooltip("Position beside the map, relative to the table center.")]
            public Vector3 offset;
        }

        public event Action<SceneCommand> CommandRendered;

        MapBuilder Map => MapBuilder.Instance;

        // Active effects
        readonly HashSet<string> highlightNodes = new HashSet<string>();
        readonly List<(string a, string b)> highlightEdges = new List<(string, string)>();
        readonly Dictionary<string, int> blast = new Dictionary<string, int>();
        string blastOrigin;
        readonly HashSet<string> ghosted = new HashSet<string>();
        List<string> flowPath;
        readonly List<Transform> flowDots = new List<Transform>();
        GameObject ghost;
        readonly List<(LineRenderer line, Transform from, Transform to)> ghostLinks = new List<(LineRenderer, Transform, Transform)>();
        float ghostT;

        void Start() => AtlasServices.WhenReady(b => b.SceneCommandIssued += Render);

        void OnDestroy() { if (AtlasServices.Backend != null) AtlasServices.Backend.SceneCommandIssued -= Render; }

        public void Render(SceneCommand cmd)
        {
            if (Map == null) return;
            var p = cmd.Payload;
            switch (cmd.kind)
            {
                case "highlight_nodes":
                    highlightNodes.Clear(); highlightEdges.Clear();
                    foreach (var id in p.nodes ?? Array.Empty<string>()) highlightNodes.Add(id);
                    foreach (var e in Map.Edges)
                        if (highlightNodes.Contains(e.Data.from) && highlightNodes.Contains(e.Data.to)) highlightEdges.Add((e.Data.from, e.Data.to));
                    break;

                case "highlight_path":
                    highlightNodes.Clear(); highlightEdges.Clear();
                    var nodes = p.nodes ?? Array.Empty<string>();
                    foreach (var id in nodes) highlightNodes.Add(id);
                    for (int i = 0; i + 1 < nodes.Length; i++) highlightEdges.Add((nodes[i], nodes[i + 1]));
                    break;

                case "animate_flow":
                    StartFlow(p.nodes);
                    if (highlightNodes.Count == 0) goto case "highlight_path";
                    break;

                case "show_blast_radius":
                    blast.Clear();
                    blastOrigin = p.node;
                    foreach (var d in p.dependents ?? Array.Empty<BlastDependent>()) blast[d.id] = d.depth;
                    break;

                case "set_layer":
                    Map.SetLayer(AtlasEnums.ParseLayer(p.layer));
                    break;

                case "focus_node":
                    Map.Focus(p.node);
                    break;

                case "show_ghost_architecture":
                    ShowGhost(p.example);
                    break;

                case "clear":
                    ClearAll();
                    break;

                default:
                    Debug.LogWarning($"[Atlas] Unknown scene command '{cmd.kind}'");
                    return;
            }
            ApplyColors();
            CommandRendered?.Invoke(cmd);
        }

        public void ClearAll()
        {
            highlightNodes.Clear(); highlightEdges.Clear(); blast.Clear(); blastOrigin = null; ghosted.Clear();
            StopFlow();
            if (ghost != null) Destroy(ghost);
            ghost = null; ghostLinks.Clear();
            Map?.ClearFocus();
            ApplyColors();
        }

        // ------------------------------------------------------------------ colors

        void ApplyColors()
        {
            bool anything = highlightNodes.Count > 0 || blast.Count > 0 || ghosted.Count > 0;
            foreach (var kv in Map.Nodes)
            {
                Color? c = null;
                if (kv.Key == blastOrigin) c = AtlasTheme.BlastOrigin;
                else if (blast.TryGetValue(kv.Key, out var depth)) c = AtlasTheme.BlastColor(depth);
                else if (highlightNodes.Contains(kv.Key)) c = AtlasTheme.Highlight;
                else if (ghosted.Contains(kv.Key)) c = AtlasTheme.Ghost;
                kv.Value.SetEmphasis(c, anything);
            }
            foreach (var e in Map.Edges)
            {
                Color? c = null;
                bool fromIn = e.Data.from == blastOrigin || blast.ContainsKey(e.Data.from);
                bool toIn = e.Data.to == blastOrigin || blast.ContainsKey(e.Data.to);
                if (blast.Count > 0 && fromIn && toIn)
                {
                    int d = Mathf.Max(blast.TryGetValue(e.Data.from, out var a) ? a : 0, blast.TryGetValue(e.Data.to, out var b) ? b : 0);
                    c = AtlasTheme.BlastColor(d);
                }
                else if (highlightEdges.Any(h => e.Connects(h.a, h.b))) c = AtlasTheme.Highlight;
                e.SetEmphasis(c, anything);
            }
        }

        // ------------------------------------------------------------------ flow

        void StartFlow(string[] path)
        {
            StopFlow();
            if (path == null || path.Length < 2) return;
            flowPath = path.Where(id => Map.GetNode(id) != null).ToList();
            float length = PathLength();
            int count = Mathf.Clamp(Mathf.RoundToInt(length / flowSpacing), 3, 40);
            var mat = AtlasMaterials.Glow(AtlasTheme.Speaking);
            for (int i = 0; i < count; i++)
            {
                var dot = AtlasMaterials.Primitive(PrimitiveType.Sphere, "FlowDot", Map.transform).transform;
                dot.localScale = Vector3.one * dotSize;
                dot.GetComponent<Renderer>().sharedMaterial = mat;
                flowDots.Add(dot);
            }
        }

        void StopFlow()
        {
            foreach (var d in flowDots) if (d != null) Destroy(d.gameObject);
            flowDots.Clear();
            flowPath = null;
        }

        float PathLength()
        {
            float len = 0;
            for (int i = 0; i + 1 < flowPath.Count; i++)
                len += Vector3.Distance(Map.GetNode(flowPath[i]).transform.position, Map.GetNode(flowPath[i + 1]).transform.position);
            return len;
        }

        Vector3 PointAt(float dist)
        {
            for (int i = 0; i + 1 < flowPath.Count; i++)
            {
                var a = Map.GetNode(flowPath[i]).transform.position;
                var b = Map.GetNode(flowPath[i + 1]).transform.position;
                float seg = Vector3.Distance(a, b);
                if (dist <= seg) return Vector3.Lerp(a, b, seg > 0 ? dist / seg : 0);
                dist -= seg;
            }
            return Map.GetNode(flowPath[flowPath.Count - 1]).transform.position;
        }

        // ------------------------------------------------------------------ ghost

        void ShowGhost(string example)
        {
            if (ghost != null) Destroy(ghost);
            ghostLinks.Clear(); ghosted.Clear();
            var g = AtlasServices.Backend.Graph;
            var ex = billingExample; // one prepared example for the demo
            if (!string.IsNullOrEmpty(example) && example != ex.example)
                Debug.Log($"[Atlas] No ghost example named '{example}', showing '{ex.example}'.");

            ghost = new GameObject("Ghost: " + ex.example);
            ghost.transform.SetParent(Map.transform, false);
            ghost.transform.localPosition = ex.offset;
            ghostT = 0;

            var moved = ex.moveHints.Select(h => GraphUtil.FindByHint(g, h)).Where(n => n != null).Distinct().ToList();
            var connects = ex.connectHints.Select(h => GraphUtil.FindByHint(g, h)).Where(n => n != null).Distinct().ToList();
            foreach (var n in moved) ghosted.Add(n.id);

            var custom = ghostPrefabs.FirstOrDefault(gp => gp.example == ex.example && gp.prefab != null);
            if (custom != null) { Instantiate(custom.prefab, ghost.transform, false); }
            else
            {
                float w = Mathf.Max(0.16f, moved.Count * 0.08f + 0.04f);
                var shell = AtlasMaterials.Primitive(PrimitiveType.Cube, "Service", ghost.transform);
                shell.transform.localScale = new Vector3(w, 0.07f, 0.11f);
                shell.transform.localPosition = new Vector3(0, 0.035f, 0);
                shell.GetComponent<Renderer>().sharedMaterial = AtlasMaterials.Glow(new Color(AtlasTheme.Ghost.r, AtlasTheme.Ghost.g, AtlasTheme.Ghost.b, 0.16f));
                WorldLabel.Create(ghost.transform, ex.title, new Vector3(0, 0.095f, 0), 15f, AtlasTheme.Ghost);

                var innerMat = AtlasMaterials.Glow(new Color(AtlasTheme.Ghost.r, AtlasTheme.Ghost.g, AtlasTheme.Ghost.b, 0.55f));
                for (int i = 0; i < moved.Count; i++)
                {
                    var box = AtlasMaterials.Primitive(PrimitiveType.Cube, "Moved: " + moved[i].id, ghost.transform);
                    box.transform.localScale = new Vector3(0.06f, 0.025f, 0.04f);
                    box.transform.localPosition = new Vector3((i - (moved.Count - 1) / 2f) * 0.075f, 0.02f, 0);
                    box.GetComponent<Renderer>().sharedMaterial = innerMat;
                    WorldLabel.Create(box.transform.parent, moved[i].name, box.transform.localPosition + new Vector3(0, 0.03f, -0.035f), 11f, AtlasTheme.Text);
                }
            }

            var lineMat = AtlasMaterials.Glow(AtlasTheme.Ghost);
            foreach (var n in connects)
            {
                var target = Map.GetNodeTransform(n.id);
                if (target == null) continue;
                var go = new GameObject("Link → " + n.id, typeof(LineRenderer));
                go.transform.SetParent(ghost.transform, false);
                var lr = go.GetComponent<LineRenderer>();
                lr.sharedMaterial = lineMat;
                lr.widthMultiplier = 0.003f;
                lr.positionCount = 2;
                lr.startColor = new Color(AtlasTheme.Ghost.r, AtlasTheme.Ghost.g, AtlasTheme.Ghost.b, 0.9f);
                lr.endColor = new Color(AtlasTheme.Ghost.r, AtlasTheme.Ghost.g, AtlasTheme.Ghost.b, 0.2f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostLinks.Add((lr, ghost.transform, target));
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (flowPath != null && flowDots.Count > 0 && Map != null)
            {
                float len = PathLength();
                if (len > 0.001f)
                {
                    float spacing = len / flowDots.Count;
                    float head = (Time.time * flowSpeed) % spacing;
                    for (int i = 0; i < flowDots.Count; i++)
                    {
                        float d = head + i * spacing;
                        flowDots[i].position = PointAt(d) + Vector3.up * 0.004f;
                        // Fade in at the start, out at the end
                        float edge = Mathf.Min(d, len - d) / Mathf.Max(0.0001f, spacing);
                        flowDots[i].localScale = Vector3.one * dotSize * Mathf.Clamp01(edge);
                    }
                }
            }

            if (ghost != null)
            {
                ghostT = Mathf.MoveTowards(ghostT, 1, Time.deltaTime * 2.5f);
                float s = 1 - Mathf.Pow(1 - ghostT, 3);
                ghost.transform.localScale = Vector3.one * Mathf.Max(0.001f, s);
                foreach (var (line, from, to) in ghostLinks)
                {
                    if (to == null) continue;
                    line.SetPosition(0, from.position + Vector3.up * 0.035f);
                    line.SetPosition(1, to.position);
                }
            }
        }
    }
}
