# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl openssh-client \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["applications/OpenGitBase.Api/OpenGitBase.Api.csproj", "applications/OpenGitBase.Api/"]
COPY ["common/OpenGitBase.Common/OpenGitBase.Common.csproj", "common/OpenGitBase.Common/"]
COPY ["common/OpenGitBase.Cqrs/OpenGitBase.Cqrs.csproj", "common/OpenGitBase.Cqrs/"]
COPY ["common/OpenGitBase.Cqrs.EfCore/OpenGitBase.Cqrs.EfCore.csproj", "common/OpenGitBase.Cqrs.EfCore/"]
COPY ["common/OpenGitBase.Common.SendGrid/OpenGitBase.Common.SendGrid.csproj", "common/OpenGitBase.Common.SendGrid/"]
COPY ["features/users/OpenGitBase.Features.Users/OpenGitBase.Features.Users.csproj", "features/users/OpenGitBase.Features.Users/"]
COPY ["features/users/OpenGitBase.Features.Users.Contracts/OpenGitBase.Features.Users.Contracts.csproj", "features/users/OpenGitBase.Features.Users.Contracts/"]
COPY ["features/public-git-ssh-key/OpenGitBase.Features.PublicGitSshKey/OpenGitBase.Features.PublicGitSshKey.csproj", "features/public-git-ssh-key/OpenGitBase.Features.PublicGitSshKey/"]
COPY ["features/public-git-ssh-key/OpenGitBase.Features.PublicGitSshKey.Contracts/OpenGitBase.Features.PublicGitSshKey.Contracts.csproj", "features/public-git-ssh-key/OpenGitBase.Features.PublicGitSshKey.Contracts/"]
COPY ["features/repository/OpenGitBase.Features.Repository/OpenGitBase.Features.Repository.csproj", "features/repository/OpenGitBase.Features.Repository/"]
COPY ["features/repository/OpenGitBase.Features.Repository.Contracts/OpenGitBase.Features.Repository.Contracts.csproj", "features/repository/OpenGitBase.Features.Repository.Contracts/"]
COPY ["features/repository-member/OpenGitBase.Features.RepositoryMember/OpenGitBase.Features.RepositoryMember.csproj", "features/repository-member/OpenGitBase.Features.RepositoryMember/"]
COPY ["features/repository-member/OpenGitBase.Features.RepositoryMember.Contracts/OpenGitBase.Features.RepositoryMember.Contracts.csproj", "features/repository-member/OpenGitBase.Features.RepositoryMember.Contracts/"]
COPY ["features/organization/OpenGitBase.Features.Organization/OpenGitBase.Features.Organization.csproj", "features/organization/OpenGitBase.Features.Organization/"]
COPY ["features/organization/OpenGitBase.Features.Organization.Contracts/OpenGitBase.Features.Organization.Contracts.csproj", "features/organization/OpenGitBase.Features.Organization.Contracts/"]
COPY ["features/storage-node/OpenGitBase.Features.StorageNode/OpenGitBase.Features.StorageNode.csproj", "features/storage-node/OpenGitBase.Features.StorageNode/"]
COPY ["features/storage-node/OpenGitBase.Features.StorageNode.Contracts/OpenGitBase.Features.StorageNode.Contracts.csproj", "features/storage-node/OpenGitBase.Features.StorageNode.Contracts/"]
COPY ["features/git-access-token/OpenGitBase.Features.GitAccessToken/OpenGitBase.Features.GitAccessToken.csproj", "features/git-access-token/OpenGitBase.Features.GitAccessToken/"]
COPY ["features/git-access-token/OpenGitBase.Features.GitAccessToken.Contracts/OpenGitBase.Features.GitAccessToken.Contracts.csproj", "features/git-access-token/OpenGitBase.Features.GitAccessToken.Contracts/"]

RUN --mount=type=cache,id=opengitbase-nuget,target=/root/.nuget/packages \
    dotnet restore "applications/OpenGitBase.Api/OpenGitBase.Api.csproj"

COPY stylecop.json pesser-Default.ruleset ./
COPY common/ common/
COPY features/ features/
COPY applications/OpenGitBase.Api/ applications/OpenGitBase.Api/
WORKDIR "/src/applications/OpenGitBase.Api"
RUN --mount=type=cache,id=opengitbase-nuget,target=/root/.nuget/packages \
    dotnet build "OpenGitBase.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN --mount=type=cache,id=opengitbase-nuget,target=/root/.nuget/packages \
    dotnet publish "OpenGitBase.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenGitBase.Api.dll"]
