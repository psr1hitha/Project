using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Atlas.UI;

namespace Atlas.Map
{
    /// <summary>
    /// Spawns every node and edge under one Map parent, places the map at table height in front of
    /// the user, and animates between the three layouts (code / business / architecture).
    ///
    /// Hierarchy:  Atlas Map (this, the table anchor)  →  Content (moved/scaled by focus_node)  →  nodes, edges
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MapBuilder : MonoBehaviour
    {
        public static MapBuilder Instance { get; private set; }

        [Header("Look")]
        [Tooltip("Optional: H1's grabbable node prefab (Meta Grab building block). Leave empty for plain boxes.")]
        public GameObject nodePrefab;
        public Vector3 nodeSize = new Vector3(0.07f, 0.03f, 0.045f);

        [Header("Placement")]
        public bool placeOnStart = true;
        [Tooltip("Meters in front of the head.")]
        public float distance = 0.55f;
        [Tooltip("Meters below the eyes (table height).")]
        public float belowEyes = 0.5f;

        [Header("Interaction")]
        [Tooltip("Pulling a node this far from its spot asks the agent for its blast radius.")]
        public float pullOutDistance = 0.12f;
        public bool askOnPullOut = true;

        public Transform Content { get; private set; }
        public MapLayer Layer { get; private set; } = MapLayer.Code;
        public IReadOnlyDictionary<string, NodeView> Nodes => nodes;
        public IReadOnlyList<EdgeView> Edges => edges;
        public event Action Placed;
        public event Action Built;

        readonly Dictionary<string, NodeView> nodes = new Dictionary<string, NodeView>();
        readonly List<EdgeView> edges = new List<EdgeView>();
        readonly Dictionary<string, string> selections = new Dictionary<string, string>();
        readonly List<(TextMeshProUGUI label, MapLayer layer)> groupLabels = new List<(TextMeshProUGUI, MapLayer)>();
        Vector3 focusOffset; float focusScale = 1f;

        void Awake()
        {
            Instance = this;
            Content = new GameObject("Content").transform;
            Content.SetParent(transform, false);
        }

        IEnumerator Start()
        {
            AtlasServices.WhenReady(b =>
            {
                b.GraphLoaded += Build;
                b.NodeMoved += OnNodeMoved;
                b.LayerChanged += SetLayer;
                b.SelectionChanged += OnSelection;
                if (b.Graph != null && nodes.Count == 0) Build(b.Graph);
            });

            if (placeOnStart)
            {
                // Head tracking needs a few frames before the pose is real.
                yield return new WaitForSeconds(0.5f);
                PlaceInFrontOfUser();
            }
        }

        void OnDestroy()
        {
            var b = AtlasServices.Backend;
            if (b != null)
            {
                b.GraphLoaded -= Build;
                b.NodeMoved -= OnNodeMoved;
                b.LayerChanged -= SetLayer;
                b.SelectionChanged -= OnSelection;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Puts the table center half a meter in front of the user at table height, facing them.</summary>
        public void PlaceInFrontOfUser()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;
            var fwd = head.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.SetPositionAndRotation(head.position + fwd * distance + Vector3.down * belowEyes,
                                             Quaternion.LookRotation(fwd, Vector3.up));
            Placed?.Invoke();
        }

        // ------------------------------------------------------------------ build

        public void Build(ArchitectureGraph g)
        {
            foreach (Transform c in Content) Destroy(c.gameObject);
            nodes.Clear(); edges.Clear(); groupLabels.Clear();

            foreach (var n in g.nodes)
            {
                var go = new GameObject();
                go.transform.SetParent(Content, false);
                var view = go.AddComponent<NodeView>();
                view.Init(n, nodeSize, nodePrefab);
                nodes[n.id] = view;
            }
            foreach (var e in g.edges)
            {
                if (!nodes.TryGetValue(e.from, out var a) || !nodes.TryGetValue(e.to, out var b)) continue;
                var go = new GameObject("Edge", typeof(LineRenderer));
                go.transform.SetParent(Content, false);
                var ev = go.AddComponent<EdgeView>();
                ev.Init(e, a, b);
                edges.Add(ev);
            }
            BuildGroupLabels(g);

            Layer = AtlasServices.Backend?.CurrentLayer ?? MapLayer.Code;
            ApplyLayout(snap: true);
            Built?.Invoke();
        }

        void BuildGroupLabels(ArchitectureGraph g)
        {
            foreach (var f in g.nodes.Select(n => n.feature).Distinct())
                groupLabels.Add((WorldLabel.Create(Content, f, Vector3.zero, 17f, AtlasTheme.Text, true, "Feature: " + f), MapLayer.Business));
            foreach (var k in KindOrder(g))
                groupLabels.Add((WorldLabel.Create(Content, Plural(k), Vector3.zero, 15f, AtlasTheme.KindColor(k), true, "Kind: " + k), MapLayer.Architecture));
        }

        // ------------------------------------------------------------------ layouts

        public void SetLayer(MapLayer layer)
        {
            Layer = layer;
            ApplyLayout(snap: false);
        }

        void ApplyLayout(bool snap)
        {
            var g = AtlasServices.Backend?.Graph;
            if (g == null) return;
            var targets = Layer switch
            {
                MapLayer.Business => BusinessLayout(g, out _),
                MapLayer.Architecture => ArchitectureLayout(g, out _),
                _ => g.nodes.ToDictionary(n => n.id, n => n.Position),
            };
            var lift = Vector3.up * nodeSize.y / 2;
            foreach (var kv in targets)
            {
                if (!nodes.TryGetValue(kv.Key, out var v)) continue;
                v.HomeLocal = kv.Value + lift;
                if (snap) v.transform.localPosition = v.HomeLocal;
            }

            // Group labels
            Dictionary<string, Vector3> centers = null;
            if (Layer == MapLayer.Business) BusinessLayout(g, out centers);
            if (Layer == MapLayer.Architecture) ArchitectureLayout(g, out centers);
            foreach (var (label, layer) in groupLabels)
            {
                bool on = layer == Layer;
                label.canvas.enabled = on;
                if (!on || centers == null) continue;
                var key = label.canvas.name.Substring(label.canvas.name.IndexOf(':') + 2);
                if (centers.TryGetValue(key, out var c)) label.canvas.transform.localPosition = c;
            }
        }

        /// <summary>Clusters nodes by feature, clusters spread across the table.</summary>
        Dictionary<string, Vector3> BusinessLayout(ArchitectureGraph g, out Dictionary<string, Vector3> labelPos)
        {
            var result = new Dictionary<string, Vector3>();
            labelPos = new Dictionary<string, Vector3>();
            var groups = g.nodes.GroupBy(n => n.feature ?? "Other").OrderBy(gr => gr.Key).ToList();
            int cols = Mathf.CeilToInt(Mathf.Sqrt(groups.Count));
            int rows = Mathf.CeilToInt(groups.Count / (float)cols);
            const float cellW = 0.24f, cellD = 0.2f;
            for (int i = 0; i < groups.Count; i++)
            {
                int cx = i % cols, cz = i / cols;
                var center = new Vector3((cx - (cols - 1) / 2f) * cellW, 0, ((rows - 1) / 2f - cz) * cellD);
                var members = groups[i].ToList();
                int mCols = Mathf.CeilToInt(Mathf.Sqrt(members.Count));
                int mRows = Mathf.CeilToInt(members.Count / (float)mCols);
                for (int j = 0; j < members.Count; j++)
                {
                    int mx = j % mCols, mz = j / mCols;
                    result[members[j].id] = center + new Vector3((mx - (mCols - 1) / 2f) * 0.085f, 0, ((mRows - 1) / 2f - mz) * 0.07f);
                }
                labelPos[groups[i].Key] = center + new Vector3(0, 0.075f, mRows * 0.035f + 0.02f);
            }
            return result;
        }

        /// <summary>One row per kind: online programs nearest the user, then batch, then data files.</summary>
        Dictionary<string, Vector3> ArchitectureLayout(ArchitectureGraph g, out Dictionary<string, Vector3> labelPos)
        {
            var result = new Dictionary<string, Vector3>();
            labelPos = new Dictionary<string, Vector3>();
            var kinds = KindOrder(g);
            for (int r = 0; r < kinds.Count; r++)
            {
                var row = g.nodes.Where(n => n.kind == kinds[r]).ToList();
                float z = (r - (kinds.Count - 1) / 2f) * 0.13f;
                for (int i = 0; i < row.Count; i++)
                    result[row[i].id] = new Vector3((i - (row.Count - 1) / 2f) * 0.095f, 0, z);
                labelPos[kinds[r]] = new Vector3(-(row.Count / 2f) * 0.095f - 0.09f, 0.03f, z);
            }
            return result;
        }

        static List<string> KindOrder(ArchitectureGraph g)
        {
            int Rank(string k)
            {
                k = (k ?? "").ToLowerInvariant();
                if (k.Contains("online") || k.Contains("screen")) return 0;
                if (k.Contains("batch")) return 1;
                if (k.Contains("file") || k.Contains("data")) return 2;
                return 3;
            }
            return g.nodes.Select(n => n.kind).Distinct().OrderBy(Rank).ThenBy(k => k).ToList();
        }

        static string Plural(string k) => string.IsNullOrEmpty(k) ? "Other" : char.ToUpper(k[0]) + k.Substring(1) + "s";

        // ------------------------------------------------------------------ backend events

        void OnNodeMoved(string id, Vector3 pos)
        {
            if (!nodes.TryGetValue(id, out var v) || v.Held) return;
            if (Layer == MapLayer.Code) v.HomeLocal = pos + Vector3.up * nodeSize.y / 2;
        }

        void OnSelection(string identity, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) selections.Remove(identity); else selections[identity] = nodeId;
            var b = AtlasServices.Backend;
            var mine = b?.LocalIdentity;
            var myColor = b?.LocalUser != null ? AtlasTheme.RoleColor(b.LocalUser.role) : AtlasTheme.Agent;
            foreach (var v in nodes.Values) v.SetSelection(null);
            foreach (var kv in selections)
                if (nodes.TryGetValue(kv.Value, out var v))
                    v.SetSelection(kv.Key == mine ? myColor : AtlasTheme.OtherSelection);
        }

        /// <summary>Called by NodeView when a hand or the mouse lets go.</summary>
        public void OnNodeReleased(NodeView v)
        {
            var b = AtlasServices.Backend;
            if (b == null) return;
            float pulled = Vector3.Distance(v.transform.localPosition, v.HomeLocal);

            if (Layer == MapLayer.Code)
            {
                var local = v.transform.localPosition - Vector3.up * nodeSize.y / 2;
                v.HomeLocal = v.transform.localPosition;
                b.MoveNode(v.Data.id, local);
            }
            // In business/architecture views positions are computed, so the node glides back.

            if (askOnPullOut && pulled > pullOutDistance)
            {
                b.SelectNode(v.Data.id);
                b.AddTranscript($"I pulled out {v.Data.name}. What depends on it?");
            }
        }

        // ------------------------------------------------------------------ helpers for the renderer

        public NodeView GetNode(string id) => id != null && nodes.TryGetValue(id, out var v) ? v : null;
        public Transform GetNodeTransform(string id) => GetNode(id)?.transform;

        public void Focus(string id)
        {
            var v = GetNode(id);
            if (v == null) return;
            focusScale = 1.35f;
            var p = v.HomeLocal; p.y = 0;
            focusOffset = -p * focusScale;
        }

        public void ClearFocus() { focusScale = 1f; focusOffset = Vector3.zero; }

        void Update()
        {
            float k = 1 - Mathf.Exp(-6f * Time.deltaTime);
            Content.localPosition = Vector3.Lerp(Content.localPosition, focusOffset, k);
            Content.localScale = Vector3.Lerp(Content.localScale, Vector3.one * focusScale, k);

            // Optional: press R in the editor to re-place the map in front of you.
            if (InputCompat.KeyDown(KeyCode.R) && !InputCompat.TypingInUI) PlaceInFrontOfUser();
        }
    }
}
