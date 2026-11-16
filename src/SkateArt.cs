using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Materials, textures, the deck mesh and the shop icon - all generated in code.
    internal static class SkateArt
    {
        private static Shader shader;
        private static bool shaderSearched;
        private static Material grip, graphic, wood, wheel, metal;
        private static Mesh deckMesh;
        private static Sprite icon;

        // ------------------------------------------------------------ shader

        // The default Unity material is pink in this game (its shader isn't in the build),
        // so we look for a shader the game actually uses.
        private static Shader GetShader()
        {
            if (shaderSearched) return shader;
            shaderSearched = true;
            string[] names = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit" };
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null && s.isSupported) { shader = s; break; }
            }
            if (shader == null)
            {
                // fall back to whatever lit shader the loaded materials use
                var seen = new HashSet<string>();
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m == null || m.shader == null || !m.shader.isSupported) continue;
                    string sn = m.shader.name;
                    seen.Add(sn);
                    if (shader == null && sn.Contains("Lit") && !sn.StartsWith("Hidden") && (m.HasProperty("_BaseColor") || m.HasProperty("_Color")))
                        shader = m.shader;
                }
                SkatePlugin.Log.LogWarning("URP Lit not found. Loaded shaders: " + string.Join(", ", seen));
            }
            SkatePlugin.Log.LogInfo("Board shader: " + (shader != null ? shader.name : "NONE"));
            return shader;
        }

        public static Material NewMat(Color color, Texture2D tex = null, float smooth = 0.2f, float metallic = 0f)
        {
            var s = GetShader();
            var m = s != null ? new Material(s) : new Material(Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return m;
        }

        public static Material Grip => grip ? grip : grip = NewMat(Color.white, GripTex(), 0.05f);
        public static Material Graphic => graphic ? graphic : graphic = NewMat(Color.white, GraphicTex(), 0.35f);
        public static Material Wood => wood ? wood : wood = NewMat(Color.white, WoodTex(), 0.25f);
        public static Material Wheel => wheel ? wheel : wheel = NewMat(new Color(0.96f, 0.93f, 0.82f), null, 0.45f);
        public static Material Metal => metal ? metal : metal = NewMat(new Color(0.7f, 0.72f, 0.76f), null, 0.7f, 0.8f);

        // board parts (upgrades)
        private static Material wheelSpeed, wheelSoft, maple, rocketRed;
        public static Material RocketRed => rocketRed ? rocketRed : rocketRed = NewMat(new Color(0.85f, 0.12f, 0.1f), null, 0.55f, 0.2f);
        public static Material WheelSpeed => wheelSpeed ? wheelSpeed : wheelSpeed = NewMat(new Color(1f, 0.38f, 0.12f), null, 0.65f);  // hard, shiny, orange
        public static Material WheelSoft => wheelSoft ? wheelSoft : wheelSoft = NewMat(new Color(0.4f, 0.9f, 0.45f), null, 0.12f);    // rubbery, matte, green
        public static Material Maple => maple ? maple : maple = NewMat(Color.white, MapleTex(), 0.3f);                                 // light maple plies on the edge

        // Light maple layers (the edge of a maple deck), no pink glue line
        private static Texture2D MapleTex()
        {
            var t = NewTex(4, 32, TextureWrapMode.Repeat);
            var px = new Color[4 * 32];
            for (int y = 0; y < 32; y++)
            {
                Color c = (y / 3) % 2 == 0 ? new Color(0.97f, 0.88f, 0.66f) : new Color(0.9f, 0.76f, 0.52f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // Shop icon of a board part: two wheels on an axle, a bare deck, a rocket, a jerry can
        public static Sprite PartIcon(Color color, PartKind kind)
        {
            bool wheels = kind == PartKind.Wheels;
            if (kind == PartKind.Rocket || kind == PartKind.Fuel) return ShapeIcon(color, kind);
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[S * S];
            Color dark = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                float dx = x - S * 0.5f, dy = y - S * 0.5f;
                if (wheels)
                {
                    if (Mathf.Abs(dy) < 4f && Mathf.Abs(dx) < 50f) c = new Color(0.72f, 0.74f, 0.78f, 1f); // axle
                    foreach (float wx in new[] { -34f, 34f })
                    {
                        float d = Mathf.Sqrt((dx - wx) * (dx - wx) + dy * dy);
                        if (d < 26f) c = d > 22f ? dark : d < 7f ? new Color(0.72f, 0.74f, 0.78f, 1f) : new Color(color.r, color.g, color.b, 1f);
                    }
                }
                else
                {
                    float ang = 35f * Mathf.Deg2Rad, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                    float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                    const float r = 15f, straight = 37f;
                    float du = Mathf.Max(0f, Mathf.Abs(u) - straight);
                    float dist = Mathf.Sqrt(du * du + v * v);
                    if (dist < r) c = dist > r - 2.5f ? dark : ((int)((v + 20f) / 3f) % 2 == 0 ? new Color(color.r, color.g, color.b, 1f) : new Color(color.r * 0.9f, color.g * 0.86f, color.b * 0.8f, 1f));
                }
                px[y * S + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            var sp = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sp;
        }

        private static Sprite ShapeIcon(Color color, PartKind kind)
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[S * S];
            Color red = new Color(0.88f, 0.14f, 0.1f, 1f), dark = new Color(0.35f, 0.05f, 0.04f, 1f), metal = new Color(0.72f, 0.74f, 0.78f, 1f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                float dx = x - S * 0.5f, dy = y - S * 0.5f;
                if (kind == PartKind.Rocket)
                {
                    // tilted up-right: body, pointy nose, fins, a flame out of the back
                    float ang = 35f * Mathf.Deg2Rad, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                    float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                    if (u > -60f && u < -34f && Mathf.Abs(v) < (u + 60f) * 0.35f) c = Color.Lerp(new Color(1f, 0.95f, 0.4f, 1f), new Color(1f, 0.4f, 0.05f, 1f), (-34f - u) / 26f); // flame
                    if (u > -34f && u < -26f && Mathf.Abs(v) < 11f) c = metal;                                 // nozzle
                    if (u > -40f && u < -20f && Mathf.Abs(v) < 26f - (u + 40f) * 0.6f && Mathf.Abs(v) > 8f) c = dark; // fins
                    if (u > -26f && u < 28f && Mathf.Abs(v) < 13f) c = Mathf.Abs(v) > 10.5f ? dark : (u > 0f && u < 6f ? Color.white : red); // body + white band
                    if (u >= 28f && u < 52f && Mathf.Abs(v) < 13f * (52f - u) / 24f) c = red;             // nose cone
                }
                else
                {
                    // jerry can: rounded box, a handle on top, a spout, a yellow cross
                    bool body = Mathf.Abs(dx) < 34f && dy > -48f && dy < 30f;
                    if (body) c = (Mathf.Abs(dx) > 31f || dy < -45f || dy > 27f) ? dark : red;
                    if (body && (Mathf.Abs(dx - dy * 0.0f) < 4f && dy > -38f && dy < 20f || Mathf.Abs(dy + 9f) < 4f && Mathf.Abs(dx) < 22f)) c = new Color(color.r, color.g, color.b, 1f);
                    if (dy >= 30f && dy < 42f && dx > -26f && dx < 6f && !(dy < 38f && dx > -20f && dx < 0f)) c = dark; // handle
                    if (dx > 14f && dx < 26f && dy >= 30f && dy < 50f) c = metal;                                       // spout
                }
                px[y * S + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            var sp = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sp;
        }

        // ------------------------------------------------------------ textures

        private static Texture2D NewTex(int w, int h, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }

        // Grip tape on top of the deck. Whole deck = whole picture (top of the picture = nose).
        public static Texture2D GripTex(int W = 192, int H = 768)
        {
            var t = NewTex(W, H, TextureWrapMode.Clamp);
            var rng = new System.Random(11);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                float g = 0.07f + (float)rng.NextDouble() * 0.08f;
                px[i] = new Color(g, g, g * 1.05f);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // The art on the bottom of the deck (you see it during flips). Top of the picture = nose.
        public static Texture2D GraphicTex(int W = 128, int H = 384)
        {
            var t = NewTex(W, H, TextureWrapMode.Clamp);
            float k = W / 128f;
            var px = new Color[W * H];
            Color a = new Color(1f, 0.25f, 0.55f), b = new Color(0.38f, 0.16f, 0.85f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = y / (float)H;
                Color c = Color.Lerp(a, b, v);
                if ((int)((x + y) / (14 * k)) % 2 == 0) c *= 0.86f;
                // big ring + dot in the middle
                float dx = (x - W * 0.5f) / k, dy = (y - H * 0.5f) / k;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 38 && r < 48) c = Color.white;
                if (r < 18) c = new Color(1f, 0.85f, 0.2f);
                // stripes near nose and tail
                if (Mathf.Abs(y - H * 0.18f) < 5 * k || Mathf.Abs(y - H * 0.82f) < 5 * k) c = Color.white;
                c.a = 1f;
                px[y * W + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // Plywood layers on the edge of the deck
        private static Texture2D WoodTex()
        {
            var t = NewTex(4, 32, TextureWrapMode.Repeat);
            var px = new Color[4 * 32];
            for (int y = 0; y < 32; y++)
            {
                Color c = (y / 4) % 2 == 0 ? new Color(0.86f, 0.66f, 0.42f) : new Color(0.72f, 0.5f, 0.3f);
                if (y >= 14 && y < 18) c = new Color(1f, 0.3f, 0.6f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------ deck mesh

        public const float DeckLength = 0.80f;
        public const float DeckWidth = 0.21f;
        public const float DeckThickness = 0.013f;

        private static float HalfWidth(float z)
        {
            float r = DeckWidth * 0.5f;
            float straight = DeckLength * 0.5f - r;
            float dz = Mathf.Abs(z) - straight;
            if (dz <= 0) return r;
            return Mathf.Sqrt(Mathf.Max(0f, r * r - dz * dz));
        }

        private static float KickHeight(float z)
        {
            const float start = 0.235f;
            float az = Mathf.Abs(z);
            if (az <= start) return 0f;
            float k = (az - start) / (DeckLength * 0.5f - start);
            return k * k * 0.06f;
        }

        // Rounded nose/tail, kicked up ends, slight concave. 3 submeshes: grip, graphic, edge.
        public static Mesh DeckMesh()
        {
            if (deckMesh) return deckMesh;
            const int rows = 64, cols = 10;
            float half = DeckLength * 0.5f - 0.0005f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var bottom = new List<int>();
            var side = new List<int>();

            // denser rows near the rounded ends
            float RowZ(int i)
            {
                float t = i / (float)rows * 2f - 1f;              // -1..1
                float s = Mathf.Sign(t) * (1f - Mathf.Pow(1f - Mathf.Abs(t), 1.6f));
                return s * half;
            }

            void Surface(bool isTop, List<int> tris)
            {
                int start = verts.Count;
                for (int i = 0; i <= rows; i++)
                {
                    float z = RowZ(i);
                    float hw = Mathf.Max(HalfWidth(z), 0.001f);
                    float y = KickHeight(z);
                    for (int j = 0; j <= cols; j++)
                    {
                        float u = j / (float)cols;
                        float x = Mathf.Lerp(-hw, hw, u);
                        float concave = 0.005f * (2 * u - 1) * (2 * u - 1);
                        float yy = y + concave + (isTop ? DeckThickness * 0.5f : -DeckThickness * 0.5f);
                        verts.Add(new Vector3(x, yy, z));
                        float uu = x / DeckWidth + 0.5f;
                        // one picture per side, top of the picture = nose, not mirrored when you look at that side
                        uvs.Add(new Vector2(isTop ? uu : 1f - uu, z / DeckLength + 0.5f));
                    }
                }
                for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    int a = start + i * (cols + 1) + j, b = a + 1, c = a + cols + 1, d = c + 1;
                    if (isTop) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                }
            }
            Surface(true, top);
            Surface(false, bottom);

            // edge band all the way around
            void Edge(bool right)
            {
                int start = verts.Count;
                for (int i = 0; i <= rows; i++)
                {
                    float z = RowZ(i);
                    float hw = Mathf.Max(HalfWidth(z), 0.001f) * (right ? 1f : -1f);
                    float y = KickHeight(z) + 0.005f;
                    verts.Add(new Vector3(hw, y + DeckThickness * 0.5f, z));
                    verts.Add(new Vector3(hw, y - DeckThickness * 0.5f, z));
                    uvs.Add(new Vector2(z * 4f, 1f));
                    uvs.Add(new Vector2(z * 4f, 0f));
                }
                for (int i = 0; i < rows; i++)
                {
                    int ti = start + i * 2, bi = ti + 1, ti1 = ti + 2, bi1 = ti + 3;
                    if (!right) { side.Add(ti); side.Add(bi); side.Add(ti1); side.Add(ti1); side.Add(bi); side.Add(bi1); }
                    else { side.Add(ti); side.Add(ti1); side.Add(bi); side.Add(ti1); side.Add(bi1); side.Add(bi); }
                }
            }
            Edge(false);
            Edge(true);

            var mesh = new Mesh { name = "CWSkate_Deck" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(bottom, 1);
            mesh.SetTriangles(side, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            deckMesh = mesh;
            return mesh;
        }

        // ------------------------------------------------------------ the insurance policy (a sheet of paper)

        private static Material paper;
        // Icons\policy_paper.png in the project (built into the DLL) if there is one, otherwise drawn in code
        public static Material Paper => paper ? paper : paper = NewMat(Color.white, EmbeddedTexture("policy_paper") ?? PolicyTex(128, 180), 0.1f);

        // white paper: a dark blue header, lines of "text", a signature line and a red round stamp
        private static Texture2D PolicyTex(int W, int H)
        {
            var t = NewTex(W, H, TextureWrapMode.Clamp);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H; // v = 1 at the top
                Color c = new Color(0.96f, 0.95f, 0.9f);
                if (v > 0.86f && v < 0.95f && u > 0.08f && u < 0.92f) c = new Color(0.12f, 0.2f, 0.45f); // header
                if (v > 0.3f && v < 0.8f && u > 0.1f && u < 0.9f && ((int)(v * H) % 9) < 2)
                {
                    // lines of text, each a bit shorter or longer
                    float len = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin((int)(v * H / 9) * 1.7f));
                    if (u < 0.1f + 0.8f * len) c = new Color(0.45f, 0.45f, 0.5f);
                }
                if (v > 0.17f && v < 0.18f && u > 0.1f && u < 0.5f) c = new Color(0.2f, 0.2f, 0.25f); // signature line
                float dx = u - 0.72f, dy = (v - 0.18f) * H / W;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 0.14f && (r > 0.11f || r < 0.06f)) c = new Color(0.8f, 0.12f, 0.1f); // the stamp
                c.a = 1f;
                px[y * W + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // shop / inventory icon of the policy (if there's no Icons\insurance_policy.png)
        public static Sprite PolicyIcon()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[S * S];
            var ink = new Color(0.12f, 0.12f, 0.16f, 1f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                bool sheet = x > 30 && x < 98 && y > 14 && y < 114;
                if (sheet) c = (x < 34 || x > 94 || y < 18 || y > 110) ? ink : new Color(0, 0, 0, 0);
                if (sheet && x > 40 && x < 88 && y > 50 && y < 98 && (y % 10) < 3 && x < 40 + 48 * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(y * 0.3f)))) c = ink; // text lines
                float dx = x - 76, dy = y - 34, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 13f && (r > 10f || r < 5f)) c = ink; // stamp
                px[y * S + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            var sp = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sp;
        }

        // ------------------------------------------------------------ the Ride to Hell pro board skin

        // a tiny 5x7 pixel font, just the letters we need (rows from the top, 1 = ink)
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
            { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
            { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
        };

        // writes a word centred at (cx, cy) (texture coordinates, y up), each font pixel = px x px
        private static void Word(Color[] img, int W, int H, string word, int cx, int cy, int px, Color ink, Color shadow)
        {
            int cw = 6 * px, w = word.Length * cw - px, h = 7 * px;
            int x0 = cx - w / 2, yTop = cy + h / 2;
            for (int pass = 0; pass < 2; pass++)
            {
                int off = pass == 0 ? px / 2 + 1 : 0; // a dark shadow first, then the letters
                Color c = pass == 0 ? shadow : ink;
                for (int k = 0; k < word.Length; k++)
                {
                    if (!Glyphs.TryGetValue(word[k], out var g)) continue;
                    for (int r = 0; r < 7; r++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (g[r][col] != '1') continue;
                        for (int dy = 0; dy < px; dy++)
                        for (int dx = 0; dx < px; dx++)
                        {
                            int x = x0 + k * cw + col * px + dx + off, y = yTop - r * px - dy - off;
                            if (x >= 0 && x < W && y >= 0 && y < H) img[y * W + x] = c;
                        }
                    }
                }
            }
        }

        // fire rising from the bottom (the tail): 0..1 how much flame there is at (u, v), v = 0 at the tail
        private static float Flame(float u, float v, float height)
        {
            float tongues = 0.5f + 0.5f * Mathf.Sin(u * 23f + Mathf.Sin(u * 7f) * 2f) * Mathf.Sin(u * 11f + 1.3f);
            float top = height * (0.55f + 0.45f * tongues);
            return Mathf.Clamp01((top - v) / (top * 0.35f + 0.001f));
        }

        // Bottom of the pro board: black, fire from the tail, RIDE / TO / HELL in red, a red ring at the nose.
        public static Texture2D RideToHellGraphic(int W = 256, int H = 1024)
        {
            var t = NewTex(W, H, TextureWrapMode.Clamp);
            var px = new Color[W * H];
            var rng = new System.Random(666);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H; // v = 0 at the tail (bottom of the picture), 1 at the nose
                float grain = 0.03f * (float)rng.NextDouble();
                Color c = new Color(0.06f + grain, 0.04f + grain, 0.05f + grain);
                float f = Flame(u, v, 0.42f);
                if (f > 0f)
                {
                    // red at the edge of the fire, orange inside, yellow at the root
                    Color fire = Color.Lerp(new Color(0.75f, 0.05f, 0.03f), Color.Lerp(new Color(1f, 0.45f, 0.05f), new Color(1f, 0.9f, 0.35f), Mathf.Clamp01(1f - v / 0.18f)), f);
                    c = Color.Lerp(c, fire, Mathf.Clamp01(f * 1.3f));
                }
                // a red ring and a dot near the nose
                float dx = (x - W * 0.5f) / W * 4f, dy = (v - 0.84f) * 4f * H / W / 4f * 4f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 0.55f && r < 0.68f) c = new Color(0.85f, 0.08f, 0.06f);
                if (r < 0.2f) c = new Color(0.85f, 0.08f, 0.06f);
                c.a = 1f;
                px[y * W + x] = c;
            }
            int s = Mathf.Max(3, W / 36);
            Color ink = new Color(0.9f, 0.08f, 0.06f), shadow = new Color(0.02f, 0.01f, 0.01f);
            Word(px, W, H, "RIDE", W / 2, (int)(H * 0.66f), s, ink, shadow);
            Word(px, W, H, "TO", W / 2, (int)(H * 0.58f), s, ink, shadow);
            Word(px, W, H, "HELL", W / 2, (int)(H * 0.50f), s, ink, shadow);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // Top of the pro board: black grip tape with red flames licking up from the tail.
        public static Texture2D RideToHellGrip(int W = 192, int H = 768)
        {
            var t = NewTex(W, H, TextureWrapMode.Clamp);
            var px = new Color[W * H];
            var rng = new System.Random(13);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float g = 0.05f + (float)rng.NextDouble() * 0.07f;
                Color c = new Color(g, g * 0.9f, g * 0.9f);
                float f = Flame(x / (float)W, y / (float)H, 0.22f);
                if (f > 0f) c = Color.Lerp(c, new Color(0.7f, 0.06f, 0.04f) * (0.8f + 0.2f * (float)rng.NextDouble()), f * 0.85f);
                c.a = 1f;
                px[y * W + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------ shop icons from PNG files

        private static readonly Dictionary<string, Sprite> embedded = new Dictionary<string, Sprite>();

        // A PNG from the project's Icons folder, built into the DLL (Icons\rocket.png -> "rocket").
        // null if there's no such picture: then the icon drawn in code is used.
        public static Sprite EmbeddedIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (embedded.TryGetValue(name, out var cached)) return cached;
            Sprite sp = null;
            var t = LoadEmbedded(name, false);
            if (t != null)
            {
                sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = "CWSkate_Icon_" + name;
                sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            SkatePlugin.Log.LogInfo("Shop icon '" + name + "': " + (sp != null ? "PNG" : "drawn in code"));
            embedded[name] = sp;
            return sp;
        }

        // A picture for a model (e.g. Icons\policy_paper.png), built into the DLL. null if there isn't one.
        public static Texture2D EmbeddedTexture(string name)
        {
            var t = LoadEmbedded(name, true);
            SkatePlugin.Log.LogInfo("Texture '" + name + "': " + (t != null ? "PNG" : "drawn in code"));
            return t;
        }

        private static Texture2D LoadEmbedded(string name, bool mipmaps)
        {
            try
            {
                using (var s = typeof(SkateArt).Assembly.GetManifestResourceStream("CWSkate.Icons." + name + ".png"))
                {
                    if (s == null) return null;
                    var bytes = new byte[s.Length];
                    int read = 0;
                    while (read < bytes.Length) { int n = s.Read(bytes, read, bytes.Length - read); if (n <= 0) break; read += n; }
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                    if (!t.LoadImage(bytes)) { Object.Destroy(t); return null; }
                    t.wrapMode = TextureWrapMode.Clamp;
                    t.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
                    if (mipmaps) t.anisoLevel = 4;
                    return t;
                }
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Picture '" + name + "': " + e.Message); return null; }
        }

        // ------------------------------------------------------------ shop icon

        public static Sprite Icon()
        {
            if (icon) return icon;
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var px = new Color[S * S];
            float ang = 35f * Mathf.Deg2Rad, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            Color deckA = new Color(1f, 0.25f, 0.55f), deckB = new Color(0.38f, 0.16f, 0.85f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - S * 0.5f, dy = y - S * 0.5f;
                float u = dx * ca + dy * sa;   // along the board
                float v = -dx * sa + dy * ca;  // across
                Color c = new Color(0, 0, 0, 0);
                // wheels peeking out
                foreach (float wu in new[] { -30f, 30f })
                foreach (float wv in new[] { -17f, 17f })
                {
                    float wd = Mathf.Sqrt((u - wu) * (u - wu) + (v - wv) * (v - wv));
                    if (wd < 7.5f) c = wd < 6f ? new Color(0.97f, 0.94f, 0.85f, 1) : new Color(0.2f, 0.2f, 0.2f, 1);
                }
                // stadium shaped deck
                const float r = 15f, straight = 37f;
                float du = Mathf.Max(0f, Mathf.Abs(u) - straight);
                float dist = Mathf.Sqrt(du * du + v * v);
                if (dist < r)
                {
                    Color dc = Color.Lerp(deckA, deckB, (u + 52f) / 104f);
                    if (dist > r - 2.5f) dc = new Color(0.12f, 0.08f, 0.15f);
                    else if (Mathf.Abs(u) < 7f && Mathf.Abs(v) < 7f) dc = new Color(1f, 0.85f, 0.2f);
                    c = new Color(dc.r, dc.g, dc.b, 1f);
                }
                px[y * S + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            icon = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            icon.name = "CWSkate_Icon";
            icon.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return icon;
        }
    }
}
