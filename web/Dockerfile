# 빌드 컨텍스트는 저장소 루트다. NEXT_PUBLIC_*은 빌드 때 번들에 박히므로 build arg로 받는다.
FROM node:22-slim AS build
RUN corepack enable
WORKDIR /app
COPY web/package.json web/pnpm-lock.yaml web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY web ./
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_TELEMETRY_DISABLED=1
RUN pnpm build

FROM node:22-slim
RUN corepack enable
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1
COPY --from=build /app ./
USER node
EXPOSE 3000
CMD ["pnpm", "start"]
