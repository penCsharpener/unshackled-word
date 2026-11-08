# Development image. The source is bind-mounted by compose; node_modules lives in a named volume
# so the host never gets a node_modules folder.
FROM oven/bun:1.4

WORKDIR /app
RUN mkdir node_modules && chown bun:bun /app node_modules
USER bun

COPY --chown=bun:bun package.json bun.lock* ./
RUN bun install

COPY --chown=bun:bun . .

EXPOSE 5173
CMD ["bun", "run", "dev"]
