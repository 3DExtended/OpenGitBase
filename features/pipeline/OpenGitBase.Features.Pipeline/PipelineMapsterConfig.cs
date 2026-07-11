using Mapster;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline;

public class PipelineMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<PipelineEntity, PipelineDto>()
            .Map(dest => dest.Id, src => PipelineId.From(src.Id))
            .Map(dest => dest.Name, src => src.Name);
        config
            .NewConfig<PipelineDto, PipelineEntity>()
            .Map(
                dest => dest.Id,
                src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value
            )
            .Map(dest => dest.Name, src => src.Name);

        config
            .NewConfig<PipelineRunEntity, PipelineRunDto>()
            .Map(dest => dest.Id, src => PipelineRunId.From(src.Id));
        config
            .NewConfig<PipelineJobEntity, PipelineJobDto>()
            .Map(dest => dest.Id, src => PipelineJobId.From(src.Id))
            .Map(dest => dest.RunId, src => PipelineRunId.From(src.RunId));
        config
            .NewConfig<BaseImageCatalogEntity, BaseImageCatalogEntryDto>()
            .Map(dest => dest.Id, src => BaseImageCatalogEntryId.From(src.Id));
        config
            .NewConfig<DependencyPromotionRequestEntity, DependencyPromotionRequestDto>()
            .Map(dest => dest.Id, src => DependencyPromotionRequestId.From(src.Id))
            .Map(dest => dest.PromotionJobScheduled, _ => true);
        config
            .NewConfig<DomainAllowanceRequestEntity, DomainAllowanceRequestDto>()
            .Map(dest => dest.Id, src => DomainAllowanceRequestId.From(src.Id));
    }
}
