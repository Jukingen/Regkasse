/**
 * Legacy API boundary.
 * /api/Payment alias is deprecated and scheduled for removal; prefer canonical /api/admin/payments/* and /api/pos/payment/*.
 * See README.md for migration scope and rules.
 */

export {
  legacyPaymentQueryKeys,
  useLegacyPaymentList,
  useLegacyPaymentById,
} from './payment';
