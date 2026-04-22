using System;
using System.Linq;
using System.Text.Json;
using Godot;
using HermesBridge.HermesBridgeCode.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// Pure-dispatch table for command payloads coming in via <see cref="BridgeCommandReader"/>.
/// Every dispatch method MUST run on the Godot main thread and MUST return
/// (status, message) WITHOUT throwing (catch internally).
///
/// status values: "ok" | "error" | "ignored"
/// </summary>
internal static class BridgeCommandDispatcher
{
    public static (string status, string message) Dispatch(string type, JsonElement command)
    {
        try
        {
            return type switch
            {
                "EndTurn" => DispatchEndTurn(command),
                "PlayCard" => DispatchPlayCard(command),
                "SelectReward" => DispatchSelectReward(command),
                "SkipReward" => DispatchSkipReward(command),
                "SkipAllRewards" => DispatchSkipAllRewards(command),
                "SelectCardOption" => DispatchSelectCardOption(command),
                "SelectCardAlternative" => DispatchSelectCardAlternative(command),
                "SelectEventOption" => DispatchSelectEventOption(command),
                "SelectRestOption" => DispatchSelectRestOption(command),
                "Proceed" => DispatchProceed(command),
                "SelectMapNode" => DispatchSelectMapNode(command),
                "ContinueRun" => DispatchContinueRun(command),
                "AbandonRun" => DispatchAbandonRun(command),
                "StartRun" => DispatchStartRun(command),
                "ReturnToMenu" => DispatchReturnToMenu(command),
                "GiveUp" => DispatchGiveUp(command),
                "Purchase" => DispatchPurchase(command),
                "PurchaseCardRemoval" => DispatchPurchaseCardRemoval(command),
                "LeaveShop" => DispatchLeaveShop(command),
                "UsePotion" => DispatchUsePotion(command),
                "DiscardPotion" => DispatchDiscardPotion(command),
                "SelectCardsInGrid" => DispatchSelectCardsInGrid(command),
                "ChooseACard" => DispatchChooseACard(command),
                "HandSelectCard" => DispatchHandSelectCard(command),
                "HandDeselectCard" => DispatchHandDeselectCard(command),
                "HandConfirmSelect" => DispatchHandConfirmSelect(command),
                "HandCancelSelect" => DispatchHandCancelSelect(command),
                "OpenChest" => DispatchOpenChest(command),
                "SelectTreasureRelic" => DispatchSelectTreasureRelic(command),
                "DumpScene" => DispatchDumpScene(command),
                "DumpMapPoints" => DispatchDumpMapPoints(command),
                _ => ("error", $"unknown command type: {type}"),
            };
        }
        catch (Exception ex)
        {
            return ("error", $"dispatcher threw for {type}: {ex.Message}");
        }
    }

    /// <summary>
    /// Ends the current player's turn. Maps to <c>PlayerCmd.EndTurn(player, canBackOut: false, actionDuringEnemyTurn: null)</c>.
    /// Optional stale-state guards:
    ///   expectedRevision (int): reject if the live bridge revision no longer matches.
    ///   expectedScreen (string): reject if the coarse screen label changed.
    ///   expectedCurrentSide (string): reject if combat ownership changed.
    ///   expectedRoundNumber (int): reject if the combat round changed.
    /// </summary>
    private static (string status, string message) DispatchEndTurn(JsonElement command)
    {
        var player = BridgeSingleton.CurrentPlayer;
        if (player is null)
        {
            return ("error", "no current player (not in combat?)");
        }

        if (command.TryGetProperty("expectedRevision", out var expectedRevisionEl) && expectedRevisionEl.ValueKind == JsonValueKind.Number)
        {
            var expectedRevision = expectedRevisionEl.GetInt32();
            var liveRevision = BridgeSnapshotWriter.CurrentRevision;
            if (expectedRevision != liveRevision)
            {
                return ("error", $"EndTurn guard mismatch: expected revision {expectedRevision}, live revision is {liveRevision}");
            }
        }

        if (command.TryGetProperty("expectedScreen", out var expectedScreenEl) && expectedScreenEl.ValueKind == JsonValueKind.String)
        {
            var expectedScreen = expectedScreenEl.GetString();
            var liveScreen = BridgeSnapshotWriter.CurrentScreen;
            if (!string.Equals(expectedScreen, liveScreen, StringComparison.Ordinal))
            {
                return ("error", $"EndTurn guard mismatch: expected screen '{expectedScreen}', live screen is '{liveScreen}'");
            }
        }

        var combatState = BridgeSingleton.CurrentCombatRoom?.CombatState ?? player.Creature?.CombatState;

        if (command.TryGetProperty("expectedCurrentSide", out var expectedSideEl) && expectedSideEl.ValueKind == JsonValueKind.String)
        {
            var expectedSide = expectedSideEl.GetString();
            var liveSide = combatState?.CurrentSide.ToString();
            if (!string.Equals(expectedSide, liveSide, StringComparison.Ordinal))
            {
                return ("error", $"EndTurn guard mismatch: expected currentSide {expectedSide}, live currentSide is {liveSide}");
            }
        }

        if (command.TryGetProperty("expectedRoundNumber", out var expectedRoundEl) && expectedRoundEl.ValueKind == JsonValueKind.Number)
        {
            var expectedRound = expectedRoundEl.GetInt32();
            var liveRound = combatState?.RoundNumber ?? -1;
            if (expectedRound != liveRound)
            {
                return ("error", $"EndTurn guard mismatch: expected roundNumber {expectedRound}, live roundNumber is {liveRound}");
            }
        }

        try
        {
            // PlayerCmd.EndTurn is void; it queues the end-of-turn animations / enemy turn internally.
            PlayerCmd.EndTurn(player, canBackOut: false, actionDuringEnemyTurn: null);
            BridgeTrace.Log("DispatchEndTurn invoked PlayerCmd.EndTurn");
            return ("ok", "EndTurn invoked");
        }
        catch (Exception ex)
        {
            return ("error", $"PlayerCmd.EndTurn threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Plays a card from the player's hand.
    /// Args:
    ///   handIndex (int, required): 0-based index into <c>PlayerCombatState.Hand.Cards</c>.
    ///   targetIndex (int, optional): 0-based index into <c>CombatState.Enemies</c>. Required for
    ///     cards with single-enemy targeting; ignored for self-target / no-target / multi-target cards.
    ///   targetSelf (bool, optional): if true, target the player's own creature instead of an enemy
    ///     (for self-targeting cards that take a creature parameter).
    ///
    /// Maps to <c>CardModel.TryManualPlay(Creature target)</c>. Returns "error" with the
    /// <c>UnplayableReason</c> if the card cannot be played.
    /// </summary>
    private static (string status, string message) DispatchPlayCard(JsonElement command)
    {
        var player = BridgeSingleton.CurrentPlayer;
        if (player is null) return ("error", "no current player (not in combat?)");

        if (!command.TryGetProperty("handIndex", out var handIdxEl) || handIdxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "PlayCard requires numeric 'handIndex'");
        }
        var handIndex = handIdxEl.GetInt32();

        // Resolve hand pile.
        var hand = player.PlayerCombatState?.Hand;
        if (hand is null) return ("error", "player has no Hand pile (not in combat?)");
        var cards = hand.Cards;
        if (cards is null || handIndex < 0 || handIndex >= cards.Count)
        {
            return ("error", $"handIndex {handIndex} out of range (hand size {cards?.Count ?? 0})");
        }
        var card = cards[handIndex];

        if (command.TryGetProperty("expectedCardId", out var expectedIdEl) && expectedIdEl.ValueKind == JsonValueKind.String)
        {
            var expectedId = expectedIdEl.GetString();
            var liveId = BridgeStateExtractor.SafeModelId(card.Id);
            if (!string.Equals(expectedId, liveId, StringComparison.Ordinal))
            {
                return ("error", $"handIndex {handIndex} mismatch: expected card id {expectedId}, live card is {liveId}");
            }
        }
        if (command.TryGetProperty("expectedTitle", out var expectedTitleEl) && expectedTitleEl.ValueKind == JsonValueKind.String)
        {
            var expectedTitle = expectedTitleEl.GetString();
            var liveTitle = card.Title;
            if (!string.Equals(expectedTitle, liveTitle, StringComparison.Ordinal))
            {
                return ("error", $"handIndex {handIndex} mismatch: expected title '{expectedTitle}', live card is '{liveTitle}'");
            }
        }

        // Resolve target (optional).
        Creature? target = null;
        if (command.TryGetProperty("targetSelf", out var selfEl) && selfEl.ValueKind == JsonValueKind.True)
        {
            target = player.Creature;
        }
        else if (command.TryGetProperty("targetIndex", out var tgtIdxEl) && tgtIdxEl.ValueKind == JsonValueKind.Number)
        {
            var tgtIndex = tgtIdxEl.GetInt32();
            var combatState = BridgeSingleton.CurrentCombatRoom?.CombatState ?? player.Creature?.CombatState;
            var enemies = combatState?.Enemies;
            if (enemies is null || tgtIndex < 0 || tgtIndex >= enemies.Count)
            {
                return ("error", $"targetIndex {tgtIndex} out of range (enemy count {enemies?.Count ?? 0})");
            }
            target = enemies[tgtIndex];
        }

        try
        {
            var ok = card.TryManualPlay(target);
            BridgeTrace.Log($"DispatchPlayCard handIndex={handIndex} card={card.Title ?? "<?>"} target={(target is null ? "<none>" : target.Name ?? "<?>")} ok={ok}");
            return ok
                ? ("ok", $"played {card.Title ?? card.Id?.Entry ?? "card"}")
                : ("error", "TryManualPlay returned false (card unplayable: bad target / not enough energy / etc)");
        }
        catch (Exception ex)
        {
            return ("error", $"TryManualPlay threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Accepts a reward from the post-combat rewards screen.
    /// Args:
    ///   rewardPosition (int, preferred): 0-based array index into state.json's
    ///     <c>rewards</c> array. Stable within a single screen session; the
    ///     unambiguous addressing mode.
    ///   rewardIndex (int, legacy): the <c>index</c> field (RewardsSetIndex) on a
    ///     reward. Prior to bug Rw2 this was documented as a stable per-reward
    ///     identifier, but in practice event-procured potion pairs share the same
    ///     RewardsSetIndex, so this mode cannot address duplicate entries. If both
    ///     rewardPosition and rewardIndex are provided, rewardPosition wins.
    ///
    /// For gold / potion / relic rewards this fully resolves the reward
    /// synchronously and surfaces <c>OnSelectWrapper</c>'s success/failure result:
    /// a <c>false</c> return (e.g. PotionReward with full inventory) now yields an
    /// <c>error</c> status instead of silent success (bug Rw1). For card rewards
    /// the call remains async — the sub-screen must open before
    /// <c>SelectCardOption</c> can follow up.
    /// </summary>
    private static (string status, string message) DispatchSelectReward(JsonElement command)
    {
        var rewards = RewardsScreenSetRewardsPatch.LastRewards;
        if (rewards is null || rewards.Count == 0) return ("error", "no active rewards screen");

        Reward? reward = null;
        int resolvedPos = -1;
        int resolvedSetIdx = -1;

        if (command.TryGetProperty("rewardPosition", out var posEl) && posEl.ValueKind == JsonValueKind.Number)
        {
            var pos = posEl.GetInt32();
            if (pos < 0 || pos >= rewards.Count)
            {
                return ("error", $"rewardPosition {pos} out of range (rewards count {rewards.Count})");
            }
            reward = rewards[pos];
            resolvedPos = pos;
            resolvedSetIdx = reward?.RewardsSetIndex ?? -1;
        }
        else if (command.TryGetProperty("rewardIndex", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
        {
            var idx = idxEl.GetInt32();
            // Warn if the legacy addressing is ambiguous.
            var matches = rewards.Where(r => r.RewardsSetIndex == idx).ToList();
            if (matches.Count == 0)
            {
                var available = string.Join(",", rewards.Select(r => r.RewardsSetIndex));
                return ("error", $"no reward with RewardsSetIndex={idx} (available: [{available}])");
            }
            if (matches.Count > 1)
            {
                var positions = string.Join(",", rewards
                    .Select((r, i) => (r, i))
                    .Where(t => t.r.RewardsSetIndex == idx)
                    .Select(t => t.i));
                BridgeTrace.Log($"SelectReward bug Rw2: ambiguous rewardIndex={idx} matches positions [{positions}]; picking first");
            }
            reward = matches[0];
            resolvedPos = rewards.Select((r, i) => (r, i)).First(t => ReferenceEquals(t.r, reward)).i;
            resolvedSetIdx = idx;
        }
        else
        {
            return ("error", "SelectReward requires numeric 'rewardPosition' or 'rewardIndex'");
        }

        if (reward is null)
        {
            return ("error", "SelectReward resolved to null reward");
        }

        try
        {
            // One-shot diagnostic for SpecialCardReward (bug U investigation).
            try { BugUDiagnostic.OnSelectReward(reward); } catch (Exception exDiag) { BridgeTrace.Log($"BugU[diag] OnSelectReward threw: {exDiag.Message}"); }

            var rewardTypeName = reward.GetType().Name;
            var task = reward.OnSelectWrapper();

            if (reward is CardReward)
            {
                // CardReward opens a sub-screen; awaiting would block the UI pump.
                // Attach a logging continuation and report ok; the sub-screen's
                // own state hooks surface the result, and SelectCardOption is the
                // meaningful next step.
                try
                {
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            var inner = t.Exception?.GetBaseException();
                            BridgeTrace.Log($"SelectReward.OnSelectWrapper FAULTED ({rewardTypeName}): {inner?.GetType().Name}: {inner?.Message}");
                        }
                        else if (t.IsCanceled) { BridgeTrace.Log($"SelectReward.OnSelectWrapper CANCELED ({rewardTypeName})"); }
                        else { BridgeTrace.Log($"SelectReward.OnSelectWrapper completed ({rewardTypeName}) result={t.Result}"); }
                    }, System.Threading.Tasks.TaskScheduler.Current);
                }
                catch (Exception exCont) { BridgeTrace.Log($"SelectReward continuation attach threw: {exCont.Message}"); }

                RewardsScreenSetRewardsPatch.RefreshVisibleRewards("SelectRewardRefreshVisible");
                BridgeTrace.Log($"DispatchSelectReward pos={resolvedPos} setIdx={resolvedSetIdx} type={rewardTypeName} (async sub-screen)");
                return ("ok", $"opened {rewardTypeName} sub-screen (position={resolvedPos}, RewardsSetIndex={resolvedSetIdx})");
            }

            // Gold / Potion / Relic / other synchronous rewards: block on the
            // result so OnSelectWrapper's bool return (false = refused, e.g.
            // PotionReward with full inventory) becomes a visible error
            // instead of a silent ok (bug Rw1).
            bool claimed;
            try
            {
                claimed = task.GetAwaiter().GetResult();
            }
            catch (Exception exAwait)
            {
                var inner = exAwait is AggregateException ae && ae.InnerException is not null ? ae.InnerException : exAwait;
                RewardsScreenSetRewardsPatch.RefreshVisibleRewards("SelectRewardRefreshVisible");
                BridgeTrace.Log($"DispatchSelectReward pos={resolvedPos} setIdx={resolvedSetIdx} type={rewardTypeName} THREW: {inner.GetType().Name}: {inner.Message}");
                return ("error", $"Reward.OnSelect threw: {inner.Message}");
            }

            RewardsScreenSetRewardsPatch.RefreshVisibleRewards("SelectRewardRefreshVisible");
            if (!claimed)
            {
                BridgeTrace.Log($"DispatchSelectReward pos={resolvedPos} setIdx={resolvedSetIdx} type={rewardTypeName} REFUSED (OnSelectWrapper returned false)");
                var hint = reward switch
                {
                    PotionReward => "; likely full potion inventory — DiscardPotion first or Skip",
                    _ => "",
                };
                return ("error", $"reward refused ({rewardTypeName} position={resolvedPos} RewardsSetIndex={resolvedSetIdx}){hint}");
            }

            BridgeTrace.Log($"DispatchSelectReward pos={resolvedPos} setIdx={resolvedSetIdx} type={rewardTypeName} claimed");
            return ("ok", $"selected {rewardTypeName} (position={resolvedPos}, RewardsSetIndex={resolvedSetIdx})");
        }
        catch (Exception ex)
        {
            return ("error", $"Reward.OnSelect threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Skips a single reward (e.g. declining a card pick). Uses <c>Reward.OnSkipped()</c>.
    /// Args: rewardIndex (int, required) - the <c>index</c> field in state.json (RewardsSetIndex).
    /// For <c>CardReward</c>, <c>OnSkipped</c> alone does NOT remove the reward from the
    /// pending set (vanilla relies on the NRewardsScreen skip button dispatch). We therefore
    /// close the card-selection overlay and explicitly remove the reward from the linked set
    /// and from our tracked <c>LastRewards</c> so <c>Proceed</c> is unblocked.
    /// </summary>
    private static (string status, string message) DispatchSkipReward(JsonElement command)
    {
        var rewards = RewardsScreenSetRewardsPatch.LastRewards;
        if (rewards is null || rewards.Count == 0) return ("error", "no active rewards screen");

        // Dual addressing (bug Rw2): prefer 'rewardPosition' (array index — always
        // unique) over legacy 'rewardIndex' (RewardsSetIndex — not guaranteed
        // unique, e.g. event-procured potions often share indices).
        Reward? reward = null;
        int resolvedPos = -1;
        int resolvedSetIdx = -1;

        if (command.TryGetProperty("rewardPosition", out var posEl) && posEl.ValueKind == JsonValueKind.Number)
        {
            var pos = posEl.GetInt32();
            if (pos < 0 || pos >= rewards.Count)
            {
                return ("error", $"rewardPosition {pos} out of range (count {rewards.Count})");
            }
            reward = rewards[pos];
            resolvedPos = pos;
            resolvedSetIdx = reward.RewardsSetIndex;
        }
        else if (command.TryGetProperty("rewardIndex", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
        {
            var idx = idxEl.GetInt32();
            var matches = rewards.Where(r => r.RewardsSetIndex == idx).ToList();
            if (matches.Count == 0)
            {
                var available = string.Join(",", rewards.Select(r => r.RewardsSetIndex));
                return ("error", $"no reward with RewardsSetIndex={idx} (available: [{available}])");
            }
            if (matches.Count > 1)
            {
                BridgeTrace.Log($"DispatchSkipReward: ambiguous rewardIndex={idx} matches {matches.Count}; using first. Prefer 'rewardPosition'.");
            }
            reward = matches[0];
            resolvedPos = rewards.Select((r, i) => (r, i)).First(t => ReferenceEquals(t.r, reward)).i;
            resolvedSetIdx = idx;
        }
        else
        {
            return ("error", "SkipReward requires numeric 'rewardPosition' or 'rewardIndex'");
        }

        try
        {
            reward.OnSkipped();
            CleanupSkippedReward(reward, "SkipRewardRemove");

            BridgeTrace.Log($"DispatchSkipReward pos={resolvedPos} setIdx={resolvedSetIdx} type={reward.GetType().Name}");
            return ("ok", $"skipped {reward.GetType().Name} (position={resolvedPos}, RewardsSetIndex={resolvedSetIdx})");
        }
        catch (Exception ex)
        {
            return ("error", $"Reward.OnSkipped threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Post-OnSkipped cleanup for rewards whose OnSkipped implementation does NOT
    /// remove them from the parent set / visible list (true for PotionReward,
    /// RelicReward, GoldReward, and CardReward in vanilla STS2). We mirror the
    /// NRewardsScreen skip-button flow:
    ///   1. If it's a CardReward, close the card-selection overlay.
    ///   2. Remove from ParentRewardSet.
    ///   3. Remove from our tracked LastRewards + refresh snapshot.
    /// Each step is guarded so a single failure still allows the others to run.
    /// </summary>
    private static void CleanupSkippedReward(Reward reward, string trigger)
    {
        if (reward is CardReward)
        {
            try
            {
                var overlay = CardRewardRefreshOptionsPatch.LastScreen;
                if (overlay != null)
                {
                    NOverlayStack.Instance?.Remove(overlay);
                }
            }
            catch (Exception exOverlay)
            {
                BridgeTrace.Log($"{trigger} overlay-close threw: {exOverlay.Message}");
            }
        }

        try
        {
            reward.ParentRewardSet?.RemoveReward(reward);
        }
        catch (Exception exParent)
        {
            BridgeTrace.Log($"{trigger} ParentRewardSet.RemoveReward threw: {exParent.Message}");
        }

        try
        {
            RewardsScreenSetRewardsPatch.RemoveReward(reward, trigger);
        }
        catch (Exception exLast)
        {
            BridgeTrace.Log($"{trigger} LastRewards refresh threw: {exLast.Message}");
        }
    }

    /// <summary>
    /// Convenience: skip every reward currently on offer and proceed. Useful when
    /// Hermes decides nothing on the screen is worth taking.
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchSkipAllRewards(JsonElement command)
    {
        var rewards = RewardsScreenSetRewardsPatch.LastRewards;
        if (rewards is null || rewards.Count == 0) return ("error", "no active rewards screen");

        // Snapshot the list since CleanupSkippedReward mutates LastRewards via
        // RewardsScreenSetRewardsPatch.RemoveReward on each iteration.
        var snapshot = rewards.ToList();
        int skipped = 0;
        foreach (var r in snapshot)
        {
            try
            {
                r.OnSkipped();
                CleanupSkippedReward(r, "SkipAllRewardsRemove");
                skipped++;
            }
            catch (Exception ex) { BridgeTrace.Log($"SkipAllRewards: {r.GetType().Name} threw: {ex.Message}"); }
        }
        BridgeTrace.Log($"DispatchSkipAllRewards skipped={skipped}/{snapshot.Count}");

        // Bug Rw3: after OnSkipped + ParentRewardSet removal on every entry,
        // LastRewards is empty but the NRewardsScreen panel itself can remain
        // visible (its close path lives on the Proceed button, not on
        // reward-set depletion). Mirror DispatchProceed's rewards branch so
        // callers don't need a follow-up Proceed call.
        var remaining = RewardsScreenSetRewardsPatch.LastRewards;
        var panel = RewardsScreenSetRewardsPatch.LastScreen;
        if ((remaining is null || remaining.Count == 0)
            && panel is not null
            && Godot.GodotObject.IsInstanceValid(panel)
            && panel.Visible)
        {
            try
            {
                var m = panel.GetType().GetMethod("OnProceedButtonPressed",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m is null)
                {
                    BridgeTrace.Log("DispatchSkipAllRewards: could not resolve NRewardsScreen.OnProceedButtonPressed; panel left visible");
                }
                else
                {
                    m.Invoke(panel, new object?[] { null });
                    BridgeTrace.Log("DispatchSkipAllRewards: closed panel via NRewardsScreen.OnProceedButtonPressed");
                }
            }
            catch (Exception exClose)
            {
                BridgeTrace.Log($"DispatchSkipAllRewards: OnProceedButtonPressed threw: {exClose.Message}");
            }
        }

        return ("ok", $"skipped {skipped}/{snapshot.Count} rewards");
    }

    /// <summary>
    /// Picks a card from the open card-reward sub-screen
    /// (<c>NCardRewardSelectionScreen</c>).
    /// Args:
    ///   cardIndex (int, required): 0-based positional index into
    ///   <c>cardRewardOptions.cards</c> in state.json (matches the <c>index</c> field
    ///   on each entry — it IS positional here, unlike the parent rewards screen).
    ///
    /// Maps to <c>NCardRewardSelectionScreen.SelectCard(NCardHolder)</c>. The holder
    /// is resolved by walking the <c>_cardRow</c> children and matching by
    /// <c>holder.CardModel</c> reference equality with <c>_options[idx].Card</c>.
    /// </summary>
    private static (string status, string message) DispatchSelectCardOption(JsonElement command)
    {
        var screen = CardRewardRefreshOptionsPatch.LastScreen;
        var options = CardRewardRefreshOptionsPatch.LastOptions;
        if (options is null || options.Count == 0)
        {
            return ("error", "no active card-reward sub-screen");
        }

        // The cached screen reference can become invalid if the overlay was
        // closed+reopened (animation cycle) or if a duplicate overlay was spawned
        // by a manual click. Fall back to scanning the scene tree for the
        // currently-visible NCardRewardSelectionScreen instance.
        if (screen is null || !Godot.GodotObject.IsInstanceValid(screen))
        {
            BridgeTrace.Log("DispatchSelectCardOption: cached LastScreen invalid, scanning scene tree");
            screen = FindLiveCardRewardScreen();
            if (screen is null)
            {
                return ("error", "no live NCardRewardSelectionScreen found in scene tree");
            }
            CardRewardRefreshOptionsPatch.LastScreen = screen;
        }

        if (!command.TryGetProperty("cardIndex", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectCardOption requires numeric 'cardIndex'");
        }
        var idx = idxEl.GetInt32();
        if (idx < 0 || idx >= options.Count)
        {
            return ("error", $"cardIndex {idx} out of range (count {options.Count})");
        }

        var targetCard = options[idx].Card;
        if (targetCard is null) return ("error", $"options[{idx}].Card is null");

        // Resolve NCardHolder by walking the screen's children. The holders live
        // under _cardRow; we pull that field via reflection because it's private.
        var cardRowField = screen.GetType().GetField("_cardRow",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (cardRowField?.GetValue(screen) is not Node cardRow || !Godot.GodotObject.IsInstanceValid(cardRow))
        {
            // Cached screen had a disposed _cardRow. Try one more time with a
            // fresh tree scan in case AfterOverlayShown's __instance lagged.
            var alt = FindLiveCardRewardScreen();
            if (alt is null || ReferenceEquals(alt, screen))
            {
                return ("error", "could not access screen._cardRow (Control disposed)");
            }
            CardRewardRefreshOptionsPatch.LastScreen = alt;
            screen = alt;
            cardRowField = screen.GetType().GetField("_cardRow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cardRowField?.GetValue(screen) is not Node altRow || !Godot.GodotObject.IsInstanceValid(altRow))
            {
                return ("error", "could not access screen._cardRow even after rescan");
            }
            cardRow = altRow;
        }

        NCardHolder? matched = null;
        FindHolderRecursive(cardRow, targetCard, ref matched);
        if (matched is null)
        {
            return ("error", $"no NCardHolder found whose CardModel matches options[{idx}]");
        }

        try
        {
            // SelectCard is private; invoke via reflection.
            var selectMethod = screen.GetType().GetMethod("SelectCard",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (selectMethod is null)
            {
                return ("error", "could not resolve NCardRewardSelectionScreen.SelectCard via reflection");
            }
            selectMethod.Invoke(screen, new object[] { matched });
            BridgeTrace.Log($"DispatchSelectCardOption idx={idx} card={targetCard.Title ?? "<?>"}");
            return ("ok", $"selected card {targetCard.Title ?? targetCard.Id?.Entry ?? "?"} at idx {idx}");
        }
        catch (Exception ex)
        {
            return ("error", $"SelectCard threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks the active scene tree to find a live, visible NCardRewardSelectionScreen
    /// instance. Used when the cached LastScreen reference has been disposed
    /// (overlay close/reopen cycle, or stale ghost from a duplicate overlay).
    /// </summary>
    private static NCardRewardSelectionScreen? FindLiveCardRewardScreen()
    {
        try
        {
            var ngame = NGame.Instance;
            Node? searchRoot = ngame;
            try
            {
                var tree = ngame?.GetTree();
                if (tree is not null) searchRoot = tree.Root;
            }
            catch { }
            if (searchRoot is null) return null;

            NCardRewardSelectionScreen? best = null;
            NCardRewardSelectionScreen? anyValid = null;
            FindCardRewardScreenRecursive(searchRoot, ref best, ref anyValid);
            return best ?? anyValid;
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"FindLiveCardRewardScreen threw: {ex.Message}");
            return null;
        }
    }

    private static void FindCardRewardScreenRecursive(Node? node, ref NCardRewardSelectionScreen? bestVisible, ref NCardRewardSelectionScreen? anyValid)
    {
        if (node is null) return;
        if (node is NCardRewardSelectionScreen s && Godot.GodotObject.IsInstanceValid(s))
        {
            if (anyValid is null) anyValid = s;
            // Prefer a visible Control; tree may contain stale invisible duplicates.
            try
            {
                if (s.Visible && bestVisible is null) bestVisible = s;
            }
            catch { }
        }
        try
        {
            foreach (var child in node.GetChildren())
            {
                if (bestVisible is not null) return;
                FindCardRewardScreenRecursive(child, ref bestVisible, ref anyValid);
            }
        }
        catch { }
    }

    private static void FindHolderRecursive(Node node, CardModel target, ref NCardHolder? hit)
    {
        if (hit is not null) return;
        if (node is NCardHolder h && ReferenceEquals(h.CardModel, target))
        {
            hit = h;
            return;
        }
        foreach (var child in node.GetChildren())
        {
            if (hit is not null) return;
            FindHolderRecursive(child, target, ref hit);
        }
    }

    /// <summary>
    /// Invokes a non-card alternative on the card-reward sub-screen, e.g. "Skip" or
    /// "Reroll". Maps to invoking <c>CardRewardAlternative.OnSelect</c> (which is a
    /// <c>Func&lt;Task&gt;</c>) directly — the screen's UI is just a button bound to that.
    /// Args:
    ///   alternativeIndex (int, optional): 0-based positional index into
    ///     <c>cardRewardOptions.alternatives</c>.
    ///   optionId (string, optional): match by <c>OptionId</c> (e.g. "Skip"). One of
    ///     <c>alternativeIndex</c> or <c>optionId</c> must be provided; <c>optionId</c>
    ///     wins if both are present.
    /// </summary>
    private static (string status, string message) DispatchSelectCardAlternative(JsonElement command)
    {
        var extras = CardRewardRefreshOptionsPatch.LastExtraOptions;
        if (extras is null || extras.Count == 0)
        {
            return ("error", "no card-reward alternatives currently available");
        }

        MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative? chosen = null;

        if (command.TryGetProperty("optionId", out var idEl) && idEl.ValueKind == JsonValueKind.String)
        {
            var wantId = idEl.GetString();
            chosen = extras.FirstOrDefault(a => a.OptionId == wantId);
            if (chosen is null)
            {
                var avail = string.Join(",", extras.Select(a => a.OptionId));
                return ("error", $"no alternative with OptionId='{wantId}' (available: [{avail}])");
            }
        }
        else if (command.TryGetProperty("alternativeIndex", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
        {
            var idx = idxEl.GetInt32();
            if (idx < 0 || idx >= extras.Count)
            {
                return ("error", $"alternativeIndex {idx} out of range (count {extras.Count})");
            }
            chosen = extras[idx];
        }
        else
        {
            return ("error", "SelectCardAlternative requires 'optionId' (string) or 'alternativeIndex' (int)");
        }

        try
        {
            // OnSelect is a Func<Task>; fire-and-forget on main thread.
            var task = chosen.OnSelect?.Invoke();
            BridgeTrace.Log($"DispatchSelectCardAlternative optionId={chosen.OptionId}");
            return ("ok", $"invoked alternative {chosen.OptionId}");
        }
        catch (Exception ex)
        {
            return ("error", $"CardRewardAlternative.OnSelect threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Chooses an option in the current event. Maps to <c>EventOption.Chosen()</c>.
    /// Args:
    ///   optionIndex (int, required): 0-based index into <c>ev.CurrentOptions</c>,
    ///   matching the <c>options</c> array in state.json's <c>event</c> payload.
    /// </summary>
    private static (string status, string message) DispatchSelectEventOption(JsonElement command)
    {
        var room = BridgeSingleton.CurrentEventRoom;
        if (room is null) return ("error", "no current event room");

        var ev = room.LocalMutableEvent ?? room.CanonicalEvent;
        if (ev is null) return ("error", "event room has no event model");

        if (!command.TryGetProperty("optionIndex", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectEventOption requires numeric 'optionIndex'");
        }
        var idx = idxEl.GetInt32();

        var options = ev.CurrentOptions;
        if (options is null) return ("error", "event has no CurrentOptions");

        int count = 0;
        MegaCrit.Sts2.Core.Events.EventOption? chosen = null;
        foreach (var o in options)
        {
            if (count == idx && o is MegaCrit.Sts2.Core.Events.EventOption eo) { chosen = eo; break; }
            count++;
        }
        // Recount to report total (CurrentOptions is IEnumerable, not IList).
        int total = 0; foreach (var _ in options) total++;

        if (chosen is null)
        {
            return ("error", $"optionIndex {idx} out of range (count {total})");
        }

        try
        {
            // Fire-and-forget (main thread); game fires its own state hooks.
            _ = chosen.Chosen();
            BridgeTrace.Log($"DispatchSelectEventOption idx={idx} title={BridgeStateExtractor.SafeLocString(chosen.Title) ?? "<?>"}");
            // Event options can mutate run state (card add/remove/upgrade, duplicate,
            // relic grant, gold/hp delta, potion grant). Schedule a deferred refresh
            // so deck/relics/potions reflect the change without waiting for the next
            // PlayCard. Fixes bugs J and Q in this session's tracker.
            ScheduleDeferredStateRefresh(2, "SelectEventOptionResolve", includeCombat: false, includeRun: true);
            return ("ok", $"chose option {idx}");
        }
        catch (Exception ex)
        {
            return ("error", $"EventOption.Chosen threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Chooses an option at a rest site (heal / smith / mend / cook / etc.).
    /// Maps to <c>RestSiteOption.OnSelect()</c>.
    /// Args:
    ///   optionIndex (int, required): 0-based index into <c>room.Options</c>, matching
    ///   the <c>options</c> array in state.json's <c>restSite</c> payload.
    /// </summary>
    private static (string status, string message) DispatchSelectRestOption(JsonElement command)
    {
        var room = BridgeSingleton.CurrentRestSiteRoom;
        if (room is null) return ("error", "no current rest site room");

        if (!command.TryGetProperty("optionIndex", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectRestOption requires numeric 'optionIndex'");
        }
        var idx = idxEl.GetInt32();

        if (room.Options is null) return ("error", "rest site has no Options");

        int count = 0;
        RestSiteOption? chosen = null;
        foreach (var o in room.Options)
        {
            if (count == idx && o is RestSiteOption rso) { chosen = rso; break; }
            count++;
        }
        int total = 0; foreach (var _ in room.Options) total++;

        if (chosen is null)
        {
            return ("error", $"optionIndex {idx} out of range (count {total})");
        }

        if (!chosen.IsEnabled)
        {
            return ("error", $"rest option {idx} ({chosen.GetType().Name}) is disabled");
        }

        // Route through NRestSiteButton.SelectOption so the full selection
        // chain fires the same way a real player click does:
        //   SelectOption -> DisableOptions -> await ChooseLocalOption
        //     -> AfterSelectingOption -> AfterSelectingOptionAsync
        //        -> HideChoices + UpdateRestSiteOptions (consumes the option)
        //        + ShowProceedButton  -> EnableOptions.
        // Calling ChooseLocalOption directly (previous attempt) mutated
        // game state but left the buttons visible because the UI refresh
        // chain lives on the button, not on the synchronizer.
        // In single-player OnAfterPlayerSelectedRestSiteOption is a no-op
        // (early return when Players.Count <= 1).
        try
        {
            var nroom = NRestSiteRoom.Instance;
            if (nroom is null)
            {
                return ("error", "NRestSiteRoom.Instance is null");
            }
            var button = nroom.GetButtonForOption(chosen);
            if (button is null)
            {
                return ("error", $"no NRestSiteButton found for option idx={idx}");
            }
            var selectMi = typeof(NRestSiteButton).GetMethod("SelectOption",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (selectMi is null)
            {
                return ("error", "could not resolve NRestSiteButton.SelectOption via reflection");
            }
            // Fire-and-forget like OnRelease does.
            _ = selectMi.Invoke(button, new object[] { chosen });
            BridgeTrace.Log($"DispatchSelectRestOption idx={idx} type={chosen.GetType().Name} via NRestSiteButton.SelectOption");
            // Rest options can mutate run state (Smith upgrade, heal, dig,
            // remove, etc). Schedule a deferred refresh so run JSON reflects
            // the change without waiting for the next state-pushing action.
            ScheduleDeferredStateRefresh(2, "SelectRestOptionResolve", includeCombat: false, includeRun: true);
            return ("ok", $"selected {chosen.GetType().Name} at index {idx}");
        }
        catch (Exception ex)
        {
            return ("error", $"NRestSiteButton.SelectOption threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Generic "click the Proceed button" command. Routes to the appropriate
    /// underlying handler based on which screen is currently active:
    ///   - Rewards screen   → <c>NRewardsScreen.OnProceedButtonPressed(null)</c>
    ///   - RestSite room    → <c>NRestSiteRoom.OnProceedButtonReleased(null)</c>
    ///   - Merchant room    → presses the merchant's ProceedButton
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchProceed(JsonElement command)
    {
        // 1) Rewards screen path - preferred when LastScreen is set + rewards screen is active.
        var rewardsScreen = RewardsScreenSetRewardsPatch.LastScreen;
        if (rewardsScreen is not null && Godot.GodotObject.IsInstanceValid(rewardsScreen) && rewardsScreen.Visible)
        {
            // Bug H guard: refuse to Proceed from a Rewards screen while any
            // uncollected reward is still visible. Historically the game's
            // OnProceedButtonPressed would silently abandon remaining rewards
            // (gold/potion/relic that the consumer never claimed), which caused
            // lost loot during scripted play. Force consumers to explicitly
            // SelectReward or SkipReward every entry before leaving.
            RewardsScreenSetRewardsPatch.RefreshVisibleRewards("ProceedRewardsGuard");
            var pending = RewardsScreenSetRewardsPatch.LastRewards;
            if (pending is not null && pending.Count > 0)
            {
                var summary = string.Join(",", pending.Select(r => $"{r.GetType().Name}#{r.RewardsSetIndex}"));
                return ("error", $"rewards pending; collect or skip first: [{summary}]");
            }

            try
            {
                var m = rewardsScreen.GetType().GetMethod("OnProceedButtonPressed",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m is null) return ("error", "could not resolve NRewardsScreen.OnProceedButtonPressed");
                m.Invoke(rewardsScreen, new object?[] { null });
                BridgeTrace.Log("DispatchProceed via NRewardsScreen.OnProceedButtonPressed");
                return ("ok", "proceeded from rewards screen");
            }
            catch (Exception ex)
            {
                return ("error", $"NRewardsScreen.OnProceedButtonPressed threw: {ex.Message}");
            }
        }

        // 2) Rest site path. CurrentRestSiteRoom is the RestSiteRoom *entity*
        // (game state); OnProceedButtonReleased lives on the NRestSiteRoom
        // Godot node. Resolve via NRestSiteRoom.Instance.
        var rest = BridgeSingleton.CurrentRestSiteRoom;
        if (rest is not null)
        {
            try
            {
                var nrest = NRestSiteRoom.Instance;
                if (nrest is null) return ("error", "NRestSiteRoom.Instance is null");
                var m = typeof(NRestSiteRoom).GetMethod("OnProceedButtonReleased",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m is null) return ("error", "could not resolve NRestSiteRoom.OnProceedButtonReleased");
                m.Invoke(nrest, new object?[] { null });
                BridgeTrace.Log("DispatchProceed via NRestSiteRoom.OnProceedButtonReleased");
                return ("ok", "proceeded from rest site");
            }
            catch (Exception ex)
            {
                return ("error", $"NRestSiteRoom.OnProceedButtonReleased threw: {ex.Message}");
            }
        }

        // 3) Merchant path - use MerchantRoom.Exit(IRunState) (same pattern as EventRoom).
        var merchant = BridgeSingleton.CurrentMerchantRoom;
        if (merchant is not null)
        {
            var (s, m) = ExitMerchantRoom(merchant);
            if (s == "ok") BridgeTrace.Log("DispatchProceed via MerchantRoom.Exit");
            return (s, m);
        }

        // 3b) Treasure room path. Mirrors the rest-site pattern - the proceed
        // button lives on the NTreasureRoom Godot node; OnProceedButtonReleased
        // is non-public so resolve via reflection. Captured node is set by
        // TreasureRoomReadyPatch.
        var treasure = BridgeSingleton.CurrentTreasureRoom;
        if (treasure is not null)
        {
            try
            {
                var ntreasure = BridgeSingleton.CurrentTreasureNode;
                if (ntreasure is null || !Godot.GodotObject.IsInstanceValid(ntreasure))
                {
                    return ("error", "no live NTreasureRoom captured (TreasureRoomReadyPatch not fired?)");
                }
                var m = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom).GetMethod(
                    "OnProceedButtonReleased",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m is null) return ("error", "could not resolve NTreasureRoom.OnProceedButtonReleased");
                m.Invoke(ntreasure, new object?[] { null });
                BridgeTrace.Log("DispatchProceed via NTreasureRoom.OnProceedButtonReleased");
                return ("ok", "proceeded from treasure room");
            }
            catch (Exception ex)
            {
                return ("error", $"NTreasureRoom.OnProceedButtonReleased threw: {ex.Message}");
            }
        }

        // 4) Event room path - press "leave" on a finished event.
        var eventRoom = BridgeSingleton.CurrentEventRoom;
        if (eventRoom is not null)
        {
            // Bug E2 diagnostic: dump the NEventRoom + eventRoom state once to
            // help us identify the "pending continuation button" field. We want
            // to know: is there a visible button/option waiting for click at the
            // moment DispatchProceed is invoked?
            try { BugE2Diagnostic.Emit(eventRoom); } catch (Exception ex) { BridgeTrace.Log($"BugE2[diag] threw: {ex.Message}"); }

            // 4a) PRIMARY: NEventRoom.Proceed() — the canonical UI "leave event"
            // path. Static public Task method on the Godot node class. This
            // handles any post-event animations/popups (curse reveal, relic
            // reveal, etc.) before invoking map-open. We only use this when the
            // NEventRoom.Instance is actually alive.
            try
            {
                var nEvtInstanceProp = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom)
                    .GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var nEvt = nEvtInstanceProp?.GetValue(null);
                if (nEvt is not null && nEvt is Godot.GodotObject godotObj && Godot.GodotObject.IsInstanceValid(godotObj))
                {
                    var proceedMethod = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom)
                        .GetMethod("Proceed", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (proceedMethod is not null)
                    {
                        var task = proceedMethod.Invoke(null, null) as System.Threading.Tasks.Task;
                        if (task is not null)
                        {
                            task.ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    var inner = t.Exception?.GetBaseException();
                                    BridgeTrace.Log($"NEventRoom.Proceed FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                                }
                                else { BridgeTrace.Log("NEventRoom.Proceed completed"); }
                            });
                            BridgeTrace.Log("DispatchProceed via NEventRoom.Proceed");
                            return ("ok", "proceeded from event (NEventRoom.Proceed)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"NEventRoom.Proceed attempt failed, falling back: {ex.Message}");
            }

            // 4b) FALLBACK: EventRoom.Exit(IRunState) — logical-model level exit.
            try
            {
                var rm = RunManager.Instance;
                if (rm is null) return ("error", "RunManager.Instance null; cannot Exit event");
                var stateProp = rm.GetType().GetProperty("State",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var state = stateProp?.GetValue(rm) as MegaCrit.Sts2.Core.Runs.IRunState;
                if (state is null) return ("error", "RunManager.State null/not-IRunState; cannot Exit event");
                var task = eventRoom.Exit(state);
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var inner = t.Exception?.GetBaseException();
                        BridgeTrace.Log($"EventRoom.Exit FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                    }
                    else { BridgeTrace.Log("EventRoom.Exit completed"); }
                });
                BridgeTrace.Log("DispatchProceed via EventRoom.Exit (fallback)");
                return ("ok", "proceeded from event (EventRoom.Exit fallback)");
            }
            catch (Exception ex)
            {
                return ("error", $"EventRoom.Exit threw: {ex.Message}");
            }
        }

        return ("error", "no active proceedable screen (rewards/rest/merchant/event)");
    }

    /// <summary>
    /// Travels to a map node. Maps to <c>NMapScreen.TravelToMapCoord(MapCoord)</c>
    /// (which is public). The target must be currently travelable — i.e. it
    /// should appear in <c>state.json</c>'s <c>map.available</c> array. The game
    /// enforces its own travelability rules and will silently no-op (or reject)
    /// invalid targets, but we also pre-validate against <c>_mapPointDictionary</c>
    /// so we can return a useful error.
    ///
    /// Args:
    ///   col (int, required)
    ///   row (int, required)
    /// </summary>
    private static (string status, string message) DispatchSelectMapNode(JsonElement command)
    {
        if (!command.TryGetProperty("col", out var colEl) || colEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectMapNode requires numeric 'col'");
        }
        if (!command.TryGetProperty("row", out var rowEl) || rowEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectMapNode requires numeric 'row'");
        }
        int col = colEl.GetInt32();
        int row = rowEl.GetInt32();

        var screen = NMapScreen.Instance;
        if (screen is null || !Godot.GodotObject.IsInstanceValid(screen))
        {
            return ("error", "no active NMapScreen (map not currently visible)");
        }

        // Validate against the live travelability dictionary. MapCoord is a
        // struct with a well-defined Equals, so dict lookup works.
        try
        {
            var dictField = typeof(NMapScreen).GetField("_mapPointDictionary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dictField?.GetValue(screen) is System.Collections.IDictionary dict)
            {
                var wanted = new MapCoord { col = col, row = row };
                bool found = false;
                bool travelable = false;
                var travelProp = typeof(NMapPoint).GetProperty("IsTravelable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (System.Collections.DictionaryEntry e in dict)
                {
                    try
                    {
                        if (e.Key is MapCoord k && k.col == col && k.row == row)
                        {
                            found = true;
                            if (e.Value is NMapPoint np && travelProp?.GetValue(np) is bool t)
                                travelable = t;
                            break;
                        }
                    }
                    catch { }
                }
                if (!found) return ("error", $"no map point at ({col},{row})");
                if (!travelable) return ("error", $"map point ({col},{row}) is not currently travelable");
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: fall through to TravelToMapCoord and let the game decide.
            BridgeTrace.Log($"SelectMapNode validation threw: {ex.Message}");
        }

        try
        {
            var coord = new MapCoord { col = col, row = row };
            // TravelToMapCoord returns Task; fire-and-forget on main thread.
            _ = screen.TravelToMapCoord(coord);
            BridgeTrace.Log($"DispatchSelectMapNode col={col} row={row}");
            return ("ok", $"traveling to ({col},{row})");
        }
        catch (Exception ex)
        {
            return ("error", $"TravelToMapCoord threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Clicks "Continue" on the main menu to resume the saved run. Maps to
    /// <c>NMainMenu.OnContinueButtonPressedAsync()</c>. Requires the main
    /// menu to be visible (state.screen.name == "MainMenu").
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchContinueRun(JsonElement command)
    {
        var menu = NGame.Instance?.MainMenu;
        if (menu is null || !Godot.GodotObject.IsInstanceValid(menu))
        {
            return ("error", "main menu not available (NGame.Instance.MainMenu null)");
        }
        try
        {
            var mi = menu.GetType().GetMethod("OnContinueButtonPressedAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi is null) return ("error", "OnContinueButtonPressedAsync not found on NMainMenu");
            _ = mi.Invoke(menu, null);
            BridgeTrace.Log("DispatchContinueRun invoked OnContinueButtonPressedAsync");
            return ("ok", "continue pressed");
        }
        catch (Exception ex)
        {
            return ("error", $"OnContinueButtonPressedAsync threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Abandons the currently saved run from the main menu. Invokes
    /// <c>NMainMenu.AbandonRun()</c> which pops the confirmation popup, and
    /// then — by default — also confirms "Yes" on the popup. Pass
    /// <c>"confirm": false</c> to stop before the confirmation (the popup
    /// will still be visible and Hermes can send another command with
    /// <c>confirm=true</c> to proceed, or dismiss it externally).
    ///
    /// Args:
    ///   confirm (bool, optional, default true): if true, also press "Yes" on
    ///     the confirmation popup after a short delay.
    /// </summary>
    private static (string status, string message) DispatchAbandonRun(JsonElement command)
    {
        var menu = NGame.Instance?.MainMenu;
        if (menu is null || !Godot.GodotObject.IsInstanceValid(menu))
        {
            return ("error", "main menu not available (NGame.Instance.MainMenu null)");
        }

        bool confirm = true;
        if (command.TryGetProperty("confirm", out var cEl) && cEl.ValueKind == JsonValueKind.False)
        {
            confirm = false;
        }

        try
        {
            menu.AbandonRun();
            BridgeTrace.Log($"DispatchAbandonRun invoked AbandonRun (confirm={confirm})");

            if (!confirm)
            {
                return ("ok", "abandon popup opened (confirm=false)");
            }

            // Popup opens async — schedule a retry chain on the main-thread
            // pump. Each tick re-polls for the popup and presses Yes once found.
            // Limited to ~2s worth of frames.
            ScheduleAbandonConfirm(attemptsLeft: 120);
            return ("ok", "abandon invoked (confirm popup handled if present; run abandoned either way)");
        }
        catch (Exception ex)
        {
            return ("error", $"NMainMenu.AbandonRun threw: {ex.Message}");
        }
    }

    private static void ScheduleAbandonConfirm(int attemptsLeft)
    {
        BridgeMainThreadDispatcher.Enqueue(() =>
        {
            // Popup may be parented anywhere under the SceneTree root, not just
            // under NGame. Search from the tree root for any NAbandonRunConfirmPopup.
            NAbandonRunConfirmPopup? popup = null;
            var ngame = NGame.Instance;
            Node? searchRoot = ngame;
            try
            {
                var tree = ngame?.GetTree();
                if (tree is not null) searchRoot = tree.Root;
            }
            catch { }
            FindPopupRecursive(searchRoot, ref popup);

            // Fallback: scan ALL NAbandonRunConfirmPopup instances via GodotObject.
            if (popup is null)
            {
                try
                {
                    var all = System.AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return System.Array.Empty<Type>(); } })
                        .Where(t => t == typeof(NAbandonRunConfirmPopup))
                        .ToList();
                    // no global registry; the tree walk above is the best we can do
                }
                catch { }
            }

            if (popup is not null && Godot.GodotObject.IsInstanceValid(popup))
            {
                try
                {
                    var mi = popup.GetType().GetMethod("OnYesButtonPressed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi is null)
                    {
                        BridgeTrace.Log("ScheduleAbandonConfirm: OnYesButtonPressed not found");
                        return;
                    }
                    mi.Invoke(popup, new object?[] { null });
                    BridgeTrace.Log("ScheduleAbandonConfirm: Yes pressed");
                    try { BridgeSnapshotWriter.RequestWrite("AbandonConfirmed"); } catch { }
                }
                catch (Exception ex)
                {
                    BridgeTrace.Log($"ScheduleAbandonConfirm threw: {ex.Message}");
                }
                return;
            }
            if (attemptsLeft > 0)
            {
                if (attemptsLeft % 30 == 0)
                {
                    BridgeTrace.Log($"ScheduleAbandonConfirm: still waiting, attemptsLeft={attemptsLeft}, searchRoot={(searchRoot?.GetType().Name ?? "null")}");
                }
                ScheduleAbandonConfirm(attemptsLeft - 1);
            }
            else
            {
                BridgeTrace.Log("ScheduleAbandonConfirm: popup never appeared");
                // Dump scene tree for diagnosis
                try
                {
                    if (searchRoot is not null) DumpTree(searchRoot, 0, 3);
                }
                catch (Exception ex) { BridgeTrace.Log($"DumpTree threw: {ex.Message}"); }
            }
        });
    }

    private static void DumpTree(Node node, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        try
        {
            BridgeTrace.Log($"{new string(' ', depth * 2)}{node.GetType().Name} '{node.Name}'");
        }
        catch { }
        if (depth == maxDepth) return;
        foreach (var child in node.GetChildren())
        {
            DumpTree(child, depth + 1, maxDepth);
        }
    }

    private static void FindPopupRecursive(Node? node, ref NAbandonRunConfirmPopup? hit)
    {
        if (node is null || hit is not null) return;
        if (node is NAbandonRunConfirmPopup p) { hit = p; return; }
        foreach (var child in node.GetChildren())
        {
            if (hit is not null) return;
            FindPopupRecursive(child, ref hit);
        }
    }

    /// <summary>
    /// Starts a new singleplayer run directly, bypassing the character-select
    /// UI. Maps to <c>NGame.StartNewSingleplayerRun(CharacterModel, shouldSave:true, acts:null, modifiers:null, seed:..., GameMode.Standard, ascensionLevel:0, dailyTime:null)</c>.
    /// Requires the main menu to be active (no run currently loaded).
    ///
    /// Args:
    ///   character (string, optional, default "IRONCLAD"): <c>ModelId.Entry</c>
    ///     of the CharacterModel to play (e.g. "IRONCLAD", "SILENT", "DEFECT").
    ///   seed (string, optional, default ""): run seed; empty means random.
    ///   ascensionLevel (int, optional, default 0).
    ///   gameMode (string, optional, default "Standard"): one of
    ///     <c>Standard</c>, <c>Custom</c>, <c>Daily</c>.
    /// </summary>
    private static (string status, string message) DispatchStartRun(JsonElement command)
    {
        if (NGame.Instance is null)
        {
            return ("error", "NGame.Instance not ready");
        }
        if (RunManager.Instance is not null && RunManager.Instance.IsInProgress)
        {
            return ("error", "a run is already in progress; AbandonRun first");
        }

        string charEntry = "IRONCLAD";
        if (command.TryGetProperty("character", out var cEl) && cEl.ValueKind == JsonValueKind.String)
        {
            charEntry = (cEl.GetString() ?? "IRONCLAD").ToUpperInvariant();
        }

        string seed = "";
        if (command.TryGetProperty("seed", out var sEl) && sEl.ValueKind == JsonValueKind.String)
        {
            seed = sEl.GetString() ?? "";
        }
        // The game treats an empty seed string as the literal seed "" (deterministic),
        // NOT as "generate random". Synthesize one here so repeat StartRun calls without
        // an explicit seed actually vary. Format: decimal digits of a 64-bit random value,
        // which mirrors what the character-select UI produces.
        if (string.IsNullOrEmpty(seed))
        {
            var rng = new System.Random();
            // Two 32-bit draws concatenated to get a full 64-bit spread.
            ulong hi = (ulong)(uint)rng.Next(int.MinValue, int.MaxValue);
            ulong lo = (ulong)(uint)rng.Next(int.MinValue, int.MaxValue);
            ulong combined = (hi << 32) ^ lo;
            seed = combined.ToString(System.Globalization.CultureInfo.InvariantCulture);
            BridgeTrace.Log($"DispatchStartRun: empty seed, synthesized '{seed}'");
        }

        int ascension = 0;
        if (command.TryGetProperty("ascensionLevel", out var aEl) && aEl.ValueKind == JsonValueKind.Number)
        {
            ascension = aEl.GetInt32();
        }

        GameMode gameMode = GameMode.Standard;
        if (command.TryGetProperty("gameMode", out var gEl) && gEl.ValueKind == JsonValueKind.String)
        {
            var gm = gEl.GetString() ?? "Standard";
            if (!Enum.TryParse<GameMode>(gm, ignoreCase: true, out gameMode))
            {
                return ("error", $"unknown gameMode '{gm}' (expected Standard/Custom/Daily)");
            }
        }

        CharacterModel? character = null;
        try
        {
            foreach (var cm in ModelDb.AllCharacters)
            {
                if (cm?.Id?.Entry is string e && string.Equals(e, charEntry, StringComparison.OrdinalIgnoreCase))
                {
                    character = cm;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            return ("error", $"ModelDb.AllCharacters threw: {ex.Message}");
        }
        if (character is null)
        {
            return ("error", $"no CharacterModel with Id.Entry='{charEntry}' in ModelDb.AllCharacters");
        }

        try
        {
            // NGame.StartNewSingleplayerRun(CharacterModel, bool shouldSave,
            //   IReadOnlyList<string> acts, IReadOnlyList<ModifierModel> modifiers,
            //   string seed, GameMode gameMode, int ascensionLevel, long? dailyTime)
            // Returns Task<...>. Attach continuation to log faults.
            // acts: pass all registered ActModels (standard three-act structure).
            // Passing empty throws IndexOutOfRange in NGame.StartRun.
            var acts = System.Linq.Enumerable.ToList(ModelDb.Acts);
            var task = NGame.Instance.StartNewSingleplayerRun(
                character,
                shouldSave: true,
                acts: acts,
                modifiers: System.Array.Empty<MegaCrit.Sts2.Core.Models.ModifierModel>(),
                seed: seed,
                gameMode: gameMode,
                ascensionLevel: ascension,
                dailyTime: null);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var inner = t.Exception?.GetBaseException();
                    BridgeTrace.Log($"StartNewSingleplayerRun FAULTED: {inner?.GetType().Name}: {inner?.Message}\n{inner?.StackTrace}");
                }
                else if (t.IsCanceled)
                {
                    BridgeTrace.Log("StartNewSingleplayerRun canceled");
                }
                else
                {
                    BridgeTrace.Log("StartNewSingleplayerRun completed OK");
                }
            });
            BridgeTrace.Log($"DispatchStartRun character={charEntry} seed='{seed}' asc={ascension} mode={gameMode}");
            return ("ok", $"started run: {charEntry} (mode={gameMode}, asc={ascension}, seed='{seed}')");
        }
        catch (Exception ex)
        {
            return ("error", $"NGame.StartNewSingleplayerRun threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Dismisses the game-over (death/victory) screen back to the main menu.
    /// Maps to <c>NGameOverScreen.ReturnToMainMenu()</c> if a game-over screen
    /// is currently visible, otherwise falls back to
    /// <c>NGame.Instance.ReturnToMainMenu()</c>.
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchReturnToMenu(JsonElement command)
    {
        // Prefer the game-over screen's own return path if it's present; it
        // handles the outro animation cleanly.
        NGameOverScreen? gameOver = null;
        FindGameOverRecursive(NGame.Instance, ref gameOver);
        if (gameOver is not null && Godot.GodotObject.IsInstanceValid(gameOver) && gameOver.Visible)
        {
            try
            {
                var mi = gameOver.GetType().GetMethod("ReturnToMainMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi is null) return ("error", "ReturnToMainMenu not found on NGameOverScreen");
                mi.Invoke(gameOver, null);
                BridgeTrace.Log("DispatchReturnToMenu via NGameOverScreen.ReturnToMainMenu");
                return ("ok", "returning to main menu from game-over screen");
            }
            catch (Exception ex)
            {
                return ("error", $"NGameOverScreen.ReturnToMainMenu threw: {ex.Message}");
            }
        }

        var ngame = NGame.Instance;
        if (ngame is null) return ("error", "NGame.Instance not ready");
        try
        {
            _ = ngame.ReturnToMainMenu();
            BridgeTrace.Log("DispatchReturnToMenu via NGame.ReturnToMainMenu");
            return ("ok", "returning to main menu");
        }
        catch (Exception ex)
        {
            return ("error", $"NGame.ReturnToMainMenu threw: {ex.Message}");
        }
    }

    /// <summary>
    /// In-run "give up" — ends the current run, which surfaces the
    /// <c>NGameOverScreen</c>. Primary path: <c>RunManager.Abandon()</c>
    /// (verified to surface the game-over screen correctly). Fallback:
    /// scene-tree search for <c>NAbandonRunButton.OnRelease()</c>.
    ///
    /// After this command completes, <c>GameOverScreenReadyPatch</c> will
    /// auto-dismiss the game-over screen back to the main menu after a short
    /// grace period (~3s).
    ///
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchGiveUp(JsonElement command)
    {
        if (RunManager.Instance is null || !RunManager.Instance.IsInProgress)
        {
            return ("error", "no run in progress; GiveUp only works mid-run");
        }

        // Primary path: RunManager.Abandon(). Live-verified to surface
        // NGameOverScreen correctly.
        try
        {
            var rm = RunManager.Instance;
            var mi = rm.GetType().GetMethod("Abandon",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi is not null)
            {
                mi.Invoke(rm, null);
                BridgeTrace.Log("DispatchGiveUp invoked RunManager.Abandon()");
                return ("ok", "RunManager.Abandon() invoked; game-over screen should appear then auto-dismiss");
            }
            BridgeTrace.Log("DispatchGiveUp: RunManager.Abandon method not found; falling back to NAbandonRunButton search");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"DispatchGiveUp: RunManager.Abandon threw: {ex.Message}; falling back");
        }

        // Fallback: walk scene tree for the Settings→Abandon button.
        NAbandonRunButton? button = null;
        Node? searchRoot = NGame.Instance;
        try
        {
            var tree = NGame.Instance?.GetTree();
            if (tree?.Root is not null) searchRoot = tree.Root;
        }
        catch { }
        FindAbandonButtonRecursive(searchRoot, ref button);

        if (button is null || !Godot.GodotObject.IsInstanceValid(button))
        {
            return ("error", "RunManager.Abandon failed and NAbandonRunButton not found in scene tree");
        }

        try
        {
            var mi = button.GetType().GetMethod("OnRelease",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi is null) return ("error", "OnRelease not found on NAbandonRunButton");
            mi.Invoke(button, null);
            BridgeTrace.Log("DispatchGiveUp invoked NAbandonRunButton.OnRelease (fallback path)");
            return ("ok", "give-up pressed via NAbandonRunButton");
        }
        catch (Exception ex)
        {
            return ("error", $"NAbandonRunButton.OnRelease threw: {ex.Message}");
        }
    }

    private static void FindAbandonButtonRecursive(Node? node, ref NAbandonRunButton? hit)
    {
        if (node is null || hit is not null) return;
        if (node is NAbandonRunButton b) { hit = b; return; }
        foreach (var child in node.GetChildren())
        {
            if (hit is not null) return;
            FindAbandonButtonRecursive(child, ref hit);
        }
    }

    private static void FindGameOverRecursive(Node? node, ref NGameOverScreen? hit)
    {
        if (node is null || hit is not null) return;
        if (node is NGameOverScreen g) { hit = g; return; }
        foreach (var child in node.GetChildren())
        {
            if (hit is not null) return;
            FindGameOverRecursive(child, ref hit);
        }
    }

    // ---- Shop (merchant) commands ----

    /// <summary>
    /// Purchases an item from the current merchant inventory.
    /// Args:
    ///   category (string, required): one of
    ///     <c>"character_card"</c>, <c>"colorless_card"</c>, <c>"potion"</c>,
    ///     <c>"relic"</c>. Matches the list names in the extracted shop payload.
    ///   index (int, required): 0-based index into that category's entry list.
    ///
    /// Maps to <c>MerchantEntry.OnTryPurchaseWrapper(inventory, ignoreCost:false)</c>.
    /// Returns "error" with the <c>PurchaseStatus</c> on failure (FailureGold,
    /// FailureOutOfStock, FailureSpace, FailureForbidden).
    /// </summary>
    private static (string status, string message) DispatchPurchase(JsonElement command)
    {
        var merchant = BridgeSingleton.CurrentMerchantRoom;
        if (merchant is null)
        {
            return ("error", "no current merchant room (not at shop?)");
        }
        var inv = merchant.Inventory;
        if (inv is null)
        {
            return ("error", "merchant has no inventory");
        }

        if (!command.TryGetProperty("category", out var catEl) || catEl.ValueKind != JsonValueKind.String)
        {
            return ("error", "missing required 'category' (string)");
        }
        if (!command.TryGetProperty("index", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "missing required 'index' (int)");
        }
        string category = catEl.GetString()!.ToLowerInvariant();
        int index = idxEl.GetInt32();

        System.Collections.IEnumerable? source = category switch
        {
            "character_card" or "charactercard" or "character" => inv.CharacterCardEntries,
            "colorless_card" or "colorlesscard" or "colorless" => inv.ColorlessCardEntries,
            "potion" or "potions" => inv.PotionEntries,
            "relic" or "relics" => inv.RelicEntries,
            _ => null,
        };
        if (source is null)
        {
            return ("error", $"unknown category '{category}' (expected character_card/colorless_card/potion/relic)");
        }

        MerchantEntry? entry = null;
        int i = 0;
        foreach (var e in source)
        {
            if (i == index) { entry = e as MerchantEntry; break; }
            i++;
        }
        if (entry is null)
        {
            return ("error", $"no {category} entry at index {index} (found {i} entries)");
        }

        return InvokePurchase(entry, inv, $"Purchase category={category} index={index}");
    }

    /// <summary>
    /// Purchases the shop's card-removal service (the one-shot "remove a card
    /// from your deck" option). Pricing scales with uses — the entry itself
    /// tracks cost.
    ///
    /// After the server accepts, the game surfaces a grid-select popup for
    /// the player to choose which card to remove; that selection is NOT
    /// driven by this command yet (state extractor should expose it; see
    /// follow-up work).
    ///
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchPurchaseCardRemoval(JsonElement command)
    {
        var merchant = BridgeSingleton.CurrentMerchantRoom;
        if (merchant is null)
        {
            return ("error", "no current merchant room (not at shop?)");
        }
        var inv = merchant.Inventory;
        var removal = inv?.CardRemovalEntry;
        if (inv is null || removal is null)
        {
            return ("error", "merchant has no card-removal entry");
        }
        return InvokePurchase(removal, inv, "PurchaseCardRemoval");
    }

    /// <summary>
    /// Leaves the current shop. Maps to <c>MerchantRoom.Exit(runState)</c>,
    /// the same room-exit pattern used by <c>EventRoom.Exit</c>. Equivalent
    /// to <c>Proceed</c> when the active room is a shop.
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchLeaveShop(JsonElement command)
    {
        var merchant = BridgeSingleton.CurrentMerchantRoom;
        if (merchant is null)
        {
            return ("error", "no current merchant room (not at shop?)");
        }
        return ExitMerchantRoom(merchant);
    }

    private static (string status, string message) ExitMerchantRoom(MerchantRoom merchant)
    {
        // MerchantRoom.Exit is history-recording ONLY (logs CardChoice/ModelChoice
        // entries to the map-point history). It does NOT trigger any UI
        // transition to the map. NMerchantRoom has no static Proceed() method
        // (unlike NEventRoom). So we replicate what NEventRoom.Proceed does:
        // SetTravelEnabled(true) + Open(false) on the singleton NMapScreen.
        // That is the actual "leave room → map" transition used across rooms.
        try
        {
            var rm = RunManager.Instance;
            if (rm is null) return ("error", "RunManager.Instance null; cannot Exit merchant");
            var stateProp = rm.GetType().GetProperty("State",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var state = stateProp?.GetValue(rm) as MegaCrit.Sts2.Core.Runs.IRunState;
            if (state is null) return ("error", "RunManager.State null/not-IRunState; cannot Exit merchant");

            // 1) Record history via MerchantRoom.Exit (fire-and-forget — the task
            //    completes synchronously since Exit just appends history entries).
            var task = merchant.Exit(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var inner = t.Exception?.GetBaseException();
                    BridgeTrace.Log($"MerchantRoom.Exit FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                }
                else { BridgeTrace.Log("MerchantRoom.Exit completed (history recorded)"); }
            });

            // 2) Open the map screen. This is the real UI transition.
            try
            {
                var mapScreen = NMapScreen.Instance;
                if (mapScreen is null || !Godot.GodotObject.IsInstanceValid(mapScreen))
                {
                    BridgeTrace.Log("ExitMerchantRoom: NMapScreen.Instance null/invalid — cannot open map");
                    return ("error", "NMapScreen.Instance null/invalid; shop exit recorded but map cannot open");
                }
                mapScreen.SetTravelEnabled(true);
                mapScreen.Open(false); // isOpenedFromTopBar = false
                BridgeTrace.Log("ExitMerchantRoom: NMapScreen.Open(false) invoked");
            }
            catch (Exception exMap)
            {
                BridgeTrace.Log($"ExitMerchantRoom: NMapScreen.Open threw: {exMap.GetType().Name}: {exMap.Message}");
                return ("error", $"NMapScreen.Open threw: {exMap.Message}");
            }

            return ("ok", "leaving shop (history recorded + map opened)");
        }
        catch (Exception ex)
        {
            return ("error", $"MerchantRoom.Exit threw: {ex.Message}");
        }
    }

    private static (string status, string message) InvokePurchase(MerchantEntry entry, MerchantInventory inv, string logCtx)
    {
        // Prefer OnTryPurchaseWrapper — it wraps cost validation + restock logic.
        // For MerchantCardRemovalEntry there's a 3-arg overload (cancelable); the
        // base MerchantEntry.OnTryPurchaseWrapper has 2 args. Pick based on type.
        try
        {
            var entryType = entry.GetType();
            var methods = entryType.GetMethods(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            System.Reflection.MethodInfo? wrapper = null;
            foreach (var m in methods)
            {
                if (m.Name != "OnTryPurchaseWrapper") continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 &&
                    ps[0].ParameterType == typeof(MerchantInventory) &&
                    ps[1].ParameterType == typeof(bool))
                {
                    wrapper = m;
                    break;
                }
            }
            // Fallback: OnTryPurchase (2-arg).
            if (wrapper is null)
            {
                foreach (var m in methods)
                {
                    if (m.Name != "OnTryPurchase") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 &&
                        ps[0].ParameterType == typeof(MerchantInventory) &&
                        ps[1].ParameterType == typeof(bool))
                    {
                        wrapper = m;
                        break;
                    }
                }
            }
            if (wrapper is null)
            {
                return ("error", $"no OnTryPurchaseWrapper/OnTryPurchase(MerchantInventory,bool) on {entryType.Name}");
            }

            // Up-front guard: surface predictable PurchaseStatus reasons before the
            // async call so the reply is informative (the Task resolves after dispatch).
            try
            {
                if (!entry.IsStocked) return ("error", "purchase failed: FailureOutOfStock (not stocked)");
                if (!entry.EnoughGold) return ("error", $"purchase failed: FailureGold (cost={entry.Cost})");
            }
            catch { /* best-effort guards */ }

            var task = wrapper.Invoke(entry, new object[] { inv, false }) as System.Threading.Tasks.Task;
            if (task is null)
            {
                return ("error", "OnTryPurchase returned null Task");
            }
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var inner = t.Exception?.GetBaseException();
                    BridgeTrace.Log($"{logCtx} FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                    return;
                }
                // Result is Task<PurchaseStatus>; read via reflection to avoid a generic cast.
                try
                {
                    var resultProp = t.GetType().GetProperty("Result");
                    var status = resultProp?.GetValue(t);
                    BridgeTrace.Log($"{logCtx} completed: PurchaseStatus={status}");
                    // Re-push shop state so Hermes sees updated gold/inventory.
                    try { BridgeSingleton.PushCurrentShop("PostPurchase"); } catch { }
                }
                catch (Exception ex)
                {
                    BridgeTrace.Log($"{logCtx} result-read threw: {ex.Message}");
                }
            });
            BridgeTrace.Log($"{logCtx} invoked {wrapper.Name} on {entryType.Name} cost={entry.Cost}");
            return ("ok", $"purchase invoked ({entryType.Name}, cost={entry.Cost}); status in trace");
        }
        catch (Exception ex)
        {
            return ("error", $"InvokePurchase threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses a potion from the player's potion belt. Maps to
    /// <c>PotionModel.EnqueueManualUse(Creature target)</c>, which invokes the
    /// potion's BeforeUse hook and enqueues a UsePotionAction through the
    /// RunManager's ActionQueueSynchronizer. Works in and out of combat.
    ///
    /// Args:
    ///   slotIndex (int, required): 0-based index into <c>player.PotionSlots</c>
    ///     (matches positional index in state.json's <c>run.potions</c>, including
    ///     null entries for empty slots).
    ///   targetIndex (int, optional): 0-based index into <c>combat.enemies</c>,
    ///     required for potions whose <c>targetType</c> demands a single creature
    ///     target (e.g. Fire Potion, Poison Potion). Ignored otherwise.
    ///   targetSelf (bool, optional): target the player's own creature instead
    ///     of an enemy. Used by potions that heal/buff self when <c>targetType</c>
    ///     accepts a creature parameter but you want self-targeting.
    /// </summary>
    private static (string status, string message) DispatchUsePotion(JsonElement command)
    {
        var player = BridgeSingleton.CurrentPlayer ?? ResolvePlayerFromRun();
        if (player is null) return ("error", "no current player (not in run?)");

        if (!command.TryGetProperty("slotIndex", out var slotEl) || slotEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "UsePotion requires numeric 'slotIndex'");
        }
        var slotIndex = slotEl.GetInt32();

        var slots = player.PotionSlots;
        if (slots is null || slotIndex < 0 || slotIndex >= slots.Count)
        {
            return ("error", $"slotIndex {slotIndex} out of range (potion slots: {slots?.Count ?? 0})");
        }
        var potion = slots[slotIndex];
        if (potion is null)
        {
            return ("error", $"potion slot {slotIndex} is empty");
        }

        // Resolve target (optional).
        Creature? target = null;
        if (command.TryGetProperty("targetSelf", out var selfEl) && selfEl.ValueKind == JsonValueKind.True)
        {
            target = player.Creature;
        }
        else if (command.TryGetProperty("targetIndex", out var tgtIdxEl) && tgtIdxEl.ValueKind == JsonValueKind.Number)
        {
            var tgtIndex = tgtIdxEl.GetInt32();
            var combatState = BridgeSingleton.CurrentCombatRoom?.CombatState ?? player.Creature?.CombatState;
            var enemies = combatState?.Enemies;
            if (enemies is null || tgtIndex < 0 || tgtIndex >= enemies.Count)
            {
                return ("error", $"targetIndex {tgtIndex} out of range (enemy count {enemies?.Count ?? 0})");
            }
            target = enemies[tgtIndex];
        }
        else
        {
            // No explicit target supplied. For potions whose targeting type accepts
            // the player (e.g. AnyPlayer potions like Swift Potion / Strength Potion
            // when used on self), EnqueueManualUse(null) silently stalls awaiting a
            // UI target pick. Default to the player's creature so the action resolves
            // immediately. Potions with TargetType == None ignore the argument.
            var ttName = string.Empty;
            try { ttName = potion.TargetType.ToString() ?? string.Empty; } catch { }
            if (ttName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = player.Creature;
            }
        }

        try
        {
            BugFDiagnostic.OnEnqueue(potion, target);
            potion.EnqueueManualUse(target);
            ScheduleDeferredStateRefresh(2, "UsePotionResolve", includeCombat: true, includeRun: true);
            BridgeTrace.Log($"DispatchUsePotion slot={slotIndex} potion={potion.Id?.Entry ?? "<?>"} target={(target is null ? "<none>" : target.Name ?? "<?>")}");
            return ("ok", $"enqueued use of {potion.Id?.Entry ?? "potion"} (slot {slotIndex})");
        }
        catch (Exception ex)
        {
            return ("error", $"PotionModel.EnqueueManualUse threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Discards a potion without using it. Maps to <c>PotionCmd.Discard(potion)</c>,
    /// which is async but fire-and-forget from the dispatcher's perspective.
    /// Args:
    ///   slotIndex (int, required): 0-based index into <c>player.PotionSlots</c>.
    /// </summary>
    private static (string status, string message) DispatchDiscardPotion(JsonElement command)
    {
        var player = BridgeSingleton.CurrentPlayer ?? ResolvePlayerFromRun();
        if (player is null) return ("error", "no current player (not in run?)");

        if (!command.TryGetProperty("slotIndex", out var slotEl) || slotEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "DiscardPotion requires numeric 'slotIndex'");
        }
        var slotIndex = slotEl.GetInt32();

        var slots = player.PotionSlots;
        if (slots is null || slotIndex < 0 || slotIndex >= slots.Count)
        {
            return ("error", $"slotIndex {slotIndex} out of range (potion slots: {slots?.Count ?? 0})");
        }
        var potion = slots[slotIndex];
        if (potion is null)
        {
            return ("error", $"potion slot {slotIndex} is empty");
        }

        try
        {
            var task = PotionCmd.Discard(potion);
            ScheduleDeferredStateRefresh(2, "DiscardPotionResolve", includeCombat: true, includeRun: true);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var inner = t.Exception?.GetBaseException();
                    BridgeTrace.Log($"DiscardPotion FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                }
                else { BridgeTrace.Log($"DiscardPotion completed (slot {slotIndex})"); }
            });
            BridgeTrace.Log($"DispatchDiscardPotion slot={slotIndex} potion={potion.Id?.Entry ?? "<?>"}");
            return ("ok", $"discarded {potion.Id?.Entry ?? "potion"} (slot {slotIndex})");
        }
        catch (Exception ex)
        {
            return ("error", $"PotionCmd.Discard threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Fallback player resolution for out-of-combat command handlers (e.g.
    /// UsePotion/DiscardPotion on the Map screen for potions like Fruit Juice
    /// that are usable outside combat). Pulls the primary player from
    /// RunManager.Instance.State.Players. Returns null if no run is active.
    /// </summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? ResolvePlayerFromRun()
    {
        try
        {
            var rm = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
            if (rm is null) return null;
            var stateProp = rm.GetType().GetProperty("State",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var state = stateProp?.GetValue(rm);
            if (state is null) return null;
            var playersProp = state.GetType().GetProperty("Players");
            if (playersProp?.GetValue(state) is not System.Collections.IEnumerable players) return null;
            foreach (var p in players)
            {
                if (p is MegaCrit.Sts2.Core.Entities.Players.Player pl) return pl;
            }
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"ResolvePlayerFromRun threw: {ex.Message}");
        }
        return null;
    }

    private static void ScheduleDeferredStateRefresh(int pumpTicks, string trigger, bool includeCombat, bool includeRun)
    {
        BridgeMainThreadDispatcher.EnqueueAfterPumpTicks(pumpTicks, () =>
        {
            try
            {
                if (includeCombat)
                {
                    BridgeSingleton.PushCurrentCombat(trigger);
                }

                if (includeRun)
                {
                    BridgeSingleton.PushCurrentRun(trigger);
                }
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"ScheduleDeferredStateRefresh({trigger}) threw: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Selects one or more cards on the active card-grid selection screen
    /// (<c>NCardGridSelectionScreen</c> family). Covers:
    ///   - <c>NSimpleCardSelectScreen</c> (shop card removal, Smith, Dew Gaze, etc.):
    ///     clicking a card auto-completes the selection via <c>CompleteSelection</c>.
    ///     Pass exactly one index.
    ///   - <c>NDeckCardSelectScreen</c> (choose N from deck prompts):
    ///     click each desired card, then dispatcher calls <c>ConfirmSelection(null)</c>
    ///     to finalize.
    ///
    /// Args:
    ///   cardIndices (int[], required): positional indices into
    ///     <c>cardGrid.cards</c> in state.json (0-based, matches the <c>index</c>
    ///     field on each entry).
    /// </summary>
    private static (string status, string message) DispatchSelectCardsInGrid(JsonElement command)
    {
        var screen = Patches.CardGridSelectionConnectPatch.LastScreen;
        var cards = Patches.CardGridSelectionConnectPatch.LastCards;
        if (screen is null || !Godot.GodotObject.IsInstanceValid(screen) || cards is null)
        {
            return ("error", "no active card-grid selection screen");
        }

        if (!command.TryGetProperty("cardIndices", out var idxArr) || idxArr.ValueKind != JsonValueKind.Array)
        {
            return ("error", "SelectCardsInGrid requires 'cardIndices' (int[])");
        }

        var indices = new System.Collections.Generic.List<int>();
        foreach (var el in idxArr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Number)
                return ("error", "cardIndices must contain only numbers");
            indices.Add(el.GetInt32());
        }
        if (indices.Count == 0) return ("error", "cardIndices is empty");
        foreach (var i in indices)
        {
            if (i < 0 || i >= cards.Count)
                return ("error", $"cardIndex {i} out of range (grid size {cards.Count})");
        }

        var screenType = screen.GetType();

        // CARDGRIDAPPLY diagnostic: enumerate all candidate finalize/confirm
        // methods across the type hierarchy so we can see what the screen
        // actually exposes when card-removal callbacks don't fire.
        try
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var t = screenType; t != null && t != typeof(object); t = t.BaseType)
            {
                var methods = t.GetMethods(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly);
                foreach (var m in methods)
                {
                    var n = m.Name;
                    if (n != "ConfirmSelection" && n != "CompleteSelection"
                        && n != "CheckIfSelectionComplete" && n != "OnCardClicked"
                        && n != "OnConfirmButtonPressed" && n != "Finalize"
                        && n != "Complete" && n != "Apply" && n != "FinishSelection")
                        continue;
                    var paramList = string.Join(",", System.Linq.Enumerable.Select(m.GetParameters(), p => p.ParameterType.Name));
                    var sig = $"{t.Name}.{n}({paramList})";
                    if (seen.Add(sig))
                        BridgeTrace.Log($"SelectCardsInGrid[diag] method {sig}");
                }
            }
        }
        catch (System.Exception diagEx)
        {
            BridgeTrace.Log($"SelectCardsInGrid[diag] enumeration threw: {diagEx.Message}");
        }

        // Look up the right OnCardClicked — declared per subclass; use the
        // most-derived instance-accessible one.
        var onClickedMethod = screenType.GetMethod(
            "OnCardClicked",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(CardModel) },
            null);
        if (onClickedMethod is null)
        {
            return ("error", $"no OnCardClicked(CardModel) on {screenType.Name}");
        }

        try
        {
            foreach (var i in indices)
            {
                onClickedMethod.Invoke(screen, new object[] { cards[i] });
                BridgeTrace.Log($"SelectCardsInGrid: OnCardClicked idx={i} card={cards[i].Title ?? cards[i].Id?.Entry ?? "<?>"}");
            }

            // For grid screens that require an explicit finalize step (upgrade
            // screen, generic deck picker, etc.), invoke CheckIfSelectionComplete
            // directly. OnCardClicked on NSimpleCardSelectScreen auto-completes
            // and does NOT declare CheckIfSelectionComplete, so this gate skips
            // it harmlessly. We avoid ConfirmSelection(NButton) because several
            // subclasses early-return on null button arg (they use it as the
            // click-source).
            var check = screenType.GetMethod(
                "CheckIfSelectionComplete",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                null,
                System.Type.EmptyTypes,
                null);
            var tryInvoke = (string name) =>
            {
                var mi = screenType.GetMethod(
                    name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (mi is null) return false;
                var ps = mi.GetParameters();
                object[] args;
                if (ps.Length == 0) args = System.Array.Empty<object>();
                else if (ps.Length == 1) args = new object[] { null };
                else return false;
                try
                {
                    mi.Invoke(screen, args);
                    BridgeTrace.Log($"SelectCardsInGrid: {name} invoked on {screenType.Name}");
                    return true;
                }
                catch (Exception fx)
                {
                    var inner = fx is System.Reflection.TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : fx;
                    BridgeTrace.Log($"SelectCardsInGrid: {name} on {screenType.Name} threw: {inner.GetType().Name}: {inner.Message}");
                    return false;
                }
            };
            if (check is not null)
            {
                check.Invoke(screen, null);
                BridgeTrace.Log($"SelectCardsInGrid: CheckIfSelectionComplete invoked on {screenType.Name}");
                // NDeckCardSelectScreen.CheckIfSelectionComplete IS the commit:
                // per IL, it calls TaskCompletionSource.SetResult and removes
                // the overlay when _selectedCards.Count >= MinSelect. Do NOT
                // call ConfirmSelection afterward — ConfirmSelection is just
                // `=> CheckIfSelectionComplete()` (7-byte wrapper), so a second
                // invocation double-SetResults and throws InvalidOperationException.
                // Side effects of the commit (e.g. Transform applying new cards
                // into run.deck) may not be reflected in state.json until a
                // later hook; that is a separate refresh-lag concern handled
                // by DeckChangedPatches and CardGridSelectionExit.
            }
            else
            {
                // Fallback for screens like NDeckTransformSelectScreen that
                // don't declare CheckIfSelectionComplete but DO require an
                // explicit finalize via CompleteSelection(NButton) or
                // ConfirmSelection(NButton). Try CompleteSelection first
                // (it's the "commit effects" step on screens that distinguish
                // Confirm-pressed from Complete-apply). Pass null for the
                // NButton arg — most implementations either ignore it or
                // only use it to source the caller; we'll see in trace if
                // that early-returns.
                var invoked = tryInvoke("CompleteSelection") || tryInvoke("ConfirmSelection");
                if (!invoked)
                    BridgeTrace.Log($"SelectCardsInGrid: no CheckIfSelectionComplete/CompleteSelection/ConfirmSelection on {screenType.Name} (auto-complete assumed)");
            }

            return ("ok", $"selected {indices.Count} card(s) on {screenType.Name}");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"OnCardClicked/Confirm threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Picks a card (or skips) on an active <c>NChooseACardSelectionScreen</c>
    /// — the 3-card overlay opened by <c>CardSelectCmd.FromChooseACardScreen</c>
    /// (Attack/Skill/Power potions, etc.). The screen's <c>_completionSource</c>
    /// unblocks the awaiting potion OnUse state machine which then generates
    /// the chosen card (free-this-turn) into the player's hand.
    ///
    /// Args (exactly one):
    ///   cardIndex (int, 0-based into <c>chooseACardScreen.cards</c>): select that card.
    ///   skip (bool, true): press the skip button (only valid if <c>canSkip</c>).
    /// </summary>
    private static (string status, string message) DispatchChooseACard(JsonElement command)
    {
        var screen = Patches.ChooseACardSelectionReadyPatch.LastScreen;
        var cards = Patches.ChooseACardSelectionReadyPatch.LastCards;
        if (screen is null || !Godot.GodotObject.IsInstanceValid(screen) || cards is null)
        {
            return ("error", "no active choose-a-card selection screen");
        }

        var hasIndex = command.TryGetProperty("cardIndex", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number;
        var wantsSkip = command.TryGetProperty("skip", out var skipEl)
            && skipEl.ValueKind == JsonValueKind.True;

        if (hasIndex == wantsSkip)
        {
            return ("error", "ChooseACard requires exactly one of 'cardIndex' (int) or 'skip' (true)");
        }

        var screenType = screen.GetType();

        if (wantsSkip)
        {
            if (!Patches.ChooseACardSelectionReadyPatch.LastCanSkip)
                return ("error", "this choose-a-card screen does not allow skipping");

            var skipMethod = screenType.GetMethod(
                "OnSkipButtonReleased",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (skipMethod is null)
                return ("error", "OnSkipButtonReleased not found on screen");
            try
            {
                skipMethod.Invoke(screen, new object?[] { null });
                BridgeTrace.Log("ChooseACard: skipped");
                return ("ok", "skipped choose-a-card");
            }
            catch (Exception ex)
            {
                var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
                return ("error", $"OnSkipButtonReleased threw: {inner.GetType().Name}: {inner.Message}");
            }
        }

        var cardIndex = idxEl.GetInt32();
        if (cardIndex < 0 || cardIndex >= cards.Count)
            return ("error", $"cardIndex {cardIndex} out of range (0..{cards.Count - 1})");

        // _cardRow holds NGridCardHolder children (subclass of NCardHolder) in
        // the same order as _cards. Fetch the matching holder and pass it to
        // the private SelectHolder(NCardHolder) method.
        var cardRowField = screenType.GetField(
            "_cardRow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (cardRowField is null)
            return ("error", "_cardRow field missing");
        var cardRow = cardRowField.GetValue(screen) as Godot.Node;
        if (cardRow is null)
            return ("error", "_cardRow null or not a Node");

        var childCount = cardRow.GetChildCount();
        if (cardIndex >= childCount)
            return ("error", $"_cardRow has only {childCount} children (cardIndex {cardIndex})");

        var holderNode = cardRow.GetChild(cardIndex);
        var holder = holderNode as NCardHolder;
        if (holder is null)
            return ("error", $"_cardRow child[{cardIndex}] is {holderNode?.GetType().Name ?? "null"}, not NCardHolder");

        var selectMethod = screenType.GetMethod(
            "SelectHolder",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(NCardHolder) },
            null);
        if (selectMethod is null)
            return ("error", "SelectHolder(NCardHolder) not found");

        try
        {
            selectMethod.Invoke(screen, new object[] { holder });
            var title = cards[cardIndex].Title ?? cards[cardIndex].Id?.Entry ?? "<?>";
            BridgeTrace.Log($"ChooseACard: selected idx={cardIndex} card={title}");
            return ("ok", $"selected card {cardIndex} ({title})");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"SelectHolder threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Selects a card in the current in-hand selection prompt
    /// (<see cref="NPlayerHand"/> in SimpleSelect / UpgradeSelect mode —
    /// Burning Pact Exhaust, Armaments Upgrade, Gambler's Brew Discard, etc.).
    /// The hand IS the selector — there is no overlay screen.
    ///
    /// Resolves the <see cref="NCardHolder"/> for the card at <c>handIndex</c>
    /// via <c>NPlayerHand.GetCardHolder(CardModel)</c>, then invokes the private
    /// <c>OnHolderPressed(NCardHolder)</c> which routes to
    /// <c>SelectCardInSimpleMode</c>/<c>UpgradeMode</c> or a toggle-deselect
    /// depending on mode and current selection set. When <c>minSelect == maxSelect</c>
    /// (the common case: exhaust 1, upgrade 1) the Nth click auto-completes the
    /// selection via <c>CheckIfSelectionComplete</c>.
    ///
    /// Args:
    ///   handIndex (int, required): 0-based index into <c>combat.hand.cards</c>,
    ///     matching the <c>handIndex</c> field on each entry in
    ///     <c>handSelect.cards</c>.
    /// </summary>
    private static (string status, string message) DispatchHandSelectCard(JsonElement command)
    {
        var hand = NPlayerHand.Instance;
        if (hand is null || !Godot.GodotObject.IsInstanceValid(hand))
            return ("error", "NPlayerHand.Instance not available");
        if (!hand.IsInCardSelection)
            return ("error", "hand is not in card selection mode");

        if (!command.TryGetProperty("handIndex", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
            return ("error", "HandSelectCard requires numeric 'handIndex'");
        int handIndex = idxEl.GetInt32();

        var player = BridgeSingleton.CurrentPlayer;
        var cards = player?.PlayerCombatState?.Hand?.Cards;
        if (cards is null || handIndex < 0 || handIndex >= cards.Count)
            return ("error", $"handIndex {handIndex} out of range (hand size {cards?.Count ?? 0})");
        var cardModel = cards[handIndex];

        NCardHolder? holder;
        try { holder = hand.GetCardHolder(cardModel); }
        catch (Exception ex) { return ("error", $"GetCardHolder threw: {ex.Message}"); }
        if (holder is null || !Godot.GodotObject.IsInstanceValid(holder))
            return ("error", $"no NCardHolder found for hand card {handIndex}");

        var mi = Patches.NPlayerHandSelectPatchState.OnHolderPressedMi;
        if (mi is null) return ("error", "NPlayerHand.OnHolderPressed not found via reflection");

        try
        {
            mi.Invoke(hand, new object[] { holder });
            var title = cardModel.Title ?? cardModel.Id?.Entry ?? "<?>";
            BridgeTrace.Log($"DispatchHandSelectCard handIndex={handIndex} card={title}");
            return ("ok", $"pressed hand card {handIndex} ({title})");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"OnHolderPressed threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Deselects an already-selected card in the active in-hand selection
    /// prompt. Useful for multi-select prompts (e.g. Gambler's Brew discard N
    /// where the user changes their mind). Maps to
    /// <c>NPlayerHand.DeselectCard(NCard)</c>.
    /// Args: handIndex (int, required) — index into <c>combat.hand.cards</c>.
    /// </summary>
    private static (string status, string message) DispatchHandDeselectCard(JsonElement command)
    {
        var hand = NPlayerHand.Instance;
        if (hand is null || !Godot.GodotObject.IsInstanceValid(hand))
            return ("error", "NPlayerHand.Instance not available");
        if (!hand.IsInCardSelection)
            return ("error", "hand is not in card selection mode");

        if (!command.TryGetProperty("handIndex", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
            return ("error", "HandDeselectCard requires numeric 'handIndex'");
        int handIndex = idxEl.GetInt32();

        var player = BridgeSingleton.CurrentPlayer;
        var cards = player?.PlayerCombatState?.Hand?.Cards;
        if (cards is null || handIndex < 0 || handIndex >= cards.Count)
            return ("error", $"handIndex {handIndex} out of range");
        var cardModel = cards[handIndex];

        NCard? ncard;
        try { ncard = hand.GetCard(cardModel); }
        catch (Exception ex) { return ("error", $"GetCard threw: {ex.Message}"); }
        if (ncard is null || !Godot.GodotObject.IsInstanceValid(ncard))
            return ("error", $"no NCard for hand card {handIndex}");

        try
        {
            hand.DeselectCard(ncard);
            BridgeTrace.Log($"DispatchHandDeselectCard handIndex={handIndex}");
            return ("ok", $"deselected hand card {handIndex}");
        }
        catch (Exception ex)
        {
            return ("error", $"DeselectCard threw: {ex.Message}");
        }
    }

    /// <summary>
    /// Presses the confirm button on a multi-select hand prompt
    /// (<c>requireManualConfirmation</c> = true, e.g. min != max). Maps to
    /// the private <c>NPlayerHand.OnSelectModeConfirmButtonPressed(NButton)</c>.
    /// No-op/error if the selection does not require manual confirmation or
    /// if <c>selectedCount &lt; minSelect</c>.
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchHandConfirmSelect(JsonElement command)
    {
        var hand = NPlayerHand.Instance;
        if (hand is null || !Godot.GodotObject.IsInstanceValid(hand))
            return ("error", "NPlayerHand.Instance not available");
        if (!hand.IsInCardSelection)
            return ("error", "hand is not in card selection mode");

        var mi = Patches.NPlayerHandSelectPatchState.OnConfirmMi;
        if (mi is null) return ("error", "NPlayerHand.OnSelectModeConfirmButtonPressed not found");

        try
        {
            mi.Invoke(hand, new object?[] { null });
            BridgeTrace.Log("DispatchHandConfirmSelect invoked OnSelectModeConfirmButtonPressed");
            return ("ok", "confirmed hand selection");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"OnSelectModeConfirmButtonPressed threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Cancels the active hand selection if the prompt allows it
    /// (<c>cancelable</c> = true). Maps to
    /// <c>NPlayerHand.CancelHandSelectionIfNecessary()</c>. The underlying
    /// method checks <c>_prefs.Cancelable</c> internally and no-ops otherwise.
    /// Args: none.
    /// </summary>
    private static (string status, string message) DispatchHandCancelSelect(JsonElement command)
    {
        var hand = NPlayerHand.Instance;
        if (hand is null || !Godot.GodotObject.IsInstanceValid(hand))
            return ("error", "NPlayerHand.Instance not available");
        if (!hand.IsInCardSelection)
            return ("error", "hand is not in card selection mode");

        var mi = Patches.NPlayerHandSelectPatchState.CancelMi;
        if (mi is null) return ("error", "NPlayerHand.CancelHandSelectionIfNecessary not found");

        try
        {
            mi.Invoke(hand, null);
            BridgeTrace.Log("DispatchHandCancelSelect invoked CancelHandSelectionIfNecessary");
            return ("ok", "cancel attempted (no-op if not cancelable)");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"CancelHandSelectionIfNecessary threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Opens the treasure chest. Maps to <c>NTreasureRoom.OpenChest()</c> via
    /// the captured live node (set by TreasureRoomReadyPatch). Args: none.
    /// Effect: <c>_hasChestBeenOpened</c> flips true and the relic collection
    /// becomes visible (or empty-chest VFX plays). The TreasureChestOpened
    /// postfix re-pushes treasure state so Hermes sees the populated
    /// relicChoices list.
    /// </summary>
    private static (string status, string message) DispatchOpenChest(JsonElement command)
    {
        var room = BridgeSingleton.CurrentTreasureRoom;
        if (room is null) return ("error", "no current TreasureRoom");
        var node = BridgeSingleton.CurrentTreasureNode;
        if (node is null || !Godot.GodotObject.IsInstanceValid(node))
        {
            return ("error", "no live NTreasureRoom captured (TreasureRoomReadyPatch not fired?)");
        }

        // Guard: if the chest is already opened, OpenChest is a no-op visual but
        // we surface a clearer status.
        try
        {
            var f = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom).GetField(
                "_hasChestBeenOpened",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f?.GetValue(node) is bool b && b)
            {
                return ("ignored", "chest already opened");
            }
        }
        catch { /* fall through and try anyway */ }

        try
        {
            var m = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom).GetMethod(
                "OpenChest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m is null) return ("error", "could not resolve NTreasureRoom.OpenChest");
            var task = m.Invoke(node, null) as System.Threading.Tasks.Task;
            task?.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var inner = t.Exception?.GetBaseException();
                    BridgeTrace.Log($"NTreasureRoom.OpenChest FAULTED: {inner?.GetType().Name}: {inner?.Message}");
                }
                else { BridgeTrace.Log("NTreasureRoom.OpenChest task completed"); }
            });
            BridgeTrace.Log("DispatchOpenChest invoked NTreasureRoom.OpenChest");
            return ("ok", "OpenChest invoked");
        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"NTreasureRoom.OpenChest threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Picks a relic from the opened treasure chest. Maps to
    /// <c>NTreasureRoomRelicCollection.PickRelic(NTreasureRoomRelicHolder)</c>.
    /// Args:
    ///   index (int, required): the holder index as exposed in
    ///     <c>state.treasure.relicChoices[].index</c>.
    /// Errors if the chest is not yet open or no holder matches the index.
    /// </summary>
    private static (string status, string message) DispatchSelectTreasureRelic(JsonElement command)
    {
        if (!command.TryGetProperty("index", out var idxEl) || idxEl.ValueKind != JsonValueKind.Number)
        {
            return ("error", "SelectTreasureRelic requires numeric 'index'");
        }
        int index = idxEl.GetInt32();

        var node = BridgeSingleton.CurrentTreasureNode;
        if (node is null || !Godot.GodotObject.IsInstanceValid(node))
        {
            return ("error", "no live NTreasureRoom captured");
        }

        try
        {
            var collectionField = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom).GetField(
                "_relicCollection",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var collection = collectionField?.GetValue(node) as NTreasureRoomRelicCollection;
            if (collection is null) return ("error", "NTreasureRoom._relicCollection is null (chest not opened?)");

            var holdersField = typeof(NTreasureRoomRelicCollection).GetField(
                "_holdersInUse",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (holdersField?.GetValue(collection) is not System.Collections.Generic.List<NTreasureRoomRelicHolder> holders)
            {
                return ("error", "_holdersInUse missing or wrong type");
            }
            if (holders.Count == 0) return ("error", "no relic holders available (empty chest?)");

            NTreasureRoomRelicHolder? match = null;
            foreach (var h in holders)
            {
                int hi;
                try { hi = h.Index; } catch { continue; }
                if (hi == index) { match = h; break; }
            }
            if (match is null)
            {
                var available = string.Join(",", holders.ConvertAll(h => { try { return h.Index.ToString(); } catch { return "?"; } }));
                return ("error", $"no holder with index {index}; available=[{available}]");
            }

            var pickMethod = typeof(NTreasureRoomRelicCollection).GetMethod(
                "PickRelic",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (pickMethod is null) return ("error", "could not resolve NTreasureRoomRelicCollection.PickRelic");
            pickMethod.Invoke(collection, new object?[] { match });
            BridgeTrace.Log($"DispatchSelectTreasureRelic invoked PickRelic(index={index})");
            return ("ok", $"picked relic at holder index {index}");        }
        catch (Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return ("error", $"PickRelic threw: {inner.GetType().Name}: {inner.Message}");
        }
    }

    /// <summary>
    /// Diagnostic: walks the live scene tree rooted at SceneTree.Root and logs
    /// every visible Control/Node whose type name matches a filter substring,
    /// or everything if no filter given. Logs to trace.log.
    /// Args:
    ///   filter (string, optional): substring match on type FullName (case-insensitive).
    ///     Default: matches "Room", "Screen", "Overlay", "Chest", "Relic", "Event", "Neow", "Map".
    ///   maxDepth (int, optional): recursion cap. Default 20.
    ///   onlyVisible (bool, optional): only log Control nodes with Visible=true. Default true.
    /// Safe to call anywhere; purely read-only.
    /// </summary>
    private static (string status, string message) DispatchDumpScene(JsonElement command)
    {
        string filter = "";
        if (command.TryGetProperty("filter", out var fEl) && fEl.ValueKind == JsonValueKind.String)
            filter = fEl.GetString() ?? "";
        int maxDepth = 20;
        if (command.TryGetProperty("maxDepth", out var dEl) && dEl.ValueKind == JsonValueKind.Number)
            maxDepth = dEl.GetInt32();
        bool onlyVisible = true;
        if (command.TryGetProperty("onlyVisible", out var vEl) && (vEl.ValueKind == JsonValueKind.True || vEl.ValueKind == JsonValueKind.False))
            onlyVisible = vEl.GetBoolean();

        // Default interesting-type filters.
        string[] defaultFilters = filter.Length == 0
            ? new[] { "Room", "Screen", "Overlay", "Chest", "Relic", "Event", "Neow", "Map", "Treasure", "Prologue", "Intro" }
            : new[] { filter };

        try
        {
            var ngame = NGame.Instance;
            Node? root = ngame?.GetTree()?.Root;
            if (root is null) return ("error", "no SceneTree.Root");

            int matchCount = 0;
            BridgeTrace.Log($"DumpScene BEGIN filters=[{string.Join(",", defaultFilters)}] onlyVisible={onlyVisible}");
            DumpSceneRecursive(root, 0, maxDepth, defaultFilters, onlyVisible, ref matchCount);
            BridgeTrace.Log($"DumpScene END matches={matchCount}");
            return ("ok", $"dumped {matchCount} matching nodes to trace.log");
        }
        catch (Exception ex)
        {
            return ("error", $"DumpScene threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void DumpSceneRecursive(Node? node, int depth, int maxDepth, string[] filters, bool onlyVisible, ref int matches)
    {
        if (node is null || depth > maxDepth) return;
        try
        {
            var tn = node.GetType().FullName ?? "?";
            bool typeMatch = false;
            foreach (var f in filters)
            {
                if (tn.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) { typeMatch = true; break; }
            }
            if (typeMatch)
            {
                bool visible = true;
                string nname = "";
                try { nname = node.Name.ToString(); } catch { }
                try
                {
                    if (node is Control c) visible = c.Visible;
                }
                catch { }
                if (!onlyVisible || visible)
                {
                    string pad = new string(' ', depth * 2);
                    BridgeTrace.Log($"DumpScene  {pad}[{depth}] {tn} name=\"{nname}\" visible={visible}");
                    matches++;
                }
            }
            foreach (var child in node.GetChildren())
            {
                DumpSceneRecursive(child, depth + 1, maxDepth, filters, onlyVisible, ref matches);
            }
        }
        catch { }
    }

    /// <summary>
    /// Diagnostic: dumps every entry in NMapScreen._mapPointDictionary to trace.log,
    /// including coord, NMapPoint subclass type, MapPointState, IsTravelable, and
    /// if available MapPoint.PointType. Used to find off-grid nodes like
    /// NAncientMapPoint that don't appear in actMap.GetAllMapPoints().
    /// </summary>
    private static (string status, string message) DispatchDumpMapPoints(JsonElement command)
    {
        try
        {
            var screen = NMapScreen.Instance;
            if (screen is null || !Godot.GodotObject.IsInstanceValid(screen))
                return ("error", "no active NMapScreen");

            var dictField = typeof(NMapScreen).GetField("_mapPointDictionary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dictField?.GetValue(screen) is not System.Collections.IDictionary dict)
                return ("error", "_mapPointDictionary not found or not an IDictionary");

            var travelProp = typeof(NMapPoint).GetProperty("IsTravelable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var stateProp = typeof(NMapPoint).GetProperty("State",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var pointProp = typeof(NMapPoint).GetProperty("Point",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            int count = 0;
            BridgeTrace.Log($"DumpMapPoints BEGIN dictCount={dict.Count}");
            foreach (System.Collections.DictionaryEntry e in dict)
            {
                try
                {
                    string keyStr = "?";
                    try
                    {
                        if (e.Key is MapCoord mc) keyStr = $"({mc.col},{mc.row})";
                        else keyStr = e.Key?.ToString() ?? "null";
                    }
                    catch { }

                    string typeStr = "?";
                    string stateStr = "?";
                    string travelStr = "?";
                    string pointTypeStr = "?";
                    if (e.Value is NMapPoint np)
                    {
                        typeStr = np.GetType().FullName ?? "?";
                        try { stateStr = stateProp?.GetValue(np)?.ToString() ?? "?"; } catch { }
                        try { travelStr = travelProp?.GetValue(np)?.ToString() ?? "?"; } catch { }
                        try
                        {
                            var mp = pointProp?.GetValue(np);
                            if (mp is MapPoint mmp)
                            {
                                try { pointTypeStr = mmp.PointType.ToString(); } catch { }
                            }
                        }
                        catch { }
                    }
                    BridgeTrace.Log($"DumpMapPoints  key={keyStr} type={typeStr} state={stateStr} travelable={travelStr} pointType={pointTypeStr}");
                    count++;
                }
                catch (Exception ex)
                {
                    BridgeTrace.Log($"DumpMapPoints  entry threw: {ex.Message}");
                }
            }

            // Also log NMapScreen flags relevant to travelability.
            try
            {
                var isTravelEnabledProp = typeof(NMapScreen).GetProperty("IsTravelEnabled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var isTravelingProp = typeof(NMapScreen).GetProperty("IsTraveling",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var debugTravelProp = typeof(NMapScreen).GetProperty("IsDebugTravelEnabled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                BridgeTrace.Log($"DumpMapPoints  NMapScreen.IsTravelEnabled={isTravelEnabledProp?.GetValue(screen)} IsTraveling={isTravelingProp?.GetValue(screen)} IsDebugTravelEnabled={debugTravelProp?.GetValue(screen)}");
            }
            catch (Exception ex) { BridgeTrace.Log($"DumpMapPoints screen flags threw: {ex.Message}"); }

            BridgeTrace.Log($"DumpMapPoints END count={count}");
            return ("ok", $"dumped {count} map points to trace.log");
        }
        catch (Exception ex)
        {
            return ("error", $"DumpMapPoints threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

// ---- Bug E2 diagnostic: dump NEventRoom / EventRoom shape once when
// DispatchProceed is invoked on an event. Goal: find the "pending continuation
// button" field so we can (a) surface it in state.json (Bug E1) and (b) click
// it from Proceed (Bug E2 fix) or at least refuse with a helpful error.
internal static class BugE2Diagnostic
{
    private static bool _emitted = false;
    public static void Emit(object eventRoom)
    {
        if (_emitted) return;
        _emitted = true;
        try
        {
            BridgeTrace.Log($"BugE2[diag]: eventRoom type = {eventRoom.GetType().FullName}");
            DumpMembers("eventRoom", eventRoom, depth: 0);

            // Also dump NEventRoom.Instance (Godot node) if alive.
            try
            {
                var nEvtType = typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom);
                var instProp = nEvtType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var nEvt = instProp?.GetValue(null);
                if (nEvt is not null)
                {
                    BridgeTrace.Log($"BugE2[diag]: NEventRoom.Instance type = {nEvt.GetType().FullName}");
                    DumpMembers("NEventRoom", nEvt, depth: 0);
                }
                else
                {
                    BridgeTrace.Log("BugE2[diag]: NEventRoom.Instance is null");
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugE2[diag] NEventRoom dump threw: {ex.Message}"); }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugE2[diag] Emit threw: {ex.Message}"); }
    }

    private static void DumpMembers(string tag, object obj, int depth)
    {
        if (obj is null || depth > 1) return;
        var t = obj.GetType();
        string pad = new string(' ', depth * 2);
        try
        {
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                object? val = null;
                try { val = f.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                BridgeTrace.Log($"BugE2[diag]{pad} {tag}.FIELD {f.FieldType.Name} {f.Name} = {SummarizeShort(val)}");
            }
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object? val = null;
                try { val = p.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                BridgeTrace.Log($"BugE2[diag]{pad} {tag}.PROP  {p.PropertyType.Name} {p.Name} = {SummarizeShort(val)}");
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugE2[diag] DumpMembers({tag}) threw: {ex.Message}"); }
    }

    private static string SummarizeShort(object? v)
    {
        if (v is null) return "<null>";
        try
        {
            if (v is string s) return $"\"{(s.Length > 120 ? s.Substring(0, 120) + "…" : s)}\"";
            if (v is System.Collections.ICollection c) return $"{v.GetType().Name}[Count={c.Count}]";
            var str = v.ToString() ?? "<null.ToString>";
            return str.Length > 120 ? str.Substring(0, 120) + "…" : str;
        }
        catch (Exception ex) { return $"<threw {ex.GetType().Name}>"; }
    }
}

// ---- Bug F diagnostic: log full potion shape + target type on UsePotion, once
// per distinct potion ID, to help figure out why "Touch of Insanity" (TargetType
// == Self) silently fails after EnqueueManualUse(player.Creature). Candidates:
// (a) follow-up ChooseACard is expected and we're not handling it;
// (b) EnqueueManualUse variant for Self-target differs;
// (c) the potion's Use() needs an alternate entry point.
internal static class BugFDiagnostic
{
    private static readonly System.Collections.Generic.HashSet<string> _seen = new();
    public static void OnEnqueue(object potion, object? target)
    {
        try
        {
            var idProp = potion.GetType().GetProperty("Id");
            var idObj = idProp?.GetValue(potion);
            var idEntry = idObj?.GetType().GetProperty("Entry")?.GetValue(idObj)?.ToString() ?? "<?>";
            if (!_seen.Add(idEntry)) return;
            var tt = "?"; try { tt = potion.GetType().GetProperty("TargetType")?.GetValue(potion)?.ToString() ?? "?"; } catch { }
            var targetName = "<null>";
            if (target is not null)
            {
                try { targetName = target.GetType().GetProperty("Name")?.GetValue(target)?.ToString() ?? "<?>"; } catch { }
            }
            BridgeTrace.Log($"BugF[diag]: potion={idEntry} targetType={tt} target={targetName} potionClass={potion.GetType().FullName}");
            var t = potion.GetType();
            foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var ps = m.GetParameters();
                BridgeTrace.Log($"BugF[diag]: {idEntry} METHOD {m.ReturnType.Name} {m.Name}({string.Join(",", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name + " " + p.Name))})");
            }
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                object? val = null;
                try { val = p.GetValue(potion); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                BridgeTrace.Log($"BugF[diag]: {idEntry} PROP {p.PropertyType.Name} {p.Name} = {val}");
            }
            // Walk base types for relevant entry points
            var bt = t.BaseType;
            while (bt is not null && bt != typeof(object))
            {
                BridgeTrace.Log($"BugF[diag]: {idEntry} base = {bt.FullName}");
                bt = bt.BaseType;
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugF[diag] threw: {ex.Message}"); }
    }
}

// ---- Bug U diagnostic: SpecialCardReward returns ok but deck doesn't gain
// the card. Logs one-shot reflection dump of the reward class (methods,
// properties, base-type chain) so we can figure out whether (a) OnSelect is
// a no-op, (b) it awaits a UI confirmation we're bypassing, or (c) the card
// was already added at construction time and OnSelect only handles cleanup.
// Also snapshots deck size before + a deferred check after to prove whether
// deck actually grows.
internal static class BugUDiagnostic
{
    private static readonly System.Collections.Generic.HashSet<string> _seen = new();

    public static void OnSelectReward(object reward)
    {
        try
        {
            var typeName = reward.GetType().Name;
            // Only instrument SpecialCardReward (and any other non-base/card/gold/potion/relic/removal type we haven't mapped).
            if (typeName != "SpecialCardReward") return;

            if (_seen.Add(typeName))
            {
                BridgeTrace.Log($"BugU[diag]: reward class = {reward.GetType().FullName}");
                var t = reward.GetType();
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    var ps = m.GetParameters();
                    BridgeTrace.Log($"BugU[diag]: METHOD {m.ReturnType.Name} {m.Name}({string.Join(",", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name + " " + p.Name))})");
                }
                foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    object? val = null;
                    try { val = p.GetValue(reward); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugU[diag]: PROP {p.PropertyType.Name} {p.Name} = {SummarizeShort(val)}");
                }
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    object? val = null;
                    try { val = f.GetValue(reward); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugU[diag]: FIELD {f.FieldType.Name} {f.Name} = {SummarizeShort(val)}");
                }
                var bt = t.BaseType;
                while (bt is not null && bt != typeof(object))
                {
                    BridgeTrace.Log($"BugU[diag]: base = {bt.FullName}");
                    bt = bt.BaseType;
                }
            }

            // Snapshot deck size + target card identity every time (cheap, useful).
            var playerProp = reward.GetType().GetProperty("Player", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var player = playerProp?.GetValue(reward);
            int beforeCount = -1;
            try
            {
                var runState = player?.GetType().GetProperty("RunState")?.GetValue(player);
                var deck = runState?.GetType().GetProperty("Deck")?.GetValue(runState);
                if (deck is System.Collections.IEnumerable en)
                {
                    int c = 0; foreach (var _ in en) c++; beforeCount = c;
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugU[diag] deck snapshot threw: {ex.Message}"); }

            // Try to read the reward's target card (commonly a Card/CardModel field).
            var cardInfo = "<?>";
            try
            {
                foreach (var p in reward.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (p.PropertyType.Name == "CardModel")
                    {
                        var c = p.GetValue(reward);
                        cardInfo = c?.GetType().GetProperty("Id")?.GetValue(c)?.ToString() ?? c?.ToString() ?? "<null>";
                        break;
                    }
                }
            }
            catch { }
            BridgeTrace.Log($"BugU[diag]: pre-select deckCount={beforeCount} card={cardInfo}");

            // Deferred deck re-check ~400ms later to see if OnSelect grew the deck.
            try
            {
                var timer = Godot.Engine.GetMainLoop() as Godot.SceneTree;
                if (timer is not null)
                {
                    timer.CreateTimer(0.4).Timeout += () =>
                    {
                        try
                        {
                            int afterCount = -1;
                            var runState = player?.GetType().GetProperty("RunState")?.GetValue(player);
                            var deck = runState?.GetType().GetProperty("Deck")?.GetValue(runState);
                            if (deck is System.Collections.IEnumerable en)
                            {
                                int c = 0; foreach (var _ in en) c++; afterCount = c;
                            }
                            BridgeTrace.Log($"BugU[diag]: post-select deckCount={afterCount} (delta={afterCount - beforeCount})");
                        }
                        catch (Exception ex) { BridgeTrace.Log($"BugU[diag] post-check threw: {ex.Message}"); }
                    };
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugU[diag] timer schedule threw: {ex.Message}"); }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugU[diag] OnSelectReward outer threw: {ex.Message}"); }
    }

    private static string SummarizeShort(object? v)
    {
        if (v is null) return "<null>";
        try
        {
            if (v is string s) return $"\"{(s.Length > 120 ? s.Substring(0, 120) + "…" : s)}\"";
            if (v is System.Collections.ICollection c) return $"{v.GetType().Name}[Count={c.Count}]";
            var str = v.ToString() ?? "<null.ToString>";
            return str.Length > 120 ? str.Substring(0, 120) + "…" : str;
        }
        catch (Exception ex) { return $"<threw {ex.GetType().Name}>"; }
    }
}
