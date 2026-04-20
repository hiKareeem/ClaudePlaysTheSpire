using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Tracks in-hand card selection prompts triggered by cards/effects that call
/// <c>CardSelectCmd.FromHand</c>/<c>FromHandForDiscard</c>/<c>FromHandForUpgrade</c>
/// (e.g. Burning Pact's Exhaust-a-card, Armaments' upgrade-a-card,
/// Gambler's Brew's discard-N-cards). These do NOT open an overlay screen —
/// the hand itself becomes the selector UI driven by
/// <see cref="NPlayerHand.SelectCards"/>.
///
/// We postfix <c>RefreshSelectModeConfirmButton</c> (fired inside SelectCards's
/// async state machine AFTER <c>_prefs</c>, <c>_currentSelectionFilter</c>,
/// <c>_selectionCompletionSource</c> and header are all populated, and also
/// called after every card click/deselect to re-eval the confirm state) to
/// snapshot the current selection state. When the selection completes,
/// <c>AfterCardsSelected</c> fires and we clear the payload.
///
/// Consumed by <c>BridgeCommandDispatcher.DispatchHandSelectCard</c> etc.
/// </summary>
internal static class NPlayerHandSelectPatchState
{
    // NPlayerHand itself is a singleton (Instance), so we don't need to
    // remember the "last" instance — but we DO need to remember whether a
    // selection is currently active so the exit patch can fire the clear
    // exactly once.
    public static bool Active;

    private static readonly Type HandType = typeof(NPlayerHand);

    public static readonly FieldInfo? SelectedCardsField = HandType.GetField(
        "_selectedCards", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly FieldInfo? PrefsField = HandType.GetField(
        "_prefs", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly FieldInfo? FilterField = HandType.GetField(
        "_currentSelectionFilter", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly FieldInfo? ModeField = HandType.GetField(
        "_currentMode", BindingFlags.Instance | BindingFlags.NonPublic);

    public static readonly MethodInfo? OnHolderPressedMi = HandType.GetMethod(
        "OnHolderPressed", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly MethodInfo? OnConfirmMi = HandType.GetMethod(
        "OnSelectModeConfirmButtonPressed", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly MethodInfo? CancelMi = HandType.GetMethod(
        "CancelHandSelectionIfNecessary", BindingFlags.Instance | BindingFlags.NonPublic);
}

[HarmonyPatch(typeof(NPlayerHand), "RefreshSelectModeConfirmButton")]
internal static class NPlayerHandRefreshSelectPatch
{
    public static void Postfix(NPlayerHand __instance)
    {
        try
        {
            if (!__instance.IsInCardSelection)
            {
                // Not in a selection — don't spam writes. Exit patch handles
                // the clear separately.
                return;
            }

            NPlayerHandSelectPatchState.Active = true;

            // Read private state via reflection.
            var selected = NPlayerHandSelectPatchState.SelectedCardsField?.GetValue(__instance)
                as IReadOnlyList<CardModel>;
            var filter = NPlayerHandSelectPatchState.FilterField?.GetValue(__instance)
                as Func<CardModel, bool>;
            var prefsBoxed = NPlayerHandSelectPatchState.PrefsField?.GetValue(__instance);

            // Mode as string via the public property (enum.ToString).
            string? modeName = null;
            try { modeName = __instance.CurrentMode.ToString(); } catch { }

            // Prompt from prefs.Prompt (LocString).
            string? prompt = null;
            try
            {
                if (prefsBoxed is not null)
                {
                    var promptProp = prefsBoxed.GetType().GetProperty("Prompt");
                    if (promptProp?.GetValue(prefsBoxed) is LocString locStr)
                    {
                        prompt = BridgeStateExtractor.SafeLocString(locStr);
                    }
                }
            }
            catch { }

            // Current hand cards — BridgeSingleton.CurrentPlayer.PlayerCombatState.Hand.Cards.
            IReadOnlyList<CardModel>? handCards = null;
            try
            {
                handCards = BridgeSingleton.CurrentPlayer?.PlayerCombatState?.Hand?.Cards;
            }
            catch { }

            var payload = BridgeStateExtractor.ExtractHandSelect(
                __instance, handCards, filter, selected, prefsBoxed, modeName, prompt);

            BridgeSnapshotWriter.SetHandSelect(payload, "HandSelectRefresh");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"NPlayerHandRefreshSelectPatch threw: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand), "AfterCardsSelected")]
internal static class NPlayerHandAfterSelectedPatch
{
    public static void Postfix(NPlayerHand __instance)
    {
        try
        {
            if (!NPlayerHandSelectPatchState.Active) return;
            NPlayerHandSelectPatchState.Active = false;
            BridgeSnapshotWriter.SetHandSelect(null, "HandSelectExit");
            BridgeTrace.Log("NPlayerHandAfterSelected: cleared hand-select payload");

            // The selection just resolved (exhaust/upgrade/discard). The
            // snapshot's combat view (hand list, exhaust pile, etc.) is now
            // stale — push a fresh combat extraction so state.json reflects
            // the outcome without waiting for the next turn hook.
            BridgeSingleton.PushCurrentCombat("HandSelectExit");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"NPlayerHandAfterSelectedPatch threw: {ex.Message}");
        }
    }
}
