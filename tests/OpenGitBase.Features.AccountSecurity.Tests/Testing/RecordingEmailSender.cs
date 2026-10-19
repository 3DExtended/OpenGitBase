using System.Collections.Concurrent;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

/// <summary>Stands in for SendGrid: records every email, or fails the way the test asks.</summary>
public sealed class RecordingEmailSender : IQueryHandler<EmailSendQuery, Unit>
{
    private readonly ConcurrentQueue<EmailSendQuery> _sent = new();

    public EmailSenderFailure Failure { get; set; }

    public IReadOnlyList<EmailSendQuery> Sent => _sent.ToList();

    public Task<Option<Unit>> RunQueryAsync(EmailSendQuery query, CancellationToken cancellationToken) =>
        Failure switch
        {
            EmailSenderFailure.Throws => throw new HttpRequestException("SendGrid is unreachable"),
            EmailSenderFailure.ReturnsNone => Task.FromResult(Option<Unit>.None),
            _ => Record(query),
        };

    private Task<Option<Unit>> Record(EmailSendQuery query)
    {
        _sent.Enqueue(query);
        return Task.FromResult<Option<Unit>>(Unit.Value);
    }
}
