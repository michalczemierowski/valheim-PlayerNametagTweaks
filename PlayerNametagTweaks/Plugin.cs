using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace PlayerNametagTweaks
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private Harmony harmony;
        private ConfigEntry<float> nametagDistance;
        private ConfigEntry<bool> scaleDownOverDistance;
        private ConfigEntry<bool> showNametagsWhenSneaking;

        private void Awake()
        {
            Instance = this;
            harmony = new Harmony("main");
            harmony.PatchAll(typeof(Plugin));

            nametagDistance = Config.Bind(
                "General",
                "Nametag Distance",
                64f
            );
            scaleDownOverDistance = Config.Bind(
                "General",
                "Scale down nametag over distance",
                true
            );
            showNametagsWhenSneaking = Config.Bind(
                "General",
                "Show nametag when crouching",
                true,
                configDescription: new ConfigDescription("Show player nametags when they're sneaking/crouching")
            );
        }

        private static FieldInfo playerPositionRefPointField = typeof(EnemyHud).GetField("m_refPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo hudsDictionaryField = typeof(EnemyHud).GetField("m_huds", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo hudDataMainGuiField = typeof(EnemyHud).Assembly.GetType("EnemyHud+HudData").GetField("m_gui", BindingFlags.Public | BindingFlags.Instance);

        [HarmonyPatch(typeof(EnemyHud), "TestShow")]
        [HarmonyPostfix]
        private static void ShowCharacterHudPostfix(ref EnemyHud __instance, ref bool __result, Character c, bool isVisible)
        {
            if (c == null || !c.IsPlayer())
                return;
            if (!Instance.showNametagsWhenSneaking.Value && c.IsCrouching())
                return;

            var localPlayerPos = (Vector3)playerPositionRefPointField.GetValue(__instance);
            float distance = Vector3.Distance(localPlayerPos, c.transform.position);
            if (distance > Instance.nametagDistance.Value)
                return;

            if (Instance.scaleDownOverDistance.Value && TryGetCharacterHudData(__instance, c, out var hudData))
                HandleHudScaling(hudData, distance);

            __result = true;
        }

        private static void HandleHudScaling(object hudData, float distance)
        {
            var gui = hudDataMainGuiField.GetValue(hudData) as GameObject;
            if (gui == null)
                return;

            const float scaleMin = 0.25f;
            const float scaleMax = 1f;
            float distance01 = distance / Instance.nametagDistance.Value;
            float scale = scaleMin + (scaleMax - scaleMin) * (1f - distance01);
            gui.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static bool TryGetCharacterHudData(EnemyHud hudController, Character character, out object hudData)
        {
            if (hudsDictionaryField.GetValue(hudController) is IDictionary huds)
            {
                foreach (DictionaryEntry entry in huds)
                {
                    if (entry.Key == null || entry.Value == null)
                        continue;

                    if (ReferenceEquals(entry.Key, character))
                    {
                        hudData = entry.Value;
                        return hudData != null;
                    }
                }
            }

            hudData = null;
            return false;
        }
    }
}