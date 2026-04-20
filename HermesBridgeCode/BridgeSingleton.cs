using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// Singleton model that subscribes to combat + run lifecycle hooks via the
/// BaseLib <see cref="CustomSingletonModel"/> extension point. Auto-registered
/// by BaseLib / ModelDb scanning; no explicit instantiation required.
///
/// Each hook:
///   1. Writes a BridgeTrace entry so we can verify it fired post-deploy.
///   2. Updates <see cref="BridgeSnapshotWriter"/> with coarse screen info
///      plus structured combat / run payloads.
///   3. Returns <see cref="Task.CompletedTask"/> \u2014 we do NOT perform async work.
///
/// Hooks intentionally guard every read with try/catch and never rethrow.
/// </summary>
public sealed class BridgeSingleton : CustomSingletonModel
{
    /// <summary>
    /// Last seen active <see cref="CombatRoom"/>. Recorded on room-enter so
    /// that turn-start hooks (which only receive a <see cref="Player"/>) can
    /// still reach the combat state.
    /// </summary>
    private CombatRoom? _currentCombatRoom;

    /// <summary>Primary player captured at combat start / turn start.</summary>
    private Player? _currentPlayer;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static Player? CurrentPlayer => _instance?._currentPlayer;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static CombatRoom? CurrentCombatRoom => _instance?._currentCombatRoom;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static EventRoom? CurrentEventRoom => _instance?._currentEventRoom;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static MerchantRoom? CurrentMerchantRoom => _instance?._currentMerchantRoom;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static RestSiteRoom? CurrentRestSiteRoom => _instance?._currentRestSiteRoom;

    /// <summary>Public accessor for command dispatchers.</summary>
    public static TreasureRoom? CurrentTreasureRoom => _instance?._currentTreasureRoom;

    /// <summary>Public accessor for command dispatchers (live Godot node, set by TreasureRoomReadyPatch).</summary>
    public static NTreasureRoom? CurrentTreasureNode => _instance?._currentTreasureNode;

    /// <summary>Last seen active <see cref="EventRoom"/>.</summary>
    private EventRoom? _currentEventRoom;

    /// <summary>Last seen active <see cref="MerchantRoom"/> (shop).</summary>
    private MerchantRoom? _currentMerchantRoom;

    /// <summary>Last seen active <see cref="RestSiteRoom"/> (campfire).</summary>
    private RestSiteRoom? _currentRestSiteRoom;

    /// <summary>Last seen active <see cref="TreasureRoom"/> (chest).</summary>
    private TreasureRoom? _currentTreasureRoom;

    /// <summary>Live <see cref="NTreasureRoom"/> Godot node, captured by TreasureRoomReadyPatch.</summary>
    private NTreasureRoom? _currentTreasureNode;

    public BridgeSingleton() : base(receiveCombatHooks: true, receiveRunHooks: true)
    {
        BridgeTrace.Log("BridgeSingleton ctor");
        _instance = this;
    }

    // -------- Run / navigation lifecycle --------

    public override Task BeforeRoomEntered(AbstractRoom room)
    {
        try
        {
            var id = room?.ModelId;
            var idStr = id is null ? "<null>" : $"{id.Category}:{id.Entry}";
            BridgeTrace.Log($"Hook BeforeRoomEntered roomType={room?.RoomType} id={idStr}");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook BeforeRoomEntered threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        try
        {
            var roomType = room?.RoomType.ToString() ?? "<null>";
            BridgeTrace.Log($"Hook AfterRoomEntered roomType={roomType}");
            _currentCombatRoom = room as CombatRoom;
            _currentEventRoom = room as EventRoom;
            _currentMerchantRoom = room as MerchantRoom;
            _currentRestSiteRoom = room as RestSiteRoom;
            _currentTreasureRoom = room as TreasureRoom;
            // Clear stale node ref when leaving treasure room; the new
            // NTreasureRoom._Ready postfix will repopulate it on entry.
            if (_currentTreasureRoom is null) _currentTreasureNode = null;
            BridgeSnapshotWriter.SetScreen($"Room:{roomType}", "AfterRoomEntered");
            PushRun("AfterRoomEntered");

            if (_currentEventRoom is not null)
            {
                PushEvent("AfterRoomEntered");
            }
            else
            {
                // Leaving an event room -> clear event payload.
                BridgeSnapshotWriter.SetEvent(null, "AfterRoomEntered-ClearEvent");
            }

            if (_currentMerchantRoom is not null)
            {
                PushShop("AfterRoomEntered");
            }
            else
            {
                BridgeSnapshotWriter.SetShop(null, "AfterRoomEntered-ClearShop");
            }

            if (_currentRestSiteRoom is not null)
            {
                PushRestSite("AfterRoomEntered");
            }
            else
            {
                BridgeSnapshotWriter.SetRestSite(null, "AfterRoomEntered-ClearRestSite");
            }

            if (_currentTreasureRoom is not null)
            {
                PushTreasure("AfterRoomEntered");
            }
            else
            {
                BridgeSnapshotWriter.SetTreasure(null, "AfterRoomEntered-ClearTreasure");
            }

            // Map state changes whenever the player advances rooms (visited coords,
            // travelable nodes, current coord all shift).
            PushCurrentMap("AfterRoomEntered");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterRoomEntered threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task BeforeRewardsOffered(Player player, IReadOnlyList<Reward> rewards)
    {
        try
        {
            BridgeTrace.Log($"Hook BeforeRewardsOffered rewardCount={rewards?.Count ?? 0}");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook BeforeRewardsOffered threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        try
        {
            BridgeTrace.Log($"Hook AfterRewardTaken rewardType={reward?.GetType().Name ?? "<null>"}");
            Patches.RewardsScreenSetRewardsPatch.RemoveReward(reward, "AfterRewardTaken");
            PushRun("AfterRewardTaken");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterRewardTaken threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    // -------- Combat lifecycle --------

    public override Task BeforeCombatStart()
    {
        try
        {
            BridgeTrace.Log("Hook BeforeCombatStart");
            BridgeSnapshotWriter.SetScreen("Combat", "BeforeCombatStart");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook BeforeCombatStart threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        try
        {
            _currentPlayer = player;
            var gold = SafeGold(player);
            BridgeTrace.Log($"Hook AfterPlayerTurnStart gold={gold} maxEnergy={SafeMaxEnergy(player)}");
            PushCombat(player, "AfterPlayerTurnStart");
            PushRun("AfterPlayerTurnStart");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterPlayerTurnStart threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        try
        {
            BridgeTrace.Log($"Hook BeforeTurnEnd side={side}");
            if (_currentPlayer is not null) PushCombat(_currentPlayer, "BeforeTurnEnd");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook BeforeTurnEnd threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        try
        {
            BridgeTrace.Log("Hook AfterCardPlayed");
            if (_currentPlayer is not null) PushCombat(_currentPlayer, "AfterCardPlayed");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterCardPlayed threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            BridgeTrace.Log("Hook AfterCardPlayedLate");
            if (_currentPlayer is not null) PushCombat(_currentPlayer, "AfterCardPlayedLate");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterCardPlayedLate threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        try
        {
            var encounter = BridgeStateExtractor.SafeModelIdOrNull(room?.Encounter?.Id);
            BridgeTrace.Log($"Hook AfterCombatEnd encounter={encounter}");
            BridgeSnapshotWriter.SetScreen("PostCombat", "AfterCombatEnd");
            BridgeSnapshotWriter.SetCombat(null, "AfterCombatEnd");
            _currentCombatRoom = null;
            _currentPlayer = null;
            PushRun("AfterCombatEnd");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterCombatEnd threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        try
        {
            BridgeTrace.Log("Hook AfterCombatVictory");
        }
        catch (Exception ex) { BridgeTrace.Log($"Hook AfterCombatVictory threw: {ex.Message}"); }
        return Task.CompletedTask;
    }

    // -------- Push helpers --------

    private void PushCombat(Player player, string trigger)
    {
        try
        {
            var payload = BridgeStateExtractor.ExtractCombat(player, _currentCombatRoom);
            BridgeSnapshotWriter.SetCombat(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCombat({trigger}) threw: {ex.Message}");
        }
    }

    private static void PushRun(string trigger)
    {
        try
        {
            var payload = BridgeStateExtractor.ExtractRun();
            BridgeSnapshotWriter.SetRun(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushRun({trigger}) threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Public entry point so patches (e.g. shop purchase postfix) can refresh
    /// the run-level snapshot. Without this, gold/deck/potions/relics on the
    /// run side stay frozen at last room-entry value after a purchase.
    /// </summary>
    public static void PushCurrentRun(string trigger) => PushRun(trigger);

    /// <summary>
    /// Public entry point so patches/dispatcher code can refresh the combat
    /// snapshot (hand/piles/energy/enemies) after events that don't fire one
    /// of the built-in combat hooks. Examples: potion-generated cards being
    /// added to hand (Attack/Skill/Power potions), transient pile mutations
    /// from overlays, etc. Safe no-op outside combat (uses CurrentPlayer).
    /// </summary>
    public static void PushCurrentCombat(string trigger)
    {
        var player = CurrentPlayer;
        if (player is null) return;
        var singleton = _instance;
        if (singleton is null) return;
        singleton.PushCombat(player, trigger);
    }

    private void PushEvent(string trigger)
    {
        try
        {
            var ev = _currentEventRoom?.LocalMutableEvent ?? _currentEventRoom?.CanonicalEvent;
            if (ev is null)
            {
                BridgeTrace.Log($"PushEvent({trigger}) skipped: no event on EventRoom");
                return;
            }
            var payload = BridgeStateExtractor.ExtractEvent(ev);
            BridgeSnapshotWriter.SetEvent(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushEvent({trigger}) threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Called from the <c>NEventRoom._Ready</c> Harmony patch so that the
    /// event snapshot is captured as soon as the UI is ready (covers the case
    /// where a run is loaded directly into an event room and
    /// <see cref="AfterRoomEntered"/> doesn't fire again).
    /// </summary>
    public static void PushCurrentEvent(string trigger)
    {
        try
        {
            var instance = _instance;
            if (instance is null)
            {
                BridgeTrace.Log($"PushCurrentEvent({trigger}) skipped: no singleton instance");
                return;
            }
            instance.PushEvent(trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCurrentEvent({trigger}) threw: {ex.Message}");
        }
    }

    private void PushShop(string trigger)
    {
        try
        {
            if (_currentMerchantRoom is null)
            {
                BridgeTrace.Log($"PushShop({trigger}) skipped: no MerchantRoom");
                return;
            }
            var payload = BridgeStateExtractor.ExtractShop(_currentMerchantRoom);
            BridgeSnapshotWriter.SetShop(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushShop({trigger}) threw: {ex.Message}");
        }
    }

    public static void PushCurrentShop(string trigger)
    {
        try
        {
            var instance = _instance;
            if (instance is null)
            {
                BridgeTrace.Log($"PushCurrentShop({trigger}) skipped: no singleton instance");
                return;
            }
            instance.PushShop(trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCurrentShop({trigger}) threw: {ex.Message}");
        }
    }

    private void PushRestSite(string trigger)
    {
        try
        {
            if (_currentRestSiteRoom is null)
            {
                BridgeTrace.Log($"PushRestSite({trigger}) skipped: no RestSiteRoom");
                return;
            }
            var payload = BridgeStateExtractor.ExtractRestSite(_currentRestSiteRoom);
            BridgeSnapshotWriter.SetRestSite(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushRestSite({trigger}) threw: {ex.Message}");
        }
    }

    public static void PushCurrentRestSite(string trigger)
    {
        try
        {
            var instance = _instance;
            if (instance is null)
            {
                BridgeTrace.Log($"PushCurrentRestSite({trigger}) skipped: no singleton instance");
                return;
            }
            instance.PushRestSite(trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCurrentRestSite({trigger}) threw: {ex.Message}");
        }
    }

    private void PushTreasure(string trigger)
    {
        try
        {
            if (_currentTreasureRoom is null)
            {
                BridgeTrace.Log($"PushTreasure({trigger}) skipped: no TreasureRoom");
                return;
            }
            var payload = BridgeStateExtractor.ExtractTreasure(_currentTreasureRoom, _currentTreasureNode);
            BridgeSnapshotWriter.SetTreasure(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushTreasure({trigger}) threw: {ex.Message}");
        }
    }

    public static void PushCurrentTreasure(string trigger)
    {
        try
        {
            var instance = _instance;
            if (instance is null)
            {
                BridgeTrace.Log($"PushCurrentTreasure({trigger}) skipped: no singleton instance");
                return;
            }
            instance.PushTreasure(trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCurrentTreasure({trigger}) threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Called from <c>NTreasureRoom._Ready</c> postfix to capture the live
    /// Godot node so command dispatchers (OpenChest / SelectTreasureRelic /
    /// Proceed) and the extractor can reach UI state (chest opened flag,
    /// relic holders).
    /// </summary>
    public static void SetCurrentTreasureNode(NTreasureRoom node, string trigger)
    {
        try
        {
            var instance = _instance;
            if (instance is null)
            {
                BridgeTrace.Log($"SetCurrentTreasureNode({trigger}) skipped: no singleton instance");
                return;
            }
            instance._currentTreasureNode = node;
            BridgeTrace.Log($"SetCurrentTreasureNode({trigger}) captured node");
            instance.PushTreasure(trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"SetCurrentTreasureNode({trigger}) threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Pushes the current act map snapshot. Safe to call at any time; returns
    /// silently if there is no active run. Uses the live
    /// <c>NMapScreen.Instance</c> to compute the set of travelable next nodes,
    /// so it should be called after the map screen has laid out its points
    /// (i.e. after <c>SetMap</c>), as well as whenever the player advances.
    /// </summary>
    public static void PushCurrentMap(string trigger)
    {
        try
        {
            var payload = BridgeStateExtractor.ExtractMap();
            BridgeSnapshotWriter.SetMap(payload, trigger);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"PushCurrentMap({trigger}) threw: {ex.Message}");
        }
    }

    // -------- Instance tracking (for static helpers) --------

    private static BridgeSingleton? _instance;

    // -------- Helpers (never throw) --------

    private static int SafeGold(Player player)
    {
        try { return player?.Gold ?? -1; } catch { return -1; }
    }

    private static int SafeMaxEnergy(Player player)
    {
        try { return player?.MaxEnergy ?? -1; } catch { return -1; }
    }
}
