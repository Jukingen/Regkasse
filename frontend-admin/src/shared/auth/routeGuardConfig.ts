/**
 * Route guard behaviour. Default: fail-closed (no silent allow).
 * Only set to true for a controlled dev/demo migration period when backend does not yet send permissions.
 *
 * Production: `NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS` must not be `"true"` during
 * `next build` (NODE_ENV=production) or the build fails. The exported flag is always false in production.
 */
const IS_PRODUCTION = process.env.NODE_ENV === 'production';

const rawAllowEmptyPermissionsForRouteAccess =
  process.env.NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS === 'true';

if (IS_PRODUCTION && rawAllowEmptyPermissionsForRouteAccess) {
  throw new Error(
    'NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS must not be "true" when NODE_ENV is production. Remove it or set it to false before building for production.'
  );
}

/** Always false in production; elsewhere mirrors the env var after the build-time guard above. */
export const ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS = IS_PRODUCTION
  ? false
  : rawAllowEmptyPermissionsForRouteAccess;
