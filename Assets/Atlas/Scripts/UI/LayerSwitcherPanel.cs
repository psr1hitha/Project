using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>Segmented control for code / business / architecture. Calls set_layer and mirrors view_state.layer.</summary>
    public class LayerSwitcherPanel : AtlasPanel
    {
        [SerializeField] Button[] segments = new Button[3];
        [SerializeField] TextMeshProUGUI caption;

        protected override Vector2 CanvasSize => new Vector2(440, 130);

        static readonly string[] Captions =
        {
            "Programs and files where they live in the code",
            "Grouped by business feature",
            "Online programs, batch jobs and data files",
        };

        protected override void Build()
        {
            var card = UIFactory.Card(transform, 420, new Vector2(0.5f, 0.5f), 10, 8);
            var row = UIFactory.Row(card, 6);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < 3; i++)
            {
                segments[i] = UIFactory.Button(row, ((MapLayer)i).Label(), AtlasTheme.Surface, AtlasTheme.Text, 42, AtlasTheme.Body);
                UIFactory.LE(segments[i].gameObject, minH: 42, prefH: 42, flexW: 1);
            }
            caption = UIFactory.Text(card, Captions[0], AtlasTheme.Small, AtlasTheme.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
        }

        protected override void Wire()
        {
            for (int i = 0; i < 3; i++)
            {
                var layer = (MapLayer)i;
                segments[i].onClick.AddListener(() => B?.SetLayer(layer));
            }
        }

        protected override void OnBackend(IAtlasBackend b)
        {
            b.LayerChanged += Show;
            Show(b.CurrentLayer);
        }

        void OnDestroy() { if (B != null) B.LayerChanged -= Show; }

        void Show(MapLayer layer)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == (int)layer;
                ((Image)segments[i].targetGraphic).color = on ? AtlasTheme.Agent : AtlasTheme.Surface;
                segments[i].GetComponentInChildren<TextMeshProUGUI>().color = on ? AtlasTheme.PanelBg : AtlasTheme.TextMuted;
            }
            caption.text = Captions[(int)layer];
        }
    }
}
