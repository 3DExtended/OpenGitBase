namespace OpenGitBase.Dispatcher.Tests.RealGit;

internal sealed record GitResult(int ExitCode, string StdOut, string StdErr);
