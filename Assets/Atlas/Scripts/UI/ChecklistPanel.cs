using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>This person's checklist_item rows with a progress bar; items tick as the agent completes them.</summary>
    public class ChecklistPanel : AtlasPanel
    {
        [SerializeField] TextMeshProUGUI userName, roleChip, progressLabel;
        [SerializeField] Image roleDot, roleChipBg, progressFill;
        [SerializeField] RectTransform itemList;

        readonly Dictionary<ulong, (RectTransform row, Image box, Image tick, TextMeshProUGUI text)> rows = new Dictionary<ulong, (RectTransform, Image, Image, TextMeshProUGUI)>();
        readonly Dictionary<ulong, float> popUntil = new Dictionary<ulong, float>();
        float fillTarget, fillNow;
        Color roleColor = AtlasTheme.Agent;

        protected override Vector2 CanvasSize => new Vector2(360, 480);

        protected override void Build()
        {
            ((RectTransform)transform).pivot = new Vector2(0.5f, 0f);
            var card = UIFactory.Card(transform, 340, new Vector2(0.5f, 0f), 20, 12);

            var who = UIFactory.Row(card, 10);
            roleDot = UIFactory.Panel("RoleDot", who, AtlasTheme.Agent, 6);
            UIFactory.LE(roleDot.gameObject, minW: 12, prefW: 12, minH: 12, prefH: 12);
            userName = UIFactory.Text(who, "—", AtlasTheme.Body, AtlasTheme.Text, FontStyles.Bold);
            UIFactory.LE(userName.gameObject, flexW: 1);
            roleChip = UIFactory.Chip(who, "Role", AtlasTheme.Agent, out roleChipBg);

            UIFactory.Text(card, "Your missions", AtlasTheme.H2, AtlasTheme.Text, FontStyles.Bold);

            // Progress bar: track + child fill whose right anchor = progress.
            var track = UIFactory.Panel("Progress", card, AtlasTheme.Surface, 4);
            UIFactory.LE(track.gameObject, minH: 8, prefH: 8);
            progressFill = UIFactory.Panel("Fill", track.transform, AtlasTheme.Agent, 4);
            var fr = progressFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            progressLabel = UIFactory.Text(card, "0 of 0 done", AtlasTheme.Small, AtlasTheme.TextMuted);

            itemList = UIFactory.Rect("Items", card);
            UIFactory.VLayout(itemList.gameObject, 0, 6);
        }

        protected override void OnBackend(IAtlasBackend b)
        {
            b.ChecklistChanged += Render;
            b.UserChanged += OnUser;
            b.LocalUserJoined += OnUser;
            if (b.LocalUser != null) OnUser(b.LocalUser);
            Render(b.MyChecklist);
        }

        void OnDestroy()
        {
            if (B == null) return;
            B.ChecklistChanged -= Render;
            B.UserChanged -= OnUser;
            B.LocalUserJoined -= OnUser;
        }

        void OnUser(UserInfo u)
        {
            if (u.identity != B.LocalIdentity) return;
            roleColor = AtlasTheme.RoleColor(u.role);
            userName.text = u.name;
            roleChip.text = u.role.Label();
            roleChip.color = roleColor;
            roleChipBg.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.18f);
            roleDot.color = roleColor;
            progressFill.color = roleColor;
            foreach (var kv in rows) Style(kv.Key, B.MyChecklist.FirstOrDefault(i => i.id == kv.Key)?.done ?? false);
        }

        void Render(IReadOnlyList<ChecklistItem> items)
        {
            if (items == null) return;
            var ids = new HashSet<ulong>(items.Select(i => i.id));
            foreach (var stale in rows.Keys.Where(k => !ids.Contains(k)).ToList())
            {
                Destroy(rows[stale].row.gameObject);
                rows.Remove(stale);
            }

            foreach (var item in items)
            {
                if (!rows.TryGetValue(item.id, out var r)) { r = MakeRow(item); rows[item.id] = r; }
                bool wasDone = r.tick.enabled;
                r.text.text = item.done ? $"<s>{item.text}</s>" : item.text;
                Style(item.id, item.done);
                if (item.done && !wasDone) popUntil[item.id] = Time.time + 0.35f;
            }

            int done = items.Count(i => i.done);
            fillTarget = items.Count == 0 ? 0 : (float)done / items.Count;
            progressLabel.text = items.Count == 0 ? "Join to get your missions"
                : done == items.Count ? "All missions done. Nice work."
                : $"{done} of {items.Count} done";
        }

        (RectTransform, Image, Image, TextMeshProUGUI) MakeRow(ChecklistItem item)
        {
            var row = UIFactory.Row(itemList, 12, TextAnchor.UpperLeft, "Item");
            var box = UIFactory.Panel("Box", row, AtlasTheme.Surface, 6);
            UIFactory.LE(box.gameObject, minW: 24, prefW: 24, minH: 24, prefH: 24);
            var tick = UIFactory.Panel("Tick", box.transform, roleColor, 4);
            UIFactory.Stretch(tick.rectTransform, 5);
            tick.enabled = false;
            var t = UIFactory.Text(row, item.text, AtlasTheme.Body, AtlasTheme.Text);
            UIFactory.LE(t.gameObject, flexW: 1);
            return (row, box, tick, t);
        }

        void Style(ulong id, bool done)
        {
            if (!rows.TryGetValue(id, out var r)) return;
            r.tick.enabled = done;
            r.tick.color = roleColor;
            r.box.color = done ? new Color(roleColor.r, roleColor.g, roleColor.b, 0.25f) : AtlasTheme.Surface;
            r.text.color = done ? AtlasTheme.TextMuted : AtlasTheme.Text;
        }

        protected override void Update()
        {
            base.Update();
            if (progressFill == null) return;
            fillNow = Mathf.MoveTowards(fillNow, fillTarget, Time.deltaTime * 1.5f);
            progressFill.rectTransform.anchorMax = new Vector2(fillNow, 1);

            foreach (var kv in rows)
            {
                float s = 1f;
                if (popUntil.TryGetValue(kv.Key, out var until) && Time.time < until)
                    s = 1f + Mathf.Sin((1 - (until - Time.time) / 0.35f) * Mathf.PI) * 0.35f;
                kv.Value.box.rectTransform.localScale = Vector3.one * s;
            }
        }
    }
}
