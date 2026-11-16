using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;

namespace CWSkate
{
    // One board look: picture for the bottom (graphic) and the top (grip). Null = built-in.
    internal class SkinSet
    {
        public string Name;
        public Material Grip, Graphic;
        public int Version;
        private readonly List<Object> owned = new List<Object>();

        public void Own(Object o) { if (o != null) owned.Add(o); }

        public void Destroy()
        {
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear();
        }
    }

    // Board skins from the SkateSkins folder next to the mod:
    //   <name>.png       - bottom of the deck (the art you see during flips)
    //   <name>_grip.png  - top of the deck (optional)
    // Top of the picture = nose of the board. Other players with the mod get your skin sent to them.
    internal static class Skins
    {
        public static readonly SkinSet Default = new SkinSet { Name = "default", Version = 0 };

        // the Ride to Hell pro board's own look (drawn in code, the same for everyone)
        private static SkinSet rideToHell;
        public static SkinSet RideToHell
        {
            get
            {
                if (rideToHell != null) return rideToHell;
                var s = new SkinSet { Name = "ridetohell", Version = ++versionCounter };
                var g = SkateArt.RideToHellGraphic();
                var gr = SkateArt.RideToHellGrip();
                s.Graphic = MakeMat(s, g, 0.4f);
                s.Grip = MakeMat(s, gr, 0.05f);
                rideToHell = s;
                return s;
            }
        }

        private static SkinSet local;
        private static string localLoadedName;
        private static int versionCounter;
        private static byte[] sendGraphic, sendGrip;
        private static readonly Dictionary<int, SkinSet> remote = new Dictionary<int, SkinSet>();
        private static readonly HashSet<int> knownActors = new HashSet<int>();
        private static bool sentAfterJoin;
        private static float tickTimer;
        private static List<string> available;

        public static string Folder
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(SkatePlugin).Assembly.Location);
                if (string.IsNullOrEmpty(dir)) dir = BepInEx.Paths.PluginPath;
                return Path.Combine(dir, "SkateSkins");
            }
        }

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string g = Path.Combine(Folder, "default.png"), gr = Path.Combine(Folder, "default_grip.png");
                if (!File.Exists(g)) WriteTex(g, SkateArt.GraphicTex(256, 1024));
                if (!File.Exists(gr)) WriteTex(gr, SkateArt.GripTex(192, 768));
                string readme = Path.Combine(Folder, "README.txt");
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        "Скины для доски CW Skate\r\n" +
                        "========================\r\n\r\n" +
                        "Каждый скин — это картинка PNG (или JPG):\r\n" +
                        "  имя.png       — низ доски (рисунок, который видно во время флипов)\r\n" +
                        "  имя_grip.png  — верх доски, шкурка (необязательно)\r\n\r\n" +
                        "Как сделать свой:\r\n" +
                        "  1. Скопируй default.png (и если хочешь default_grip.png) и переименуй, например в fire.png и fire_grip.png.\r\n" +
                        "  2. Нарисуй что хочешь. Верх картинки = нос доски. Картинка растягивается на всю доску,\r\n" +
                        "     лучше всего соотношение сторон 1:4 (например 256 x 1024). Углы картинки не видно — доска закруглённая.\r\n" +
                        "  3. В игре нажми F8 -> вкладка «Скин» -> выбери свой. Кнопка «Обновить» подхватывает новые файлы без перезапуска.\r\n\r\n" +
                        "Игроки с модом видят твой скин, даже если у них нет этого файла.\r\n", System.Text.Encoding.UTF8);
                string readmeEn = Path.Combine(Folder, "README_EN.txt");
                if (!File.Exists(readmeEn))
                    File.WriteAllText(readmeEn,
                        "CW Skate board skins\r\n" +
                        "====================\r\n\r\n" +
                        "Every skin is a PNG (or JPG) picture:\r\n" +
                        "  name.png       - the bottom of the board (the graphic you see during flips)\r\n" +
                        "  name_grip.png  - the top of the board, the grip tape (optional)\r\n\r\n" +
                        "Making your own:\r\n" +
                        "  1. Copy default.png (and default_grip.png if you like) and rename them, e.g. fire.png and fire_grip.png.\r\n" +
                        "  2. Draw whatever you want. The top of the picture = the nose of the board. The picture is stretched over\r\n" +
                        "     the whole board, a 1:4 aspect ratio works best (e.g. 256 x 1024). The corners don't show - the board is rounded.\r\n" +
                        "  3. In the game press F8 -> the Skin tab -> pick yours. The Refresh button picks up new files without a restart.\r\n\r\n" +
                        "Players with the mod see your skin, even if they don't have the file.\r\n", System.Text.Encoding.UTF8);
                string readmeFr = Path.Combine(Folder, "README_FR.txt");
                if (!File.Exists(readmeFr))
                    File.WriteAllText(readmeFr,
                        "Skins de planche CW Skate\r\n" +
                        "=========================\r\n\r\n" +
                        "Chaque skin est une image PNG (ou JPG) :\r\n" +
                        "  nom.png       - le dessous de la planche (le graphisme qu'on voit pendant les flips)\r\n" +
                        "  nom_grip.png  - le dessus de la planche, le grip (facultatif)\r\n\r\n" +
                        "Faire le tien :\r\n" +
                        "  1. Copie default.png (et default_grip.png si tu veux) et renomme-les, par exemple fire.png et fire_grip.png.\r\n" +
                        "  2. Dessine ce que tu veux. Le haut de l'image = le nose de la planche. L'image est étirée sur\r\n" +
                        "     toute la planche, un format 1:4 marche le mieux (par exemple 256 x 1024). Les coins ne se voient pas - la planche est arrondie.\r\n" +
                        "  3. En jeu, appuie sur F8 -> l'onglet Skin -> choisis le tien. Le bouton Actualiser charge les nouveaux fichiers sans redémarrer.\r\n\r\n" +
                        "Les joueurs qui ont le mod voient ton skin, même s'ils n'ont pas le fichier.\r\n", System.Text.Encoding.UTF8);
                string readmeZh = Path.Combine(Folder, "README_ZH.txt");
                if (!File.Exists(readmeZh))
                    File.WriteAllText(readmeZh,
                        "CW Skate 滑板皮肤\r\n" +
                        "=================\r\n\r\n" +
                        "每个皮肤就是一张 PNG（或 JPG）图片：\r\n" +
                        "  名字.png       - 滑板底面（翻板时能看到的图案）\r\n" +
                        "  名字_grip.png  - 滑板正面，砂纸（可选）\r\n\r\n" +
                        "自制皮肤：\r\n" +
                        "  1. 复制 default.png（想要的话也复制 default_grip.png）并改名，比如 fire.png 和 fire_grip.png。\r\n" +
                        "  2. 随便画。图片上方 = 板头。图片会拉伸铺满整块滑板，\r\n" +
                        "     长宽比 1:4 效果最好（比如 256 x 1024）。四个角看不到 - 滑板是圆角的。\r\n" +
                        "  3. 在游戏里按 F8 -> “皮肤”页 -> 选你的皮肤。“刷新”按钮不用重启就能读到新文件。\r\n\r\n" +
                        "装了模组的玩家都能看到你的皮肤，就算他们没有这个文件。\r\n", System.Text.Encoding.UTF8);
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Skin folder setup failed: " + e.Message); }
            SkatePlugin.Skin.SettingChanged += (s, a) => localLoadedName = null;
        }

        private static void WriteTex(string path, Texture2D t)
        {
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.Destroy(t);
        }

        // ------------------------------------------------------------ list / choose

        public static List<string> Available
        {
            get
            {
                if (available == null) Refresh();
                return available;
            }
        }

        public static void Refresh()
        {
            available = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(Folder))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (n.EndsWith("_grip", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (!available.Contains(n)) available.Add(n);
                }
            }
            catch { }
            available.Sort(System.StringComparer.OrdinalIgnoreCase);
            available.Remove("default");
            available.Insert(0, "default");
            localLoadedName = null; // reload the current one too (the file may have been edited)
        }

        public static void Cycle(int dir)
        {
            var list = Available;
            int i = list.FindIndex(n => string.Equals(n, SkatePlugin.Skin.Value, System.StringComparison.OrdinalIgnoreCase));
            i = (i + dir + list.Count) % list.Count;
            SkatePlugin.Skin.Value = list[i];
        }

        public static void OpenFolder()
        {
            try { Application.OpenURL("file:///" + Folder.Replace('\\', '/')); } catch { }
        }

        // ------------------------------------------------------------ local skin

        public static SkinSet Local
        {
            get
            {
                if (local == null || localLoadedName != SkatePlugin.Skin.Value) LoadLocal();
                return local;
            }
        }

        private static string FindFile(string name)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                string p = Path.Combine(Folder, name + ext);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static void LoadLocal()
        {
            string name = SkatePlugin.Skin.Value;
            localLoadedName = name;
            var old = local;
            local = new SkinSet { Name = name, Version = ++versionCounter };
            sendGraphic = sendGrip = new byte[0];
            try
            {
                string g = FindFile(name), gr = FindFile(name + "_grip");
                if (g == null && name != "default")
                {
                    SkatePlugin.Log.LogWarning("Skin '" + name + "' not found in " + Folder);
                    g = FindFile("default");
                    gr = FindFile("default_grip");
                }
                if (g != null)
                {
                    var t = LoadTex(File.ReadAllBytes(g));
                    if (t != null) { local.Graphic = MakeMat(local, t, 0.35f); sendGraphic = Encode(t, 256, 1024); }
                }
                if (gr != null)
                {
                    var t = LoadTex(File.ReadAllBytes(gr));
                    if (t != null) { local.Grip = MakeMat(local, t, 0.05f); sendGrip = Encode(t, 128, 512); }
                }
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Failed to load skin '" + name + "': " + e.Message); }
            old?.Destroy();
            BroadcastSkin(null);
        }

        private static Texture2D LoadTex(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!t.LoadImage(data)) { Object.Destroy(t); return null; }
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            return t;
        }

        private static Material MakeMat(SkinSet set, Texture2D t, float smooth)
        {
            var m = SkateArt.NewMat(Color.white, t, smooth);
            set.Own(t);
            set.Own(m);
            return m;
        }

        // shrink and pack as JPG so it can be sent over the network
        private static byte[] Encode(Texture2D src, int maxW, int maxH)
        {
            try
            {
                float k = Mathf.Min(1f, Mathf.Min(maxW / (float)src.width, maxH / (float)src.height));
                int w = Mathf.Max(8, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(8, Mathf.RoundToInt(src.height * k));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                byte[] bytes = small.EncodeToJPG(80);
                Object.Destroy(small);
                return bytes;
            }
            catch (System.Exception e)
            {
                SkatePlugin.Log.LogWarning("Skin encode failed: " + e.Message);
                return new byte[0];
            }
        }

        // ------------------------------------------------------------ other players

        public static SkinSet ForActor(int actor)
        {
            return remote.TryGetValue(actor, out var s) ? s : Default;
        }

        public static void OnRemoteSkin(int actor, byte[] graphic, byte[] grip)
        {
            if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber) return;
            var set = new SkinSet { Name = "remote", Version = ++versionCounter };
            var g = LoadTex(graphic);
            if (g != null) set.Graphic = MakeMat(set, g, 0.35f);
            var gr = LoadTex(grip);
            if (gr != null) set.Grip = MakeMat(set, gr, 0.05f);
            if (remote.TryGetValue(actor, out var old)) old.Destroy();
            remote[actor] = set;
        }

        // send our skin to everyone (targets == null) or to players who just joined
        private static void BroadcastSkin(int[] targets)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
            if (local == null) { var _ = Local; return; } // loading sends it
            NetSync.SendSkin(sendGraphic ?? new byte[0], sendGrip ?? new byte[0], targets);
        }

        // Called every frame by the manager.
        public static void Tick()
        {
            tickTimer -= Time.unscaledDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = 1f;
            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode)
                {
                    knownActors.Clear();
                    sentAfterJoin = false;
                    foreach (var s in remote.Values) s.Destroy();
                    remote.Clear();
                    return;
                }
                var others = PhotonNetwork.PlayerListOthers;
                var newcomers = new List<int>();
                var present = new HashSet<int>();
                foreach (var p in others)
                {
                    present.Add(p.ActorNumber);
                    if (knownActors.Add(p.ActorNumber)) newcomers.Add(p.ActorNumber);
                }
                // forget players who left
                var gone = new List<int>();
                foreach (var a in knownActors) if (!present.Contains(a)) gone.Add(a);
                foreach (var a in gone)
                {
                    knownActors.Remove(a);
                    if (remote.TryGetValue(a, out var s)) { s.Destroy(); remote.Remove(a); }
                }
                if (!sentAfterJoin)
                {
                    sentAfterJoin = true;
                    if (others.Length > 0) BroadcastSkin(null);
                }
                else if (newcomers.Count > 0) BroadcastSkin(newcomers.ToArray());
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Skin sync: " + e.Message); }
        }
    }
}
