using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SpawnTracker
{
    [BepInPlugin(Guid, ModName, Version)]
    public class SpawnTrackerPlugin : BaseUnityPlugin
    {
        public const string Guid = "nnikoloo.SpawnTracker";
        public const string ModName = "SpawnTracker";
        public const string Version = "0.1.1";

        // Private game fields (see CreatureSpawner / SpawnArea in assembly_valheim.dll)
        private static readonly AccessTools.FieldRef<CreatureSpawner, ZNetView> CreatureSpawnerView =
            AccessTools.FieldRefAccess<CreatureSpawner, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<SpawnArea, ZNetView> SpawnAreaView =
            AccessTools.FieldRefAccess<SpawnArea, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<SpawnArea, float> SpawnAreaTimer =
            AccessTools.FieldRefAccess<SpawnArea, float>("m_spawnTimer");
        private static readonly List<CreatureSpawner> AllCreatureSpawners =
            (List<CreatureSpawner>)AccessTools.Field(typeof(CreatureSpawner), "m_creatureSpawners").GetValue(null);

        private static readonly Color Alive = new Color(0.45f, 0.9f, 0.45f);
        private static readonly Color Waiting = new Color(1f, 0.82f, 0.3f);
        private static readonly Color Ready = new Color(1f, 0.45f, 0.35f);
        private static readonly Color Inactive = new Color(0.7f, 0.7f, 0.7f);

        private ConfigEntry<KeyboardShortcut> _toggleKey;
        private ConfigEntry<float> _range;
        private ConfigEntry<bool> _showLabels;
        private ConfigEntry<bool> _showList;
        private ConfigEntry<int> _listSize;

        private bool _visible = true;
        private float _nextScan;
        private readonly List<Entry> _entries = new List<Entry>();
        private GUIStyle _labelStyle;
        private GUIStyle _listStyle;

        private class Entry
        {
            public Vector3 Position;
            public string Title;
            public string Status;
            public Color Color;
            public float Distance;
        }

        private void Awake()
        {
            _toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F8),
                "Key to show or hide spawn information.");
            _range = Config.Bind("General", "Range", 80f,
                "Maximum distance (metres) at which spawners are shown.");
            _showLabels = Config.Bind("Display", "ShowLabels", true,
                "Show floating labels above each spawner.");
            _showList = Config.Bind("Display", "ShowList", true,
                "Show the on-screen list of nearby spawners.");
            _listSize = Config.Bind("Display", "ListSize", 8,
                "How many spawners to show in the list.");
            Logger.LogInfo($"{ModName} {Version} loaded. Press {_toggleKey.Value} to toggle.");
        }

        private void Update()
        {
            if (_toggleKey.Value.IsDown())
                _visible = !_visible;

            if (!_visible || Time.time < _nextScan)
                return;
            _nextScan = Time.time + 0.5f;
            Scan();
        }

        private void Scan()
        {
            _entries.Clear();
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null || ZDOMan.instance == null)
                return;

            Vector3 playerPos = player.transform.position;
            float range = _range.Value;

            foreach (CreatureSpawner spawner in AllCreatureSpawners)
            {
                if (spawner == null)
                    continue;
                float distance = Vector3.Distance(playerPos, spawner.transform.position);
                if (distance > range)
                    continue;
                Entry entry = DescribeCreatureSpawner(spawner, distance);
                if (entry != null)
                    _entries.Add(entry);
            }

            foreach (SpawnArea area in FindObjectsOfType<SpawnArea>())
            {
                float distance = Vector3.Distance(playerPos, area.transform.position);
                if (distance > range)
                    continue;
                Entry entry = DescribeSpawnArea(area, distance);
                if (entry != null)
                    _entries.Add(entry);
            }

            _entries.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        // Mirrors CreatureSpawner.UpdateSpawner / CanRespawnNow.
        private static Entry DescribeCreatureSpawner(CreatureSpawner spawner, float distance)
        {
            ZDO zdo = CreatureSpawnerView(spawner)?.GetZDO();
            if (zdo == null || spawner.m_creaturePrefab == null)
                return null;

            var entry = new Entry
            {
                Position = spawner.transform.position,
                Title = CreatureName(spawner.m_creaturePrefab),
                Distance = distance,
            };

            ZDOConnection connection = zdo.GetConnection();
            bool spawnedBefore = connection != null && connection.m_type == ZDOExtraData.ConnectionType.Spawned;
            ZDOID spawnId = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned);

            if (!spawnId.IsNone() && ZDOMan.instance.GetZDO(spawnId) != null)
            {
                entry.Status = "Alive";
                entry.Color = Alive;
                return entry;
            }

            if (spawner.m_respawnTimeMinuts <= 0f && spawnedBefore)
            {
                entry.Status = "Dead · does not respawn";
                entry.Color = Inactive;
                return entry;
            }

            if (spawner.m_respawnTimeMinuts > 0f && spawnedBefore)
            {
                var lastAlive = new DateTime(zdo.GetLong(ZDOVars.s_aliveTime, 0L));
                double remaining = spawner.m_respawnTimeMinuts * 60.0 - (ZNet.instance.GetTime() - lastAlive).TotalSeconds;
                if (remaining > 0)
                {
                    entry.Status = $"Respawns in {FormatTime(remaining)}";
                    entry.Color = Waiting;
                    return entry;
                }
            }

            List<string> blockers = CreatureSpawnerBlockers(spawner, distance);
            entry.Status = blockers.Count == 0
                ? "Ready to spawn"
                : "Ready · " + string.Join(", ", blockers.ToArray());
            entry.Color = blockers.Count == 0 ? Ready : Waiting;
            return entry;
        }

        private static List<string> CreatureSpawnerBlockers(CreatureSpawner spawner, float distance)
        {
            var blockers = new List<string>();
            Vector3 pos = spawner.transform.position;

            if (!spawner.m_spawnAtDay && EnvMan.IsDay())
                blockers.Add("waiting for night");
            if (!spawner.m_spawnAtNight && EnvMan.IsNight())
                blockers.Add("waiting for day");
            if (!spawner.m_spawnInPlayerBase && EffectArea.IsPointInsideArea(pos, EffectArea.Type.PlayerBase) != null)
                blockers.Add("blocked by a player base");
            if (distance > spawner.m_triggerDistance)
                blockers.Add($"get within {spawner.m_triggerDistance:0} m");
            else if (spawner.m_triggerNoise > 0f)
                blockers.Add("needs noise nearby");

            List<string> keys = ZoneSystem.instance != null ? ZoneSystem.instance.GetGlobalKeys() : null;
            if (keys != null)
            {
                if (spawner.m_requiredGlobalKey != "" && !keys.Contains(spawner.m_requiredGlobalKey))
                    blockers.Add("needs more world progress");
                if (spawner.m_blockingGlobalKey != "" && keys.Contains(spawner.m_blockingGlobalKey))
                    blockers.Add("disabled by world progress");
            }
            return blockers;
        }

        // Mirrors SpawnArea.UpdateSpawn / SpawnOne (nests, bone piles, etc.).
        private static Entry DescribeSpawnArea(SpawnArea area, float distance)
        {
            ZNetView view = SpawnAreaView(area);
            if (view == null || !view.IsValid())
                return null;

            var names = area.m_prefabs
                .Where(p => p.m_prefab != null)
                .Select(p => CreatureName(p.m_prefab))
                .Distinct()
                .ToArray();

            var entry = new Entry
            {
                Position = area.transform.position,
                Title = "Spawner: " + (names.Length > 0 ? string.Join(", ", names) : "?"),
                Distance = distance,
            };

            CountInstances(area, out int near, out int total);
            string counts = $"{near}/{area.m_maxNear} near, {total}/{area.m_maxTotal} total";

            if (near >= area.m_maxNear || total >= area.m_maxTotal)
            {
                entry.Status = $"Full ({counts})";
                entry.Color = Alive;
            }
            else if (!view.IsOwner())
            {
                entry.Status = $"Timer owned by another player ({counts})";
                entry.Color = Inactive;
            }
            else if (!Player.IsPlayerInRange(area.transform.position, area.m_triggerDistance))
            {
                entry.Status = $"Inactive, too far ({counts})";
                entry.Color = Inactive;
            }
            else
            {
                // The game adds 2 s to the timer every 2 s, so the real value moves in steps.
                float remaining = Mathf.Max(0f, area.m_spawnIntervalSec - SpawnAreaTimer(area));
                entry.Status = $"Next in ~{FormatTime(remaining)} ({counts})";
                entry.Color = remaining <= 4f ? Ready : Waiting;
            }
            return entry;
        }

        private static void CountInstances(SpawnArea area, out int near, out int total)
        {
            near = 0;
            total = 0;
            Vector3 pos = area.transform.position;
            foreach (BaseAI ai in BaseAI.BaseAIInstances)
            {
                if (ai == null || !IsFromArea(area, ai.gameObject))
                    continue;
                float d = Utils.DistanceXZ(ai.transform.position, pos);
                if (d < area.m_nearRadius)
                    near++;
                if (d < area.m_farRadius)
                    total++;
            }
        }

        private static bool IsFromArea(SpawnArea area, GameObject go)
        {
            Character character = go.GetComponent<Character>();
            if (character != null && character.IsTamed())
                return false;
            foreach (SpawnArea.SpawnData data in area.m_prefabs)
            {
                if (data.m_prefab != null && go.name.StartsWith(data.m_prefab.name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string CreatureName(GameObject prefab)
        {
            Character character = prefab.GetComponent<Character>();
            if (character != null && Localization.instance != null)
                return Localization.instance.Localize(character.m_name);
            return prefab.name;
        }

        private static string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        private void OnGUI()
        {
            if (!_visible || _entries.Count == 0 || Player.m_localPlayer == null)
                return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    richText = true,
                };
                _listStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            }

            if (_showLabels.Value)
                DrawLabels();
            if (_showList.Value)
                DrawList();
        }

        private void DrawLabels()
        {
            Camera cam = Utils.GetMainCamera();
            if (cam == null)
                return;

            foreach (Entry e in _entries)
            {
                Vector3 screen = cam.WorldToScreenPoint(e.Position + Vector3.up * 2f);
                if (screen.z <= 0f)
                    continue;
                string text = $"{e.Title} <size=12>({e.Distance:0} m)</size>\n<color=#{ColorUtility.ToHtmlStringRGB(e.Color)}>{e.Status}</color>";
                var rect = new Rect(screen.x - 160f, Screen.height - screen.y - 22f, 320f, 44f);
                DrawShadowed(rect, text, _labelStyle);
            }
        }

        private void DrawList()
        {
            int count = Mathf.Min(_listSize.Value, _entries.Count);
            const float width = 460f;
            const float lineHeight = 20f;
            float x = Screen.width - width - 20f;
            float y = 220f;

            DrawShadowed(new Rect(x, y, width, lineHeight), $"<b>Nearby spawners</b> <size=11>({_toggleKey.Value} to hide)</size>", _listStyle);
            for (int i = 0; i < count; i++)
            {
                Entry e = _entries[i];
                y += lineHeight;
                string text = $"{e.Distance,4:0} m  {e.Title}: <color=#{ColorUtility.ToHtmlStringRGB(e.Color)}>{e.Status}</color>";
                DrawShadowed(new Rect(x, y, width, lineHeight), text, _listStyle);
            }
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), StripColors(text), style);
            GUI.color = old;
            GUI.Label(rect, text, style);
        }

        private static string StripColors(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", "");
        }
    }
}
