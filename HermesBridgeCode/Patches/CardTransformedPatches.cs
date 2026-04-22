using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Forces a <c>run</c> snapshot refresh whenever a card is transformed in
/// (or out of) the deck. Without this, events like Wood Carvings silently
/// complete — the underlying deck is mutated correctly, but
/// <c>state.run.deck[]</c> in state.json stays at its pre-event contents
/// until the next organic run-level write (map travel, combat end, etc.),
/// giving controllers false signal that the transform never happened.
///
/// <c>CardModel.AfterTransformedTo</c> fires on the newly-minted card after
/// it's been placed into the deck collection. <c>AfterTransformedFrom</c>
/// fires on the card being removed. Patching both provides redundant
/// coverage; each only writes a run payload, which is idempotent.
/// </summary>
[HarmonyPatch(typeof(CardModel), "AfterTransformedTo")]
internal static class CardAfterTransformedToPatch
{
    public static void Postfix(CardModel __instance)
    {
        var title = __instance?.Title ?? "<null>";
        BridgeTrace.Log($"CardModel.AfterTransformedTo fired card={title}");
        BridgeSingleton.PushCurrentRun("CardAfterTransformedTo");
    }
}

[HarmonyPatch(typeof(CardModel), "AfterTransformedFrom")]
internal static class CardAfterTransformedFromPatch
{
    public static void Postfix(CardModel __instance)
    {
        var title = __instance?.Title ?? "<null>";
        BridgeTrace.Log($"CardModel.AfterTransformedFrom fired card={title}");
        BridgeSingleton.PushCurrentRun("CardAfterTransformedFrom");
    }
}
