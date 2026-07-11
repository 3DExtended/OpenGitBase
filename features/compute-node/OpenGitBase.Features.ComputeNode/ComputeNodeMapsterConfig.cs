using Mapster;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode;

public sealed class ComputeNodeMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<ComputeNodeEntity, ComputeNodeDto>()
            .Map(dest => dest.Id, src => ComputeNodeId.From(src.Id));
    }
}
