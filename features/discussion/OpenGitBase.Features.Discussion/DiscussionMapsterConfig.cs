using Mapster;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion;

public class DiscussionMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<DiscussionEntity, DiscussionDto>()
            .Map(dest => dest.Id, src => DiscussionId.From(src.Id))
            .Map(dest => dest.Status, src => (DiscussionStatus)src.Status)
            .Map(dest => dest.CreatorUserId, src => UserId.From(src.CreatorUserId))
            .Map(
                dest => dest.AssigneeUserId,
                src =>
                    src.AssigneeUserId == null
                        ? null
                        : UserId.From(src.AssigneeUserId.Value)
            );
    }
}
