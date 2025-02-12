static class DotEnv
{
    public static void Load()
    {
        var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(envFile))
        {
            foreach (var line in File.ReadAllLines(envFile))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                    throw new Exception($"Invalid .env line: {line}");

                var key = parts[0];
                var value = parts[1];
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
