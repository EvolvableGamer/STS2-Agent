using System.Runtime.InteropServices;

namespace STS2AIAgent.Game;

// Engine identity is platform-specific even when Steam's build ID is shared.
internal static class PublicBuildProfile
{
    private static readonly IReadOnlyDictionary<(string Platform, Architecture Arch), string?> EngineHashes =
        new Dictionary<(string, Architecture), string?>
        {
            [("darwin", Architecture.Arm64)] = "e7ceb80669bfaf5c8fccabaa126ae2bb283aba514be5b5b55612579cfd285f18",
            [("win32", Architecture.X64)] = null // Must be measured on the collaborator's actual Windows installation.
        };

    internal static string? ExpectedHash(string platform, Architecture arch) =>
        EngineHashes.TryGetValue((platform, arch), out var hash) ? hash : null;

    internal static void Validate(string platform, Architecture arch, string actual)
    {
        var expected = ExpectedHash(platform, arch);
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException($"Public build23811903 cannot load on {platform}/{arch}: validated engine SHA-256 is not registered. Measure it on that platform before enabling this profile.");
        if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Public build23811903 engine SHA-256 mismatch on {platform}/{arch}; refusing to initialize.");
    }
}
