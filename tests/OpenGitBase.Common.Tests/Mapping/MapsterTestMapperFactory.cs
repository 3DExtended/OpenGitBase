using Mapster;
using MapsterMapper;

namespace OpenGitBase.Common.Tests.Mapping;

public static class MapsterTestMapperFactory
{
    public static IMapper Create<TRegister>()
        where TRegister : IRegister, new()
    {
        var config = new TypeAdapterConfig();
        new TRegister().Register(config);
        return new Mapper(config);
    }
}
