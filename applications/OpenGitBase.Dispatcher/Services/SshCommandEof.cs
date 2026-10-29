using System.Reflection;
using Renci.SshNet;

namespace OpenGitBase.Dispatcher.Services;

/// <summary>
/// Sends SSH_MSG_CHANNEL_EOF on an executing <see cref="SshCommand"/>. SSH.NET sends it only when the
/// stream from <see cref="SshCommand.CreateInputStream"/> is disposed after at least one byte went
/// through it (ChannelInputStream.Dispose checks <c>_totalPosition &gt; 0</c>, 2025.1.0 and develop),
/// and has no public way to send it otherwise. GitSessionProxyServiceTests pins these internals.
/// </summary>
internal static class SshCommandEof
{
#pragma warning disable S3011 // Accessibility bypass -- SSH.NET has no public way to send EOF, see the summary
    private static readonly FieldInfo ChannelField =
        typeof(SshCommand).GetField("_channel", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(SshCommand).FullName, "_channel");
#pragma warning restore S3011

    private static readonly MethodInfo SendEofMethod =
        ChannelField.FieldType.GetInterfaces()
            .Append(ChannelField.FieldType)
            .Select(type => type.GetMethod("SendEof", Type.EmptyTypes))
            .FirstOrDefault(method => method is not null)
        ?? throw new MissingMethodException(ChannelField.FieldType.FullName, "SendEof");

    internal static void Send(SshCommand command)
    {
        var channel =
            ChannelField.GetValue(command)
            ?? throw new InvalidOperationException("The command is not executing.");
        try
        {
            SendEofMethod.Invoke(channel, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            // The channel is closed: the command already finished, so nothing waits for input.
        }
    }
}
