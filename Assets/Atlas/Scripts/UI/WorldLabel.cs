using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>Small always-facing text label with a dark pill behind it (readable over passthrough).</summary>
    public static class WorldLabel
    {
        public static TextMeshProUGUI Create(Transform parent, string text, Vector3 localPos, float px = 13f, Color? color = null, bool pill = true, string name = "Label")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            UIFactory.SetupWorldCanvas(go, new Vector2(320, 80));
            // Keep labels a constant real-world size even if the parent is scaled.
            var lossy = parent.lossyScale.x;
            if (lossy > 1e-5f) go.transform.localScale = Vector3.one * AtlasTheme.PixelsToMeters / lossy;
            go.GetComponent<GraphicRaycaster>().enabled = false;   // labels never block pinches
            var face = go.AddComponent<FaceCamera>();
            face.smoothing = 0;

            Transform host = go.transform;
            if (pill)
            {
                var bg = UIFactory.Panel("Pill", go.transform, new Color(0.05f, 0.07f, 0.11f, 0.82f), 9f);
                bg.raycastTarget = false;
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                var h = UIFactory.HLayout(bg.gameObject, 0, 0, TextAnchor.MiddleCenter);
                h.padding = new RectOffset(10, 10, 4, 5);
                UIFactory.FitHeight(bg.gameObject, fitWidth: true);
                host = bg.transform;
            }
            var t = UIFactory.Text(host, text, px, color ?? AtlasTheme.Text, FontStyles.Bold, TextAlignmentOptions.Center);
            if (!pill)
            {
                UIFactory.Stretch(t.rectTransform);
            }
            return t;
        }
    }
}
