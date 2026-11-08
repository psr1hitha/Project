// Atlas — shared data contracts (mirrors "Shared contracts" in the Build Plan).
// Team Brain's generated SpacetimeDB bindings get converted into these types by the
// backend adapter, so the UI never depends on the bindings directly.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atlas
{
    public enum Role { Engineer, PM, Architect }
    public enum MapLayer { Code, Business, Architecture }
    public enum TaskStatus { Waiting, Working, Done }

    public static class AtlasEnums
    {
        // Wire format = the strings used in SpacetimeDB tables.
        public static string ToWire(this Role r) => r switch
        {
            Role.Engineer => "engineer",
            Role.PM => "pm",
            _ => "architect",
        };

        public static Role ParseRole(string s) => (s ?? "").ToLowerInvariant() switch
        {
            "pm" => Role.PM,
            "architect" => Role.Architect,
            _ => Role.Engineer,
        };

        public static string Label(this Role r) => r switch
        {
            Role.Engineer => "Engineer",
            Role.PM => "PM",
            _ => "Architect",
        };

        public static string ToWire(this MapLayer l) => l switch
        {
            MapLayer.Code => "code",
            MapLayer.Business => "business",
            _ => "architecture",
        };

        public static MapLayer ParseLayer(string s) => (s ?? "").ToLowerInvariant() switch
        {
            "business" => MapLayer.Business,
            "architecture" => MapLayer.Architecture,
            _ => MapLayer.Code,
        };

        public static string Label(this MapLayer l) => l switch
        {
            MapLayer.Code => "Code",
            MapLayer.Business => "Business",
            _ => "Architecture",
        };

        public static string ToWire(this TaskStatus s) => s switch
        {
            TaskStatus.Waiting => "waiting",
            TaskStatus.Working => "working",
            _ => "done",
        };

        public static TaskStatus ParseStatus(string s) => (s ?? "").ToLowerInvariant() switch
        {
            "working" => TaskStatus.Working,
            "done" => TaskStatus.Done,
            _ => TaskStatus.Waiting,
        };
    }

    // ---------- data/architecture.json ----------

    [Serializable]
    public class NodeData
    {
        public string id;
        public string name;
        public string kind;      // "batch program", "online program", "data file", "screen", ...
        public string feature;   // business group for the PM view
        public string[] files;
        public string summary;
        public float x, y, z;    // meters from table center

        public Vector3 Position
        {
            get => new Vector3(x, y, z);
            set { x = value.x; y = value.y; z = value.z; }
        }
    }

    [Serializable]
    public class EdgeData
    {
        public string id;
        public string from;
        public string to;
        public string label;
    }

    [Serializable]
    public class ArchitectureGraph
    {
        public List<NodeData> nodes = new List<NodeData>();
        public List<EdgeData> edges = new List<EdgeData>();

        public NodeData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in nodes) if (n.id == id) return n;
            return null;
        }

        public static ArchitectureGraph FromJson(string json)
        {
            var g = JsonUtility.FromJson<ArchitectureGraph>(json) ?? new ArchitectureGraph();
            g.nodes ??= new List<NodeData>();
            g.edges ??= new List<EdgeData>();
            for (int i = 0; i < g.edges.Count; i++)
                if (string.IsNullOrEmpty(g.edges[i].id)) g.edges[i].id = "e" + i;
            return g;
        }
    }

    // ---------- SpacetimeDB rows (UI-side shape) ----------

    [Serializable]
    public class UserInfo
    {
        public string identity;
        public string name;
        public Role role;
        public bool online;
    }

    [Serializable]
    public class AgentTask
    {
        public ulong id;
        public string askedBy;   // identity, or "agent" for unprompted comments
        public string question;
        public string nodeId;    // node selected by the asker when they asked (may be empty)
        public TaskStatus status;
        public DateTime createdAt;
    }

    [Serializable]
    public class AgentStep
    {
        public ulong id;
        public ulong taskId;
        public string text;
        public DateTime createdAt;
    }

    [Serializable]
    public class AgentAnswer
    {
        public ulong id;
        public ulong taskId;
        public Role forRole;
        public string text;
        public DateTime createdAt;
    }

    [Serializable]
    public class ChecklistItem
    {
        public ulong id;
        public string identity;
        public string text;
        public bool done;
    }

    [Serializable]
    public class SceneCommand
    {
        public ulong id;
        public ulong taskId;
        public string kind;
        public string payloadJson;

        ScenePayload parsed;
        public ScenePayload Payload
        {
            get
            {
                if (parsed == null)
                {
                    try { parsed = JsonUtility.FromJson<ScenePayload>(payloadJson ?? "{}"); }
                    catch (Exception e) { Debug.LogWarning($"[Atlas] Bad scene command payload: {e.Message}\n{payloadJson}"); }
                    parsed ??= new ScenePayload();
                    if (string.IsNullOrEmpty(parsed.kind)) parsed.kind = kind;
                }
                return parsed;
            }
        }
    }

    /// <summary>Union of every scene-command payload shape in the Build Plan.</summary>
    [Serializable]
    public class ScenePayload
    {
        public string kind;
        public string[] nodes;          // highlight_path, highlight_nodes, animate_flow
        public string node;             // show_blast_radius, focus_node
        public BlastDependent[] dependents;
        public string layer;            // set_layer
        public string example;          // show_ghost_architecture
    }

    [Serializable]
    public class BlastDependent
    {
        public string id;
        public int depth;
    }
}
