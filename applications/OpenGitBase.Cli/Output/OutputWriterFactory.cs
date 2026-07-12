namespace OpenGitBase.Cli.Output;

public static class OutputWriterFactory
{
    public static IOutputWriter Create(bool json, TextWriter output, TextWriter error) =>
        json ? new JsonOutputWriter(output) : new HumanOutputWriter(output, error);
}
