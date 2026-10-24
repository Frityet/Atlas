using System;
using System.IO;
using UnityEngine;

namespace Atlas
{
    public static class NativeBootstrap
    {
        public static bool IsInitialized => AtlasPlugin.Instance;

        // This also runs when these DLLs are included in a newly built Unity player.
        // An existing player needs the small managed bootstrap installed by the harness.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (Application.isEditor || IsInitialized) return;
            if (Application.unityVersion != "2021.3.45f2")
                throw new NotSupportedException("Atlas native target requires Unity 2021.3.45f2.");
            Sodalite.Sodalite.Initialize();
            var host = new GameObject("Atlas Unity 2021");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<AtlasPlugin>();
            if (!IsInitialized)
            {
                UnityEngine.Object.Destroy(host);
                throw new InvalidOperationException("Atlas hooks could not be initialized. Check the player log.");
            }
            string directory = Environment.GetEnvironmentVariable("ATLAS_MAP_DIRECTORY")
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AtlasMaps"));
            if (Directory.Exists(directory))
                foreach (var bundle in Directory.GetFiles(directory, "*.atlas", SearchOption.AllDirectories))
                    AtlasPlugin.RegisterScene(bundle);
            Debug.Log("ATLAS_UNITY2021_READY maps=" + AtlasPlugin.CustomSceneInfos.Count);
        }
    }
}
