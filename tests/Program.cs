using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using HermesBridge.HermesBridgeCode;

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

void AtomicWriterRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "hermesbridge-writer-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);
    var statePath = Path.Combine(tempRoot, "state.json");
    var staleSharedTempPath = Path.Combine(tempRoot, "state.json.tmp");

    try
    {
        using var blocker = new FileStream(staleSharedTempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        AtomicFileWriter.WriteText(statePath, "{\"hello\":1}");

        var written = File.ReadAllText(statePath);
        AssertTrue(written == "{\"hello\":1}", "Atomic writer should persist payload to destination");
        Console.WriteLine("PASS: atomic writer ignores locked stale shared temp path and writes destination.");
    }
    finally
    {
        try { Directory.Delete(tempRoot, true); } catch { }
    }
}

void HandIndexExtractionRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeStateExtractor.cs"));
    AssertTrue(source.Contains("public static object ExtractCard(CardModel card, int? handIndex = null)"),
        "ExtractCard should accept an optional handIndex parameter");
    AssertTrue(source.Contains("handIndex,"),
        "ExtractCard should emit handIndex in serialized card payloads");
    AssertTrue(source.Contains("hand = ExtractPile(pcs?.Hand, includeHandIndices: true)"),
        "Combat hand extraction should pass includeHandIndices: true");
    Console.WriteLine("PASS: hand extraction now includes explicit handIndex values.");
}

void RewardRefreshRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "RewardsScreenSetRewardsPatch.cs"));
    AssertTrue(source.Contains("public static void RefreshVisibleRewards(string trigger)"),
        "Rewards patch should expose RefreshVisibleRewards helper");
    AssertTrue(source.Contains("CardReward cardReward => cardReward.Cards?.Any() == true"),
        "RefreshVisibleRewards should filter exhausted card rewards");
    AssertTrue(source.Contains("!parentRewards.Any(r => ReferenceEquals(r, reward))"),
        "RefreshVisibleRewards should drop rewards removed from the parent reward set");
    AssertTrue(source.Contains("RelicReward relicReward => relicReward.ClaimedRelic is null"),
        "RefreshVisibleRewards should filter taken relic rewards");
    AssertTrue(source.Contains("PotionReward potionReward => potionReward.ClaimedPotion is null"),
        "RefreshVisibleRewards should filter taken potion rewards");

    var dispatcher = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeCommandDispatcher.cs"));
    AssertTrue(dispatcher.Contains("RewardsScreenSetRewardsPatch.RefreshVisibleRewards(\"SelectRewardRefreshVisible\")"),
        "SelectReward dispatch should trigger visible reward refresh immediately");
    Console.WriteLine("PASS: reward selection triggers an immediate filtered rewards refresh.");
}

void RelicRewardFallbackRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeStateExtractor.cs"));
    AssertTrue(source.Contains("var rewardRelic = rr.ClaimedRelic ?? ReflectRelicRewardRelic(rr);"),
        "Relic reward extraction should fall back to private _relic field when ClaimedRelic is null");
    AssertTrue(source.Contains("if (rewardRarity == \"None\" && rewardRelic is not null)"),
        "Relic reward extraction should repair rarity when Reward.Rarity reports None");
    Console.WriteLine("PASS: relic reward extraction has a fallback path for elite relic rewards.");
}

void CardModifierExtractionRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeStateExtractor.cs"));
    AssertTrue(source.Contains("dynamicDescription = SafeLocString(enchantment.DynamicDescription)"),
        "BridgeStateExtractor should extract enchantment details");
    AssertTrue(source.Contains("public static object ExtractAffliction(AfflictionModel affliction)"),
        "BridgeStateExtractor should extract affliction details");
    AssertTrue(source.Contains("enchantment,"),
        "ExtractCard should emit enchantment payload");
    AssertTrue(source.Contains("affliction,"),
        "ExtractCard should emit affliction payload");
    Console.WriteLine("PASS: card extraction now includes enchantment and affliction payloads.");
}

void CardGridExitScreenRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "CardGridSelectionPatch.cs"));
    AssertTrue(source.Contains("BridgeSnapshotWriter.SetScreen(roomScreen, \"CardGridSelectionExit\")"),
        "Card grid exit should restore the enclosing room/combat screen");
    AssertTrue(source.Contains("BridgeSingleton.CurrentMerchantRoom is not null ? \"Room:Shop\""),
        "Card grid exit should specifically restore Room:Shop when leaving shop removal");
    Console.WriteLine("PASS: card-grid exit restores the enclosing screen context.");
}

void DeferredPotionRefreshRegression()
{
    var dispatcher = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeCommandDispatcher.cs"));
    AssertTrue(dispatcher.Contains("ScheduleDeferredStateRefresh(2, \"UsePotionResolve\", includeCombat: true, includeRun: true);"),
        "UsePotion should schedule a deferred combat+run refresh after the immediate dispatch snapshot");
    AssertTrue(dispatcher.Contains("ScheduleDeferredStateRefresh(2, \"DiscardPotionResolve\", includeCombat: true, includeRun: true);"),
        "DiscardPotion should schedule a deferred combat+run refresh after the immediate dispatch snapshot");
    AssertTrue(dispatcher.Contains("private static void ScheduleDeferredStateRefresh(int pumpTicks, string trigger, bool includeCombat, bool includeRun)"),
        "BridgeCommandDispatcher should define the deferred refresh helper");

    var pump = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeMainThreadDispatcher.cs"));
    AssertTrue(pump.Contains("public static bool EnqueueAfterPumpTicks(int ticks, Action action)"),
        "Main-thread dispatcher should expose delayed pump scheduling for async game effects");
    Console.WriteLine("PASS: potion commands now schedule deferred state refreshes.");
}

void TransitionClearRegression()
{
    var mapOpen = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "MapScreenOpenPatch.cs"));
    AssertTrue(mapOpen.Contains("BridgeSnapshotWriter.ClearTransientPayloads(\"MapScreenOpen\")"),
        "Map screen open should clear stale room payloads before publishing map state");

    var gameOver = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "GameOverScreenReadyPatch.cs"));
    AssertTrue(gameOver.Contains("BridgeSnapshotWriter.ClearTransientPayloads(\"GameOverScreenReady\")"),
        "GameOver screen should clear stale combat/shop/treasure payloads");
    AssertTrue(gameOver.Contains("BridgeSingleton.PushCurrentRun(\"GameOverScreenReady\")"),
        "GameOver screen should refresh run payload after death resolution");

    var menu = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "MainMenuReadyPatch.cs"));
    AssertTrue(menu.Contains("BridgeSnapshotWriter.ClearTransientPayloads(\"MainMenuReady\")"),
        "Main menu should clear stale in-run payloads before publishing menu state");
    Console.WriteLine("PASS: screen transitions now clear stale transient payloads.");
}

void PlayCardGuardRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeCommandDispatcher.cs"));
    AssertTrue(source.Contains("expectedCardId"),
        "PlayCard should accept an expectedCardId guard");
    AssertTrue(source.Contains("expectedTitle"),
        "PlayCard should accept an expectedTitle guard");
    AssertTrue(source.Contains("handIndex {handIndex} mismatch: expected card id"),
        "PlayCard should report a diagnostic mismatch when handIndex points at the wrong live card");
    Console.WriteLine("PASS: PlayCard now supports stale-hand guardrails.");
}

void RestSiteCleanupRegression()
{
    var restSitePatchPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "RestSiteRoomAfterSelectingOptionPatch.cs");
    var source = File.ReadAllText(restSitePatchPath);
    AssertTrue(!source.Contains("[HarmonyPatch(typeof(NRestSiteRoom), \"AfterSelectingOption\")]"),
        "Rest-site cleanup should not leave a sync AfterSelectingOption patch behind");
    AssertTrue(!source.Contains("PushCurrentRestSite(\"RestSiteAfterSelectingOption\")"),
        "Rest-site cleanup should remove the redundant RestSiteAfterSelectingOption push");
    AssertTrue(source.Contains("[HarmonyPatch(typeof(NRestSiteRoom), \"UpdateRestSiteOptions\")]"),
        "Rest-site cleanup should keep the UpdateRestSiteOptions patch");
    AssertTrue(source.Contains("BridgeSingleton.PushCurrentRun(\"RestSiteUpdateOptions\")"),
        "Rest-site cleanup should keep the post-consumption run refresh");
    Console.WriteLine("PASS: rest-site cleanup keeps only the post-consumption update patch.");
}

void EventProceedScreenRegression()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "EventRoomProceedPatch.cs"));
    AssertTrue(source.Contains("BridgeSnapshotWriter.SetEvent(null, \"EventProceed-ClearPayload\")"),
        "Event proceed should still clear the event payload");
    AssertTrue(!source.Contains("BridgeSnapshotWriter.SetScreen(\"EventClosed\", \"EventProceed\")"),
        "Event proceed should not overwrite a live map transition with screen=EventClosed");
    Console.WriteLine("PASS: event proceed no longer stomps Map with EventClosed.");
}

void RewardConsumptionRegression()
{
    var rewardsPatch = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "Patches", "RewardsScreenSetRewardsPatch.cs"));
    AssertTrue(rewardsPatch.Contains("public static void RemoveReward(Reward? reward, string trigger)"),
        "Rewards patch should expose a helper to remove consumed rewards from the cached set");
    AssertTrue(rewardsPatch.Contains("LastRewards = rewards.Where(r => !ReferenceEquals(r, reward)).ToList();"),
        "Consumed rewards should be removed from the cached LastRewards list by object identity");

    var singleton = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeSingleton.cs"));
    AssertTrue(singleton.Contains("RewardsScreenSetRewardsPatch.RemoveReward(reward, \"AfterRewardTaken\")"),
        "AfterRewardTaken should prune the consumed reward from cached rewards immediately");
    Console.WriteLine("PASS: consumed rewards are pruned from cached reward state after AfterRewardTaken.");
}

void BridgePathsResolutionRegression()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "hermesbridge-paths-test-" + Guid.NewGuid().ToString("N"));
    var dllDir = Path.Combine(tempRoot, "dll");
    var appData = Path.Combine(tempRoot, "appdata");
    Directory.CreateDirectory(dllDir);
    Directory.CreateDirectory(appData);

    try
    {
        var defaultPath = Path.Combine(appData, "SlayTheSpire2", "hermesbridge");

        // 1. SPIREBRIDGE_IPC_DIR env override wins over everything
        var envResult = BridgePaths.ResolveBaseDirectory(
            envOverride: @"D:\custom\ipc",
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var envDiag);
        AssertTrue(envResult == @"D:\custom\ipc",
            $"SPIREBRIDGE_IPC_DIR override should be returned literally; got '{envResult}'");
        AssertTrue(envDiag != null && envDiag.Contains("SPIREBRIDGE_IPC_DIR"),
            "Env override should produce a SPIREBRIDGE_IPC_DIR diagnostic");

        // 1b. HERMES_IPC_DIR is honored as deprecated alias when SPIREBRIDGE_IPC_DIR is unset
        var legacyResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: @"D:\legacy\ipc",
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var legacyDiag);
        AssertTrue(legacyResult == @"D:\legacy\ipc",
            $"HERMES_IPC_DIR alias should still resolve; got '{legacyResult}'");
        AssertTrue(legacyDiag != null && legacyDiag.Contains("DEPRECATED") && legacyDiag.Contains("HERMES_IPC_DIR"),
            "Legacy alias should produce a DEPRECATED diagnostic naming HERMES_IPC_DIR");

        // 1c. SPIREBRIDGE_IPC_DIR wins when both are set; deprecation note still logged
        var bothResult = BridgePaths.ResolveBaseDirectory(
            envOverride: @"D:\new\ipc",
            legacyEnvOverride: @"D:\old\ipc",
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var bothDiag);
        AssertTrue(bothResult == @"D:\new\ipc",
            $"SPIREBRIDGE_IPC_DIR should beat HERMES_IPC_DIR; got '{bothResult}'");
        AssertTrue(bothDiag != null && bothDiag.Contains("SPIREBRIDGE_IPC_DIR") && bothDiag.Contains("HERMES_IPC_DIR"),
            "When both set, diagnostic should mention both env vars");

        // 2. valid hermes-instance.cfg → hermesbridge-{id} under appdata
        var cfgPath = Path.Combine(dllDir, "hermes-instance.cfg");
        File.WriteAllText(cfgPath, "nonsteam");
        var cfgResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var cfgDiag);
        AssertTrue(cfgResult == Path.Combine(appData, "SlayTheSpire2", "hermesbridge-nonsteam"),
            $"Valid cfg should produce hermesbridge-{{id}} path; got '{cfgResult}'");
        AssertTrue(cfgDiag != null && cfgDiag.Contains("'nonsteam'"),
            "Cfg path should produce an instance-config diagnostic");

        // 3. cfg with UTF-8 BOM is stripped before sanitization
        File.WriteAllText(cfgPath, "\uFEFFwithbom", new UTF8Encoding(true));
        var bomResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out _);
        AssertTrue(bomResult == Path.Combine(appData, "SlayTheSpire2", "hermesbridge-withbom"),
            $"BOM should be stripped; got '{bomResult}'");

        // 4. cfg with path-traversal characters → falls through to default
        File.WriteAllText(cfgPath, "../evil");
        var evilResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var evilDiag);
        AssertTrue(evilResult == defaultPath,
            $"Invalid id should fall through to default; got '{evilResult}'");
        AssertTrue(evilDiag != null && evilDiag.Contains("no valid id"),
            "Invalid id should produce a 'no valid id' diagnostic");

        // 5. empty cfg → falls through to default, no diagnostic
        File.WriteAllText(cfgPath, "   \r\n");
        var emptyResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out _);
        AssertTrue(emptyResult == defaultPath,
            $"Empty cfg should fall through to default; got '{emptyResult}'");

        // 6. no cfg, no env → default
        File.Delete(cfgPath);
        var fallbackResult = BridgePaths.ResolveBaseDirectory(
            envOverride: null,
            legacyEnvOverride: null,
            dllDirectory: dllDir,
            appData: appData,
            diagnostic: out var fallbackDiag);
        AssertTrue(fallbackResult == defaultPath,
            $"No env, no cfg should give default; got '{fallbackResult}'");
        AssertTrue(fallbackDiag == null,
            "Default fallback should produce no diagnostic");

        // 7. SanitizeInstanceId direct unit checks
        AssertTrue(BridgePaths.SanitizeInstanceId("foo_bar-1") == "foo_bar-1",
            "SanitizeInstanceId should accept [A-Za-z0-9_-]+");
        AssertTrue(BridgePaths.SanitizeInstanceId("\uFEFF spaced ") == "spaced",
            "SanitizeInstanceId should strip BOM and trim whitespace");
        AssertTrue(BridgePaths.SanitizeInstanceId("a/b") is null,
            "SanitizeInstanceId should reject path separators");
        AssertTrue(BridgePaths.SanitizeInstanceId("..") is null,
            "SanitizeInstanceId should reject parent-dir tokens");
        AssertTrue(BridgePaths.SanitizeInstanceId("") is null,
            "SanitizeInstanceId should reject empty input");

        Console.WriteLine("PASS: BridgePaths resolves env, cfg, BOM, traversal, empty, fallback correctly.");
    }
    finally
    {
        try { Directory.Delete(tempRoot, true); } catch { }
    }
}

void EndTurnGuardRegression()
{
    var dispatcher = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HermesBridgeCode", "BridgeCommandDispatcher.cs"));
    AssertTrue(dispatcher.Contains("expectedRevision"),
        "EndTurn should accept an expectedRevision guard");
    AssertTrue(dispatcher.Contains("expectedScreen"),
        "EndTurn should accept an expectedScreen guard");
    AssertTrue(dispatcher.Contains("expectedCurrentSide"),
        "EndTurn should accept an expectedCurrentSide guard");
    AssertTrue(dispatcher.Contains("expectedRoundNumber"),
        "EndTurn should accept an expectedRoundNumber guard");
    AssertTrue(dispatcher.Contains("EndTurn guard mismatch: expected revision"),
        "EndTurn should report a diagnostic mismatch for stale revisions");
    AssertTrue(dispatcher.Contains("EndTurn guard mismatch: expected currentSide"),
        "EndTurn should report a diagnostic mismatch for stale side assumptions");
    Console.WriteLine("PASS: EndTurn now supports stale-state guardrails.");
}

AtomicWriterRegression();
HandIndexExtractionRegression();
RewardRefreshRegression();
RelicRewardFallbackRegression();
CardModifierExtractionRegression();
CardGridExitScreenRegression();
DeferredPotionRefreshRegression();
TransitionClearRegression();
PlayCardGuardRegression();
RestSiteCleanupRegression();
EventProceedScreenRegression();
RewardConsumptionRegression();
EndTurnGuardRegression();
BridgePathsResolutionRegression();
