// For B1, at hand-off #2.
//
// This file is compiled out until you add ATLAS_SPACETIME to
// Project Settings > Player > Scripting Define Symbols (after the generated bindings are in
// Assets/module_bindings/ and the SpacetimeDB SDK package is installed).
//
// The job: wrap your SpacetimeManager so it implements IAtlasBackend. Every UI script talks
// only to IAtlasBackend, so once this compiles you disable LocalFakeBackend and nothing else
// changes. Names below (DbConnection, Reducers.Join, Db.Node.OnInsert ...) follow the
// SpacetimeDB C# SDK's generated-code conventions; adjust to whatever `spacetime generate`
// actually produced.

#if ATLAS_SPACETIME
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Atlas
{
    [DefaultExecutionOrder(-1000)]
    public class SpacetimeBackend : MonoBehaviour, IAtlasBackend
    {
        public string serverUri = "https://maincloud.spacetimedb.com"; // or read from your shared config file
        public string moduleName = "atlas";

        DbConnection conn;
        ArchitectureGraph graph = new ArchitectureGraph();
        readonly List<ChecklistItem> myChecklist = new List<ChecklistItem>();
        readonly List<UserInfo> users = new List<UserInfo>();
        readonly Dictionary<ulong, AgentTask> tasks = new Dictionary<ulong, AgentTask>();
        MapLayer layer;

        public bool IsConnected => conn != null && conn.IsActive;
        public string LocalIdentity { get; private set; }
        public UserInfo LocalUser => users.FirstOrDefault(u => u.identity == LocalIdentity);
        public ArchitectureGraph Graph => graph;
        public MapLayer CurrentLayer => layer;
        public IReadOnlyList<ChecklistItem> MyChecklist => myChecklist;
        public IReadOnlyList<UserInfo> Users => users;
        public AgentTask GetTask(ulong id) => tasks.TryGetValue(id, out var t) ? t : null;
        public string GetSelection(string identity) => null; // TODO: look up conn.Db.Selection by identity

        public event Action<ArchitectureGraph> GraphLoaded;
        public event Action<string, Vector3> NodeMoved;
        public event Action<string, string> SelectionChanged;
        public event Action<MapLayer> LayerChanged;
        public event Action<UserInfo> UserChanged;
        public event Action<UserInfo> LocalUserJoined;
        public event Action<AgentTask> TaskUpdated;
        public event Action<AgentStep> AgentStepAdded;
        public event Action<AgentAnswer> AnswerPosted;
        public event Action<SceneCommand> SceneCommandIssued;
        public event Action<IReadOnlyList<ChecklistItem>> ChecklistChanged;

        void Awake() => AtlasServices.Register(this);
        void OnDestroy() => AtlasServices.Unregister(this);

        void Start()
        {
            conn = DbConnection.Builder()
                .WithUri(serverUri)
                .WithModuleName(moduleName)
                .OnConnect((c, identity, token) =>
                {
                    LocalIdentity = identity.ToString();
                    HookTables(c);
                    c.SubscriptionBuilder()
                        .OnApplied(_ => OnSubscribed())
                        .SubscribeToAllTables();
                })
                .OnConnectError(e => Debug.LogError("[Atlas] SpacetimeDB connect failed: " + e))
                .Build();
        }

        void Update() => conn?.FrameTick();   // keeps the connection updated every frame

        void HookTables(DbConnection c)
        {
            // Convert generated rows into Atlas types and raise the matching events. Examples:
            //
            // c.Db.Node.OnUpdate += (ctx, oldRow, row) => { var n = graph.Find(row.Id); if (n != null) { n.Position = new Vector3(row.X, row.Y, row.Z); NodeMoved?.Invoke(row.Id, n.Position); } };
            // c.Db.ViewState.OnUpdate += (ctx, o, row) => { layer = AtlasEnums.ParseLayer(row.Layer); LayerChanged?.Invoke(layer); };
            // c.Db.Selection.OnInsert / OnUpdate => SelectionChanged?.Invoke(row.Identity.ToString(), row.NodeId);
            // c.Db.AgentTask.OnInsert / OnUpdate => { var t = ToTask(row); tasks[t.id] = t; TaskUpdated?.Invoke(t); };
            // c.Db.AgentStep.OnInsert   => AgentStepAdded?.Invoke(new AgentStep { id = row.Id, taskId = row.TaskId, text = row.Text });
            // c.Db.AgentAnswer.OnInsert => AnswerPosted?.Invoke(new AgentAnswer { id = row.Id, taskId = row.TaskId, forRole = AtlasEnums.ParseRole(row.ForRole), text = row.Text });
            // c.Db.SceneCommand.OnInsert => SceneCommandIssued?.Invoke(new SceneCommand { id = row.Id, taskId = row.TaskId, kind = row.Kind, payloadJson = row.PayloadJson });
            // c.Db.ChecklistItem.OnInsert / OnUpdate => rebuild myChecklist for LocalIdentity, then ChecklistChanged?.Invoke(myChecklist);
            // c.Db.User.OnInsert / OnUpdate => update users, UserChanged?.Invoke(u); if it's you and it's new, LocalUserJoined?.Invoke(u);
        }

        void OnSubscribed()
        {
            // Build `graph` from c.Db.Node.Iter() and c.Db.Edge.Iter(), set `layer` from view_state, then:
            GraphLoaded?.Invoke(graph);
        }

        // ---- reducers ----
        public void Join(string name, Role role) => conn.Reducers.Join(name, role.ToWire());
        public void SetRole(Role role) => conn.Reducers.SetRole(role.ToWire());
        public void SelectNode(string nodeId) => conn.Reducers.SelectNode(nodeId ?? "");
        public void MoveNode(string nodeId, Vector3 p) => conn.Reducers.MoveNode(nodeId, p.x, p.y, p.z);
        public void AddTranscript(string text) => conn.Reducers.AddTranscript(text);
        public void SetLayer(MapLayer l) => conn.Reducers.SetLayer(l.ToWire());
    }
}
#endif
