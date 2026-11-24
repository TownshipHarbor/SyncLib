# SyncLib for Quest

A Quest port of [SyncLib](https://github.com/MrDuckTheFifth/SyncLib) by MrDuckTheFifth, for A Township Tale on Quest with LemonLoader.

SyncLib lets mods add their own items and prefabs. The server gives every custom prefab a number and sends the list to each player when they join. This port is the headset half: it reads that list, builds the custom prefabs with the same numbers, and tells newer SyncLib servers which ones the headset can show.

## For players

Put `SyncLib.dll` in the headset's `Mods` folder:

```
/sdcard/Android/data/com.Alta.ATownshipTale/files/MelonLoader/com.Alta.ATownshipTale/Mods/
```

On its own it adds no items. It is what Quest versions of SyncLib mods need to show their items. Custom items from mods that have no Quest version stay hidden, the same as on PC.

## What it speaks

| Server | Works |
|---|---|
| SyncLib 1.0.4 (GitHub) | the registry in one message |
| SyncLib 1.1.x | the registry in chunks, then the headset sends its prefab report |
| No SyncLib | nothing happens |

Workbench recipes from SyncLib 1.1 are not ported yet. A server that registers them will refuse a headset that lacks them, as it refuses a PC that lacks them.

## For modders

The API is SyncLib's, in the same namespaces, so most of a PC mod's code carries over:

```csharp
using MelonLoader;
using SyncLib.Items;
using System.Reflection;
using UnityEngine;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;

public class Main : MelonMod
{
    public override void OnInitializeMelon()
    {
        NetworkPrefabRegistry.RegisterPrefabs += AddPrefabs;
    }

    void AddPrefabs()
    {
        GameObject obj = NetworkPrefabRegistry.GetGameObjectFromAssetBundle(Assembly.GetExecutingAssembly(), "CubeMod.Resources.cube", "Cube Item");
        NetworkPrefab prefab = this.RegisterCustomPrefab(obj, "Cube");
        if (prefab == null) return;
        Item item = prefab.RegisterCustomItem();
    }
}
```

Differences from PC:

1. Load bundles with `NetworkPrefabRegistry.GetGameObjectFromAssetBundle` or `LoadAssetBundle`. The Quest game has no `AssetBundle.LoadFromStream`, and SyncLib reads your ATT Workshop settings from the bundle file itself, because the Workshop scripts cannot load on the headset.
2. The bundle must be built for Android with Unity 2020.3. Use `tools/unity/ExportQuestAssets.cs` in the ATT Workshop project, or turn a PC bundle into a Quest one with `tools/questbundle`.
3. Your Quest mod's `MelonInfo` name must match your PC mod's name, because the server keys every prefab as `"<id> (<mod name>)"`. If the names differ, use `NetworkPrefabRegistry.RegisterCustomPrefab("PCModName", obj, "Cube")`.
4. `RegisterPrefabs` runs again on every server you join. Registering the same GameObject again reuses what was built and only changes its number. If you add your own components in the handler, check they are not there already.
5. Materials using the built-in Standard or Legacy shaders draw black on Quest, so SyncLib switches them to the game's `Township Tale/Cheap/SimpleStandard`, keeping colour and main texture.
6. Components passed in `AdditionalComponents` get their `[GetComponent]` fields filled only for game types SyncLib knows. Fill your own.

`NetworkPrefabRegistry.ServerRegistry`, `ServerJson` and `MissingMods` show what the server sent and what this headset lacks.

## Building

`build.ps1` builds `quest\SyncLib.csproj` into `build\`. Put LemonLoader's `net8` folder and its generated `Il2CppAssemblies` in `lemon\`, and `AssetsTools.NET.dll` from the PC game's `MelonLoader\net35` folder in `lemon\net35\`. The DLL carries AssetsTools.NET inside it to read Workshop settings from bundles.

## Credits

SyncLib and ATT Workshop are by MrDuckTheFifth. AssetsTools.NET is by nesrak1, under the MIT licence. Quest port by Shadow.

This is an unofficial, fan made mod for A Township Tale. It is not affiliated with or endorsed by the owners or developers of A Township Tale.
