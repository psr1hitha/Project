using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>
    /// Shows the agent's reasoning steps for the current task, newest at the bottom, with a
    /// thinking indicator while the task is working. "Current task" = the most recent one.
    /// </summary>
    public class ReasoningPanel : AtlasPanel
    {
        [SerializeField] TextMeshProUGUI title, statusChip, question, footer;
        [SerializeField] Image statusBg;
        [SerializeField] RectTransform stepList;

        [Tooltip("Older steps scroll off the top.")]
        public int maxVisibleSteps = 7;

        AgentTask current;
        int stepCount;
        float startedAt;
        readonly List<(RectTransform row, CanvasGroup cg, TextMeshProUGUI text, Image dot)> rows = new List<(RectTransform, CanvasGroup, TextMeshProUGUI, Image)>();

        protected override Vector2 CanvasSize => new Vector2(420, 560);

        protected override void Build()
        {
            ((RectTransform)transform).pivot = new Vector2(0.5f, 0f);
            var card = UIFactory.Card(transform, 400, new Vector2(0.5f, 0f), 20, 10);

            var header = UIFactory.Row(card, 10);
            title = UIFactory.Text(header, "Atlas", AtlasTheme.H2, AtlasTheme.Text, FontStyles.Bold);
            UIFactory.LE(title.gameObject, flexW: 1);
            statusChip = UIFactory.Chip(header, "Idle", AtlasTheme.Waiting, out statusBg);

            question = UIFactory.Text(card, "", AtlasTheme.Body, AtlasTheme.TextMuted, FontStyles.Italic);

            UIFactory.Divider(card);

            stepList = UIFactory.Rect("Steps", card);
            UIFactory.VLayout(stepList.gameObject, 0, 8);

            footer = UIFactory.Text(card, "", AtlasTheme.Small, AtlasTheme.TextFaint);
        }

        protected override void OnBackend(IAtlasBackend b)
        {
            b.TaskUpdated += OnTask;
            b.AgentStepAdded += OnStep;
            ShowIdle();
        }

        void OnDestroy()
        {
            if (B == null) return;
            B.TaskUpdated -= OnTask;
            B.AgentStepAdded -= OnStep;
        }

        void ShowIdle()
        {
            title.text = "Atlas";
            question.text = "Ask a question out loud, or pinch one of the suggested questions.";
            SetStatus("Listening", AtlasTheme.Waiting);
            footer.text = "";
        }

        void OnTask(AgentTask t)
        {
            if (current == null || t.id > current.id)
            {
                current = t;
                ClearSteps();
                stepCount = 0;
                startedAt = Time.time;
                question.text = t.askedBy == "agent"
                    ? "Atlas noticed something"
                    : $"“{t.question}”  <color=#5D6880>— {AskerName(t.askedBy)}</color>";
                footer.text = "";
            }
            if (t.id != current.id) return;
            current = t;

            switch (t.status)
            {
                case TaskStatus.Waiting: SetStatus("Queued", AtlasTheme.Waiting); break;
                case TaskStatus.Working: SetStatus("Reasoning", AtlasTheme.Thinking); break;
                case TaskStatus.Done:
                    SetStatus("Answered", AtlasTheme.Success);
                    footer.text = $"{stepCount} steps · {Time.time - startedAt:0.0}s";
                    break;
            }
        }

        string AskerName(string identity)
        {
            if (B.LocalUser != null && identity == B.LocalIdentity) return "you";
            foreach (var u in B.Users) if (u.identity == identity) return u.name;
            return "teammate";
        }

        void OnStep(AgentStep s)
        {
            if (current == null || s.taskId != current.id) return;
            stepCount++;
            AddRow(s.text);
        }

        void AddRow(string text)
        {
            // Dim the older rows so the newest reads first.
            foreach (var r in rows) { r.text.color = AtlasTheme.TextMuted; r.dot.color = AtlasTheme.TextFaint; }

            var row = UIFactory.Row(stepList, 10, TextAnchor.UpperLeft, "Step");
            var cg = row.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            var dotHolder = UIFactory.Rect("DotHolder", row);
            UIFactory.LE(dotHolder.gameObject, prefW: 10, minW: 10, minH: 20, prefH: 20);
            var dot = UIFactory.Panel("Dot", dotHolder, AtlasTheme.Thinking, 5);
            dot.raycastTarget = false;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(10, 10);
            var t = UIFactory.Text(row, text, AtlasTheme.Body, AtlasTheme.Text);
            UIFactory.LE(t.gameObject, flexW: 1);
            rows.Add((row, cg, t, dot));

            while (rows.Count > maxVisibleSteps)
            {
                Destroy(rows[0].row.gameObject);
                rows.RemoveAt(0);
            }
        }

        void ClearSteps()
        {
            foreach (var r in rows) if (r.row != null) Destroy(r.row.gameObject);
            rows.Clear();
        }

        void SetStatus(string label, Color c)
        {
            statusChip.text = label;
            statusChip.color = c;
            statusBg.color = new Color(c.r, c.g, c.b, 0.18f);
        }

        protected override void Update()
        {
            base.Update();
            foreach (var r in rows) if (r.cg.alpha < 1) r.cg.alpha = Mathf.MoveTowards(r.cg.alpha, 1, Time.deltaTime * 4);

            if (current != null && current.status != TaskStatus.Done && title != null)
            {
                // Animated "Atlas is thinking •••"
                float t = Time.time * 2.5f;
                string Dot(int i) => $"<alpha=#{(int)(Mathf.Lerp(0.25f, 1f, (Mathf.Sin(t - i * 0.9f) + 1) / 2) * 255):X2}>•";
                title.text = $"Atlas is thinking {Dot(0)}{Dot(1)}{Dot(2)}";
                if (rows.Count > 0)
                {
                    var last = rows[rows.Count - 1].dot;
                    var s = 10 + Mathf.Sin(Time.time * 6) * 2;
                    last.rectTransform.sizeDelta = new Vector2(s, s);
                }
            }
            else if (title != null && current != null)
            {
                title.text = "Atlas";
            }
        }
    }
}
