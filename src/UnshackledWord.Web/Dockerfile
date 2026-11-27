# Development image. The source is bind-mounted by compose; node_modules lives in a named volume
# so the host never gets a node_modules folder.
FROM oven/bun:1.4

# Real Node for the test runner: `bun run test` otherwise finds only Bun's node stand-in on PATH, and Vitest then runs on
# Bun's runtime, where jsdom 31 fails to start ("'addEventListener' called on an object that is not a valid instance of EventTarget").
COPY --from=node:24-slim /usr/local/bin/node /usr/local/bin/node

WORKDIR /app
RUN mkdir node_modules && chown bun:bun /app node_modules
USER bun

COPY --chown=bun:bun package.json bun.lock* ./
RUN bun install

COPY --chown=bun:bun . .

EXPOSE 5173
CMD ["bun", "run", "dev"]
