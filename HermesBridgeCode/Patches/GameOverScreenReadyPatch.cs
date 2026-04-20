using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Fires when the death/victory screen (NGameOverScreen) finishes loading.
/// Reports the screen to Hermes and then auto-dismisses back to the main
/// menu after a short grace period, per the plan: "bridge auto-dismisses
/// game-over to main menu, then waits for Hermes to send explicit StartRun."
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen._Ready))]
internal static class GameOverScreenReadyPatch
{
    // Wall-clock delay before auto-dismissing. The dispatcher drains up to
    // 16 actions/frame so a tick counter is not proportional to frames; we
    // use DateTime.UtcNow instead to get a true ~3s grace period. This gives
    // Hermes / a human observer time to see screen=GameOver in state.json
    // before we return to main menu.
    private static readonly TimeSpan DismissDelay = TimeSpan.FromSeconds(3.0);

    public static void Postfix(NGameOverScreen __instance)
    {
        try
        {
            BridgeTrace.Log("NGameOverScreen _Ready postfix fired");
            BridgeSnapshotWriter.ClearTransientPayloads("GameOverScreenReady");
            BridgeSingleton.PushCurrentRun("GameOverScreenReady");
            BridgeSnapshotWriter.SetScreen("GameOver", "GameOverScreenReady");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"GameOverScreenReadyPatch SetScreen threw: {ex.Message}");
        }

        try
        {
            var dismissAt = DateTime.UtcNow + DismissDelay;
            ScheduleAutoDismiss(__instance, dismissAt);
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"GameOverScreenReadyPatch ScheduleAutoDismiss threw: {ex.Message}");
        }
    }

    private static void ScheduleAutoDismiss(NGameOverScreen screen, DateTime dismissAt)
    {
        BridgeMainThreadDispatcher.Enqueue(() =>
        {
            if (screen is null || !Godot.GodotObject.IsInstanceValid(screen))
            {
                BridgeTrace.Log("GameOverScreen auto-dismiss: screen no longer valid");
                return;
            }
            if (DateTime.UtcNow < dismissAt)
            {
                ScheduleAutoDismiss(screen, dismissAt);
                return;
            }
            try
            {
                var mi = screen.GetType().GetMethod("ReturnToMainMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi is null)
                {
                    BridgeTrace.Log("GameOverScreen auto-dismiss: ReturnToMainMenu not found; falling back to NGame.ReturnToMainMenu");
                    _ = NGame.Instance?.ReturnToMainMenu();
                    return;
                }
                mi.Invoke(screen, null);
                BridgeTrace.Log("GameOverScreen auto-dismissed via ReturnToMainMenu");
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"GameOverScreen auto-dismiss threw: {ex.Message}");
            }
        });
    }
}
