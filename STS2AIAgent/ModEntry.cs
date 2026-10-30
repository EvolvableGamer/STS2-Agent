using System.Threading;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2AIAgent.Agent;
using STS2AIAgent.Config;
using STS2AIAgent.Game;
using STS2AIAgent.Localization;
using STS2AIAgent.Server;
using STS2AIAgent.Ui;

namespace STS2AIAgent;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string LogPrefix = "[STS2AIAgent]";

    private static int _shutdownHooksRegistered;

    public static void Initialize()
    {
#if STS2_PUBLIC_23811903
        var gameAssembly = typeof(MegaCrit.Sts2.Core.Runs.RunState).Assembly.Location;
        var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(gameAssembly)));
        var platform = OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsWindows() ? "win32" : "unsupported";
        PublicBuildProfile.Validate(platform, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture, actual);
#endif
        CompanionStartupFile.Publish("initializing");
        try
        {
            Log.Info($"{LogPrefix} Initializing");
            RegisterShutdownHooks();
            // Reads the player's language before anything renders text. The game's own localization
            // manager does not exist yet at this point, so this resolves through the settings file and
            // the subscription is picked up once the overlay is built.
            LocSource.Initialize();
            GameThread.Initialize();
#if STS2_PUBLIC_23811903
            GameControl.Install();
#endif
            GameEventService.Instance.Start();
            HttpServer.Instance.Start();
            AgentRuntime.Instance.Initialize();
            // Before anything asks for a private game member, so a renamed one is in the log a player
            // attaches to a bug report rather than only on an endpoint they have never called.
            ReflectedGameMembers.ProbeAtStartup();
            NativeCoopLoadGuard.Install();
            if (InstanceRole.IsCompanion || IsHeadlessDisplay())
            {
                Log.Info($"{LogPrefix} Skipping overlay (companion={InstanceRole.IsCompanion}, headless={IsHeadlessDisplay()})");
            }
            else
            {
#if STS2_PUBLIC_23811903
                // The public bridge is usable without upstream AI onboarding. Retain the
                // entire optional UI, but do not open its first-run guide on bridge startup.
                if (System.Environment.GetEnvironmentVariable("STS2_PUBLIC_ENABLE_UPSTREAM_UI") == "1")
#endif
                    AgentOverlayHost.Install();
            }

            Log.Info($"{LogPrefix} Ready on {HttpServer.Instance.Prefix}");
        }
        catch (Exception ex)
        {
            CompanionStartupFile.Publish("failed", error: DiagnosticExport.Redact(ex.Message));
            throw;
        }
    }

    private static bool IsHeadlessDisplay()
    {
        try
        {
            return string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterShutdownHooks()
    {
        if (Interlocked.Exchange(ref _shutdownHooksRegistered, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
    }

    private static void Shutdown()
    {
        try
        {
            AgentRuntime.Instance.Shutdown();
            AgentOverlayHost.Uninstall();
            GameEventService.Instance.Stop();
            HttpServer.Instance.Stop();
        }
        catch (Exception ex)
        {
            Log.Error($"{LogPrefix} Failed during shutdown: {ex}");
        }
    }
}
