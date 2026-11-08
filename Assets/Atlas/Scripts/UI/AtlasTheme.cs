using UnityEngine;

namespace Atlas.UI
{
    /// <summary>One place for every color and size, so the look stays consistent.</summary>
    public static class AtlasTheme
    {
        static Color Hex(string h, float a = 1f) { ColorUtility.TryParseHtmlString(h, out var c); c.a = a; return c; }

        // Surfaces (dark glass that reads well over passthrough)
        public static readonly Color PanelBg = Hex("#0E1320", 0.92f);
        public static readonly Color Surface = Hex("#1A2234", 0.95f);
        public static readonly Color SurfaceHover = Hex("#243049");
        public static readonly Color Divider = Hex("#FFFFFF", 0.08f);

        // Text
        public static readonly Color Text = Hex("#EEF1F7");
        public static readonly Color TextMuted = Hex("#8E99AE");
        public static readonly Color TextFaint = Hex("#5D6880");

        // Agent + states
        public static readonly Color Agent = Hex("#7C9CFF");
        public static readonly Color Thinking = Hex("#B794F6");
        public static readonly Color Speaking = Hex("#7CF2FF");
        public static readonly Color Listening = Hex("#F87171");
        public static readonly Color Success = Hex("#4ADE80");
        public static readonly Color Waiting = Hex("#94A3B8");

        // Scene command colors
        public static readonly Color Highlight = Hex("#22D3EE");
        public static readonly Color Blast1 = Hex("#EF4444");
        public static readonly Color Blast2 = Hex("#F59E0B");
        public static readonly Color Blast3 = Hex("#FACC15");
        public static readonly Color BlastOrigin = Hex("#FFFFFF");
        public static readonly Color Ghost = Hex("#C084FC");
        public static readonly Color Edge = Hex("#9AA6BF", 0.55f);
        public static readonly Color OtherSelection = Hex("#F472B6");

        public static Color RoleColor(Role r) => r switch
        {
            Role.Engineer => Hex("#2DD4BF"),
            Role.PM => Hex("#FBBF24"),
            _ => Hex("#A78BFA"),
        };

        public static string RoleBlurb(Role r) => r switch
        {
            Role.Engineer => "Code paths, files and data flow",
            Role.PM => "Business features and customer impact",
            _ => "Services, boundaries and risk",
        };

        public static Color KindColor(string kind)
        {
            var k = (kind ?? "").ToLowerInvariant();
            if (k.Contains("batch")) return Hex("#5B8DEF");
            if (k.Contains("online") || k.Contains("screen")) return Hex("#34D399");
            if (k.Contains("file") || k.Contains("data") || k.Contains("db")) return Hex("#FB923C");
            if (k.Contains("copybook") || k.Contains("lib")) return Hex("#F472B6");
            return Hex("#A0AEC0");
        }

        public static Color BlastColor(int depth) => depth <= 1 ? Blast1 : depth == 2 ? Blast2 : Blast3;

        // Sizes. Canvases use 1 px = 1 mm, so these are millimetres in the headset.
        public const float PixelsToMeters = 0.001f;
        public const float Radius = 14f;
        public const float RadiusSmall = 8f;
        public const int Pad = 20;

        public const float H1 = 26f;
        public const float H2 = 19f;
        public const float Body = 16f;
        public const float Small = 13f;
        public const float Caps = 11.5f;
    }
}
