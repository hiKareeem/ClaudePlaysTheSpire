using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "RefreshOptions")]
internal static class CardRewardRefreshOptionsPatch
{
    /// <summary>Most recent card-creation results passed to the screen, captured by index.</summary>
    public static IReadOnlyList<CardCreationResult>? LastOptions;

    /// <summary>Most recent extra options (Skip / Reroll / etc.), captured by index.</summary>
    public static IReadOnlyList<CardRewardAlternative>? LastExtraOptions;

    /// <summary>The screen instance that received the most recent RefreshOptions call.</summary>
    public static NCardRewardSelectionScreen? LastScreen;

    public static void Postfix(
        NCardRewardSelectionScreen __instance,
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        BridgeTrace.Log("NCardRewardSelectionScreen.RefreshOptions postfix fired (capturing payload)");
        LastScreen = __instance;
        LastOptions = options;
        LastExtraOptions = extraOptions;
        var extracted = BridgeStateExtractor.ExtractCardOptions(options, extraOptions);
        BridgeSnapshotWriter.SetScreen("CardReward", "CardRewardRefreshOptions");
        BridgeSnapshotWriter.SetCardRewardOptions(extracted, "CardRewardRefreshOptionsPayload");
    }
}
