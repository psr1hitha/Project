using TMPro;
using UnityEngine;

namespace Atlas.UI
{
    /// <summary>Subtitle above the agent orb showing what Atlas is saying (also the fallback if TTS fails).</summary>
    public class CaptionPanel : AtlasPanel
    {
        [SerializeField] TextMeshProUGUI speaker, body;
        float hideAt = -1;

        protected override Vector2 CanvasSize => new Vector2(560, 260);

        protected override void Build()
        {
            ((RectTransform)transform).pivot = new Vector2(0.5f, 0f);
            var card = UIFactory.Card(transform, 520, new Vector2(0.5f, 0f), 18, 6);
            speaker = UIFactory.Caps(card, "Atlas", AtlasTheme.Speaking);
            body = UIFactory.Text(card, "", AtlasTheme.Body + 1, AtlasTheme.Text);
        }

        public void Say(string text, float seconds, string who = "Atlas")
        {
            speaker.text = who.ToUpperInvariant();
            body.text = text;
            hideAt = Time.time + seconds + 2.5f;
            SetVisible(true);
        }

        protected override void Update()
        {
            base.Update();
            if (hideAt > 0 && Time.time > hideAt) { hideAt = -1; SetVisible(false); }
        }
    }
}
