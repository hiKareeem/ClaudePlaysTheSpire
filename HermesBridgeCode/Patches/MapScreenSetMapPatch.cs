using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Hooks <see cref="NMapScreen.SetMap"/> so we can snapshot the map state
/// immediately after the screen has populated <c>_mapPointDictionary</c> with
/// the current act's points + travelability. This is the canonical moment at
/// which <see cref="BridgeStateExtractor.ExtractMap"/> has all it needs.
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class MapScreenSetMapPatch
{
    public static void Postfix(ActMap map)
    {
        BridgeTrace.Log($"NMapScreen.SetMap postfix fired (map={(map is null ? "<null>" : "<ok>")})");
        BridgeSingleton.PushCurrentMap("MapScreenSetMap");
    }
}
