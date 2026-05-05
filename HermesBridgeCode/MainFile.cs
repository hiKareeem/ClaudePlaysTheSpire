using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace HermesBridge.HermesBridgeCode;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "HermesBridge";

    /// <summary>
    /// Bridge protocol/build version. MUST match the <c>version</c> field in
    /// <c>HermesBridge.json</c>. Emitted as <c>state.modVersion</c> on every
    /// snapshot write and used by the preflight-dll-version operator script
    /// to confirm the deployed DLL matches the expected release.
    /// </summary>
    public const string BridgeVersion = "0.2.0";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    private static Harmony? _harmony;

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        _harmony ??= new Harmony(ModId);
        _harmony.PatchAll(assembly);

        BridgeTrace.Log($"Initialize start version={BridgeVersion}");
        // Flush any IPC-routing diagnostic captured during BridgePaths
        // static init (HERMES_IPC_DIR override or hermes-instance.cfg).
        // Cannot be logged from inside the static init itself due to a
        // re-entrancy cycle through BridgeTrace -> BridgePaths.BaseDirectory.
        BridgePaths.FlushInitDiagnostic(BridgeTrace.Log);
        BridgeSnapshotWriter.RequestWrite("Initialize");

        // Install the main-thread dispatcher node so the background command reader
        // can marshal game-API calls back onto the engine main thread.
        BridgeMainThreadDispatcher.EnsureInstalled();

        // Start polling for external commands (Hermes -> commands.json).
        BridgeCommandReader.Start();

        Logger.Info($"HermesBridge v{BridgeVersion} initialized. Export path: {BridgePaths.StateJsonPath}");
    }
}
