#if HERMES_DIAG_SMITH
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;

namespace HermesBridge.HermesBridgeCode.Patches;

// Diagnostic patches to trace the full Smith upgrade flow end-to-end.

[HarmonyPatch(typeof(NCardGridSelectionScreen), "CardsSelected")]
internal static class DiagCardsSelectedPatch
{
    public static void Prefix(NCardGridSelectionScreen __instance)
    {
        BridgeTrace.Log($"DIAG CardsSelected called on {__instance?.GetType().Name}");
    }
}

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), "SyncLocalChoice", new Type[] { typeof(Player), typeof(uint), typeof(PlayerChoiceResult) })]
internal static class DiagSyncLocalChoicePatch
{
    public static void Prefix(uint choiceId)
    {
        BridgeTrace.Log($"DIAG SyncLocalChoice choiceId={choiceId}");
    }
}

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), "WaitForRemoteChoice", new Type[] { typeof(Player), typeof(uint) })]
internal static class DiagWaitRemoteChoicePatch
{
    public static void Prefix(uint choiceId)
    {
        BridgeTrace.Log($"DIAG WaitForRemoteChoice ENTER choiceId={choiceId}");
    }
    public static void Postfix(uint choiceId)
    {
        BridgeTrace.Log($"DIAG WaitForRemoteChoice EXIT(sync part) choiceId={choiceId}");
    }
}

[HarmonyPatch(typeof(CardCmd), "Upgrade", new Type[] { typeof(CardModel), typeof(CardPreviewStyle) })]
internal static class DiagCardUpgradeSinglePatch
{
    public static void Prefix(CardModel card)
    {
        var title = card?.Title?.ToString() ?? "<null>";
        BridgeTrace.Log($"DIAG CardCmd.Upgrade(single) card={title}");
    }
}

[HarmonyPatch(typeof(Hook), "AfterRestSiteSmith", new Type[] { typeof(IRunState), typeof(Player) })]
internal static class DiagHookAfterSmithPatch
{
    public static void Prefix()
    {
        BridgeTrace.Log("DIAG Hook.AfterRestSiteSmith fired");
    }
}

[HarmonyPatch(typeof(CardModel), "UpgradeInternal")]
internal static class DiagCardModelUpgradeInternalPatch
{
    public static void Prefix(CardModel __instance)
    {
        var title = __instance?.Title?.ToString() ?? "<null>";
        var lvl = __instance?.CurrentUpgradeLevel ?? -1;
        BridgeTrace.Log($"DIAG CardModel.UpgradeInternal PRE card={title} lvl={lvl}");
    }
    public static void Postfix(CardModel __instance)
    {
        var title = __instance?.Title?.ToString() ?? "<null>";
        var lvl = __instance?.CurrentUpgradeLevel ?? -1;
        BridgeTrace.Log($"DIAG CardModel.UpgradeInternal POST card={title} lvl={lvl}");
    }
}

[HarmonyPatch(typeof(CardModel), "FinalizeUpgradeInternal")]
internal static class DiagCardModelFinalizeUpgradePatch
{
    public static void Postfix(CardModel __instance)
    {
        var title = __instance?.Title?.ToString() ?? "<null>";
        var lvl = __instance?.CurrentUpgradeLevel ?? -1;
        BridgeTrace.Log($"DIAG CardModel.FinalizeUpgradeInternal POST card={title} lvl={lvl}");
    }
}

// ---- Rest-site option-consumption flow ----

[HarmonyPatch(typeof(RestSiteSynchronizer), "ChooseOption", new Type[] { typeof(Player), typeof(int) })]
internal static class DiagChooseOptionPatch
{
    public static void Prefix(Player player, int optionIndex)
    {
        BridgeTrace.Log($"DIAG RestSiteSynchronizer.ChooseOption ENTER player={player?.NetId} idx={optionIndex}");
    }
}

[HarmonyPatch(typeof(RestSiteSynchronizer), "ChooseLocalOption", new Type[] { typeof(int) })]
internal static class DiagChooseLocalOptionPatch
{
    public static void Prefix(int index)
    {
        BridgeTrace.Log($"DIAG RestSiteSynchronizer.ChooseLocalOption ENTER idx={index}");
    }
    public static void Postfix()
    {
        BridgeTrace.Log("DIAG RestSiteSynchronizer.ChooseLocalOption EXIT(sync part)");
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "OnAfterPlayerSelectedRestSiteOption")]
internal static class DiagOnAfterSelectedPatch
{
    public static void Prefix(RestSiteOption option, bool success, ulong playerId)
    {
        BridgeTrace.Log($"DIAG NRestSiteRoom.OnAfterPlayerSelectedRestSiteOption option={option?.GetType().Name} success={success} netId={playerId}");
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "AfterSelectingOptionAsync")]
internal static class DiagAfterSelectingAsyncPatch
{
    public static void Prefix(RestSiteOption option)
    {
        BridgeTrace.Log($"DIAG NRestSiteRoom.AfterSelectingOptionAsync ENTER option={option?.GetType().Name}");
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
internal static class DiagUpdateRestSiteOptionsPatch
{
    public static void Prefix(NRestSiteRoom __instance)
    {
        try
        {
            int count = 0;
            if (__instance?.Options is not null)
                foreach (var _ in __instance.Options) count++;
            BridgeTrace.Log($"DIAG NRestSiteRoom.UpdateRestSiteOptions ENTER optionCount={count}");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"DIAG NRestSiteRoom.UpdateRestSiteOptions ENTER <ex: {ex.Message}>");
        }
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "AfterSelectingOption")]
internal static class DiagAfterSelectingSyncPatch
{
    public static void Prefix(RestSiteOption option)
    {
        BridgeTrace.Log($"DIAG NRestSiteRoom.AfterSelectingOption (sync) option={option?.GetType().Name}");
    }
}
#endif
