namespace OpenGitBase.Features.Organization.Contracts;

public sealed class OrganizationStorageQuotaDto
{
    public long PlatformBytesLimit { get; set; }

    public long ContributedBytesCapacity { get; set; }

    public long BytesLimit { get; set; }
}
