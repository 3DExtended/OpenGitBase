# FROM debian:bookworm-slim
FROM debian:stable-slim

# Ensure packages are up-to-date to reduce known vulnerabilities
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get upgrade -y \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    git openssh-server ca-certificates python3 curl openssl \
    nginx-light fcgiwrap spawn-fcgi \
    && apt-get autoremove -y --purge \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

RUN useradd -m -d /home/git -s /usr/bin/git-shell git \
    && mkdir -p /home/git/.ssh /var/run/sshd /srv/git /home/git/git-shell-commands /etc/opengitbase \
    && chown -R git:git /home/git /srv/git \
    && chmod 700 /home/git/.ssh \
    && chmod 755 /home/git/git-shell-commands \
    && echo "git:git" | chpasswd

COPY sshd_config /etc/ssh/sshd_config
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY storage-agent.sh /usr/local/bin/storage-agent.sh
COPY storage-http-server.py /usr/local/bin/storage-http-server.py
COPY storage_content.py /usr/local/bin/storage_content.py
COPY storage_push_validation.py /usr/local/bin/storage_push_validation.py
COPY storage-pre-receive.sh /usr/local/bin/storage-pre-receive.sh
COPY storage-git-http.sh /usr/local/bin/storage-git-http.sh
COPY scripts/storage-quorum-replicate.sh /usr/local/bin/storage-quorum-replicate.sh
COPY nginx-git-http.conf /etc/opengitbase/nginx-git-http.conf
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/storage-agent.sh \
    /usr/local/bin/storage-http-server.py /usr/local/bin/storage-git-http.sh \
    /usr/local/bin/storage-pre-receive.sh \
    /usr/local/bin/storage-quorum-replicate.sh

EXPOSE 22 8081 8082 8443
VOLUME ["/home/git/.ssh", "/srv/git"]

CMD ["/usr/local/bin/entrypoint.sh"]