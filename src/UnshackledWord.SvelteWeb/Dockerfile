FROM oven/bun:latest

RUN apt-get update && apt-get install -y git curl

WORKDIR /workspaces/web

RUN mkdir /workspaces/web/node_modules

RUN chown -R bun:bun /workspaces/web

USER bun

CMD ["sleep", "infinity"]
