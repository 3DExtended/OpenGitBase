using Mapster;

namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<StubModel, StubEntity>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Name, src => src.Name);

        config
            .NewConfig<StubEntity, StubModel>()
            .Map(dest => dest.Id, src => StubIdentifier.From(src.Id))
            .Map(dest => dest.Name, src => src.Name);
    }
}
