using UnityEngine;

namespace Atlas
{
    /// <summary>
    /// Runtime materials that survive Android builds: "Lit" copies the render pipeline's default
    /// material (always included), "Glow" uses Sprites/Default (in Always Included Shaders).
    /// </summary>
    public static class AtlasMaterials
    {
        static Material litTemplate;

        public static Material Lit(Color c)
        {
            if (litTemplate == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                litTemplate = new Material(tmp.GetComponent<MeshRenderer>().sharedMaterial) { name = "Atlas Lit" };
                Object.Destroy(tmp);
            }
            var m = new Material(litTemplate);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            return m;
        }

        /// <summary>Unlit, supports alpha. Good for glows, outlines, ghosts and flow particles.</summary>
        public static Material Glow(Color c)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh) { name = "Atlas Glow" };
            SetColor(m, c);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
