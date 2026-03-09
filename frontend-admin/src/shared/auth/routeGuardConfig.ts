/**
 * Route guard behaviour. Default: fail-closed (no silent allow).
 * Only set to true for a controlled dev/demo migration period when backend does not yet send permissions.
 */
export const ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS =
  process.env.NEXT_PUBLIC_ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS === 'true';
