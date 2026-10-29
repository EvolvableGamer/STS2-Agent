using System.Text.Json;
using System.Text.RegularExpressions;

namespace STS2AIAgent.Game;

// Generic game command receipts. No task, identity, seed policy or retry decisions.
internal sealed class GameStartCommandStore(string directory)
{
    private string FileFor(string id, string suffix)
    {
        if (!Regex.IsMatch(id, "\\A[A-Za-z0-9_-]{1,128}\\z")) throw new ArgumentException("Invalid game command ID.");
        return Path.Combine(directory, id + suffix);
    }
    public bool HasIntent(string id) => File.Exists(FileFor(id, ".intent"));
    private void WriteNew(string path, object value)
    {
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        JsonSerializer.Serialize(file, value);
        file.Flush(true);
    }
    public bool Begin(string id, string instance)
    {
        var path = FileFor(id, ".intent");
        if (File.Exists(path)) return false;
        WriteNew(path, new { instance_id = instance, outcome = "unknown" });
        return true;
    }
    public void Complete(string id, object result)
    {
        if (!HasIntent(id)) throw new InvalidOperationException("Game command intent required.");
        WriteNew(FileFor(id, ".result"), result);
    }
    public object? Read(string id, string instance)
    {
        var path = FileFor(id, ".result");
        if (!File.Exists(path)) return null;
        // Torn/malformed files throw; never invent a successful receipt.
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("instance_id").GetString() == instance ? doc.RootElement.Clone() : null;
    }
}
