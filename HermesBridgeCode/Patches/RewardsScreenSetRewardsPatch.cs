using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.SetRewards))]
internal static class RewardsScreenSetRewardsPatch
{
    /// <summary>
    /// Raw reward list captured from the most recent <c>SetRewards</c> call.
    /// Used by <see cref="BridgeCommandDispatcher"/> to look up rewards by
    /// <c>RewardsSetIndex</c> (matches the <c>index</c> field in state.json).
    /// </summary>
    public static IReadOnlyList<Reward>? LastRewards { get; private set; }

    /// <summary>The screen instance that received the most recent SetRewards call.</summary>
    public static NRewardsScreen? LastScreen { get; private set; }

    public static void RefreshVisibleRewards(string trigger)
    {
        var rewards = LastRewards;
        if (rewards is null)
        {
            BridgeSnapshotWriter.SetRewards(null, $"{trigger}-ClearRewards");
            return;
        }

        var filtered = rewards.Where(IsRewardStillVisible).ToList();
        LastRewards = filtered;
        var extracted = BridgeStateExtractor.ExtractRewards(filtered);
        BridgeSnapshotWriter.SetRewards(extracted, trigger);
    }

    public static void RemoveReward(Reward? reward, string trigger)
    {
        var rewards = LastRewards;
        if (reward is null || rewards is null)
        {
            RefreshVisibleRewards(trigger);
            return;
        }

        LastRewards = rewards.Where(r => !ReferenceEquals(r, reward)).ToList();
        RefreshVisibleRewards(trigger);
    }

    private static bool IsRewardStillVisible(Reward reward)
    {
        if (reward is null) return false;

        try
        {
            var parentRewards = reward.ParentRewardSet?.Rewards;
            if (parentRewards is not null && !parentRewards.Any(r => ReferenceEquals(r, reward)))
            {
                return false;
            }

            return reward switch
            {
                CardReward cardReward => cardReward.Cards?.Any() == true,
                RelicReward relicReward => relicReward.ClaimedRelic is null,
                PotionReward potionReward => potionReward.ClaimedPotion is null,
                _ => true,
            };
        }
        catch
        {
            return true;
        }
    }

    public static void Postfix(NRewardsScreen __instance, IEnumerable<Reward> rewards)
    {
        BridgeTrace.Log("NRewardsScreen.SetRewards postfix fired (capturing payload)");
        LastScreen = __instance;
        LastRewards = rewards?.ToList();
        var extracted = BridgeStateExtractor.ExtractRewards(rewards);
        BridgeSnapshotWriter.SetScreen("Rewards", "RewardsScreenSetRewards");
        BridgeSnapshotWriter.SetRewards(extracted, "RewardsScreenSetRewardsPayload");
    }
}
