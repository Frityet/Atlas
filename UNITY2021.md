# NativeH3VR / Unity 2021 target

`src/Atlas.Unity2021/Atlas.Unity2021.csproj` builds the Atlas runtime and mapping components for NativeH3VR on Unity **2021.3.45f2**. The original BepInEx/Unity 5.6 project remains separate.

Build Sodalite's Unity 2021 target first, then:

```sh
dotnet build src/Atlas.Unity2021/Atlas.Unity2021.csproj -c Release \
  -p:NativeH3VRManaged=/absolute/path/NewEngineH3VR.app/Contents/Resources/Data/Managed \
  -p:UnityEngineManaged=/Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/Managed/UnityEngine \
  -p:SodaliteAssembly=/absolute/path/Sodalite.dll
```

This target uses Harmony 2.4.3 rather than the Unity 5.6 MMHOOK assemblies. It reflects the current private scene/menu hooks, uses `TNH_UIManager.CurrentLevel` and `LoadCurrentlySelectedLevel`, applies the existing scene/TnH overrides, and retains leaderboard locking. Player spawn/reset markers wait for the native game managers. Teleport facing uses a direction vector. Physical-material resource paths use forward slashes on macOS.

Call `Atlas.NativeBootstrap.Initialize()` before the first game scene. It initializes Sodalite, persists the Atlas host, and registers `.atlas` files from `ATLAS_MAP_DIRECTORY`, or the `AtlasMaps` folder beside the player Data folder. A `RuntimeInitializeOnLoadMethod` is also supplied for new player builds. Merely copying a new DLL into an already-built player does not update its baked initialization list; the H3VRBackrooms harness installs an explicit managed bootstrap into a separate local copy of the player.

Each bundle contains exactly one scene and has adjacent `map.atlas.json` metadata and optional `map.atlas.png`. Native metadata additionally requires `UnityVersion` matching the player and `BuildTarget` matching its platform (`StandaloneOSX` on macOS). Existing `Identifier`, `DisplayName`, `Author`, `Description`, `DisplayMode`, and `GameMode` fields remain. Duplicate identifiers are rejected. Rebuild maps using the native target's Atlas/Sodalite assemblies; old Unity 5.6 or Windows bundles are not interchangeable.

`Application.isEditor` selects layout-preview behavior: components remain editable and serializable but do not load donor game scenes or seek a VR player. Use a separate editor-only camera. Runtime map tests must use the native player with its actual managers and resources.

The accompanying H3VRBackrooms project supplies a sandbox scene and native menu/load/reload harness. Take and Hold API adaptation is present, but a complete authored TnH map and full TnH playthrough are outside that smoke test. Game libraries and assets are local build inputs, not included in this port.
