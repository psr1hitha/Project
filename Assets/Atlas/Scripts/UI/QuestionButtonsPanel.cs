using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>
    /// The fallback question buttons: the demo questions pre-typed, each calling add_transcript.
    /// Used in the editor and whenever speech-to-text fails on stage.
    /// </summary>
    public class QuestionButtonsPanel : AtlasPanel
    {
        [Tooltip("Edit freely; keep them matched to B2's cached answers.")]
        public string[] questions =
        {
            "How does a payment get processed?",
            "What breaks if we change interest calculation?",
            "Show me the system by business feature",
            "What would billing look like as its own service?",
            "Where should I start?",
        };

        [SerializeField] List<Button> buttons = new List<Button>();
        [SerializeField] TextMeshProUGUI hint;

        protected override Vector2 CanvasSize => new Vector2(380, 440);

        protected override void Build()
        {
            ((RectTransform)transform).pivot = new Vector2(0.5f, 0f);
            var card = UIFactory.Card(transform, 360, new Vector2(0.5f, 0f), 18, 8);
            UIFactory.Caps(card, "Try asking");
            buttons.Clear();
            foreach (var q in questions)
            {
                var b = UIFactory.Button(card, q, AtlasTheme.Surface, AtlasTheme.Text, 46, AtlasTheme.Body - 1, TextAlignmentOptions.Left);
                b.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Normal;
                buttons.Add(b);
            }
            hint = UIFactory.Text(card, "Or hold pinch (Space in the editor) and just ask.", AtlasTheme.Small, AtlasTheme.TextFaint, FontStyles.Normal, TextAlignmentOptions.Center);
        }

        protected override void Wire()
        {
            for (int i = 0; i < buttons.Count && i < questions.Length; i++)
            {
                var q = questions[i];
                buttons[i].onClick.AddListener(() => B?.AddTranscript(q));
            }
        }
    }
}
