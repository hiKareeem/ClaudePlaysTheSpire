using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Tracks the active <see cref="NChooseACardSelectionScreen"/> instance (the
/// overlay opened by <c>CardSelectCmd.FromChooseACardScreen</c>, e.g. for
/// Attack/Skill/Power potions which present 3 random cards to pick from).
///
/// Captures the offered cards by reading the non-public <c>_cards</c> field
/// during <c>_Ready</c> (the ctor-like Godot lifecycle hook where <c>_cards</c>
/// is already set by <c>ShowScreen</c>), plus <c>_canSkip</c>, and exposes
/// them via <see cref="BridgeSnapshotWriter.SetChooseACardScreen"/>.
///
/// Cleared on <c>_ExitTree</c> when the overlay is popped (either after the
/// user picks or skips).
///
/// Consumed by <c>BridgeCommandDispatcher.DispatchChooseACard</c>, which
/// invokes the non-public <c>SelectHolder(NCardHolder)</c> method on the
/// screen — this completes the internal <c>TaskCompletionSource</c> that
/// <c>CardsSelected()</c> returns, unblocking the potion's OnUse state
/// machine which then calls <c>AddGeneratedCardToCombat</c>.
/// </summary>
[HarmonyPatch(typeof(NChooseACardSelectionScreen), "_Ready")]
internal static class ChooseACardSelectionReadyPatch
{
    public static NChooseACardSelectionScreen? LastScreen;
    public static IReadOnlyList<CardModel>? LastCards;
    public static bool LastCanSkip;

    private static readonly FieldInfo? CardsField = typeof(NChooseACardSelectionScreen).GetField(
        "_cards", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CanSkipField = typeof(NChooseACardSelectionScreen).GetField(
        "_canSkip", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Postfix(NChooseACardSelectionScreen __instance)
    {
        LastScreen = __instance;
        LastCards = CardsField?.GetValue(__instance) as IReadOnlyList<CardModel>;
        LastCanSkip = CanSkipField?.GetValue(__instance) is bool b && b;
        var count = LastCards?.Count ?? -1;
        BridgeTrace.Log($"ChooseACardSelectionReady: cardCount={count} canSkip={LastCanSkip}");

        try
        {
            var payload = BridgeStateExtractor.ExtractChooseACardScreen(__instance, LastCards, LastCanSkip);
            BridgeSnapshotWriter.SetChooseACardScreen(payload, "ChooseACardSelectionReady");
            BridgeSnapshotWriter.SetScreen("ChooseACardSelection", "ChooseACardSelectionReady");
        }
        catch (System.Exception ex)
        {
            BridgeTrace.Log($"ChooseACardSelectionReady extractor threw: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NChooseACardSelectionScreen), "_ExitTree")]
internal static class ChooseACardSelectionExitPatch
{
    public static void Postfix(NChooseACardSelectionScreen __instance)
    {
        if (!object.ReferenceEquals(ChooseACardSelectionReadyPatch.LastScreen, __instance))
            return;
        ChooseACardSelectionReadyPatch.LastScreen = null;
        ChooseACardSelectionReadyPatch.LastCards = null;
        ChooseACardSelectionReadyPatch.LastCanSkip = false;
        BridgeTrace.Log("ChooseACardSelectionExit: cleared");
        BridgeSnapshotWriter.SetChooseACardScreen(null, "ChooseACardSelectionExit");

        // The screen closes after the AttackPotion/SkillPotion/PowerPotion
        // state machine resumes and calls AddGeneratedCardToCombat. At this
        // point the new free-this-turn card is in the hand, but the combat
        // snapshot would remain stale until the next turn hook. Push a fresh
        // combat extraction so state.json reflects the added card.
        BridgeSingleton.PushCurrentCombat("ChooseACardSelectionExit");

        // Also reset the top-level screen marker back to Combat (it stays
        // stuck on "ChooseACardSelection" otherwise, because combat's _Ready
        // only fires once at combat start).
        if (BridgeSingleton.CurrentPlayer is not null)
        {
            BridgeSnapshotWriter.SetScreen("Combat", "ChooseACardSelectionExit");
        }
    }
}
