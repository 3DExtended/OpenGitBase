﻿﻿using Mapster;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode;

public class StorageNodeMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<StorageNodeEntity, StorageNodeDto>()
            .Map(dest => dest.Id, src => StorageNodeId.From(src.Id));

        config
            .NewConfig<StorageNodeDto, StorageNodeEntity>()
            .Map(
                dest => dest.Id,
                src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value
            );
    }
}
