namespace OpenGitBase.Dispatcher.Options;

public class DispatcherOptions
{
    public string ApiUrl { get; set; } = "http://api:8080";

    public string DispatcherId { get; set; } = "dispatcher-1";

    public int HttpPort { get; set; } = 8082;

    public string StorageSshPrivateKeyPath { get; set; } = "/run/secrets/storage_ssh_key";

    public string StorageSshUser { get; set; } = "git";

    public int StorageSshConnectTimeoutSeconds { get; set; } = 30;
}
