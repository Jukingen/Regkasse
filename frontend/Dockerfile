# syntax=docker/dockerfile:1

# -----------------------------------------------------------------------------
# Regkasse POS — static Expo web export (optional Compose profile: pos)
#
# Prefer building via root docker-compose.yml (--profile pos). Manual:
#   docker build -f frontend/Dockerfile \
#     --build-arg EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api \
#     -t regkasse-frontend-pos-web:local .
#
# Context is the repository root so Dockerfile.dockerignore can exclude siblings.
# Install uses frontend/package-lock.json (standalone).
# EXPO_PUBLIC_* are inlined at export time — use host-reachable API URLs.
# -----------------------------------------------------------------------------

ARG NODE_VERSION=22

FROM node:${NODE_VERSION}-alpine AS deps
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY frontend/package.json frontend/package-lock.json ./
COPY .npmrc ./
RUN npm ci

FROM node:${NODE_VERSION}-alpine AS builder
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY --from=deps /app/node_modules ./node_modules
COPY frontend/ ./

ARG EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api
ARG EXPO_PUBLIC_DEV_TENANT_ID=dev
ARG EXPO_PUBLIC_ADMIN_BASE_URL=http://localhost:3000

ENV EXPO_PUBLIC_API_BASE_URL=$EXPO_PUBLIC_API_BASE_URL \
    EXPO_PUBLIC_DEV_TENANT_ID=$EXPO_PUBLIC_DEV_TENANT_ID \
    EXPO_PUBLIC_ADMIN_BASE_URL=$EXPO_PUBLIC_ADMIN_BASE_URL \
    NODE_ENV=production \
    CI=1

RUN npx expo export --platform web

FROM nginx:1.27-alpine AS runner

COPY frontend/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1/ >/dev/null || exit 1

CMD ["nginx", "-g", "daemon off;"]
