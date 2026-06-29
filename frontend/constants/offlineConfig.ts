/**
 * Central offline POS configuration — limits, sync cadence, storage keys, and API paths.
 * All offline components should import from here instead of hardcoding values.
 */
export const OFFLINE_CONFIG = {
  // Limits
  MAX_OFFLINE_TRANSACTIONS: 50, // RKSV max limit
  MAX_OFFLINE_ORDERS: 100, // Separate from transactions

  // Time limits
  OFFLINE_EXPIRY_HOURS: 72, // 3 days (RKSV compliance)
  OFFLINE_WARNING_HOURS: 24, // Show warning 24 hours before expiry
  OFFLINE_CRITICAL_HOURS: 6, // Critical warning 6 hours before expiry

  // Token
  TOKEN_EXPIRY_HOURS: 168, // 7 days (for offline work)
  TOKEN_REFRESH_THRESHOLD_HOURS: 24, // Refresh token 24 hours before expiry

  // Sync
  SYNC_INTERVAL_SECONDS: 30, // Check for sync every 30 seconds
  SYNC_RETRY_MAX: 3, // Max retry attempts
  SYNC_RETRY_DELAY_SECONDS: 60, // Wait 60 seconds between retries

  // Storage
  STORAGE_PREFIX: 'offline_pos_',
  STORAGE_VERSION: '1.0',

  // UI
  BANNER_POSITION: 'bottom', // 'top' or 'bottom'
  BANNER_AUTO_HIDE_SECONDS: 10, // Auto hide success messages

  // Features
  ENABLE_OFFLINE_ORDERS: true,
  ENABLE_OFFLINE_PAYMENTS: true, // Non-fiscal only
  ENABLE_OFFLINE_GUTSCHEIN: false, // NEVER allow voucher offline

  // Backend sync endpoints
  SYNC_ENDPOINTS: {
    ORDERS: '/api/pos/offline-orders/replay',
    PAYMENTS: '/api/offline-transactions/replay',
    STATUS: '/api/pos/offline-orders/status',
  },
} as const;
