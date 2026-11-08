namespace BinVaBun.DiscordBot.Extensions;

/// <summary>
/// Minimal .env loader for local development. Values already present in the real environment win,
/// so production (Docker/Render env vars) is never overridden. The .env file itself is git-ignored.
/// </summary>
public static class DotEnv
{
    public static void Load(params string[] candidatePaths)
    {
        var paths = candidatePaths.Length > 0
            ? candidatePaths
            : [Path.Combine(Directory.GetCurrentDirectory(), ".env"), Path.Combine(AppContext.BaseDirectory, ".env")];

        foreach (var path in paths.Where(File.Exists))
        {
            foreach (var line in File.ReadAllLines(path))
                Apply(line);
            return;
        }
    }

    public static void Apply(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#') return;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0) return;

        var key = trimmed[..eq].Trim();
        var value = trimmed[(eq + 1)..].Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            value = value[1..^1];

        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, value);
    }
}

