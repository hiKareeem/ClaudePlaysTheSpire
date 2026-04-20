using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Tracks the active <see cref="NCardGridSelectionScreen"/> instance (covers its
/// subclasses <c>NSimpleCardSelectScreen</c> used for shop card removal, and
/// <c>NDeckCardSelectScreen</c> used for "choose N cards from deck" prompts).
///
/// Captures the cards list by reading the non-public <c>_cards</c> field
/// during <c>ConnectSignalsAndInitGrid</c>, and clears on <c>_ExitTree</c>.
///
/// Consumed by <c>BridgeCommandDispatcher.DispatchSelectCardsInGrid</c> to call
/// <c>OnCardClicked</c> per selected card (and <c>ConfirmSelection</c> if the
/// concrete screen is <c>NDeckCardSelectScreen</c>).
/// </summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), "ConnectSignalsAndInitGrid")]
internal static class CardGridSelectionConnectPatch
{
    public static NCardGridSelectionScreen? LastScreen;
    public static IReadOnlyList<CardModel>? LastCards;

    private static readonly FieldInfo? CardsField = typeof(NCardGridSelectionScreen).GetField(
        "_cards", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Postfix(NCardGridSelectionScreen __instance)
    {
        LastScreen = __instance;
        LastCards = CardsField?.GetValue(__instance) as IReadOnlyList<CardModel>;
        var count = LastCards?.Count ?? -1;
        var typeName = __instance?.GetType().Name ?? "<null>";
        BridgeTrace.Log($"CardGridSelectionConnect: screen={typeName} cardCount={count}");

        try
        {
            var payload = BridgeStateExtractor.ExtractCardGridSelection(__instance, LastCards);
            BridgeSnapshotWriter.SetCardGrid(payload, "CardGridSelectionConnect");
            BridgeSnapshotWriter.SetScreen("CardGridSelection", "CardGridSelectionConnect");
        }
        catch (System.Exception ex)
        {
            BridgeTrace.Log($"CardGridSelectionConnect extractor threw: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCardGridSelectionScreen), "_ExitTree")]
internal static class CardGridSelectionExitPatch
{
    public static void Postfix(NCardGridSelectionScreen __instance)
    {
        if (!object.ReferenceEquals(CardGridSelectionConnectPatch.LastScreen, __instance))
            return;
        CardGridSelectionConnectPatch.LastScreen = null;
        CardGridSelectionConnectPatch.LastCards = null;
        BridgeTrace.Log("CardGridSelectionExit: cleared");
        BridgeSnapshotWriter.SetCardGrid(null, "CardGridSelectionExit");

        var roomScreen = BridgeSingleton.CurrentMerchantRoom is not null ? "Room:Shop"
            : BridgeSingleton.CurrentRestSiteRoom is not null ? "Room:RestSite"
            : BridgeSingleton.CurrentTreasureRoom is not null ? "Room:Treasure"
            : BridgeSingleton.CurrentEventRoom is not null ? "Room:Event"
            : BridgeSingleton.CurrentCombatRoom is not null ? "Combat"
            : null;
        if (roomScreen is not null)
        {
            BridgeSnapshotWriter.SetScreen(roomScreen, "CardGridSelectionExit");
        }
    }
}
