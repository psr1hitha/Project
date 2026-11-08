using UnityEngine;
using UnityEngine.UI;

namespace Atlas.UI
{
    /// <summary>
    /// Gives an Image rounded corners using a sprite generated at runtime (no texture assets needed).
    /// The sprite isn't saved with the scene, so this component re-applies it whenever it loads.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Image))]
    public class RoundedImage : MonoBehaviour
    {
        public float radius = 14f;

        const int Size = 128;
        const int Corner = 32;
        static Sprite sprite;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null) return;
            img.sprite = GetSprite();
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = Corner / Mathf.Max(1f, radius);
        }

        public static Sprite GetSprite()
        {
            if (sprite != null) return sprite;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
                name = "AtlasRounded",
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Signed distance to a rounded square
                float fx = Mathf.Abs(x + 0.5f - Size / 2f) - (Size / 2f - Corner);
                float fy = Mathf.Abs(y + 0.5f - Size / 2f) - (Size / 2f - Corner);
                float outside = new Vector2(Mathf.Max(fx, 0), Mathf.Max(fy, 0)).magnitude;
                float inside = Mathf.Min(Mathf.Max(fx, fy), 0);
                float d = outside + inside - Corner;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                px[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(Corner, Corner, Corner, Corner));
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = "AtlasRounded";
            return sprite;
        }
    }
}
