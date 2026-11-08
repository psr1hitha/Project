// Stand-in for SpacetimeDB + the agent worker, so Team Headset never waits on Team Brain.
// - Loads the map from Resources/architecture.json (or the built-in sample).
// - Runs reducers locally with the same rules as the Build Plan.
// - Fakes the agent: claim -> reasoning steps -> answer -> scene commands -> done.
// - Keyboard shortcuts fake scene commands in the editor (see the README).
//
// Disable or delete this component once SpacetimeBackend is in the scene.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Atlas
{
    [DefaultExecutionOrder(-1000)]
    public class LocalFakeBackend : MonoBehaviour, IAtlasBackend
    {
        [Tooltip("Optional. Leave empty to load Resources/architecture.json, then the built-in sample.")]
        public TextAsset graphJson;
        [Tooltip("Seconds between fake reasoning steps.")]
        public float stepDelay = 0.8f;
        public bool keyboardShortcuts = true;
        [Tooltip("Agent speaks a first mission right after you join (scripted unprompted comment).")]
        public bool welcomeMission = true;

        // ---- state ----
        readonly List<UserInfo> users = new List<UserInfo>();
        readonly Dictionary<string, string> selections = new Dictionary<string, string>();
        readonly Dictionary<ulong, AgentTask> tasks = new Dictionary<ulong, AgentTask>();
        readonly List<ChecklistItem> checklist = new List<ChecklistItem>();
        readonly Dictionary<ulong, string[]> checklistKeywords = new Dictionary<ulong, string[]>();
        ulong nextId = 1;
        MapLayer layer = MapLayer.Code;

        public bool IsConnected => Graph != null;
        public string LocalIdentity { get; } = "local-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        public UserInfo LocalUser { get; private set; }
        public ArchitectureGraph Graph { get; private set; }
        public MapLayer CurrentLayer => layer;
        public IReadOnlyList<ChecklistItem> MyChecklist => checklist;
        public IReadOnlyList<UserInfo> Users => users;
        public AgentTask GetTask(ulong id) => tasks.TryGetValue(id, out var t) ? t : null;
        public string GetSelection(string identity) => selections.TryGetValue(identity ?? "", out var s) ? s : null;

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

        void Awake()
        {
            var text = graphJson != null ? graphJson.text : Resources.Load<TextAsset>("architecture")?.text;
            if (string.IsNullOrEmpty(text))
            {
                Debug.Log("[Atlas] No Resources/architecture.json found; using the built-in sample map.");
                text = SampleGraph.Json;
            }
            Graph = ArchitectureGraph.FromJson(text);
            AtlasServices.Register(this);
        }

        void Start() => GraphLoaded?.Invoke(Graph);

        void OnDestroy() => AtlasServices.Unregister(this);

        // =====================================================================
        // Reducers
        // =====================================================================

        public void Join(string name, Role role)
        {
            LocalUser = new UserInfo { identity = LocalIdentity, name = name, role = role, online = true };
            users.RemoveAll(u => u.identity == LocalIdentity);
            users.Add(LocalUser);
            BuildChecklist(role);
            UserChanged?.Invoke(LocalUser);
            LocalUserJoined?.Invoke(LocalUser);
            ChecklistChanged?.Invoke(checklist);

            if (welcomeMission) StartCoroutine(WelcomeMission());
        }

        public void SetRole(Role role)
        {
            if (LocalUser == null) return;
            LocalUser.role = role;
            BuildChecklist(role);
            UserChanged?.Invoke(LocalUser);
            ChecklistChanged?.Invoke(checklist);
        }

        public void SelectNode(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && Graph.Find(nodeId) == null) return;
            selections[LocalIdentity] = string.IsNullOrEmpty(nodeId) ? null : nodeId;
            SelectionChanged?.Invoke(LocalIdentity, selections[LocalIdentity]);
        }

        public void MoveNode(string nodeId, Vector3 localPosition)
        {
            var n = Graph.Find(nodeId);
            if (n == null) return;
            n.Position = localPosition;
            NodeMoved?.Invoke(nodeId, localPosition);
        }

        public void AddTranscript(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var task = new AgentTask
            {
                id = nextId++,
                askedBy = LocalIdentity,
                question = text.Trim(),
                nodeId = GetSelection(LocalIdentity),
                status = TaskStatus.Waiting,
                createdAt = DateTime.UtcNow,
            };
            tasks[task.id] = task;
            TaskUpdated?.Invoke(task);
            StartCoroutine(RunFakeAgent(task));
        }

        public void SetLayer(MapLayer l)
        {
            if (layer == l) return;
            layer = l;
            LayerChanged?.Invoke(l);
        }

        // =====================================================================
        // Keyboard fakes (editor testing)
        // =====================================================================

        void Update()
        {
            if (!keyboardShortcuts || Graph == null || InputCompat.TypingInUI) return;

            if (InputCompat.KeyDown(KeyCode.Alpha1)) Issue(0, "highlight_path", Json.Obj(("kind", Json.Str("highlight_path")), ("nodes", Json.Arr(PaymentPath()))));
            if (InputCompat.KeyDown(KeyCode.Alpha2)) Issue(0, "animate_flow", Json.Obj(("kind", Json.Str("animate_flow")), ("nodes", Json.Arr(PaymentPath()))));
            if (InputCompat.KeyDown(KeyCode.Alpha3)) IssueBlast(0, SelectedOr("interest", "calc"));
            if (InputCompat.KeyDown(KeyCode.Alpha4)) SetLayer((MapLayer)(((int)layer + 1) % 3));
            if (InputCompat.KeyDown(KeyCode.Alpha5)) Issue(0, "focus_node", Json.Obj(("kind", Json.Str("focus_node")), ("node", Json.Str(SelectedOr("interest")?.id))));
            if (InputCompat.KeyDown(KeyCode.Alpha6)) Issue(0, "show_ghost_architecture", Json.Obj(("kind", Json.Str("show_ghost_architecture")), ("example", Json.Str("billing_service"))));
            if (InputCompat.KeyDown(KeyCode.Alpha0)) Issue(0, "clear", "{\"kind\":\"clear\"}");
            if (InputCompat.KeyDown(KeyCode.T)) AddTranscript("How does a payment get processed?");
        }

        // =====================================================================
        // Fake agent — doubles as the cached fallback for the demo questions
        // =====================================================================

        class Plan
        {
            public List<string> steps = new List<string>();
            public string answer;
            public List<(string kind, string json)> commands = new List<(string, string)>();
            public string tickKeyword;
        }

        IEnumerator WelcomeMission()
        {
            yield return new WaitForSeconds(1.5f);
            var task = new AgentTask { id = nextId++, askedBy = "agent", question = "Session started", status = TaskStatus.Working, createdAt = DateTime.UtcNow };
            tasks[task.id] = task;
            TaskUpdated?.Invoke(task);
            yield return new WaitForSeconds(stepDelay);
            AddStep(task, $"Reading {Graph.nodes.Count} pieces of the system");
            yield return new WaitForSeconds(stepDelay);
            AddStep(task, "Picking a first mission for a " + LocalUser.role.Label());
            yield return new WaitForSeconds(stepDelay);
            Post(task, $"Welcome, {LocalUser.name}. This is CardDemo, a credit card system written in COBOL. Let's start by tracing how a payment gets processed. Pinch the Bill payment box to begin.");
            task.status = TaskStatus.Done;
            TaskUpdated?.Invoke(task);
        }

        IEnumerator RunFakeAgent(AgentTask task)
        {
            yield return new WaitForSeconds(0.4f);
            task.status = TaskStatus.Working;          // claim_task
            TaskUpdated?.Invoke(task);

            var plan = MakePlan(task);
            foreach (var s in plan.steps)
            {
                yield return new WaitForSeconds(stepDelay * UnityEngine.Random.Range(0.7f, 1.3f));
                AddStep(task, s);                      // add_agent_step
            }
            yield return new WaitForSeconds(stepDelay * 0.6f);

            Post(task, plan.answer);                   // post_answer
            foreach (var (kind, json) in plan.commands) Issue(task.id, kind, json);  // issue_scene_command
            Tick(task.question + " " + plan.tickKeyword);                            // complete_checklist_item

            task.status = TaskStatus.Done;             // complete_task
            TaskUpdated?.Invoke(task);
        }

        Plan MakePlan(AgentTask task)
        {
            var q = task.question.ToLowerInvariant();
            var role = LocalUser?.role ?? Role.Engineer;
            var focus = Graph.Find(task.nodeId);
            var p = new Plan();

            if (q.Contains("tell me more") && focus != null)
            {
                var neighbors = GraphUtil.Neighbors(Graph, focus.id).Select(n => n.name).Distinct().ToList();
                p.steps.Add($"Looking up {focus.name}");
                p.steps.Add($"Reading {FirstFile(focus)}");
                p.steps.Add($"Checking what {focus.name} connects to");
                p.steps.Add($"Writing it up for a {role.Label()}");
                var links = neighbors.Count > 0 ? $" It connects to {JoinNames(neighbors)}." : "";
                p.answer = role switch
                {
                    Role.PM => $"{focus.name} is part of {focus.feature}. {focus.summary}{links} If it changes, customers would notice it in {focus.feature.ToLowerInvariant()}.",
                    Role.Architect => $"{focus.name} is a {focus.kind} in {FirstFile(focus)}. {focus.summary}{links} It's coupled to the rest of the system mainly through shared files, which is the main risk when changing it.",
                    _ => $"{focus.name} lives in {FirstFile(focus)}. {focus.summary}{links}",
                };
                p.commands.Add(("focus_node", Json.Obj(("kind", Json.Str("focus_node")), ("node", Json.Str(focus.id)))));
                p.tickKeyword = "code";
                return p;
            }

            if (q.Contains("break") || q.Contains("depend") || q.Contains("impact") || q.Contains("change") || q.Contains("risk"))
            {
                var target = focus ?? SelectedOr("interest", "calc");
                var deps = GraphUtil.BlastRadius(Graph, target.id, 3);
                var direct = deps.Where(d => d.depth == 1).Select(d => Graph.Find(d.id)?.name).ToList();
                p.steps.Add($"Finding everything that depends on {target.name}");
                p.steps.Add($"Running blast radius from {target.name}");
                p.steps.Add($"Found {deps.Count} affected pieces, {direct.Count} directly");
                p.steps.Add("Ranking the risk");
                p.answer = role switch
                {
                    Role.PM => $"Changing {target.name} would affect {deps.Count} parts of the system, including {JoinNames(direct)}. Customers would see it on their balances and statements, so it needs careful testing.",
                    Role.Architect => $"{target.name} has a blast radius of {deps.Count}. Direct impact: {JoinNames(direct)}. Everything is coupled through shared files, so a contract around those files is the first step before changing it.",
                    _ => $"{deps.Count} pieces depend on {target.name}. Directly: {JoinNames(direct)}. Red is direct, orange is one step removed, yellow is further out.",
                };
                p.commands.Add(("show_blast_radius", BlastJson(target.id, deps)));
                p.tickKeyword = "blast";
                return p;
            }

            if (q.Contains("billing") && (q.Contains("service") || q.Contains("modern") || q.Contains("what if") || q.Contains("own")) || q.Contains("modernize"))
            {
                p.steps.Add("Loading the billing-as-a-service example");
                p.steps.Add("Checking which programs would move");
                p.steps.Add("Listing what still connects to the old files");
                p.answer = role switch
                {
                    Role.PM => "Here's billing pulled out as its own service, shown in purple beside the map. Payments, interest and statements would live together, and could be changed without touching account screens.",
                    Role.Architect => "The ghost shows a billing service owning payment, interest and posting logic behind an API, with the accounts and transactions files as its only remaining links to the mainframe. Those two files are the migration seam.",
                    _ => "The purple ghost is billing as its own service. Bill payment, interest calculation and posting move into it; it talks to the accounts and transactions files until they're migrated too.",
                };
                p.commands.Add(("show_ghost_architecture", Json.Obj(("kind", Json.Str("show_ghost_architecture")), ("example", Json.Str("billing_service")))));
                p.tickKeyword = "billing service";
                return p;
            }

            if (q.Contains("payment") || q.Contains("pay "))
            {
                var path = PaymentPath();
                var names = path.Select(id => Graph.Find(id)?.name ?? id).ToList();
                p.steps.Add("Finding where payments enter the system");
                p.steps.Add($"Tracing the path from {names.First()} to {names.Last()}");
                p.steps.Add($"Reading {FirstFile(Graph.Find(path.First()))}");
                p.steps.Add($"Checking what {names[Mathf.Min(2, names.Count - 1)]} does with each record");
                p.steps.Add($"Explaining it for a {role.Label()}");
                p.answer = role switch
                {
                    Role.PM => $"When a customer pays, {names.First()} records the payment right away, but the balance only changes overnight when {names[Mathf.Min(2, names.Count - 1)]} runs. That's why a balance can look out of date until the next morning.",
                    Role.Architect => $"Payments cross the online/batch boundary: {JoinNames(names)}. The online and batch sides only talk through shared files, so there's no single owner of a payment.",
                    _ => $"A payment goes {JoinNames(names)}. The online program writes a transaction record; the nightly batch reads it and updates the account balance.",
                };
                p.commands.Add(("highlight_path", Json.Obj(("kind", Json.Str("highlight_path")), ("nodes", Json.Arr(path)))));
                p.commands.Add(("animate_flow", Json.Obj(("kind", Json.Str("animate_flow")), ("nodes", Json.Arr(path)))));
                p.tickKeyword = "payment";
                return p;
            }

            if (q.Contains("business") || q.Contains("feature"))
            {
                p.steps.Add("Grouping programs by business feature");
                p.steps.Add("Switching the map to the business view");
                var features = Graph.nodes.Select(n => n.feature).Distinct().ToList();
                p.answer = $"Here's the system by business feature: {JoinNames(features)}. Billing has the most moving parts.";
                p.commands.Add(("set_layer", Json.Obj(("kind", Json.Str("set_layer")), ("layer", Json.Str("business")))));
                p.tickKeyword = "feature";
                return p;
            }

            if (q.Contains("architecture") || q.Contains("batch") || q.Contains("online"))
            {
                p.steps.Add("Sorting programs by kind");
                p.steps.Add("Switching to the architecture view");
                p.answer = "Here's the architecture view: online programs, batch programs and data files in rows. Online screens never call batch jobs; they only meet in the shared files.";
                p.commands.Add(("set_layer", Json.Obj(("kind", Json.Str("set_layer")), ("layer", Json.Str("architecture")))));
                p.tickKeyword = "batch online";
                return p;
            }

            // Default: where to start
            var start = focus ?? GraphUtil.FindByHint(Graph, "payment", "bill") ?? Graph.nodes.First();
            p.steps.Add("Looking at what you've explored so far");
            p.steps.Add($"Picking the best next step for a {role.Label()}");
            p.answer = $"Good place to start: {start.name}. {start.summary} Pinch it and ask me to tell you more.";
            p.commands.Add(("focus_node", Json.Obj(("kind", Json.Str("focus_node")), ("node", Json.Str(start.id)))));
            return p;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        List<string> PaymentPath()
        {
            var a = GraphUtil.FindByHint(Graph, "payment", "bill");
            var b = GraphUtil.FindByHint(Graph, "accounts_file", "account");
            if (a == null || b == null) return Graph.nodes.Take(3).Select(n => n.id).ToList();
            return GraphUtil.FlowPath(Graph, a.id, b.id);
        }

        NodeData SelectedOr(params string[] hints) =>
            Graph.Find(GetSelection(LocalIdentity)) ?? GraphUtil.FindByHint(Graph, hints) ?? Graph.nodes.First();

        void IssueBlast(ulong taskId, NodeData n) => Issue(taskId, "show_blast_radius", BlastJson(n.id, GraphUtil.BlastRadius(Graph, n.id, 3)));

        static string BlastJson(string id, List<BlastDependent> deps)
        {
            var arr = "[" + string.Join(",", deps.Select(d => Json.Obj(("id", Json.Str(d.id)), ("depth", d.depth.ToString())))) + "]";
            return Json.Obj(("kind", Json.Str("show_blast_radius")), ("node", Json.Str(id)), ("dependents", arr));
        }

        void Issue(ulong taskId, string kind, string json)
        {
            var cmd = new SceneCommand { id = nextId++, taskId = taskId, kind = kind, payloadJson = json };
            // Same check as the issue_scene_command reducer: reject unknown node ids.
            var p = cmd.Payload;
            if (p.nodes != null && p.nodes.Any(id => Graph.Find(id) == null)) { Debug.LogWarning($"[Atlas] Rejected {kind}: unknown node id"); return; }
            if (!string.IsNullOrEmpty(p.node) && Graph.Find(p.node) == null) { Debug.LogWarning($"[Atlas] Rejected {kind}: unknown node {p.node}"); return; }
            SceneCommandIssued?.Invoke(cmd);
            if (kind == "set_layer" && !string.IsNullOrEmpty(p.layer)) SetLayer(AtlasEnums.ParseLayer(p.layer));
        }

        void AddStep(AgentTask t, string text) =>
            AgentStepAdded?.Invoke(new AgentStep { id = nextId++, taskId = t.id, text = text, createdAt = DateTime.UtcNow });

        void Post(AgentTask t, string text) =>
            AnswerPosted?.Invoke(new AgentAnswer { id = nextId++, taskId = t.id, forRole = LocalUser?.role ?? Role.Engineer, text = text, createdAt = DateTime.UtcNow });

        static string FirstFile(NodeData n) => n?.files != null && n.files.Length > 0 ? n.files[0] : n?.name ?? "the code";

        static string JoinNames(IList<string> names)
        {
            names = names.Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (names.Count == 0) return "nothing else";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }

        // Pre-written per role, as the Build Plan scopes it. Keywords tick items when a question matches.
        void BuildChecklist(Role role)
        {
            checklist.Clear();
            checklistKeywords.Clear();
            (string text, string[] keys)[] items = role switch
            {
                Role.PM => new[]
                {
                    ("See the system by business feature", new[] { "feature", "business" }),
                    ("Learn how a customer payment is processed", new[] { "payment" }),
                    ("Find out what breaks if interest changes", new[] { "blast", "break", "interest" }),
                    ("Review the billing-as-a-service idea", new[] { "billing service", "modern" }),
                },
                Role.Architect => new[]
                {
                    ("Separate online programs from batch jobs", new[] { "batch online", "architecture" }),
                    ("Trace a payment end to end", new[] { "payment" }),
                    ("Find the riskiest shared data file", new[] { "blast", "risk", "depend" }),
                    ("Review the billing service proposal", new[] { "billing service", "modern" }),
                },
                _ => new[]
                {
                    ("Trace how a payment gets posted", new[] { "payment" }),
                    ("Check the blast radius of interest calculation", new[] { "blast", "break", "depend" }),
                    ("Open the code for one batch program", new[] { "code", "tell me more" }),
                    ("See what billing would look like as a service", new[] { "billing service", "modern" }),
                },
            };
            foreach (var (text, keys) in items)
            {
                var item = new ChecklistItem { id = nextId++, identity = LocalIdentity, text = text, done = false };
                checklist.Add(item);
                checklistKeywords[item.id] = keys;
            }
        }

        void Tick(string haystack)
        {
            haystack = haystack.ToLowerInvariant();
            bool changed = false;
            foreach (var item in checklist)
            {
                if (item.done) continue;
                if (checklistKeywords[item.id].Any(k => haystack.Contains(k))) { item.done = true; changed = true; break; }
            }
            if (changed) ChecklistChanged?.Invoke(checklist);
        }
    }
}
