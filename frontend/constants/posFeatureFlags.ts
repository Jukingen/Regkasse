/**
 * POS feature flags (client). Server still enforces policy; this only skips the ensure-ready call.
 */
export const POS_ENSURE_READY_ON_ENTRY =
  typeof process !== 'undefined' && process.env.EXPO_PUBLIC_POS_ENSURE_READY === 'false'
    ? false
    : true;
