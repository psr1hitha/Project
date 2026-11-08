using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>First thing the user sees: a name field plus Engineer / PM / Architect. Calls join.</summary>
    public class RolePickerPanel : AtlasPanel
    {
        [SerializeField] TMP_InputField nameInput;
        [SerializeField] Button[] roleButtons = new Button[3];
        [SerializeField] TextMeshProUGUI statusText;

        public event Action<Role> Joined;

        protected override Vector2 CanvasSize => new Vector2(500, 560);

        protected override void Build()
        {
            var card = UIFactory.Card(transform, 460, new Vector2(0.5f, 0.5f), 26, 14);

            UIFactory.Text(card, "Atlas", 34, AtlasTheme.Text, FontStyles.Bold);
            UIFactory.Text(card, "Explore CardDemo, a legacy COBOL system, as a map on your table. Pick how you want it explained.",
                AtlasTheme.Body, AtlasTheme.TextMuted);

            UIFactory.Caps(card, "Your name");
            nameInput = UIFactory.InputField(card, "e.g. Srihitha");

            UIFactory.Caps(card, "Your role");
            var roles = new[] { Role.Engineer, Role.PM, Role.Architect };
            for (int i = 0; i < roles.Length; i++) roleButtons[i] = RoleButton(card, roles[i]);

            statusText = UIFactory.Text(card, "Connecting…", AtlasTheme.Small, AtlasTheme.TextFaint, FontStyles.Normal, TextAlignmentOptions.Center);
        }

        static Button RoleButton(Transform parent, Role role)
        {
            var color = AtlasTheme.RoleColor(role);
            var bg = UIFactory.Panel("Role: " + role, parent, AtlasTheme.Surface, AtlasTheme.RadiusSmall);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.colors = UIFactory.Tints();
            UIFactory.LE(bg.gameObject, minH: 78, prefH: 78);
            var h = UIFactory.HLayout(bg.gameObject, 0, 16, TextAnchor.MiddleLeft);
            h.padding = new RectOffset(0, 18, 0, 0);

            var stripe = UIFactory.Panel("Stripe", bg.transform, color, 4);
            stripe.raycastTarget = false;
            UIFactory.LE(stripe.gameObject, minH: 78, prefW: 8, minW: 8);

            var col = UIFactory.Rect("Text", bg.transform);
            UIFactory.VLayout(col.gameObject, 0, 2, TextAnchor.MiddleLeft);
            UIFactory.LE(col.gameObject, flexW: 1);
            UIFactory.Text(col, role.Label(), AtlasTheme.H2, AtlasTheme.Text, FontStyles.Bold);
            UIFactory.Text(col, AtlasTheme.RoleBlurb(role), AtlasTheme.Small + 1, AtlasTheme.TextMuted);

            var go = UIFactory.Text(bg.transform, "Join  ›", AtlasTheme.Body, color, FontStyles.Bold, TextAlignmentOptions.Right, "Go");
            UIFactory.LE(go.gameObject, prefW: 80);
            return btn;
        }

        protected override void Wire()
        {
            var roles = new[] { Role.Engineer, Role.PM, Role.Architect };
            for (int i = 0; i < roleButtons.Length; i++)
            {
                var r = roles[i];
                roleButtons[i].onClick.AddListener(() => Pick(r));
                roleButtons[i].interactable = false;
            }
        }

        protected override void OnBackend(IAtlasBackend b)
        {
            foreach (var btn in roleButtons) btn.interactable = true;
            statusText.text = b.IsConnected ? "Pinch a role to join" : "Waiting for the server…";
        }

        protected override void Update()
        {
            base.Update();
            if (B != null && statusText != null && Visible)
                statusText.text = B.IsConnected ? "Pinch a role to join" : "Waiting for the server…";
        }

        void Pick(Role role)
        {
            if (B == null) return;
            var name = nameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) name = role.Label() + " " + UnityEngine.Random.Range(10, 99);
            B.Join(name, role);
            SetVisible(false);
            Joined?.Invoke(role);
        }
    }
}
