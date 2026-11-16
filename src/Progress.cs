using System.Collections.Generic;
using System.IO;
using BepInEx;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace CWSkate
{
    // Aura and board ownership, stored per game save file (or globally if the player wants).
    // The host publishes its save ID in the room, so modded clients store their progress
    // under the same save. File: BepInEx/config/cwskate_progress.txt
    internal static class Progress
    {
        private class Entry
        {
            public bool owns;
            public int pending; // boards delivered but not picked up yet
            public long aura, best;
            public Dictionary<string, long> stats = new Dictionary<string, long>();
            public List<KeyValuePair<long, string>> top = new List<KeyValuePair<long, string>>();
        }

        private const string GlobalKey = "global";
        private const string RoomSaveKey = "cwskate_save";

        private static Dictionary<string, Entry> data;
        private static string cachedKey;
        private static int cachedFrame = -1;
        private static float publishTimer;

        private static string FilePath => Path.Combine(Paths.ConfigPath, "cwskate_progress.txt");

        private static void Load()
        {
            if (data != null) return;
            data = new Dictionary<string, Entry>();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var p = line.Split('|');
                        if (p.Length < 5) continue;
                        data[p[0]] = new Entry
                        {
                            owns = p[1] == "1",
                            pending = int.TryParse(p[2], out var pn) ? Mathf.Max(0, pn) : 0,
                            aura = long.TryParse(p[3], out var a) ? a : 0,
                            best = long.TryParse(p[4], out var b) ? b : 0,
                        };
                        if (p.Length > 5) ParseStats(data[p[0]], p[5]);
                        if (p.Length > 6) ParseTop(data[p[0]], p[6]);
                    }
                }
                else
                {
                    // first start after updating from 1.0-1.2: keep the old global progress
                    data[GlobalKey] = new Entry
                    {
                        owns = SkatePlugin.OldOwnsSkateboard.Value,
                        aura = SkatePlugin.OldTotalAura.Value,
                        best = SkatePlugin.OldBestCombo.Value,
                    };
                    Write();
                }
            }
            catch (System.Exception e) { SkatePlugin.Log.LogError("Failed to load progress: " + e.Message); }
        }

        private static void ParseStats(Entry e, string s)
        {
            foreach (var kv in s.Split(','))
            {
                int i = kv.IndexOf('=');
                if (i > 0 && long.TryParse(kv.Substring(i + 1), out long v)) e.stats[kv.Substring(0, i)] = v;
            }
        }

        private static void ParseTop(Entry e, string s)
        {
            foreach (var item in s.Split(';'))
            {
                int i = item.IndexOf('~');
                if (i > 0 && long.TryParse(item.Substring(0, i), out long v)) e.top.Add(new KeyValuePair<long, string>(v, item.Substring(i + 1)));
            }
        }

        private static string StatsToString(Entry e)
        {
            var parts = new List<string>();
            foreach (var kv in e.stats) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(",", parts);
        }

        private static string TopToString(Entry e)
        {
            var parts = new List<string>();
            foreach (var kv in e.top) parts.Add(kv.Key + "~" + kv.Value);
            return string.Join(";", parts);
        }

        private static string Clean(string s) => s.Replace("|", "/").Replace(";", ",").Replace("~", "-").Replace("\n", " ").Replace("\r", "");

        // ------------------------------------------------------------ records (same place as the aura)

        public const int TopCount = 5;

        public static long Stat(string key) => AuraEntry.stats.TryGetValue(key, out long v) ? v : 0;

        public static void AddStat(string key, long amount, bool save = true)
        {
            var e = AuraEntry;
            e.stats[key] = (e.stats.TryGetValue(key, out long v) ? v : 0) + amount;
            if (save) Write();
        }

        // keeps the biggest value; true = new record
        public static bool MaxStat(string key, long value)
        {
            var e = AuraEntry;
            if (e.stats.TryGetValue(key, out long v) && v >= value) return false;
            e.stats[key] = value;
            Write();
            return true;
        }

        public static List<KeyValuePair<long, string>> TopCombos => AuraEntry.top;

        // remembers one of the best combos; returns its place (1 = best) or 0 if it didn't make the list
        public static int AddCombo(long value, string text)
        {
            var top = AuraEntry.top;
            if (text.Length > 140) text = text.Substring(0, 137) + "...";
            int place = 0;
            for (int i = 0; i <= top.Count && i < TopCount; i++)
                if (i == top.Count || value > top[i].Key) { place = i + 1; break; }
            if (place == 0) return 0;
            top.Insert(place - 1, new KeyValuePair<long, string>(value, Clean(text)));
            while (top.Count > TopCount) top.RemoveAt(top.Count - 1);
            Write();
            return place;
        }

        public static void Save() => Write();

        private static void Write()
        {
            try
            {
                var lines = new List<string> { "# CW Skate progress: key|ownsBoard|boardsWaiting|aura|bestCombo|stats|topCombos" };
                foreach (var kv in data)
                    lines.Add(kv.Key + "|" + (kv.Value.owns ? 1 : 0) + "|" + kv.Value.pending + "|" + kv.Value.aura + "|" + kv.Value.best + "|" + StatsToString(kv.Value) + "|" + TopToString(kv.Value));
                File.WriteAllLines(FilePath, lines);
            }
            catch (System.Exception e) { SkatePlugin.Log.LogError("Failed to save progress: " + e.Message); }
        }

        // Which save file we are playing right now.
        public static string SaveKey
        {
            get
            {
                if (cachedFrame == Time.frameCount && cachedKey != null) return cachedKey;
                cachedFrame = Time.frameCount;
                cachedKey = ComputeKey();
                return cachedKey;
            }
        }

        public static bool HasSave => SaveKey != "offline";

        private static string ComputeKey()
        {
            try
            {
                if (!PhotonNetwork.InRoom) return "offline";
                if (PhotonNetwork.IsMasterClient)
                {
                    var s = SaveSystem.CurrentSave;
                    if (s != null && s.Valid) return "save:" + s.GetSaveID();
                    return "room:" + PhotonNetwork.CurrentRoom.Name;
                }
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props != null && props.TryGetValue(RoomSaveKey, out object id) && id is string str && str.Length > 0)
                    return "save:" + str;
                // host has no mod: remember progress per host
                var master = PhotonNetwork.MasterClient;
                if (master != null && master.CustomProperties != null && master.CustomProperties.TryGetValue("UserID", out object uid))
                    return "host:" + uid;
                return "host:" + (master != null ? master.NickName : "unknown");
            }
            catch { return "offline"; }
        }

        // Host: tell modded clients which save this is.
        public static void Tick()
        {
            publishTimer -= Time.unscaledDeltaTime;
            if (publishTimer > 0f) return;
            publishTimer = 2f;
            try
            {
                if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.OfflineMode) return;
                var s = SaveSystem.CurrentSave;
                if (s == null || !s.Valid) return;
                string id = s.GetSaveID().ToString();
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props != null && props.TryGetValue(RoomSaveKey, out object cur) && cur as string == id) return;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { { RoomSaveKey, id } });
            }
            catch { }
        }

        private static Entry Get(bool perSave)
        {
            Load();
            string key = perSave ? SaveKey : GlobalKey;
            if (!data.TryGetValue(key, out var e))
            {
                e = new Entry();
                data[key] = e;
            }
            return e;
        }

        // (the board itself is an item in the inventory now; the old owns/pending fields stay in the file unused)
        private static Entry AuraEntry => Get(SkatePlugin.AuraPerSaveFile.Value);

        public static long Aura
        {
            get => AuraEntry.aura;
            set { AuraEntry.aura = value; Write(); }
        }

        public static long BestCombo
        {
            get => AuraEntry.best;
            set { AuraEntry.best = value; Write(); }
        }

        public static void ResetCurrent()
        {
            var e = AuraEntry;
            e.aura = 0;
            e.best = 0;
            e.stats.Clear();
            e.top.Clear();
            Write();
        }
    }
}
