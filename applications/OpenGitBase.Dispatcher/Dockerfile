FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OpenGitBase.Dispatcher.csproj", "./"]
RUN dotnet restore "OpenGitBase.Dispatcher.csproj"

COPY . .
RUN dotnet publish "OpenGitBase.Dispatcher.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-server curl ca-certificates jq \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/run/sshd \
    && ssh-keygen -A \
    && useradd -m -d /home/git -s /bin/bash -p '*' git \
    && mkdir -p /home/git/.ssh \
    && chown -R git:git /home/git \
    && chmod 700 /home/git/.ssh

COPY sshd_config /etc/ssh/sshd_config
COPY ssh-auth-hook.sh /app/ssh-auth-hook.sh
COPY dispatcher-wrapper.sh /app/dispatcher-wrapper.sh
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/ssh-auth-hook.sh /app/dispatcher-wrapper.sh /app/entrypoint.sh

COPY --from=build /app/publish .

EXPOSE 22

CMD ["/app/entrypoint.sh"]
