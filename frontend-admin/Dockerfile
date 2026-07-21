# syntax=docker/dockerfile:1

# -----------------------------------------------------------------------------
# Regkasse frontend-admin — multi-stage production image
#
# NEXT_PUBLIC_* values are inlined at `npm run build`. Pass them as build-args
# (see docker-compose.yml). Setting them only at `docker run` does NOT update
# the client bundle — see docs/DEPLOYMENT_BUILD_TIME_ENV.md.
# -----------------------------------------------------------------------------

ARG NODE_VERSION=22

# --- Dependencies (cached layer) ---------------------------------------------
FROM node:${NODE_VERSION}-alpine AS deps
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY package.json package-lock.json ./
RUN npm ci

# --- Builder -----------------------------------------------------------------
FROM node:${NODE_VERSION}-alpine AS builder
WORKDIR /app

RUN apk add --no-cache libc6-compat

COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Build-time public env (baked into the client JS)
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
ARG NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
ARG NEXT_PUBLIC_SENTRY_DSN=
ARG NEXT_PUBLIC_SENTRY_ENVIRONMENT=
ARG NEXT_PUBLIC_SENTRY_RELEASE=

ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_RKSV_ENVIRONMENT=$NEXT_PUBLIC_RKSV_ENVIRONMENT \
    NEXT_PUBLIC_SENTRY_DSN=$NEXT_PUBLIC_SENTRY_DSN \
    NEXT_PUBLIC_SENTRY_ENVIRONMENT=$NEXT_PUBLIC_SENTRY_ENVIRONMENT \
    NEXT_PUBLIC_SENTRY_RELEASE=$NEXT_PUBLIC_SENTRY_RELEASE \
    NEXT_TELEMETRY_DISABLED=1 \
    NODE_ENV=production

RUN npm run build

# --- Runner (non-root) -------------------------------------------------------
FROM node:${NODE_VERSION}-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN apk add --no-cache libc6-compat \
  && addgroup --system --gid 1001 nodejs \
  && adduser --system --uid 1001 nextjs

# Runtime needs: Next server, compiled .next, public assets, and next.config
# (config imports scripts/assertRksv… and @next/bundle-analyzer — keep builder
# node_modules so `npm start` can load next.config.mjs without missing modules).
COPY --from=builder --chown=nextjs:nodejs /app/package.json /app/package-lock.json ./
COPY --from=builder --chown=nextjs:nodejs /app/node_modules ./node_modules
COPY --from=builder --chown=nextjs:nodejs /app/.next ./.next
COPY --from=builder --chown=nextjs:nodejs /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/next.config.mjs ./
COPY --from=builder --chown=nextjs:nodejs /app/scripts ./scripts

USER nextjs

EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:'+(process.env.PORT||3000)+'/login').then((r)=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

CMD ["npm", "start"]
