namespace STS2AIAgent.Server;

internal static class ApiPortPolicy
{
    public static int? Valid(string? value) => int.TryParse(value?.Trim(), out var port) && port is > 0 and <= 65535 ? port : null;
    public static int Resolve(string? environmentPort, int? persistedPort) =>
        Valid(environmentPort) ?? (persistedPort is > 0 and <= 65535 ? persistedPort.Value : 8080);
}
