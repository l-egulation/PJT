FROM node:24.12.0-bookworm AS build
WORKDIR /workspace
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/web/package.json apps/web/package.json
COPY apps/admin-console/package.json apps/admin-console/package.json
COPY packages/contracts/package.json packages/contracts/package.json
COPY packages/client-sdk/package.json packages/client-sdk/package.json
COPY packages/ui/package.json packages/ui/package.json
COPY packages/localization/package.json packages/localization/package.json
COPY tools/content-validator/package.json tools/content-validator/package.json
RUN pnpm install --frozen-lockfile --filter @hanjjak/admin-console...
COPY apps/admin-console apps/admin-console
COPY packages/client-sdk packages/client-sdk
RUN pnpm --filter @hanjjak/admin-console build

FROM nginx:1.31.5-alpine
COPY apps/admin-console/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/apps/admin-console/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=6 CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
