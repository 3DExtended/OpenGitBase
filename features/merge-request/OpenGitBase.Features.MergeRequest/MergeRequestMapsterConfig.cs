using Mapster;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest;

public class MergeRequestMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Entities.MergeRequestEntity, MergeRequestDto>()
            .Map(dest => dest.Id, src => MergeRequestId.From(src.Id))
            .Map(dest => dest.Status, src => (MergeRequestStatus)src.Status)
            .Map(dest => dest.CreatorUserId, src => UserId.From(src.CreatorUserId));

        config.NewConfig<MergeRequestDto, Entities.MergeRequestEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.Status, src => (int)src.Status)
            .Map(dest => dest.CreatorUserId, src => src.CreatorUserId.Value);
    }
}
