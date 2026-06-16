FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-client \
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

RUN dotnet restore "applications/OpenGitBase.Api/OpenGitBase.Api.csproj"

COPY . .
WORKDIR "/src/applications/OpenGitBase.Api"
RUN dotnet build "OpenGitBase.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenGitBase.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenGitBase.Api.dll"]
