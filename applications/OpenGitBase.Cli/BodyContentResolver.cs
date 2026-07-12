namespace OpenGitBase.Cli;

public static class BodyContentResolver
{
    public static string? Resolve(string? body, FileInfo? bodyFile)
    {
        if (bodyFile is not null)
        {
            if (!bodyFile.Exists)
            {
                throw new FileNotFoundException($"Body file not found: {bodyFile.FullName}");
            }

            return File.ReadAllText(bodyFile.FullName);
        }

        return body;
    }
}
