using System.Text;

namespace PNMC.Api;

internal static class DotEnvLoader
{
    public static void Load()
    {
        foreach (var path in ResolveCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                if (key.Length == 0 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                {
                    continue;
                }

                var value = line[(separatorIndex + 1)..].Trim();
                if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                {
                    value = value[1..^1];
                }

                Environment.SetEnvironmentVariable(key, value);
            }

            // Stop after first .env found.
            return;
        }
    }

    private static IEnumerable<string> ResolveCandidatePaths()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; current is not null && depth < 6; depth++)
        {
            yield return Path.Combine(current.FullName, ".env");
            current = current.Parent;
        }
    }
}
