FROM node:22-bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils curl ca-certificates openssl postgresql-client \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /work
