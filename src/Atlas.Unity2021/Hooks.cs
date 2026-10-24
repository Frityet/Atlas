using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Atlas.MappingComponents;
using Atlas.MappingComponents.TakeAndHold;
using FistVR;
using HarmonyLib;
using Sodalite.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Atlas
{
    public partial class AtlasPlugin
    {
        private Harmony? harmony;
        private static readonly HashSet<int> ContinuingLoads = new();

        private void ApplyHooks()
        {
            harmony = new Harmony(AtlasConstants.Guid + ".unity2021");
            try
            {
                Patch(typeof(FVRSceneSettings), "Awake", nameof(BeforeSceneSettings));
                Patch(typeof(TNH_Manager), "Start", nameof(BeforeTakeAndHold));
                Patch(typeof(MainMenuScreen), "LoadScene", nameof(BeforeMenuLoad));
                Patch(typeof(TNH_UIManager), "Start", nameof(BeforeTakeAndHoldMenu));
                Patch(typeof(TNH_UIManager), "UpdateLevelSelectUi", nameof(BeforeLevelSelect));
                Patch(typeof(FVRReverbSystem), "CheckPlayerEnvironment", nameof(PlayerExists));
                Patch(typeof(FVRAmbienceController), "Update", nameof(PlayerExists));
                // Probe capabilities instead of a Steam build number (unavailable on converted players).
                if (AccessTools.Method(typeof(TNH_UIManager), "LoadCurrentlySelectedLevel") != null)
                    Patch(typeof(TNH_UIManager), "LoadCurrentlySelectedLevel", nameof(BeforeTakeAndHoldLoad));
                else
                    Patch(typeof(SceneLoader), "LoadMG", nameof(BeforeLegacyTakeAndHoldLoad));
            }
            catch { harmony.UnpatchAll(harmony.Id); harmony = null; throw; }
        }

        private void Patch(Type type, string method, string prefix)
        {
            var original = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
            harmony!.Patch(original, prefix: new HarmonyMethod(typeof(AtlasPlugin), prefix));
        }

        private void RemoveHooks()
        {
            if (harmony != null) harmony.UnpatchAll(harmony.Id);
            LeaderboardLock?.Dispose();
            LeaderboardLock = null;
            ContinuingLoads.Clear();
        }

        private static bool PlayerExists() => GM.CurrentPlayerBody;

        private static void BeforeSceneSettings(FVRSceneSettings __instance)
        {
            var settings = FindObjectOfType<SceneSettingsOverride>();
            if (settings)
            {
                settings.ApplyOverrides(__instance);
                AccessTools.Method(typeof(GM), "InitScene").Invoke(GM.Instance, null);
                LeaderboardLock ??= LeaderboardAPI.LeaderboardDisabled.TakeLock();
            }
            else
            {
                CurrentScene = null;
                LastLoadedScene = null;
                LeaderboardLock?.Dispose();
                LeaderboardLock = null;
            }
        }

        private static void BeforeTakeAndHold(TNH_Manager __instance)
        {
            if (CurrentScene == null) return;
            var settings = FindObjectOfType<TNH_ManagerOverride>();
            if (!settings) throw new InvalidOperationException("Atlas Take and Hold map has no TNH_ManagerOverride.");
            settings.ApplyOverrides(__instance);
        }

        private static bool BeforeMenuLoad(MainMenuScreen __instance)
        {
            if (ContinuingLoads.Remove(__instance.GetInstanceID())) return true;
            if (!(AccessTools.Field(typeof(MainMenuScreen), "m_def").GetValue(__instance) is CustomMainMenuSceneDef custom)) return true;
            Instance.StartCoroutine(LoadBundleThen(custom.CustomSceneInfo, () =>
            {
                custom.SceneName = OnlyScenePath(custom.CustomSceneInfo);
                ContinuingLoads.Add(__instance.GetInstanceID());
                __instance.LoadScene();
            }));
            return false;
        }

        private static void BeforeTakeAndHoldMenu(TNH_UIManager __instance)
        {
            foreach (var scene in RegisteredScenes.Where(s => s.DisplayMode == AtlasConstants.LoaderTakeAndHold))
                if (!__instance.Levels.OfType<CustomLevelData>().Any(x => x.SceneInfo.Identifier == scene.Identifier))
                    __instance.Levels.Add(new CustomLevelData(scene));
        }

        private static void BeforeLevelSelect(TNH_UIManager __instance)
        {
            if (__instance.CurrentLevel is CustomLevelData custom)
            {
                LastLoadedScene = custom.SceneInfo;
                LeaderboardLock ??= LeaderboardAPI.LeaderboardDisabled.TakeLock();
            }
            else
            {
                LastLoadedScene = null;
                LeaderboardLock?.Dispose();
                LeaderboardLock = null;
            }
        }

        private static bool BeforeTakeAndHoldLoad(TNH_UIManager __instance)
        {
            if (ContinuingLoads.Remove(__instance.GetInstanceID()) || LastLoadedScene == null) return true;
            var scene = LastLoadedScene;
            Instance.StartCoroutine(LoadBundleThen(scene, () =>
            {
                __instance.CurrentLevel.LevelSceneName = OnlyScenePath(scene);
                ContinuingLoads.Add(__instance.GetInstanceID());
                AccessTools.Method(typeof(TNH_UIManager), "LoadCurrentlySelectedLevel").Invoke(__instance, null);
            }));
            return false;
        }

        private static bool BeforeLegacyTakeAndHoldLoad(SceneLoader __instance)
        {
            if (ContinuingLoads.Remove(__instance.GetInstanceID()) || LastLoadedScene == null ||
                SceneManager.GetActiveScene().name != "TakeAndHold_Lobby_2") return true;
            var scene = LastLoadedScene;
            Instance.StartCoroutine(LoadBundleThen(scene, () =>
            {
                __instance.LevelName = OnlyScenePath(scene);
                ContinuingLoads.Add(__instance.GetInstanceID());
                AccessTools.Method(typeof(SceneLoader), "LoadMG").Invoke(__instance, null);
            }));
            return false;
        }

        private static string OnlyScenePath(CustomSceneInfo scene)
        {
            var paths = scene.SceneBundle!.GetAllScenePaths();
            if (paths.Length != 1) throw new InvalidOperationException("Atlas bundles must contain exactly one scene.");
            return paths[0];
        }

        private static IEnumerator LoadBundleThen(CustomSceneInfo scene, Action continuation)
        {
            if (!scene.SceneBundle)
            {
                var request = AssetBundle.LoadFromFileAsync(scene.SceneBundleFile.FullName);
                yield return request;
                scene.SceneBundle = request.assetBundle;
                if (!scene.SceneBundle) throw new InvalidOperationException("Failed to load Atlas bundle: " + scene.SceneBundleFile.FullName);
            }
            OnlyScenePath(scene);
            LastLoadedScene = scene;
            continuation();
        }
    }
}
