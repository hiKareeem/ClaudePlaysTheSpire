using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// Pure extraction helpers. Never throws — each extractor catches internally
/// and returns a best-effort partial structure.
/// </summary>
internal static class BridgeStateExtractor
{
    public static string SafeModelId(ModelId id)
    {
        try { return $"{id.Category}:{id.Entry}"; }
        catch { return "<unknown>"; }
    }

    public static string SafeModelIdOrNull(ModelId? id)
    {
        if (id is null) return "<null>";
        try { return $"{id.Category}:{id.Entry}"; } catch { return "<err>"; }
    }

    /// <summary>
    /// Safely obtain CombatState. The game removed Creature.CombatState in a
    /// recent patch; this helper catches MissingMethodException and falls back
    /// gracefully. Prefer room?.CombatState as the primary source.
    /// </summary>
    public static CombatState? SafeGetCombatState(CombatRoom? room, Player? player)
    {
        if (room?.CombatState is CombatState cs)
            return cs;
        try
        {
            return player?.Creature?.CombatState as CombatState;
        }
        catch (MissingMethodException)
        {
            return null;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    public static string? SafeLocString(LocString? s)
    {
        if (s is null) return null;
        try { return s.GetFormattedText(); }
        catch
        {
            try { return s.GetRawText(); }
            catch { return null; }
        }
    }

    private static bool _bugTDiagFired;
    private static bool _bugTMethDiagFired; // re-dumps EC.METH0 after adding method scan 2026-04-20
    private static bool _bugTDiscountDiagFired;

    // Counter for state_inconsistent detection (protocol-v1 §Bridge changes #4).
    // Incremented each ExtractMap call where available[] is empty AND we're on
    // a non-boss row with a known currentCoord. Resets to 0 the first tick
    // any of those guards fail. Event emits when counter reaches 2.
    private static int _consecutiveEmptyMapTicks;
    private static void EmitBugTDiagnostic(CardModel card)
    {
        if (card is null) return;
        var ec = card.EnergyCost;
        if (ec is null) return;

        // Second-pass diagnostic: dump once when we first see a card with active modifiers
        if (!_bugTDiscountDiagFired)
        {
            try
            {
                var modField = ec.GetType().GetField("_localModifiers", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                if (modField?.GetValue(ec) is System.Collections.ICollection coll && coll.Count > 0)
                {
                    _bugTDiscountDiagFired = true;
                    BridgeTrace.Log($"BugT[diag2]: card={SafeModelId(card.Id)} has {coll.Count} local modifier(s)");
                    int i = 0;
                    foreach (var mod in coll)
                    {
                        BridgeTrace.Log($"BugT[diag2]:   mod[{i}] type={mod?.GetType().FullName}");
                        if (mod is not null)
                        {
                            foreach (var p in mod.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                            {
                                if (p.GetIndexParameters().Length > 0) continue;
                                try { BridgeTrace.Log($"BugT[diag2]:     PROP {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(mod))}"); } catch { }
                            }
                            foreach (var f in mod.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                            {
                                try { BridgeTrace.Log($"BugT[diag2]:     FIELD {f.FieldType.Name} {f.Name} = {Summarize(f.GetValue(mod))}"); } catch { }
                            }
                        }
                        i++;
                    }
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugT[diag2] threw: {ex.Message}"); }
        }

        if (_bugTDiagFired && _bugTMethDiagFired) return;
        try
        {
            if (!_bugTMethDiagFired)
            {
                _bugTMethDiagFired = true;
                foreach (var mm in ec.GetType().GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
                {
                    if (mm.IsSpecialName) continue;
                    if (mm.GetParameters().Length != 0) continue;
                    if (mm.DeclaringType == typeof(object)) continue;
                    try { BridgeTrace.Log($"BugT[diag]: EC.METH0 {mm.ReturnType.Name} {mm.Name}() = {Summarize(mm.Invoke(ec, null))}"); } catch { }
                }
            }
            if (_bugTDiagFired) return;
            _bugTDiagFired = true;
            foreach (var f in ec.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                try { BridgeTrace.Log($"BugT[diag]: EC.FIELD {f.FieldType.Name} {f.Name} = {Summarize(f.GetValue(ec))}"); } catch { }
            }
            foreach (var p in ec.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                try { BridgeTrace.Log($"BugT[diag]: EC.PROP  {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(ec))}"); } catch { }
            }
            // Scan CardModel for cost-related members
            var ct = card.GetType();
            foreach (var m in ct.GetMembers(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                var n = m.Name;
                if (n.IndexOf("cost", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("discount", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (m is System.Reflection.PropertyInfo pp && pp.GetIndexParameters().Length == 0)
                {
                    try { BridgeTrace.Log($"BugT[diag]: CARD.PROP  {pp.PropertyType.Name} {pp.Name} = {Summarize(pp.GetValue(card))}"); } catch { }
                }
                else if (m is System.Reflection.FieldInfo ff)
                {
                    try { BridgeTrace.Log($"BugT[diag]: CARD.FIELD {ff.FieldType.Name} {ff.Name} = {Summarize(ff.GetValue(card))}"); } catch { }
                }
                else if (m is System.Reflection.MethodInfo mm && mm.GetParameters().Length == 0 && !mm.IsSpecialName)
                {
                    try { BridgeTrace.Log($"BugT[diag]: CARD.METH0 {mm.ReturnType.Name} {mm.Name}() = {Summarize(mm.Invoke(card, null))}"); } catch { }
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugT[diag] threw: {ex.Message}"); }
    }

    private static readonly System.Collections.Generic.HashSet<Type> _bugFlagsDiagSeen = new();
    private static void EmitBugFlagsDiagnostic(CardModel card)
    {
        if (card is null) return;
        var t0 = card.GetType();
        lock (_bugFlagsDiagSeen)
        {
            if (_bugFlagsDiagSeen.Count >= 8) return; // cap total classes dumped
            if (!_bugFlagsDiagSeen.Add(t0)) return;
        }
        try
        {
            var t = card.GetType();
            BridgeTrace.Log($"BugFlags[diag]: card={SafeModelId(card.Id)} type={t.FullName}");
            // Dump all bool-typed props/fields (both auto and computed)
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.PropertyType != typeof(bool)) continue;
                try { BridgeTrace.Log($"BugFlags[diag]: PROP bool {p.Name} = {p.GetValue(card)}"); } catch { }
            }
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (f.FieldType != typeof(bool)) continue;
                try { BridgeTrace.Log($"BugFlags[diag]: FIELD bool {f.Name} = {f.GetValue(card)}"); } catch { }
            }
            // And all string/enum props matching "target"
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.Name.IndexOf("target", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { BridgeTrace.Log($"BugFlags[diag]: PROP {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(card))}"); } catch { }
            }
            // Look for keyword/trait/tag/ethereal/exhaust/innate/retain/curse/status members (any type, non-bool — bools already dumped above)
            var kwRegex = new System.Text.RegularExpressions.Regex(
                "keyword|trait|tag|ethereal|exhaust|innate|retain|curse|status|behavior|flag|effect",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.PropertyType == typeof(bool)) continue; // already dumped
                if (!kwRegex.IsMatch(p.Name)) continue;
                try { BridgeTrace.Log($"BugFlags[diag]: KW.PROP  {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(card))}"); } catch { }
            }
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (f.FieldType == typeof(bool)) continue;
                if (!kwRegex.IsMatch(f.Name)) continue;
                try { BridgeTrace.Log($"BugFlags[diag]: KW.FIELD {f.FieldType.Name} {f.Name} = {Summarize(f.GetValue(card))}"); } catch { }
            }
            // Dump the concrete card class name (for identifying derived classes like EtherealStrike etc.)
            try { BridgeTrace.Log($"BugFlags[diag]: CLASS {t.FullName} Type={card.Type} Rarity={card.Rarity}"); } catch { }
            // Dump parent class chain
            try
            {
                var bt = t.BaseType;
                int depth = 0;
                while (bt != null && depth < 6)
                {
                    BridgeTrace.Log($"BugFlags[diag]: BASE[{depth}] {bt.FullName}");
                    bt = bt.BaseType;
                    depth++;
                }
            }
            catch { }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugFlags[diag] threw: {ex.Message}"); }
    }

    public static object ExtractCard(CardModel card, int? handIndex = null)
    {
        try
        {
            EmitBugTDiagnostic(card);
            // EmitBugFlagsDiagnostic(card); // disabled — findings captured 2026-04-20, keywords live in Keywords/CanonicalKeywords collections
            int energyCost = -1;
            int effectiveEnergyCost = -1;
            bool costsX = false;
            bool isPlayable = false;
            bool playabilityResolved = false;
            try
            {
                if (card.EnergyCost is not null)
                {
                    energyCost = card.EnergyCost.Canonical;
                    costsX = card.EnergyCost.CostsX;
                    // Authoritative effective cost after discounts/modifiers. This is what the
                    // combat engine itself calls when spending energy (see CardEnergyCost).
                    // Strongly-typed path not available on some builds, so we reflect.
                    try
                    {
                        var m = card.EnergyCost.GetType().GetMethod("GetAmountToSpend",
                            System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                        if (m != null && m.GetParameters().Length == 0)
                        {
                            var r = m.Invoke(card.EnergyCost, null);
                            if (r is int ri) effectiveEnergyCost = ri;
                        }
                    }
                    catch { }
                    if (effectiveEnergyCost < 0) effectiveEnergyCost = energyCost; // fall back to canonical
                }
                else
                {
                    energyCost = ReflectInt(card, "CanonicalEnergyCost");
                    effectiveEnergyCost = energyCost;
                }
            }
            catch { }

            try
            {
                isPlayable = card.CanPlay();
                playabilityResolved = true;
            }
            catch { }

            if (!playabilityResolved)
            {
                try { isPlayable = ReflectBool(card, "IsPlayable"); }
                catch { }
            }

            object? enchantment = null;
            try
            {
                if (card.Enchantment is not null)
                {
                    enchantment = ExtractEnchantment(card.Enchantment);
                }
            }
            catch { }

            object? affliction = null;
            try
            {
                if (card.Affliction is not null)
                {
                    affliction = ExtractAffliction(card.Affliction);
                }
            }
            catch { }

            string? description = null;
            try { description = card.GetDescriptionForPile(MegaCrit.Sts2.Core.Entities.Cards.PileType.None); }
            catch
            {
                try { description = SafeLocString(card.Description); } catch { }
            }

            return new
            {
                id = SafeModelId(card.Id),
                title = card.Title,
                description,
                type = TryEnum(() => card.Type.ToString()),
                rarity = TryEnum(() => card.Rarity.ToString()),
                energyCost,
                effectiveEnergyCost,
                costsX,
                baseStarCost = TryInt(() => card.BaseStarCost),
                currentStarCost = TryInt(() => card.CurrentStarCost),
                isPlayable,
                isUpgraded = TryBool(() => card.IsUpgraded),
                isUpgradable = TryBool(() => card.IsUpgradable),
                currentUpgradeLevel = TryInt(() => card.CurrentUpgradeLevel),
                pile = TryEnum(() => card.Pile?.Type.ToString()),
                handIndex,
                // Card behavior flags. Ethereal/Innate/Retain/Exhaust in StS2 live in the
                // Keywords collection (not as static CardModel bools as in StS1). We also
                // surface the runtime ExhaustOnNextPlay / ShouldRetainThisTurn signals which
                // are true when a power/relic has marked this specific instance for exhaust/retain
                // on its next play (independent of the card's intrinsic keyword).
                ethereal = CardHasKeyword(card, "Ethereal"),
                innate = CardHasKeyword(card, "Innate"),
                retain = CardHasKeyword(card, "Retain"),
                exhaust = CardHasKeyword(card, "Exhaust"),
                willExhaust = ReflectBool(card, "ExhaustOnNextPlay"),
                willRetain = ReflectBool(card, "ShouldRetainThisTurn"),
                // Curse/Status/Affliction flags: try keyword lookup first (unobserved in basic
                // cards but likely the right home), fall back to reflected bool for safety.
                isCurse = CardHasKeyword(card, "Curse") || ReflectBool(card, "IsCurse"),
                isStatus = CardHasKeyword(card, "Status") || ReflectBool(card, "IsStatus"),
                isAffliction = ReflectBool(card, "IsAffliction"),
                targetType = TryEnum(() => GetProp(card, "TargetType")?.ToString()),
                tags = TryEnumerableStrings(card, "Tags"),
                keywords = TryEnumerableStrings(card, "Keywords"),
                enchantment,
                affliction,
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public static object ExtractEnchantment(EnchantmentModel enchantment)
    {
        try
        {
            return new
            {
                title = SafeLocString(enchantment.Title),
                dynamicDescription = SafeLocString(enchantment.DynamicDescription),
                amount = TryInt(() => enchantment.Amount),
                status = TryEnum(() => enchantment.Status.ToString()),
                shouldGlowGold = TryBool(() => enchantment.ShouldGlowGold),
                shouldGlowRed = TryBool(() => enchantment.ShouldGlowRed),
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public static object ExtractAffliction(AfflictionModel affliction)
    {
        try
        {
            return new
            {
                title = SafeLocString(affliction.Title),
                dynamicDescription = SafeLocString(affliction.DynamicDescription),
                amount = TryInt(() => affliction.Amount),
                canAfflictUnplayableCards = TryBool(() => affliction.CanAfflictUnplayableCards),
                isStackable = TryBool(() => affliction.IsStackable),
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public static object ExtractRelic(RelicModel relic)
    {
        try
        {
            string? description = null;
            try { description = SafeLocString(relic.DynamicDescription); }
            catch { }
            return new
            {
                id = SafeModelId(relic.Id),
                title = SafeLocString(relic.Title),
                description,
                rarity = relic.Rarity.ToString(),
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private static RelicModel? ReflectRelicRewardRelic(RelicReward reward)
    {
        try
        {
            var field = typeof(RelicReward).GetField("_relic",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(reward) as RelicModel;
        }
        catch
        {
            return null;
        }
    }

    public static object ExtractPotion(PotionModel potion)
    {
        try
        {
            string? description = null;
            try { description = SafeLocString(potion.DynamicDescription); }
            catch { }
            return new
            {
                id = SafeModelId(potion.Id),
                title = SafeLocString(potion.Title),
                description,
                rarity = potion.Rarity.ToString(),
                targetType = TryEnum(() => potion.TargetType.ToString()),
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public static object ExtractCardGridSelection(
        MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen? screen,
        IReadOnlyList<CardModel>? cards)
    {
        try
        {
            var cardList = new List<object>();
            if (cards is not null)
            {
                int i = 0;
                foreach (var c in cards)
                {
                    cardList.Add(new { index = i++, card = ExtractCard(c) });
                }
            }

            var screenType = screen?.GetType().Name ?? "<null>";
            // NSimpleCardSelectScreen: single click auto-completes (used by shop
            // card removal, Smith, Dew Gaze, etc). NDeckCardSelectScreen:
            // requires explicit ConfirmSelection() after picking N cards.
            var requiresConfirm = screenType == "NDeckCardSelectScreen";
            var isSimple = screenType == "NSimpleCardSelectScreen";

            return new
            {
                visible = screen is not null,
                screenType,
                requiresConfirm,
                isSimple,
                cardCount = cardList.Count,
                cards = cardList,
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>
    /// Extracts the in-hand card selection state driven by
    /// <c>NPlayerHand.SelectCards</c> (mode SimpleSelect or UpgradeSelect).
    /// Used by cards like Burning Pact (exhaust-1), Armaments (upgrade-1),
    /// and potions like Gambler's Brew (discard-N). The selector UI *is*
    /// the hand itself — no overlay screen.
    ///
    /// The <c>handIndex</c> field on each entry matches the card's position
    /// in <c>combat.hand.cards</c> so Hermes can issue
    /// <c>HandSelectCard { handIndex }</c> consistently.
    /// </summary>
    public static object? ExtractHandSelect(
        MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand? hand,
        IReadOnlyList<CardModel>? handCards,
        System.Func<CardModel, bool>? filter,
        IReadOnlyList<CardModel>? selectedCards,
        object? prefsBoxed,
        string? modeName,
        string? prompt)
    {
        if (hand is null) return null;
        try
        {
            // Unpack CardSelectorPrefs (struct) via reflection so we avoid a
            // direct ref to the Cecil-private struct type in every caller.
            int minSelect = 1, maxSelect = 1;
            bool cancelable = false, requireManualConfirmation = false;
            bool unpoweredPreviews = false, pretendPlayable = false;
            if (prefsBoxed is not null)
            {
                var pt = prefsBoxed.GetType();
                minSelect = TryInt(() => (int)(pt.GetProperty("MinSelect")?.GetValue(prefsBoxed) ?? 1));
                maxSelect = TryInt(() => (int)(pt.GetProperty("MaxSelect")?.GetValue(prefsBoxed) ?? 1));
                cancelable = (pt.GetProperty("Cancelable")?.GetValue(prefsBoxed) as bool?) ?? false;
                requireManualConfirmation = (pt.GetProperty("RequireManualConfirmation")?.GetValue(prefsBoxed) as bool?) ?? false;
                unpoweredPreviews = (pt.GetProperty("UnpoweredPreviews")?.GetValue(prefsBoxed) as bool?) ?? false;
                pretendPlayable = (pt.GetProperty("PretendCardsCanBePlayed")?.GetValue(prefsBoxed) as bool?) ?? false;
            }

            var cards = new List<object>();
            var selectedSet = new HashSet<CardModel>(selectedCards ?? (IReadOnlyList<CardModel>)Array.Empty<CardModel>(), ReferenceEqualityComparer.Instance);
            if (handCards is not null)
            {
                for (int i = 0; i < handCards.Count; i++)
                {
                    var c = handCards[i];
                    bool selectable;
                    try { selectable = filter is null || filter(c); }
                    catch { selectable = false; }
                    cards.Add(new
                    {
                        handIndex = i,
                        selectable,
                        selected = selectedSet.Contains(c),
                        card = ExtractCard(c),
                    });
                }
            }

            // Selected cards may have been moved out of the hand container
            // (into _selectedHandCardContainer). Surface them even if they no
            // longer appear in handCards so Hermes can still see them / deselect.
            var selectedList = new List<object>();
            if (selectedCards is not null)
            {
                foreach (var c in selectedCards)
                {
                    selectedList.Add(ExtractCard(c));
                }
            }

            return new
            {
                active = true,
                mode = modeName,
                prompt,
                minSelect,
                maxSelect,
                cancelable,
                requireManualConfirmation,
                unpoweredPreviews,
                pretendPlayable,
                selectedCount = selectedList.Count,
                canAutoComplete = (minSelect == maxSelect && modeName != "UpgradeSelect"),
                cards,
                selected = selectedList,
            };
        }
        catch (Exception ex)
        {
            return new { active = true, error = ex.Message };
        }
    }

    /// <summary>
    /// Extracts the card list presented by an <c>NChooseACardSelectionScreen</c>
    /// (the overlay opened by <c>CardSelectCmd.FromChooseACardScreen</c>). Each
    /// card's <c>index</c> matches the child order of the screen's <c>_cardRow</c>
    /// Godot control — the value Hermes passes as <c>cardIndex</c> to the
    /// <c>ChooseACard</c> command.
    /// </summary>
    public static object ExtractChooseACardScreen(
        MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen? screen,
        IReadOnlyList<CardModel>? cards,
        bool canSkip)
    {
        try
        {
            var cardList = new List<object>();
            if (cards is not null)
            {
                int i = 0;
                foreach (var c in cards)
                {
                    cardList.Add(new { index = i++, card = ExtractCard(c) });
                }
            }

            return new
            {
                visible = screen is not null,
                canSkip,
                cardCount = cardList.Count,
                cards = cardList,
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    public static object ExtractPower(PowerModel power)
    {
        try
        {
            string? description = null;
            try { description = power.DumbHoverTip.Description; }
            catch
            {
                try { description = SafeLocString(power.Description); } catch { }
            }
            return new
            {
                id = SafeModelId(power.Id),
                title = SafeLocString(power.Title),
                description,
                amount = TryInt(() => power.Amount),
                type = TryEnum(() => power.Type.ToString()),
                isVisible = TryBool(() => power.IsVisible),
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    public static object? ExtractIntent(AbstractIntent intent, Creature? owner = null)
    {
        if (intent is null) return null;
        try
        {
            int? damage = null;
            int? repeats = null;
            if (intent is AttackIntent atk)
            {
                // DamageCalc is Func<decimal?> (or similar nullable numeric); invoke reflectively
                // and coerce to int to avoid taking a hard dependency on the precise type.
                try
                {
                    var dc = atk.DamageCalc;
                    if (dc is not null)
                    {
                        var raw = dc.DynamicInvoke();
                        if (raw is not null)
                        {
                            try { damage = Convert.ToInt32(raw); } catch { }
                        }
                    }
                }
                catch { }
                try { repeats = atk.Repeats; } catch { }

                // Fold owner's Strength into displayed per-hit damage so consumers
                // see effective damage. The underlying DamageCalc returns base
                // (pre-buff) damage for attack intents; AI/automation using the
                // raw value consistently under-estimates incoming damage.
                if (damage.HasValue && owner?.Powers is not null)
                {
                    try
                    {
                        foreach (var p in owner.Powers)
                        {
                            var pid = p?.Id?.Entry;
                            if (string.Equals(pid, "STRENGTH_POWER", StringComparison.OrdinalIgnoreCase))
                            {
                                damage = damage.Value + p!.Amount;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            var titleObj = GetProp(intent, "IntentTitle") as LocString;
            var prefix = ReflectString(intent, "IntentPrefix");

            // Render the intent's full UI label/description so consumers can read
            // preview values the intent object itself doesn't expose as typed fields.
            // In particular, DefendIntent / BuffIntent / DebuffIntent have no
            // numeric Amount property — the block/buff preview is baked into the
            // LocString returned by GetIntentLabel/GetIntentDescription.
            // Example: DefendIntent label -> "Gain 8 Block".
            string? label = null;
            string? description = null;
            try
            {
                IEnumerable<Creature>? opponents = null;
                try
                {
                    var cs = owner?.CombatState;
                    if (cs is not null && owner is not null)
                    {
                        opponents = cs.GetOpponentsOf(owner);
                    }
                }
                catch { }
                if (opponents is null)
                {
                    // Fallback: self-group so reflective invoke still succeeds.
                    opponents = owner is null ? Array.Empty<Creature>() : new[] { owner };
                }

                try
                {
                    var ll = CallMethod(intent, "GetIntentLabel", opponents, owner!) as LocString;
                    label = SafeLocString(ll);
                }
                catch { }
                try
                {
                    var dd = CallMethod(intent, "GetIntentDescription", opponents, owner!) as LocString;
                    description = SafeLocString(dd);
                }
                catch { }
            }
            catch { }

            return new
            {
                kind = intent.GetType().Name,
                intentType = TryEnum(() => intent.IntentType.ToString()),
                title = SafeLocString(titleObj),
                prefix,
                damage,
                repeats,
                label,
                description,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    public static object ExtractCreature(Creature c)
    {
        try
        {
            var intents = new List<object?>();
            string? nextMoveId = null;
            try
            {
                if (c.Monster is not null)
                {
                    // Prefer MonsterModel.NextMove (MoveState).Intents (IReadOnlyList<AbstractIntent>).
                    var nextMove = GetProp(c.Monster, "NextMove");
                    if (nextMove is not null)
                    {
                        nextMoveId = GetProp(nextMove, "Id") as string;
                        if (GetProp(nextMove, "Intents") is IEnumerable moveIntents)
                        {
                            foreach (var i in moveIntents)
                                if (i is AbstractIntent ai) intents.Add(ExtractIntent(ai, c));
                        }
                    }

                    // Fallback to GetIntents() if NextMove produced nothing.
                    if (intents.Count == 0)
                    {
                        var list = CallMethod(c.Monster, "GetIntents") as IEnumerable;
                        if (list is not null)
                            foreach (var i in list)
                                if (i is AbstractIntent ai) intents.Add(ExtractIntent(ai, c));
                    }
                }
            }
            catch (Exception ex) { intents.Add(new { error = ex.Message }); }

            var powers = new List<object>();
            try
            {
                if (c.Powers is not null)
                    foreach (var p in c.Powers) powers.Add(ExtractPower(p));
            }
            catch (Exception ex) { powers.Add(new { error = ex.Message }); }

            return new
            {
                id = SafeModelIdOrNull(c.ModelId),
                name = TryString(() => c.Name),
                isPlayer = TryBool(() => c.IsPlayer),
                isMonster = TryBool(() => c.IsMonster),
                isAlive = TryBool(() => c.IsAlive),
                isHittable = TryBool(() => c.IsHittable),
                currentHp = TryInt(() => c.CurrentHp),
                maxHp = TryInt(() => c.MaxHp),
                block = TryInt(() => c.Block),
                side = TryEnum(() => c.Side.ToString()),
                slotName = TryString(() => c.SlotName),
                combatId = TryNullableUInt(() => c.CombatId),
                powers,
                intents,
                nextMoveId,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    public static object ExtractPile(CardPile? pile, bool includeHandIndices = false)
    {
        if (pile is null) return new { cards = Array.Empty<object>(), count = 0 };
        try
        {
            var cards = new List<object>();
            if (pile.Cards is not null)
            {
                int index = 0;
                foreach (var c in pile.Cards)
                {
                    cards.Add(ExtractCard(c, includeHandIndices ? index : null));
                    index++;
                }
            }
            return new
            {
                type = TryEnum(() => pile.Type.ToString()),
                count = cards.Count,
                cards,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    /// <summary>
    /// Extract full combat state from a player's perspective. Pulls enemies
    /// from CombatState, HP/block from player's Creature, energy from
    /// PlayerCombatState, and piles (hand/draw/discard/exhaust).
    /// </summary>
    private static bool _bugOstyDiagnosticEmitted = false;

    private static void EmitOstyDiagnostic(object? combatState, Player? player, CombatRoom? room)
    {
        if (_bugOstyDiagnosticEmitted) return;
        if (combatState is null) return;
        _bugOstyDiagnosticEmitted = true;
        try
        {
            var targets = new (string label, object? obj)[]
            {
                ("CombatState", combatState),
                ("Player", (object?)player),
                ("Player.Creature", (object?)(player?.Creature)),
                ("CombatRoom", (object?)room),
            };
            var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var keywords = new[] { "ally", "allie", "minion", "summon", "osty", "creature", "pet", "companion", "friend" };
            foreach (var (label, obj) in targets)
            {
                if (obj is null) { BridgeTrace.Log($"BugOsty[diag]: {label} is null"); continue; }
                var t = obj.GetType();
                BridgeTrace.Log($"BugOsty[diag]: {label} type={t.FullName}");
                foreach (var f in t.GetFields(bf))
                {
                    var lname = f.Name.ToLowerInvariant();
                    if (!keywords.Any(k => lname.Contains(k))) continue;
                    object? val = null;
                    try { val = f.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugOsty[diag]: {label}.FIELD {f.FieldType.Name} {f.Name} = {Summarize(val)}");
                }
                foreach (var p in t.GetProperties(bf))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    var lname = p.Name.ToLowerInvariant();
                    if (!keywords.Any(k => lname.Contains(k))) continue;
                    object? val = null;
                    try { val = p.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugOsty[diag]: {label}.PROP {p.PropertyType.Name} {p.Name} = {Summarize(val)}");
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugOsty[diag] threw: {ex.Message}"); }
    }

    private static bool _bugOrbDiagnosticEmitted;
    private static bool _bugOrbQueueDumped;
    private static bool _bugOrbItemDumped;
    private static void EmitOrbDiagnostic(object? combatState, Player? player)
    {
        // Third pass: dump the first individual orb's shape (id, passive/evoke, type, refs).
        if (!_bugOrbItemDumped && player?.PlayerCombatState is not null)
        {
            try
            {
                var oq = player.PlayerCombatState.GetType().GetProperty("OrbQueue")?.GetValue(player.PlayerCombatState);
                if (oq is not null)
                {
                    var orbsProp = oq.GetType().GetProperty("Orbs");
                    var orbs = orbsProp?.GetValue(oq) as System.Collections.IEnumerable;
                    object? first = null;
                    if (orbs is not null) foreach (var o in orbs) { first = o; break; }
                    if (first is not null)
                    {
                        _bugOrbItemDumped = true;
                        var t = first.GetType();
                        BridgeTrace.Log($"BugOrb[diag3]: Orb type={t.FullName}");
                        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
                        {
                            try { BridgeTrace.Log($"BugOrb[diag3]:   FIELD {f.FieldType.Name} {f.Name} = {Summarize(f.GetValue(first))}"); } catch { }
                        }
                        foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
                        {
                            if (p.GetIndexParameters().Length > 0) continue;
                            try { BridgeTrace.Log($"BugOrb[diag3]:   PROP  {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(first))}"); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugOrb[diag3] threw: {ex.Message}"); }
        }

        // Second pass: once we see OrbQueue with non-empty contents, dump its shape.
        if (!_bugOrbQueueDumped && player?.PlayerCombatState is not null)
        {
            try
            {
                var pcs = player.PlayerCombatState;
                var oqProp = pcs.GetType().GetProperty("OrbQueue");
                var oq = oqProp?.GetValue(pcs);
                if (oq is not null)
                {
                    // Check any collection-like property for non-empty contents
                    var oqType = oq.GetType();
                    bool hasContent = false;
                    foreach (var p in oqType.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                    {
                        if (p.GetIndexParameters().Length > 0) continue;
                        object? v; try { v = p.GetValue(oq); } catch { continue; }
                        if (v is System.Collections.ICollection c && c.Count > 0) { hasContent = true; break; }
                    }
                    if (hasContent)
                    {
                        _bugOrbQueueDumped = true;
                        BridgeTrace.Log($"BugOrb[diag2]: OrbQueue type={oqType.FullName}");
                        foreach (var f in oqType.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                        {
                            try { BridgeTrace.Log($"BugOrb[diag2]:   FIELD {f.FieldType.Name} {f.Name} = {Summarize(f.GetValue(oq))}"); } catch { }
                        }
                        foreach (var p in oqType.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
                        {
                            if (p.GetIndexParameters().Length > 0) continue;
                            try { BridgeTrace.Log($"BugOrb[diag2]:   PROP  {p.PropertyType.Name} {p.Name} = {Summarize(p.GetValue(oq))}"); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugOrb[diag2] threw: {ex.Message}"); }
        }

        if (_bugOrbDiagnosticEmitted) return;
        if (combatState is null && player is null) return;
        _bugOrbDiagnosticEmitted = true;
        try
        {
            var targets = new (string label, object? obj)[]
            {
                ("CombatState", combatState),
                ("Player", (object?)player),
                ("Player.Creature", (object?)(player?.Creature)),
                ("Player.PlayerCombatState", (object?)(player?.PlayerCombatState)),
            };
            var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var keywords = new[] { "orb", "channel", "evoke", "focus" };
            foreach (var (label, obj) in targets)
            {
                if (obj is null) { BridgeTrace.Log($"BugOrb[diag]: {label} is null"); continue; }
                var t = obj.GetType();
                BridgeTrace.Log($"BugOrb[diag]: {label} type={t.FullName}");
                foreach (var f in t.GetFields(bf))
                {
                    var lname = f.Name.ToLowerInvariant();
                    if (!keywords.Any(k => lname.Contains(k))) continue;
                    object? val = null;
                    try { val = f.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugOrb[diag]: {label}.FIELD {f.FieldType.Name} {f.Name} = {Summarize(val)}");
                }
                foreach (var p in t.GetProperties(bf))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    var lname = p.Name.ToLowerInvariant();
                    if (!keywords.Any(k => lname.Contains(k))) continue;
                    object? val = null;
                    try { val = p.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugOrb[diag]: {label}.PROP {p.PropertyType.Name} {p.Name} = {Summarize(val)}");
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugOrb[diag] threw: {ex.Message}"); }
    }

    public static object ExtractCombat(Player player, CombatRoom? room)
    {
        try
        {
            var pcs = player?.PlayerCombatState;
            var combatState = SafeGetCombatState(room, player);
            EmitOrbDiagnostic(combatState, player);

            var enemies = new List<object>();
            try
            {
                if (combatState?.Enemies is not null)
                    foreach (var e in combatState.Enemies) enemies.Add(ExtractCreature(e));
            }
            catch (Exception ex) { enemies.Add(new { error = ex.Message }); }

            object? playerCreature = null;
            try
            {
                if (player?.Creature is not null) playerCreature = ExtractCreature(player.Creature);
            }
            catch (Exception ex) { playerCreature = new { error = ex.Message }; }

            // Allies: player-side creatures other than the player (e.g.
            // Necrobinder's Osty, other summons/pets). CombatState.Allies
            // contains the player's own Creature — filter it out. Fall back
            // to Player.Creature.Pets if Allies isn't populated.
            var allies = new List<object>();
            try
            {
                var selfCreature = player?.Creature;
                var allyList = combatState?.Allies as IEnumerable;
                if (allyList is not null)
                {
                    foreach (var a in allyList)
                    {
                        if (a is Creature cr && !ReferenceEquals(cr, selfCreature))
                            allies.Add(ExtractCreature(cr));
                    }
                }
                if (allies.Count == 0 && selfCreature is not null)
                {
                    var pets = GetProp(selfCreature, "Pets") as IEnumerable;
                    if (pets is not null)
                    {
                        foreach (var p in pets)
                            if (p is Creature cr) allies.Add(ExtractCreature(cr));
                    }
                }
            }
            catch (Exception ex) { allies.Add(new { error = ex.Message }); }

            // Orbs: Defect-only (and any char with OrbQueue). Pull from
            // Player.PlayerCombatState.OrbQueue via reflection — OrbQueue
            // exposes Orbs (IReadOnlyList<Orb>) and Capacity (int).
            List<object>? orbs = null;
            int orbCapacity = -1;
            try
            {
                if (pcs is not null)
                {
                    var oq = GetProp(pcs, "OrbQueue");
                    if (oq is not null)
                    {
                        orbCapacity = (GetProp(oq, "Capacity") as int?) ?? -1;
                        var orbList = GetProp(oq, "Orbs") as IEnumerable;
                        if (orbList is not null)
                        {
                            orbs = new List<object>();
                            int i = 0;
                            foreach (var o in orbList)
                            {
                                if (o is null) continue;
                                orbs.Add(ExtractOrb(o, i++));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { orbs = new List<object> { new { error = ex.Message } }; }

            return new
            {
                encounter = SafeModelIdOrNull(room?.Encounter?.Id),
                roundNumber = TryInt(() => combatState?.RoundNumber ?? -1),
                currentSide = TryEnum(() => combatState?.CurrentSide.ToString()),
                energy = TryInt(() => pcs?.Energy ?? -1),
                maxEnergy = TryInt(() => pcs?.MaxEnergy ?? -1),
                stars = TryInt(() => pcs?.Stars ?? -1),
                player = playerCreature,
                enemies,
                allies,
                orbs,
                orbCapacity,
                hand = ExtractPile(pcs?.Hand, includeHandIndices: true),
                drawPile = ExtractPile(pcs?.DrawPile),
                discardPile = ExtractPile(pcs?.DiscardPile),
                exhaustPile = ExtractPile(pcs?.ExhaustPile),
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    /// <summary>
    /// Extract a Defect orb (LightningOrb / FrostOrb / DarkOrb / PlasmaOrb ...).
    /// Orbs expose Id, PassiveVal, EvokeVal, and a resolved DumbHoverTip.
    /// </summary>
    private static object ExtractOrb(object orb, int index)
    {
        try
        {
            string? id = null;
            try
            {
                var idObj = GetProp(orb, "Id");
                if (idObj is not null) id = idObj.ToString();
            } catch { }

            string? title = null;
            try
            {
                var titleLoc = GetProp(orb, "Title") as LocString;
                if (titleLoc is not null) title = SafeLocString(titleLoc);
            } catch { }

            string? description = null;
            try
            {
                var hoverTipObj = GetProp(orb, "DumbHoverTip");
                if (hoverTipObj is not null)
                {
                    var descProp = hoverTipObj.GetType().GetProperty("Description");
                    description = descProp?.GetValue(hoverTipObj)?.ToString();
                }
                if (string.IsNullOrEmpty(description))
                {
                    var sd = GetProp(orb, "SmartDescription") as LocString;
                    if (sd is not null) description = SafeLocString(sd);
                }
            } catch { }

            decimal passiveVal = 0m, evokeVal = 0m;
            try { var v = GetProp(orb, "PassiveVal"); if (v is decimal pd) passiveVal = pd; } catch { }
            try { var v = GetProp(orb, "EvokeVal"); if (v is decimal ed) evokeVal = ed; } catch { }

            return new
            {
                index,
                id,
                title,
                description,
                passiveVal,
                evokeVal,
            };
        }
        catch (Exception ex) { return new { index, error = ex.Message }; }
    }

    /// <summary>
    /// Extract run-level state: floor, act, map position, relics, potions, deck.
    /// Pulls from RunManager.Instance.State + the primary player.
    /// </summary>
    private static bool _bugRelicDiagnosticEmitted = false;

    private static void EmitRelicDiagnostic(Player? player, RunState? state, int relicsFound)
    {
        if (_bugRelicDiagnosticEmitted) return;
        _bugRelicDiagnosticEmitted = true;
        try
        {
            BridgeTrace.Log($"BugRelic[diag]: relicsFound={relicsFound} (from player.Relics)");
            var targets = new (string label, object? obj)[]
            {
                ("Player", (object?)player),
                ("RunState", (object?)state),
            };
            var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var (label, obj) in targets)
            {
                if (obj is null) { BridgeTrace.Log($"BugRelic[diag]: {label} is null"); continue; }
                var t = obj.GetType();
                BridgeTrace.Log($"BugRelic[diag]: {label} type={t.FullName}");
                foreach (var f in t.GetFields(bf))
                {
                    if (!f.Name.ToLowerInvariant().Contains("relic")) continue;
                    object? val = null;
                    try { val = f.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugRelic[diag]: {label}.FIELD {f.FieldType.Name} {f.Name} = {Summarize(val)}");
                }
                foreach (var p in t.GetProperties(bf))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    if (!p.Name.ToLowerInvariant().Contains("relic")) continue;
                    object? val = null;
                    try { val = p.GetValue(obj); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                    BridgeTrace.Log($"BugRelic[diag]: {label}.PROP {p.PropertyType.Name} {p.Name} = {Summarize(val)}");
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugRelic[diag] threw: {ex.Message}"); }
    }

    public static object? ExtractRun()
    {
        try
        {
            var rmType = typeof(RunManager);
            var rm = GetStaticProp(rmType, "Instance");
            if (rm is null) return null;
            if (!ReflectBool(rm, "IsInProgress")) return null;

            var state = GetProp(rm, "State") as RunState;
            if (state is null) return null;

            Player? player = null;
            try
            {
                var players = GetProp(state, "Players") as IEnumerable;
                if (players is not null)
                    foreach (var p in players) { player = p as Player; break; }
            }
            catch { }

            var relics = new List<object>();
            try
            {
                if (player?.Relics is not null)
                    foreach (var r in player.Relics) relics.Add(ExtractRelic(r));
            }
            catch (Exception ex) { relics.Add(new { error = ex.Message }); }

            // BugRelic[diag] resolved: Player.Relics is the canonical source.
            // NEOWRELIC gap on Map screen was a timing artifact; Winged Boots
            // now appears correctly in combat. Kept EmitRelicDiagnostic
            // function for future re-arming but no longer called.

            var potions = new List<object?>();
            try
            {
                if (player?.PotionSlots is not null)
                    foreach (var p in player.PotionSlots) potions.Add(p is null ? null : ExtractPotion(p));
            }
            catch (Exception ex) { potions.Add(new { error = ex.Message }); }

            var deck = new List<object>();
            try
            {
                if (player?.Deck?.Cards is not null)
                    foreach (var c in player.Deck.Cards) deck.Add(ExtractCard(c));
            }
            catch (Exception ex) { deck.Add(new { error = ex.Message }); }

            var actModel = GetProp(state, "Act") as AbstractModel;
            var currentRoom = GetProp(state, "CurrentRoom");
            var character = GetProp(player, "Character") as AbstractModel;

            object? restSite = null;
            try
            {
                if (currentRoom is RestSiteRoom rsr) restSite = ExtractRestSite(rsr);
            }
            catch (Exception ex) { restSite = new { error = ex.Message }; }

            // Snapshot scalars once so the anonymous object below and the
            // floor-history JSONL writer both see the same values, and we
            // don't pay reflection twice.
            var ascensionLevel = ReflectInt(state, "AscensionLevel");
            var totalFloor = ReflectInt(state, "TotalFloor");
            var actFloor = ReflectInt(state, "ActFloor");
            var currentActIndex = ReflectInt(state, "CurrentActIndex");
            var currentHp = TryInt(() => player?.Creature?.CurrentHp ?? -1);
            var maxHp = TryInt(() => player?.Creature?.MaxHp ?? -1);
            var block = TryInt(() => player?.Creature?.Block ?? -1);
            var gold = TryInt(() => player?.Gold ?? -1);
            var maxPotionCount = TryInt(() => player?.MaxPotionCount ?? -1);
            var roomTypeStr = TryEnum(() => GetProp(currentRoom, "RoomType")?.ToString());

            // Count non-null potions only — PotionSlots holds nulls for empty slots.
            var potionCount = 0;
            foreach (var p in potions) if (p is not null) potionCount++;

            // Per-floor history (SpireBench): append a JSONL row when
            // totalFloor advances. No-op on stale snapshots, mid-floor
            // re-extractions, MainMenu/GameOver screens (totalFloor<=0).
            BridgeFloorHistory.TryAppendIfFloorAdvanced(
                floor: totalFloor,
                act: currentActIndex,
                hp: currentHp,
                maxHp: maxHp,
                gold: gold,
                deckSize: deck.Count,
                relicCount: relics.Count,
                potionCount: potionCount,
                roomType: roomTypeStr);

            return new
            {
                gameMode = TryEnum(() => GetProp(state, "GameMode")?.ToString()),
                ascensionLevel,
                totalFloor,
                actFloor,
                currentActIndex,
                actId = SafeModelIdOrNull(actModel?.Id),
                currentRoom = new
                {
                    id = SafeModelIdOrNull((GetProp(currentRoom, "ModelId") as ModelId)),
                    roomType = roomTypeStr,
                },
                restSite,
                currentHp,
                maxHp,
                block,
                gold,
                maxPotionCount,
                character = SafeModelIdOrNull(character?.Id),
                relics,
                potions,
                deck,
                deckSize = deck.Count,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    public static object ExtractRewards(IEnumerable<Reward>? rewards)
    {
        var list = new List<object>();
        if (rewards is null) return list;
        try
        {
            int position = 0;
            foreach (var r in rewards)
            {
                int pos = position++;
                try
                {
                    switch (r)
                    {
                        case CardReward cr:
                            list.Add(new
                            {
                                kind = "Card",
                                position = pos,
                                index = cr.RewardsSetIndex,
                                canSkip = cr.CanSkip,
                                canReroll = cr.CanReroll,
                                cards = cr.Cards?.Select(c => ExtractCard(c)).ToArray() ?? Array.Empty<object>(),
                            });
                            break;
                        case RelicReward rr:
                            var rewardRelic = rr.ClaimedRelic ?? ReflectRelicRewardRelic(rr);
                            var rewardRarity = rr.Rarity.ToString();
                            if (rewardRarity == "None" && rewardRelic is not null)
                            {
                                rewardRarity = TryEnum(() => rewardRelic.Rarity.ToString());
                            }
                            list.Add(new
                            {
                                kind = "Relic",
                                position = pos,
                                index = rr.RewardsSetIndex,
                                rarity = rewardRarity,
                                relic = rewardRelic is not null ? ExtractRelic(rewardRelic) : null,
                            });
                            break;
                        case PotionReward pr:
                            list.Add(new
                            {
                                kind = "Potion",
                                position = pos,
                                index = pr.RewardsSetIndex,
                                potion = pr.Potion is not null ? ExtractPotion(pr.Potion) : null,
                            });
                            break;
                        case GoldReward gr:
                            list.Add(new
                            {
                                kind = "Gold",
                                position = pos,
                                index = gr.RewardsSetIndex,
                                amount = gr.Amount,
                            });
                            break;
                        default:
                            list.Add(new
                            {
                                kind = r.GetType().Name,
                                position = pos,
                                index = r.RewardsSetIndex,
                            });
                            break;
                    }
                }
                catch (Exception ex)
                {
                    list.Add(new { kind = r?.GetType().Name ?? "Unknown", position = pos, error = ex.Message });
                }
            }
        }
        catch (Exception ex)
        {
            list.Add(new { kind = "EnumerateFailed", error = ex.Message });
        }
        return list;
    }

    public static object ExtractCardOptions(
        IReadOnlyList<CardCreationResult>? options,
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative>? extras)
    {
        var cards = new List<object>();
        var alts = new List<object>();
        if (options is not null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                try
                {
                    var ccr = options[i];
                    cards.Add(new
                    {
                        index = i,
                        modified = ccr.HasBeenModified,
                        card = ccr.Card is not null ? ExtractCard(ccr.Card) : null,
                    });
                }
                catch (Exception ex)
                {
                    cards.Add(new { index = i, error = ex.Message });
                }
            }
        }
        if (extras is not null)
        {
            for (int i = 0; i < extras.Count; i++)
            {
                try
                {
                    var a = extras[i];
                    alts.Add(new
                    {
                        index = i,
                        optionId = a.OptionId,
                        hotkey = a.Hotkey,
                        title = SafeLocString(a.Title),
                    });
                }
                catch (Exception ex)
                {
                    alts.Add(new { index = i, error = ex.Message });
                }
            }
        }
        return new { cards, alternatives = alts };
    }

    // ---- Event extraction ----

    // Bug D diagnostic: on first event option per TextKey whose description
    // contains an unsubstituted "{Something}" token, dump the full type shape of
    // the EventOption and its owning event so we can locate the param bag.
    private static readonly HashSet<string> _bugDDiagnosticSeen = new();

    private static void EmitBugDDiagnostic(EventOption option, string? description)
    {
        if (string.IsNullOrEmpty(description)) return;
        // crude heuristic: a {Name} token where Name is alphanumeric / starts with a letter
        if (!System.Text.RegularExpressions.Regex.IsMatch(description, @"\{[A-Za-z][A-Za-z0-9_]*\}")) return;
        string key = null!;
        try { key = option.TextKey ?? "<no-textkey>"; } catch { key = "<threw>"; }
        if (!_bugDDiagnosticSeen.Add(key)) return;
        try
        {
            var t = option.GetType();
            BridgeTrace.Log($"BugD[diag]: option type = {t.FullName}");
            BridgeTrace.Log($"BugD[diag]: description = {description}");
            // fields
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                object? val = null;
                try { val = f.GetValue(option); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                BridgeTrace.Log($"BugD[diag]: FIELD {f.FieldType.Name} {f.Name} = {Summarize(val)}");
            }
            // properties
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object? val = null;
                try { val = p.GetValue(option); } catch (Exception ex) { val = $"<threw {ex.GetType().Name}>"; }
                BridgeTrace.Log($"BugD[diag]: PROP  {p.PropertyType.Name} {p.Name} = {Summarize(val)}");
            }
            // walk up to base types for additional members declared only on bases
            var bt = t.BaseType;
            while (bt is not null && bt != typeof(object))
            {
                BridgeTrace.Log($"BugD[diag]: base = {bt.FullName}");
                bt = bt.BaseType;
            }

            // Dump LocString variable bags for Title and Description.
            // SmartFormat pulls tokens from LocString._variables; if tokens remain
            // unresolved, either no one called .Add(...) or the vars live on a
            // different LocString instance (e.g. on the page, not the option).
            try
            {
                if (option.Description is not null)
                {
                    var desc = option.Description;
                    var vars = desc.Variables;
                    BridgeTrace.Log($"BugD[diag]: Description.LocTable={desc.LocTable} LocEntryKey={desc.LocEntryKey} Variables.Count={vars.Count}");
                    foreach (var kv in vars)
                    {
                        BridgeTrace.Log($"BugD[diag]:   desc.var {kv.Key} = {Summarize(kv.Value)}");
                    }
                    // Also re-try formatting and raw for comparison
                    BridgeTrace.Log($"BugD[diag]: Description.GetFormattedText() = {Summarize(desc.GetFormattedText())}");
                    BridgeTrace.Log($"BugD[diag]: Description.GetRawText() = {Summarize(desc.GetRawText())}");
                }
                if (option.Title is not null)
                {
                    var vars = option.Title.Variables;
                    BridgeTrace.Log($"BugD[diag]: Title.Variables.Count={vars.Count}");
                }
            }
            catch (Exception ex) { BridgeTrace.Log($"BugD[diag] locstring-vars threw: {ex.Message}"); }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugD[diag] threw: {ex.Message}"); }
    }

    private static string Summarize(object? v)
    {
        if (v is null) return "<null>";
        try
        {
            if (v is string s) return $"\"{(s.Length > 160 ? s.Substring(0, 160) + "…" : s)}\"";
            if (v is System.Collections.IDictionary d)
            {
                var parts = new List<string>();
                int n = 0;
                foreach (System.Collections.DictionaryEntry kv in d)
                {
                    parts.Add($"{kv.Key}={Summarize(kv.Value)}");
                    if (++n >= 16) { parts.Add("…"); break; }
                }
                return $"Dict[{d.Count}]{{{string.Join(", ", parts)}}}";
            }
            if (v is System.Collections.IEnumerable e && v is not string)
            {
                var parts = new List<string>();
                int n = 0;
                foreach (var item in e)
                {
                    parts.Add(Summarize(item));
                    if (++n >= 8) { parts.Add("…"); break; }
                }
                return $"[{string.Join(", ", parts)}]";
            }
            var str = v.ToString() ?? "<null.ToString>";
            return str.Length > 160 ? str.Substring(0, 160) + "…" : str;
        }
        catch (Exception ex) { return $"<Summarize threw {ex.GetType().Name}: {ex.Message}>"; }
    }

    // Collect dynamic variables from an EventModel's DynamicVars bag via reflection.
    // Returns a dict of {VarName -> stringified value} suitable for manual {Token} replacement.
    private static Dictionary<string, string> CollectEventDynamicVars(EventModel? ev)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ev is null) return result;
        try
        {
            var t = ev.GetType();
            var dvProp = t.GetProperty("DynamicVars", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy);
            object? bag = dvProp?.GetValue(ev);
            if (bag is null)
            {
                var dvField = t.GetField("DynamicVars", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy);
                bag = dvField?.GetValue(ev);
            }
            if (bag is null) return result;
            // DynamicVarSet is enumerable of entries with Key/Value-like props. Try IDictionary first.
            if (bag is System.Collections.IDictionary d)
            {
                foreach (System.Collections.DictionaryEntry kv in d)
                {
                    var k = kv.Key?.ToString();
                    if (!string.IsNullOrEmpty(k)) result[k!] = kv.Value?.ToString() ?? "";
                }
                return result;
            }
            if (bag is System.Collections.IEnumerable e)
            {
                foreach (var item in e)
                {
                    if (item is null) continue;
                    var it = item.GetType();
                    var keyProp = it.GetProperty("Key") ?? it.GetProperty("Name");
                    var valProp = it.GetProperty("Value");
                    var key = keyProp?.GetValue(item)?.ToString();
                    var val = valProp?.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(key)) result[key!] = val ?? "";
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"CollectEventDynamicVars threw: {ex.Message}"); }
        return result;
    }

    // Substitute {Token} occurrences in text with values from the provided dict.
    // Only tokens with an alphanumeric name that's actually present in dict get replaced.
    private static string? SubstituteTokens(string? text, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(text) || vars.Count == 0) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\{([A-Za-z][A-Za-z0-9_]*)\}", m =>
        {
            var name = m.Groups[1].Value;
            return vars.TryGetValue(name, out var v) ? v : m.Value;
        });
    }

    public static object ExtractEventOption(EventOption option, int index, IReadOnlyDictionary<string, string>? eventDynamicVars = null)
    {
        if (option is null) return new { index, error = "null option" };
        try
        {
            RelicModel? relic = null;
            try { relic = option.Relic; } catch { }

            var description = SafeLocString(option.Description);
            if (eventDynamicVars is not null) description = SubstituteTokens(description, eventDynamicVars);
            EmitBugDDiagnostic(option, description);

            return new
            {
                index,
                title = SafeLocString(option.Title),
                description,
                textKey = TryString(() => option.TextKey),
                historyName = SafeLocString(option.HistoryName),
                isLocked = TryBool(() => option.IsLocked),
                disableOnChosen = ReflectBool(option, "DisableOnChosen"),
                wasChosen = TryBool(() => option.WasChosen),
                isProceed = TryBool(() => option.IsProceed),
                relic = relic is not null ? ExtractRelic(relic) : null,
                preview = BuildEventOptionPreview(option),
            };
        }
        catch (Exception ex) { return new { index, error = ex.Message }; }
    }

    // Ev1: reflective preview of Card / Gold / HpLoss / HpGain / Potion / MaxHp
    // outcomes exposed on EventOption or its subclasses. The game's concrete
    // option types (e.g. GoldOption, CardOption, HpLossOption) vary by event so
    // we inspect by field/property name rather than binding to a fixed type.
    private static object? BuildEventOptionPreview(EventOption option)
    {
        if (option is null) return null;
        try
        {
            var preview = new System.Collections.Generic.Dictionary<string, object?>();
            var t = option.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.FlattenHierarchy;

            // Map field/property suffixes we care about to preview keys.
            static string? NameMatches(string n)
            {
                // Normalize to lowercase for suffix match
                var ln = n.ToLowerInvariant();
                if (ln.EndsWith("goldchange") || ln == "gold" || ln.EndsWith("goldamount") || ln.EndsWith("goldgain") || ln.EndsWith("goldloss")) return "gold";
                if (ln.EndsWith("hploss") || ln.EndsWith("hpdamage") || ln.EndsWith("damage") && !ln.Contains("card")) return "hpLoss";
                if (ln.EndsWith("hpgain") || ln.EndsWith("heal") || ln.EndsWith("healamount")) return "hpGain";
                if (ln.EndsWith("maxhpchange") || ln.EndsWith("maxhp") || ln.EndsWith("maxhpgain") || ln.EndsWith("maxhploss")) return "maxHpChange";
                if (ln.EndsWith("potion") && !ln.Contains("count")) return "potion";
                if (ln.EndsWith("potionid")) return "potionId";
                if (ln.EndsWith("card") && !ln.Contains("reward")) return "card";
                if (ln.EndsWith("cardid")) return "cardId";
                if (ln.EndsWith("cardchoices") || ln.EndsWith("cardoptions")) return "cardChoices";
                return null;
            }

            void TryAdd(string key, object? raw)
            {
                if (raw is null) return;
                if (preview.ContainsKey(key)) return; // first hit wins
                // Unwrap common containers
                if (raw is int i) { preview[key] = i; return; }
                if (raw is long l) { preview[key] = l; return; }
                if (raw is bool b) { preview[key] = b; return; }
                if (raw is string s) { preview[key] = s; return; }
                // CardModel / PotionModel / RelicModel — try to summarize via title/id
                var rawT = raw.GetType();
                var titleProp = rawT.GetProperty("Title", flags) ?? rawT.GetProperty("Name", flags);
                var idProp = rawT.GetProperty("Id", flags) ?? rawT.GetProperty("ID", flags);
                string? title = null, id = null;
                try
                {
                    var tv = titleProp?.GetValue(raw);
                    if (tv is MegaCrit.Sts2.Core.Localization.LocString ls) title = SafeLocString(ls);
                    else if (tv is not null) title = tv.ToString();
                }
                catch { }
                try { id = idProp?.GetValue(raw)?.ToString(); } catch { }
                if (title is null && id is null)
                {
                    preview[key] = rawT.Name;
                }
                else
                {
                    preview[key] = new { id, title };
                }
            }

            foreach (var f in t.GetFields(flags))
            {
                var key = NameMatches(f.Name);
                if (key is null) continue;
                try { TryAdd(key, f.GetValue(option)); } catch { }
            }
            foreach (var p in t.GetProperties(flags))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                var key = NameMatches(p.Name);
                if (key is null) continue;
                try { TryAdd(key, p.GetValue(option)); } catch { }
            }

            if (preview.Count == 0) return null;
            return preview;
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"BuildEventOptionPreview threw: {ex.Message}");
            return null;
        }
    }

    private static readonly HashSet<string> _bugEvDiagnosticSeen = new();
    private static void EmitBugEvDiagnostic(EventModel ev)
    {
        if (ev is null) return;
        string key;
        try { key = ev.Id?.ToString() ?? ev.GetType().FullName ?? "<noid>"; } catch { key = "<threw>"; }
        if (!_bugEvDiagnosticSeen.Add(key)) return;
        try
        {
            var t = ev.GetType();
            BridgeTrace.Log($"BugEv[diag]: event type = {t.FullName}");
            // Dump EventModel's own Description Variables
            try
            {
                if (ev.Description is not null)
                {
                    var v = ev.Description.Variables;
                    BridgeTrace.Log($"BugEv[diag]: Event.Description.Variables.Count={v.Count}");
                    foreach (var kv in v) BridgeTrace.Log($"BugEv[diag]:   ev.desc.var {kv.Key} = {Summarize(kv.Value)}");
                }
            } catch (Exception ex) { BridgeTrace.Log($"BugEv[diag] ev.desc vars threw: {ex.Message}"); }
            // Scan all public+nonpublic fields/props for anything mentioning BatheCurses or
            // containing an IDictionary that might hold event-level vars.
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                object? val = null;
                try { val = f.GetValue(ev); } catch { }
                if (val is null) continue;
                var s = Summarize(val);
                if (s.IndexOf("BatheCurses", StringComparison.Ordinal) >= 0 ||
                    f.Name.IndexOf("variable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("page", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BridgeTrace.Log($"BugEv[diag]: FIELD {f.FieldType.Name} {f.Name} = {s}");
                }
            }
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.FlattenHierarchy))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object? val = null;
                try { val = p.GetValue(ev); } catch { }
                if (val is null) continue;
                var s = Summarize(val);
                if (s.IndexOf("BatheCurses", StringComparison.Ordinal) >= 0 ||
                    p.Name.IndexOf("variable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Name.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Name.IndexOf("page", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BridgeTrace.Log($"BugEv[diag]: PROP  {p.PropertyType.Name} {p.Name} = {s}");
                }
            }
        }
        catch (Exception ex) { BridgeTrace.Log($"BugEv[diag] threw: {ex.Message}"); }
    }

    // Returns true when the text looks like an unresolved loc key (dotted path,
    // no spaces, no BBCode). Used to suppress Neow-style meta-keys that never
    // get rendered by the game but leak into our serialized description field.
    private static bool LooksLikeUnresolvedLocKey(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.IndexOf(' ') >= 0) return false;
        if (text.IndexOf('[') >= 0) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*){2,}$");
    }

    private static string? CleanEventDescription(string? text, IReadOnlyDictionary<string, string> vars)
    {
        var substituted = SubstituteTokens(text, vars);
        return LooksLikeUnresolvedLocKey(substituted) ? null : substituted;
    }

    public static object ExtractEvent(EventModel ev)
    {
        if (ev is null) return new { error = "null event" };
        EmitBugEvDiagnostic(ev);
        try
        {
            var dynVars = CollectEventDynamicVars(ev);
            var options = new List<object>();
            try
            {
                if (ev.CurrentOptions is not null)
                {
                    int i = 0;
                    foreach (var o in ev.CurrentOptions)
                    {
                        if (o is EventOption eo) options.Add(ExtractEventOption(eo, i, dynVars));
                        i++;
                    }
                }
            }
            catch (Exception ex) { options.Add(new { error = ex.Message }); }

            return new
            {
                id = SafeModelIdOrNull(ev.Id),
                kind = ev.GetType().Name,
                title = SafeLocString(ev.Title),
                description = CleanEventDescription(SafeLocString(ev.Description), dynVars),
                initialDescription = CleanEventDescription(SafeLocString(ev.InitialDescription), dynVars),
                layoutType = TryEnum(() => ev.LayoutType.ToString()),
                isFinished = TryBool(() => ev.IsFinished),
                isShared = TryBool(() => ev.IsShared),
                options,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    // ---- Shop (MerchantRoom) extraction ----

    private static object ExtractMerchantEntry(MerchantEntry entry, int index)
    {
        if (entry is null) return new { index, error = "null entry" };
        try
        {
            object? detail = null;
            string entryKind = entry.GetType().Name;
            try
            {
                switch (entry)
                {
                    case MerchantCardEntry ce:
                        {
                            CardModel? card = null;
                            try { card = ce.CreationResult?.Card; } catch { }
                            detail = new
                            {
                                kind = "Card",
                                isOnSale = TryBool(() => ce.IsOnSale),
                                card = card is not null ? ExtractCard(card) : null,
                            };
                            break;
                        }
                    case MerchantPotionEntry pe:
                        {
                            PotionModel? pot = null;
                            try { pot = pe.Model; } catch { }
                            detail = new
                            {
                                kind = "Potion",
                                potion = pot is not null ? ExtractPotion(pot) : null,
                            };
                            break;
                        }
                    case MerchantRelicEntry re:
                        {
                            RelicModel? rel = null;
                            try { rel = re.Model; } catch { }
                            detail = new
                            {
                                kind = "Relic",
                                relic = rel is not null ? ExtractRelic(rel) : null,
                            };
                            break;
                        }
                    case MerchantCardRemovalEntry cr:
                        {
                            // BaseCost and PriceIncrease are both static on MerchantCardRemovalEntry.
                            detail = new
                            {
                                kind = "CardRemoval",
                                used = TryBool(() => cr.Used),
                                baseCost = ReflectStaticInt(typeof(MerchantCardRemovalEntry), "BaseCost"),
                                priceIncrease = ReflectStaticInt(typeof(MerchantCardRemovalEntry), "PriceIncrease"),
                            };
                            break;
                        }
                }
            }
            catch (Exception ex) { detail = new { error = ex.Message }; }

            return new
            {
                index,
                entryKind,
                cost = TryInt(() => entry.Cost),
                enoughGold = TryBool(() => entry.EnoughGold),
                isStocked = TryBool(() => entry.IsStocked),
                detail,
            };
        }
        catch (Exception ex) { return new { index, error = ex.Message }; }
    }

    public static object ExtractShop(MerchantRoom room)
    {
        if (room is null) return new { error = "null room" };
        try
        {
            var inv = room.Inventory;
            var characterCards = new List<object>();
            var colorlessCards = new List<object>();
            var potions = new List<object>();
            var relics = new List<object>();
            object? cardRemoval = null;

            try
            {
                if (inv?.CharacterCardEntries is not null)
                {
                    int i = 0;
                    foreach (var e in inv.CharacterCardEntries)
                    {
                        if (e is MerchantEntry me) characterCards.Add(ExtractMerchantEntry(me, i));
                        i++;
                    }
                }
            }
            catch (Exception ex) { characterCards.Add(new { error = ex.Message }); }

            try
            {
                if (inv?.ColorlessCardEntries is not null)
                {
                    int i = 0;
                    foreach (var e in inv.ColorlessCardEntries)
                    {
                        if (e is MerchantEntry me) colorlessCards.Add(ExtractMerchantEntry(me, i));
                        i++;
                    }
                }
            }
            catch (Exception ex) { colorlessCards.Add(new { error = ex.Message }); }

            try
            {
                if (inv?.PotionEntries is not null)
                {
                    int i = 0;
                    foreach (var e in inv.PotionEntries)
                    {
                        if (e is MerchantEntry me) potions.Add(ExtractMerchantEntry(me, i));
                        i++;
                    }
                }
            }
            catch (Exception ex) { potions.Add(new { error = ex.Message }); }

            try
            {
                if (inv?.RelicEntries is not null)
                {
                    int i = 0;
                    foreach (var e in inv.RelicEntries)
                    {
                        if (e is MerchantEntry me) relics.Add(ExtractMerchantEntry(me, i));
                        i++;
                    }
                }
            }
            catch (Exception ex) { relics.Add(new { error = ex.Message }); }

            try
            {
                if (inv?.CardRemovalEntry is not null)
                    cardRemoval = ExtractMerchantEntry(inv.CardRemovalEntry, 0);
            }
            catch (Exception ex) { cardRemoval = new { error = ex.Message }; }

            Player? player = null;
            try { player = inv?.Player; } catch { }

            return new
            {
                id = SafeModelIdOrNull(room.ModelId),
                playerGold = TryInt(() => player?.Gold ?? -1),
                characterCards,
                colorlessCards,
                potions,
                relics,
                cardRemoval,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    // ---- RestSite extraction ----

    public static object ExtractRestSiteOption(RestSiteOption option, int index)
    {
        if (option is null) return new { index, error = "null option" };
        try
        {
            // Owner is internal; fetch via reflection.
            Player? owner = GetProp(option, "Owner") as Player;

            // Some options carry extra data worth surfacing.
            object? extra = null;
            try
            {
                switch (option)
                {
                    case HealRestSiteOption heal:
                        {
                            int amount = -1;
                            try
                            {
                                if (owner is not null)
                                {
                                    var raw = typeof(HealRestSiteOption)
                                        .GetMethod("GetHealAmount", StaticAny)
                                        ?.Invoke(null, new object[] { owner });
                                    if (raw is not null) amount = Convert.ToInt32(raw);
                                }
                            }
                            catch { }
                            extra = new { kind = "Heal", healAmount = amount };
                            break;
                        }
                    case SmithRestSiteOption smith:
                        {
                            extra = new { kind = "Smith", smithCount = TryInt(() => smith.SmithCount) };
                            break;
                        }
                    case MendRestSiteOption mend:
                        {
                            int amount = -1;
                            try
                            {
                                if (owner is not null)
                                {
                                    var raw = typeof(MendRestSiteOption)
                                        .GetMethod("GetHealAmount", StaticAny)
                                        ?.Invoke(null, new object[] { owner });
                                    if (raw is not null) amount = Convert.ToInt32(raw);
                                }
                            }
                            catch { }
                            extra = new { kind = "Mend", healAmount = amount };
                            break;
                        }
                    case CookRestSiteOption cook:
                        {
                            int removable = -1;
                            try
                            {
                                if (owner is not null)
                                {
                                    var raw = CallMethod(cook, "GetRemovableCardCount", owner);
                                    if (raw is int i) removable = i;
                                }
                            }
                            catch { }
                            extra = new { kind = "Cook", removableCardCount = removable };
                            break;
                        }
                }
            }
            catch (Exception ex) { extra = new { error = ex.Message }; }

            return new
            {
                index,
                kind = option.GetType().Name,
                optionId = TryString(() => option.OptionId),
                title = SafeLocString(option.Title),
                description = SafeLocString(option.Description),
                isEnabled = TryBool(() => option.IsEnabled),
                hasOwner = owner is not null,
                extra,
            };
        }
        catch (Exception ex) { return new { index, error = ex.Message }; }
    }

    public static object ExtractRestSite(RestSiteRoom room)
    {
        if (room is null) return new { error = "null room" };
        try
        {
            var options = new List<object>();
            try
            {
                if (room.Options is not null)
                {
                    int i = 0;
                    foreach (var o in room.Options)
                    {
                        if (o is RestSiteOption rso) options.Add(ExtractRestSiteOption(rso, i));
                        i++;
                    }
                }
            }
            catch (Exception ex) { options.Add(new { error = ex.Message }); }

            return new
            {
                id = SafeModelIdOrNull(room.ModelId),
                options,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    /// <summary>
    /// Extract treasure room state. Combines the entity-level
    /// <see cref="TreasureRoom"/> (persistent, reachable from
    /// <see cref="BridgeSingleton.CurrentTreasureRoom"/>) with the live
    /// <see cref="NTreasureRoom"/> Godot node (via
    /// <see cref="BridgeSingleton.CurrentTreasureNode"/>) which holds
    /// transient UI state: whether the chest has been opened and, if so,
    /// which relic choices are currently on display in the
    /// <c>NTreasureRoomRelicCollection</c>.
    ///
    /// Returned shape:
    ///   {
    ///     id,                         // TreasureRoom model id (e.g. "Core:Treasure")
    ///     hasChestBeenOpened (bool),  // NTreasureRoom._hasChestBeenOpened
    ///     isRelicCollectionOpen (bool), // NTreasureRoom._isRelicCollectionOpen
    ///     canProceed (bool),          // chest opened AND (no choices left OR user intent to skip)
    ///     relicChoices: [ { index, relic{...} }, ... ]
    ///   }
    /// </summary>
    public static object ExtractTreasure(TreasureRoom room, NTreasureRoom? node)
    {
        if (room is null) return new { error = "null room" };
        try
        {
            bool hasChestBeenOpened = false;
            bool isRelicCollectionOpen = false;
            var relicChoices = new List<object>();

            if (node is not null)
            {
                hasChestBeenOpened = ReflectBoolField(node, "_hasChestBeenOpened");
                isRelicCollectionOpen = ReflectBoolField(node, "_isRelicCollectionOpen");

                try
                {
                    var collectionField = typeof(NTreasureRoom).GetField("_relicCollection",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var collection = collectionField?.GetValue(node) as NTreasureRoomRelicCollection;
                    if (collection is not null)
                    {
                        var holdersField = typeof(NTreasureRoomRelicCollection).GetField("_holdersInUse",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (holdersField?.GetValue(collection) is System.Collections.IEnumerable holders)
                        {
                            foreach (var h in holders)
                            {
                                try
                                {
                                    if (h is not NTreasureRoomRelicHolder holder) continue;
                                    int idx = -1;
                                    try { idx = holder.Index; } catch { }
                                    RelicModel? model = null;
                                    try { model = holder.Relic?.Model; } catch { }
                                    relicChoices.Add(new
                                    {
                                        index = idx,
                                        relic = model is not null ? ExtractRelic(model) : null,
                                    });
                                }
                                catch (Exception ex) { relicChoices.Add(new { error = ex.Message }); }
                            }
                        }
                    }
                }
                catch (Exception ex) { relicChoices.Add(new { error = ex.Message }); }
            }

            return new
            {
                id = SafeModelIdOrNull(room.ModelId),
                hasChestBeenOpened,
                isRelicCollectionOpen,
                relicChoices,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    private static bool ReflectBoolField(object obj, string fieldName)
    {
        try
        {
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var v = f?.GetValue(obj);
            return v is bool b && b;
        }
        catch { return false; }
    }

    // ---- Map extraction ----

    /// <summary>
    /// Extract the current act map: currently-travelable next nodes (from
    /// <c>NMapScreen.Instance._mapPointDictionary</c> filtered by
    /// <c>IsTravelable</c>), the player's current coord, visited coords, and
    /// the full act grid (so Hermes can plan paths).
    ///
    /// Coords are emitted as <c>{col, row}</c>. <c>pointType</c> is the string
    /// name of <c>MapPointType</c> (e.g. "Monster", "Elite", "Shop", "Rest",
    /// "Event", "Treasure", "Boss").
    ///
    /// Returns <c>null</c> if there is no active run or no act map yet.
    /// </summary>
    public static object? ExtractMap()
    {
        try
        {
            var rmType = typeof(RunManager);
            var rm = GetStaticProp(rmType, "Instance");
            if (rm is null) return null;
            if (!ReflectBool(rm, "IsInProgress")) return null;

            var state = GetProp(rm, "State") as RunState;
            if (state is null) return null;

            var actMap = state.Map;
            if (actMap is null) return null;

            // Current + visited.
            object? currentCoord = null;
            int currentRow = -1;
            int currentCol = -1;
            try
            {
                if (state.CurrentMapCoord is MapCoord cur)
                {
                    currentCoord = new { col = cur.col, row = cur.row };
                    currentRow = cur.row;
                    currentCol = cur.col;
                }
            }
            catch { }

            var visited = new List<object>();
            try
            {
                if (state.VisitedMapCoords is not null)
                    foreach (var c in state.VisitedMapCoords)
                        visited.Add(new { col = c.col, row = c.row });
            }
            catch { }

            // Travelable next nodes — pulled from the live NMapScreen. This
            // dictionary is populated by SetMap and filtered by the game's
            // own travelability rules (adjacency, ascent order, etc).
            var available = new List<object>();
            // Collect extra (off-grid) points from _mapPointDictionary so that
            // row-0 nodes like NAncientMapPoint (Tezcatara) show up in the grid
            // even though actMap.GetAllMapPoints() omits them.
            var offGridPoints = new List<object>();
            try
            {
                var screen = NMapScreen.Instance;
                if (screen is not null)
                {
                    var dictField = typeof(NMapScreen).GetField("_mapPointDictionary", InstanceAny);
                    // IsTravelable is declared protected on NMapPoint; derived-type reflection
                    // via GetProp on np.GetType() sometimes misses it, so query the base
                    // type explicitly (Public | NonPublic | Instance).
                    var travelProp = typeof(NMapPoint).GetProperty("IsTravelable",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var stateProp = typeof(NMapPoint).GetProperty("State",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (dictField?.GetValue(screen) is IDictionary dict)
                    {
                        // Build a set of (col,row) present in the grid (from actMap.GetAllMapPoints)
                        // so we can identify off-grid points below. Cheap: just enumerate again
                        // inside the loop via a HashSet.
                        var gridCoords = new HashSet<(int, int)>();
                        try
                        {
                            foreach (var mp2 in actMap.GetAllMapPoints())
                            {
                                try
                                {
                                    var c2 = (MapCoord)(typeof(MapPoint).GetField("coord", InstanceAny)?.GetValue(mp2) ?? default(MapCoord));
                                    gridCoords.Add((c2.col, c2.row));
                                }
                                catch { }
                            }
                        }
                        catch { }

                        foreach (DictionaryEntry e in dict)
                        {
                            try
                            {
                                if (e.Value is NMapPoint np)
                                {
                                    bool isTravelable = false;
                                    try
                                    {
                                        if (travelProp?.GetValue(np) is bool b) isTravelable = b;
                                    }
                                    catch { }
                                    MapCoord coord = (MapCoord)e.Key!;
                                    var mp = GetProp(np, "Point") as MapPoint;
                                    string? pt = TryEnum(() => mp?.PointType.ToString());
                                    string? st = null;
                                    try { st = stateProp?.GetValue(np)?.ToString(); } catch { }

                                    if (isTravelable)
                                    {
                                        available.Add(new
                                        {
                                            col = coord.col,
                                            row = coord.row,
                                            pointType = pt,
                                            state = st,
                                        });
                                    }
                                    else if (string.Equals(st, "Travelable", StringComparison.Ordinal))
                                    {
                                        // BKI-002 fix: post-Act-boss transition, the player has
                                        // currentCoord==null momentarily and the IsTravelable
                                        // predicate (which depends on adjacency to current
                                        // position) returns false even for legitimate entry
                                        // nodes like the row-0 Ancient. The MapPointState enum,
                                        // however, is set by the game's own bookkeeping when a
                                        // node becomes available. If state==Travelable but the
                                        // predicate disagrees, trust the enum: SelectMapNode's
                                        // own validator and TravelToMapCoord both accept these
                                        // nodes (verified in-game 2026-05-04). Without this
                                        // fallback, agents see available:[] and stall.
                                        available.Add(new
                                        {
                                            col = coord.col,
                                            row = coord.row,
                                            pointType = pt,
                                            state = st,
                                        });
                                    }

                                    // If this point is not in the act's normal grid, record it
                                    // so it can be merged into the reported grid below.
                                    if (!gridCoords.Contains((coord.col, coord.row)))
                                    {
                                        offGridPoints.Add(new
                                        {
                                            col = coord.col,
                                            row = coord.row,
                                            pointType = pt,
                                            state = st,
                                            offGrid = true,
                                            children = new List<object>(),
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                available.Add(new { error = ex.Message });
            }

            // Full grid of MapPoints for planning.
            var grid = new List<object>();
            try
            {
                foreach (var mp in actMap.GetAllMapPoints())
                {
                    try
                    {
                        var coord = (MapCoord)(typeof(MapPoint).GetField("coord", InstanceAny)?.GetValue(mp) ?? default(MapCoord));
                        var children = new List<object>();
                        if (mp.Children is not null)
                        {
                            foreach (var child in mp.Children)
                            {
                                try
                                {
                                    var cc = (MapCoord)(typeof(MapPoint).GetField("coord", InstanceAny)?.GetValue(child) ?? default(MapCoord));
                                    children.Add(new { col = cc.col, row = cc.row });
                                }
                                catch { }
                            }
                        }
                        grid.Add(new
                        {
                            col = coord.col,
                            row = coord.row,
                            pointType = TryEnum(() => mp.PointType.ToString()),
                            children,
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                grid.Add(new { error = ex.Message });
            }

            // Merge off-grid points (e.g. row-0 Ancient event nodes) so they
            // appear in state.json run.map.grid and the caller can SelectMapNode them.
            foreach (var og in offGridPoints) grid.Add(og);

            int rowCount = -1, colCount = -1;
            try { rowCount = actMap.GetRowCount(); } catch { }
            try { colCount = actMap.GetColumnCount(); } catch { }

            object? bossCoord = null;
            int bossRow = -1;
            string? bossId = null;
            string? secondBossId = null;
            try
            {
                var boss = actMap.BossMapPoint;
                if (boss is not null)
                {
                    var bc = (MapCoord)(typeof(MapPoint).GetField("coord", InstanceAny)?.GetValue(boss) ?? default(MapCoord));
                    bossCoord = new { col = bc.col, row = bc.row };
                    bossRow = bc.row;
                }

                // Boss identity is NOT carried on the MapPoint (which only holds
                // coord + PointType.Boss). It lives on the current ActModel as
                // BossEncounter / SecondBossEncounter (verified via reflection
                // dump 2026-05-05: ENCOUNTER.CEREMONIAL_BEAST_BOSS for the
                // initial probe run, ENCOUNTER:THE_KIN_BOSS for the next).
                // Pull the ModelId from there. We expose only the id (not a
                // human display name) because EncounterModel's display
                // properties are not stable/accessible via simple reflection;
                // consumers that need a pretty label can derive it from the
                // id (see tools/read-map.ps1's _PrettyBossId helper) or look
                // up against docs/benchmark/priors-<CHARACTER>.md's act-pool taxonomy.
                var actObj = GetProp(state, "Act");
                if (actObj is not null)
                {
                    var bossEnc = GetProp(actObj, "BossEncounter");
                    if (bossEnc is not null)
                    {
                        bossId = SafeModelIdOrNull(GetProp(bossEnc, "Id") as ModelId);
                    }
                    var secondEnc = GetProp(actObj, "SecondBossEncounter");
                    if (secondEnc is not null)
                    {
                        secondBossId = SafeModelIdOrNull(GetProp(secondEnc, "Id") as ModelId);
                    }
                }
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"ExtractMap boss identity probe threw: {ex.Message}");
            }

            // state_inconsistent detection (protocol-v1 §Bridge changes #4):
            // If the player has a known position AND no travelable next nodes
            // are reported AND we're not on the boss row (where available[]
            // legitimately empties when the boss has been reached), increment
            // the consecutive-empty counter. Two consecutive empty observations
            // emit one state_inconsistent event. The counter resets whenever
            // any of these guards fail.
            //
            // Boss-row guard rationale: on the boss floor itself, after the
            // map screen has been left for combat, available[] is empty by
            // design — the only "next node" was the boss. We don't want to
            // false-positive on this routine emptiness.
            //
            // currentCoord==null guard rationale: post-Act-boss transition
            // briefly leaves currentMapCoord null (BKI-002 area). Treating
            // that as "inconsistent" would fire spuriously on every act
            // boundary. We require a known coord before counting.
            try
            {
                bool currentKnown = currentRow >= 0;
                bool isBossRow = bossRow >= 0 && currentRow == bossRow;
                bool noNextNodes = available.Count == 0;
                if (currentKnown && !isBossRow && noNextNodes)
                {
                    _consecutiveEmptyMapTicks++;
                    if (_consecutiveEmptyMapTicks == 2)
                    {
                        BridgeSnapshotWriter.EmitEvent(new
                        {
                            type = "state_inconsistent",
                            cause = "available_empty_non_boss_floor",
                            currentCoord = new { col = currentCol, row = currentRow },
                            bossRow,
                            consecutiveTicks = _consecutiveEmptyMapTicks,
                            recommendedAction = "ForceRefresh",
                        }, "ExtractMap");
                    }
                }
                else
                {
                    _consecutiveEmptyMapTicks = 0;
                }
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"ExtractMap state_inconsistent detection threw: {ex.Message}");
            }

            return new
            {
                rowCount,
                colCount,
                currentCoord,
                bossCoord,
                bossId,
                secondBossId,
                available,
                visited,
                grid,
            };
        }
        catch (Exception ex) { return new { error = ex.Message }; }
    }

    // ---- try-helpers (swallow exceptions, return sentinel) ----

    /// <summary>
    /// Check whether a CardModel carries a given keyword (e.g. "Ethereal", "Innate", "Retain",
    /// "Exhaust", "Curse", "Status"). Looks in both Keywords (IReadOnlySet) and CanonicalKeywords
    /// (IEnumerable), matching by enum ToString() ordinal-ignore-case — the exact keyword-enum
    /// names in StS2 are unknown at compile time so reflection is used.
    /// </summary>
    private static bool CardHasKeyword(CardModel card, string keyword)
    {
        if (card is null) return false;
        foreach (var propName in new[] { "Keywords", "CanonicalKeywords" })
        {
            try
            {
                var coll = GetProp(card, propName) as System.Collections.IEnumerable;
                if (coll is null) continue;
                foreach (var kw in coll)
                {
                    if (kw is null) continue;
                    if (string.Equals(kw.ToString(), keyword, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static int TryInt(Func<int> f) { try { return f(); } catch { return -1; } }
    private static bool TryBool(Func<bool> f) { try { return f(); } catch { return false; } }
    private static string? TryString(Func<string?> f) { try { return f(); } catch { return null; } }
    private static string? TryEnum(Func<string?> f) { try { return f(); } catch { return null; } }
    private static uint? TryNullableUInt(Func<uint?> f) { try { return f(); } catch { return null; } }

    /// <summary>
    /// Reflectively read an IEnumerable property and return each element's ToString().
    /// Returns an empty list on miss/throw. Used for surfacing Keywords/Tags to clients.
    /// </summary>
    private static System.Collections.Generic.List<string> TryEnumerableStrings(object? target, string propName)
    {
        var result = new System.Collections.Generic.List<string>();
        if (target is null) return result;
        try
        {
            var coll = GetProp(target, propName) as System.Collections.IEnumerable;
            if (coll is null) return result;
            foreach (var item in coll)
            {
                if (item is null) continue;
                var s = item.ToString();
                if (!string.IsNullOrEmpty(s)) result.Add(s!);
            }
        }
        catch { }
        return result;
    }

    // ---- reflection helpers (for internal members) ----

    private const BindingFlags InstanceAny =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticAny =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static object? GetProp(object? target, string name, BindingFlags flags = InstanceAny)
    {
        if (target is null) return null;
        try { return target.GetType().GetProperty(name, flags)?.GetValue(target); }
        catch { return null; }
    }

    private static object? GetStaticProp(Type t, string name)
    {
        try { return t.GetProperty(name, StaticAny)?.GetValue(null); }
        catch { return null; }
    }

    private static object? CallMethod(object? target, string name, params object[] args)
    {
        if (target is null) return null;
        try { return target.GetType().GetMethod(name, InstanceAny)?.Invoke(target, args); }
        catch { return null; }
    }

    private static int ReflectInt(object? target, string propName, int fallback = -1)
    {
        var v = GetProp(target, propName);
        return v is int i ? i : fallback;
    }

    private static int ReflectStaticInt(Type t, string propName, int fallback = -1)
    {
        var v = GetStaticProp(t, propName);
        return v is int i ? i : fallback;
    }

    private static bool ReflectBool(object? target, string propName, bool fallback = false)
    {
        var v = GetProp(target, propName);
        return v is bool b ? b : fallback;
    }

    private static string? ReflectString(object? target, string propName)
    {
        var v = GetProp(target, propName);
        return v as string;
    }
}
