using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Atlas
{
    /// <summary>Graph helpers used by the fake agent and the renderer.</summary>
    public static class GraphUtil
    {
        /// <summary>Finds a node whose id or name contains the hint (case-insensitive).</summary>
        public static NodeData FindByHint(ArchitectureGraph g, params string[] hints)
        {
            if (g == null) return null;
            foreach (var h in hints)
            {
                var hl = h.ToLowerInvariant();
                var n = g.nodes.FirstOrDefault(x => x.id.ToLowerInvariant().Contains(hl))
                        ?? g.nodes.FirstOrDefault(x => (x.name ?? "").ToLowerInvariant().Contains(hl));
                if (n != null) return n;
            }
            return null;
        }

        /// <summary>Shortest path ignoring edge direction (same as trace_path).</summary>
        public static List<string> Path(ArchitectureGraph g, string from, string to)
        {
            var prev = new Dictionary<string, string> { [from] = null };
            var q = new Queue<string>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == to) break;
                foreach (var e in g.edges)
                {
                    string next = e.from == cur ? e.to : e.to == cur ? e.from : null;
                    if (next == null || prev.ContainsKey(next)) continue;
                    prev[next] = cur;
                    q.Enqueue(next);
                }
            }
            if (!prev.ContainsKey(to)) return new List<string>();
            var path = new List<string>();
            for (var c = to; c != null; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Shortest path following data flow: "reads" edges flow from the file to the reader,
        /// every other edge (writes, calls, routes to) flows from -> to. Falls back to Path().
        /// </summary>
        public static List<string> FlowPath(ArchitectureGraph g, string from, string to)
        {
            var prev = new Dictionary<string, string> { [from] = null };
            var q = new Queue<string>();
            q.Enqueue(from);
            while (q.Count > 0 && !prev.ContainsKey(to))
            {
                var cur = q.Dequeue();
                foreach (var e in g.edges)
                {
                    bool reads = (e.label ?? "").ToLowerInvariant().Contains("read");
                    string src = reads ? e.to : e.from, dst = reads ? e.from : e.to;
                    if (src != cur || prev.ContainsKey(dst)) continue;
                    prev[dst] = cur;
                    q.Enqueue(dst);
                }
            }
            if (!prev.ContainsKey(to)) return Path(g, from, to);
            var path = new List<string>();
            for (var c = to; c != null; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Blast radius, breadth-first. A node is affected if it points at the changed node
        /// (it calls/reads it), or if the changed node writes into it.
        /// </summary>
        public static List<BlastDependent> BlastRadius(ArchitectureGraph g, string nodeId, int maxDepth = 4)
        {
            var depth = new Dictionary<string, int> { [nodeId] = 0 };
            var q = new Queue<string>();
            q.Enqueue(nodeId);
            var result = new List<BlastDependent>();
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                int d = depth[cur];
                if (d >= maxDepth) continue;
                foreach (var e in g.edges)
                {
                    string next = null;
                    if (e.to == cur) next = e.from;
                    else if (e.from == cur && (e.label ?? "").ToLowerInvariant().Contains("write")) next = e.to;
                    if (next == null || depth.ContainsKey(next)) continue;
                    depth[next] = d + 1;
                    result.Add(new BlastDependent { id = next, depth = d + 1 });
                    q.Enqueue(next);
                }
            }
            return result;
        }

        public static IEnumerable<NodeData> Neighbors(ArchitectureGraph g, string id)
        {
            foreach (var e in g.edges)
            {
                if (e.from == id) { var n = g.Find(e.to); if (n != null) yield return n; }
                else if (e.to == id) { var n = g.Find(e.from); if (n != null) yield return n; }
            }
        }
    }

    /// <summary>Minimal JSON writer for scene-command payloads.</summary>
    public static class Json
    {
        public static string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public static string Arr(IEnumerable<string> items) => "[" + string.Join(",", items.Select(Str)) + "]";

        public static string Obj(params (string key, string rawValue)[] fields)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Str(fields[i].key)).Append(':').Append(fields[i].rawValue);
            }
            return sb.Append('}').ToString();
        }
    }

    /// <summary>Works with both the old Input Manager and the new Input System.</summary>
    public static class InputCompat
    {
#if ENABLE_INPUT_SYSTEM
        static Key Map(KeyCode k) => k switch
        {
            KeyCode.Alpha0 => Key.Digit0, KeyCode.Alpha1 => Key.Digit1, KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3, KeyCode.Alpha4 => Key.Digit4, KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6, KeyCode.Alpha7 => Key.Digit7, KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9, KeyCode.Space => Key.Space, KeyCode.Escape => Key.Escape,
            KeyCode.T => Key.T, KeyCode.R => Key.R, KeyCode.L => Key.L,
            _ => Key.None,
        };

        public static bool KeyDown(KeyCode k)
        {
            var kb = Keyboard.current; var key = Map(k);
            return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
        }
        public static bool KeyHeld(KeyCode k)
        {
            var kb = Keyboard.current; var key = Map(k);
            return kb != null && key != Key.None && kb[key].isPressed;
        }
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool MouseDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool MouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool MouseUp => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        public static bool HasMouse => Mouse.current != null;
#else
        public static bool KeyDown(KeyCode k) => Input.GetKeyDown(k);
        public static bool KeyHeld(KeyCode k) => Input.GetKey(k);
        public static Vector2 MousePosition => Input.mousePosition;
        public static bool MouseDown => Input.GetMouseButtonDown(0);
        public static bool MouseHeld => Input.GetMouseButton(0);
        public static bool MouseUp => Input.GetMouseButtonUp(0);
        public static bool HasMouse => Input.mousePresent;
#endif

        /// <summary>True when a text field has focus, so keyboard shortcuts don't fire while typing.</summary>
        public static bool TypingInUI
        {
            get
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var go = es != null ? es.currentSelectedGameObject : null;
                return go != null && go.GetComponent<TMPro.TMP_InputField>() != null;
            }
        }
    }
}
