# syntax=docker/dockerfile:1

# -----------------------------------------------------------------------------
# Regkasse frontend-admin — multi-stage production image
#
# NEXT_PUBLIC_* values are inlined at `npm run build`. Pass them as build-args
# (see docker-compose.yml / docker-compose.prod.yml). Setting them only at
# `docker run` does NOT update the client bundle — see docs/DEPLOYMENT_BUILD_TIME_ENV.md.
# -----------------------------------------------------------------------------

ARG NODE_VERSION=22

# --- Full dependencies (build needs TypeScript / tooling) --------------------
FROM node:${NODE_VERSION}-alpine AS deps
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY package.json package-lock.json ./
RUN npm ci

# --- Production dependencies only (runtime) ----------------------------------
FROM node:${NODE_VERSION}-alpine AS prod-deps
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY package.json package-lock.json ./
RUN npm ci --omit=dev

# --- Builder -----------------------------------------------------------------
FROM node:${NODE_VERSION}-alpine AS builder
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Build-time public env (baked into the client JS)
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
ARG NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
ARG NEXT_PUBLIC_RELEASE_STAGE=
ARG NEXT_PUBLIC_SENTRY_DSN=
ARG NEXT_PUBLIC_SENTRY_ENVIRONMENT=
ARG NEXT_PUBLIC_SENTRY_RELEASE=

ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_RKSV_ENVIRONMENT=$NEXT_PUBLIC_RKSV_ENVIRONMENT \
    NEXT_PUBLIC_RELEASE_STAGE=$NEXT_PUBLIC_RELEASE_STAGE \
    NEXT_PUBLIC_SENTRY_DSN=$NEXT_PUBLIC_SENTRY_DSN \
    NEXT_PUBLIC_SENTRY_ENVIRONMENT=$NEXT_PUBLIC_SENTRY_ENVIRONMENT \
    NEXT_PUBLIC_SENTRY_RELEASE=$NEXT_PUBLIC_SENTRY_RELEASE \
    NEXT_TELEMETRY_DISABLED=1 \
    NODE_ENV=production

RUN npm run build

# --- Runner (non-root, production node_modules only) -------------------------
FROM node:${NODE_VERSION}-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN apk add --no-cache libc6-compat wget \
  && addgroup --system --gid 1001 nodejs \
  && adduser --system --uid 1001 nextjs

# Runtime: next start loads next.config.mjs + scripts/assertRksv…;
# bundle-analyzer is loaded only when ANALYZE=true (see next.config.mjs).
COPY --from=prod-deps --chown=nextjs:nodejs /app/node_modules ./node_modules
COPY --from=builder --chown=nextjs:nodejs /app/package.json /app/package-lock.json ./
COPY --from=builder --chown=nextjs:nodejs /app/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/next.config.mjs ./
COPY --from=builder --chown=nextjs:nodejs /app/scripts ./scripts

USER nextjs

EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD wget -qO- http://127.0.0.1:${PORT:-3000}/health >/dev/null || exit 1

CMD ["npm", "start"]
