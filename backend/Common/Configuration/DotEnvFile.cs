namespace TechStore.Api.Common.Configuration;

/// <summary>Loads KEY=VALUE lines from a git-ignored .env file into environment variables for local development.</summary>
public static class DotEnvFile
{
    public static IReadOnlyList<KeyValuePair<string, string>> Parse(IEnumerable<string> lines)
    {
        List<KeyValuePair<string, string>> values = [];
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line["export ".Length..].TrimStart();
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];
            values.Add(new(key, value));
        }
        return values;
    }

    /// <summary>Sets each variable from <paramref name="path"/> unless it is already set, so the shell and launch profile win.</summary>
    public static void LoadIfPresent(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var (key, value) in Parse(File.ReadLines(path)))
        {
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
