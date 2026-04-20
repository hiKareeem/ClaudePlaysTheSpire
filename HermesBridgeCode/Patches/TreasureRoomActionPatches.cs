using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Re-pushes treasure state after the chest open animation completes so
/// Hermes sees <c>hasChestBeenOpened=true</c> and the populated
/// <c>relicChoices</c> list. Patched as a postfix on <c>OpenChest</c>
/// (which is async); the postfix runs at method-launch time, but the
/// flags <c>_hasChestBeenOpened</c> / <c>_isRelicCollectionOpen</c> are
/// flipped synchronously near the top of <c>OpenChest</c>, so by the
/// time the postfix runs they already reflect the new state. Holders
/// are populated synchronously by <see cref="NTreasureRoomRelicCollection.InitializeRelics"/>.
/// </summary>
[HarmonyPatch(typeof(NTreasureRoom), "OpenChest")]
internal static class TreasureChestOpenedPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NTreasureRoom.OpenChest postfix fired");
        BridgeSingleton.PushCurrentTreasure("TreasureChestOpened");
    }
}

/// <summary>
/// Re-pushes treasure state after a relic is picked. The collection clears
/// the picked holder out of <c>_holdersInUse</c> internally, so the
/// extractor's relicChoices list naturally shrinks.
/// </summary>
[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "PickRelic")]
internal static class TreasureRelicPickedPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NTreasureRoomRelicCollection.PickRelic postfix fired");
        BridgeSingleton.PushCurrentTreasure("TreasureRelicPicked");
        // Player relics changed -> refresh run snapshot too.
        BridgeSingleton.PushCurrentRun("TreasureRelicPicked");
    }
}
