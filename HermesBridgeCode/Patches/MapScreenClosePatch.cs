using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
internal static class MapScreenClosePatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NMapScreen.Close postfix fired");

        // If the user peeked at the map mid-combat (top-bar map button),
        // Close here fires while combat is still live underneath. Restore
        // the Combat screen and refresh the combat payload so controllers
        // don't see an empty `combat` block while the fight is ongoing.
        // COMBATBLIND fix (2026-04-20).
        if (BridgeSingleton.CurrentCombatRoom is not null)
        {
            BridgeSnapshotWriter.SetScreen("Combat", "MapScreenClose-CombatActive");
            BridgeSingleton.PushCurrentCombat("MapScreenClose-CombatActive");
            return;
        }

        BridgeSnapshotWriter.SetScreen("MapClosed", "MapScreenClose");
    }
}
