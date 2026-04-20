using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Fires every time a shop purchase completes (or fails). We re-push the shop
/// snapshot so stock/prices and player gold stay in sync, AND re-push the run
/// snapshot so gold/deck/potions/relics on the run side also update.
///
/// The run-side push is essential: without it, state.run.gold and state.run.{deck,
/// potions,relics} become stale after a purchase because no run-level hook (such
/// as AfterRoomEntered / AfterRewardTaken / AfterCombatEnd) fires on a purchase.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), "OnPurchaseCompleted")]
internal static class MerchantInventoryOnPurchaseCompletedPatch
{
    public static void Postfix(PurchaseStatus status, MerchantEntry entry)
    {
        BridgeTrace.Log($"NMerchantInventory.OnPurchaseCompleted status={status} entryType={entry?.GetType().Name ?? "<null>"}");
        BridgeSingleton.PushCurrentShop("PurchaseCompleted");
        BridgeSingleton.PushCurrentRun("PurchaseCompleted");
    }
}
