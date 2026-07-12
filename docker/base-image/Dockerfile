ARG BASE_IMAGE=docker.io/library/alpine:3.20

FROM ${BASE_IMAGE}

RUN apk add --no-cache bash socat shadow \
  && adduser -D -u 1000 -h /home/ogb ogb \
  && mkdir -p /usr/local/bin /usr/local/lib/opengitbase /etc/ogb

COPY applications/OpenGitBase.ComputeGuestAgent/ogb-guest-agent.sh /usr/local/bin/ogb-guest-agent
RUN chmod +x /usr/local/bin/ogb-guest-agent

COPY docker/base-image/ogb-guest-agent.service /etc/ogb/guest-agent.service

RUN echo "opengitbase-base-image" > /etc/ogb/base-image-marker
