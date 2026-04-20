using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Re-push rest-site state after the async option-selection flow has actually
/// consumed the chosen option and finalized any deck mutation.
///
/// <para>
/// <c>UpdateRestSiteOptions</c> is invoked from
/// <c>AfterSelectingOptionAsync</c> after the rest-site UI has been refreshed.
/// This is the stable post-selection moment we want: smith upgrades have landed,
/// consumed options are gone, and the next snapshot reflects the real room
/// state instead of a transient pre-refresh view.
/// </para>
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
internal static class RestSiteRoomUpdateOptionsPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NRestSiteRoom.UpdateRestSiteOptions postfix");
        // Push run first so deck changes (Smith upgrades) land, then rest
        // site so the consumed options list is fresh.
        BridgeSingleton.PushCurrentRun("RestSiteUpdateOptions");
        BridgeSingleton.PushCurrentRestSite("RestSiteUpdateOptions");
    }
}
