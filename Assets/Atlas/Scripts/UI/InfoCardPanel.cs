using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Atlas.Map;

namespace Atlas.UI
{
    /// <summary>
    /// Floats above the selected node: name, kind, files and summary right away, then the agent's
    /// answer for this user's role when it arrives. "Tell me more", "What depends on this?" and
    /// "Modernize this" send questions through add_transcript.
    /// </summary>
    public class InfoCardPanel : AtlasPanel
    {
        [SerializeField] TextMeshProUGUI kindChip, featureChip, title, files, summary, agentHeader, answer;
        [SerializeField] Image kindChipBg;
        [SerializeField] Button tellMore, dependsOn, modernize, close;

        [Tooltip("Height above the node, in meters.")]
        public float heightAboveNode = 0.06f;

        string nodeId;
        Transform anchor;
        bool waiting;
        readonly Dictionary<string, string> answersByNode = new Dictionary<string, string>();

        protected override Vector2 CanvasSize => new Vector2(460, 520);

        protected override void Build()
        {
            // Pivot at the bottom so the card grows upward from the node.
            ((RectTransform)transform).pivot = new Vector2(0.5f, 0f);
            var card = UIFactory.Card(transform, 440, new Vector2(0.5f, 0f), 22, 10);

            var header = UIFactory.Row(card, 8);
            kindChip = UIFactory.Chip(header, "batch program", AtlasTheme.KindColor("batch"), out kindChipBg);
            featureChip = UIFactory.Chip(header, "Billing", AtlasTheme.TextMuted, out _);
            var spacer = UIFactory.Rect("Spacer", header);
            UIFactory.LE(spacer.gameObject, flexW: 1);
            close = UIFactory.Button(header, "×", AtlasTheme.Surface, AtlasTheme.TextMuted, 34, 22, TextAlignmentOptions.Center, 17);
            UIFactory.LE(close.gameObject, minH: 34, prefH: 34, prefW: 34, minW: 34);
            close.GetComponentInChildren<TextMeshProUGUI>().margin = Vector4.zero;

            title = UIFactory.Text(card, "Node name", AtlasTheme.H1, AtlasTheme.Text, FontStyles.Bold);
            files = UIFactory.Text(card, "FILE.cbl", AtlasTheme.Small, AtlasTheme.TextMuted);
            summary = UIFactory.Text(card, "Summary", AtlasTheme.Body, AtlasTheme.Text);

            UIFactory.Divider(card);

            agentHeader = UIFactory.Text(card, "ATLAS", AtlasTheme.Caps, AtlasTheme.Agent, FontStyles.Bold);
            answer = UIFactory.Text(card, "", AtlasTheme.Body, AtlasTheme.TextMuted, FontStyles.Italic);

            var row = UIFactory.Row(card, 8);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            tellMore = UIFactory.Button(row, "Tell me more", AtlasTheme.Agent, AtlasTheme.PanelBg, 44, AtlasTheme.Small + 1);
            dependsOn = UIFactory.Button(row, "What depends on this?", AtlasTheme.Surface, AtlasTheme.Text, 44, AtlasTheme.Small + 1);
            UIFactory.LE(tellMore.gameObject, minH: 44, prefH: 44, flexW: 1);
            UIFactory.LE(dependsOn.gameObject, minH: 44, prefH: 44, flexW: 1.4f);

            modernize = UIFactory.Button(card, "Modernize this", AtlasTheme.Surface, AtlasTheme.Ghost, 40, AtlasTheme.Small + 1);
        }

        protected override void Wire()
        {
            close.onClick.AddListener(() => B?.SelectNode(null));
            tellMore.onClick.AddListener(() => Ask($"Tell me more about {title.text}"));
            dependsOn.onClick.AddListener(() => Ask($"What depends on {title.text}? What breaks if it changes?"));
            modernize.onClick.AddListener(() => Ask($"Modernize {title.text}: what would it look like as its own service?"));
        }

        protected override void OnBackend(IAtlasBackend b)
        {
            b.SelectionChanged += OnSelection;
            b.AnswerPosted += OnAnswer;
            b.TaskUpdated += OnTask;
        }

        void OnDestroy()
        {
            if (B == null) return;
            B.SelectionChanged -= OnSelection;
            B.AnswerPosted -= OnAnswer;
            B.TaskUpdated -= OnTask;
        }

        void OnSelection(string identity, string id)
        {
            if (identity != B.LocalIdentity) return;
            if (string.IsNullOrEmpty(id)) { nodeId = null; anchor = null; SetVisible(false); return; }
            Show(id);
        }

        void Show(string id)
        {
            var n = B.Graph?.Find(id);
            if (n == null) return;
            nodeId = id;
            anchor = MapBuilder.Instance != null ? MapBuilder.Instance.GetNodeTransform(id) : null;

            var kc = AtlasTheme.KindColor(n.kind);
            kindChip.text = n.kind;
            kindChip.color = kc;
            kindChipBg.color = new Color(kc.r, kc.g, kc.b, 0.18f);
            featureChip.text = n.feature;
            title.text = n.name;
            files.text = n.files != null && n.files.Length > 0 ? string.Join("  ·  ", n.files) : "";
            summary.text = n.summary;

            var role = B.LocalUser?.role ?? Role.Engineer;
            agentHeader.text = $"ATLAS · FOR {role.Label().ToUpperInvariant()}";
            waiting = false;
            if (answersByNode.TryGetValue(id, out var cached)) SetAnswer(cached);
            else SetHint();

            SetVisible(true);
            PlaceNow();
        }

        void SetHint()
        {
            answer.text = "Pinch Tell me more for an explanation written for your role.";
            answer.color = AtlasTheme.TextFaint;
            answer.fontStyle = FontStyles.Italic;
        }

        void SetAnswer(string text)
        {
            answer.text = text;
            answer.color = AtlasTheme.Text;
            answer.fontStyle = FontStyles.Normal;
        }

        void Ask(string question)
        {
            if (B == null || nodeId == null) return;
            B.AddTranscript(question);
            waiting = true;
        }

        void OnTask(AgentTask t)
        {
            if (t.askedBy != B.LocalIdentity || t.nodeId != nodeId) return;
            waiting = t.status != TaskStatus.Done;
        }

        void OnAnswer(AgentAnswer a)
        {
            var t = B.GetTask(a.taskId);
            if (t == null || string.IsNullOrEmpty(t.nodeId)) return;
            var myRole = B.LocalUser?.role ?? Role.Engineer;
            if (a.forRole != myRole) return;
            answersByNode[t.nodeId] = a.text;
            if (t.nodeId == nodeId) { waiting = false; SetAnswer(a.text); }
        }

        protected override void Update()
        {
            base.Update();
            if (waiting && answer != null)
            {
                int dots = 1 + (int)(Time.time * 3f) % 3;
                answer.text = "Thinking" + new string('.', dots);
                answer.color = AtlasTheme.Thinking;
                answer.fontStyle = FontStyles.Italic;
            }
        }

        void LateUpdate()
        {
            if (anchor != null && Visible) Place(false);
        }

        void PlaceNow() => Place(true);

        void Place(bool snap)
        {
            if (anchor == null) return;
            var target = anchor.position + Vector3.up * heightAboveNode;
            // Nudge slightly toward the user so the card never clips into the node.
            var cam = Camera.main;
            if (cam != null)
            {
                var toCam = cam.transform.position - anchor.position; toCam.y = 0;
                target += toCam.normalized * 0.03f;
            }
            transform.position = snap ? target : Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
