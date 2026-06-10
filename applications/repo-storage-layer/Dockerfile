# FROM debian:bookworm-slim
FROM debian:stable-slim

# Ensure packages are up-to-date to reduce known vulnerabilities
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get upgrade -y \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    git openssh-server ca-certificates \
    && apt-get autoremove -y --purge \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

RUN useradd -m -d /home/git -s /usr/bin/git-shell git \
    && mkdir -p /home/git/.ssh /var/run/sshd /srv/git /home/git/git-shell-commands \
    && chown -R git:git /home/git /srv/git \
    && chmod 700 /home/git/.ssh \
    && chmod 755 /home/git/git-shell-commands \
    && echo "git:git" | chpasswd

COPY sshd_config /etc/ssh/sshd_config
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

EXPOSE 22
VOLUME ["/home/git/.ssh", "/srv/git"]

CMD ["/usr/local/bin/entrypoint.sh"]