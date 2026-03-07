/**
 * Legacy API boundary. All admin usage of /api/Payment and /api/Cart should go through this folder.
 * See README.md for scope and rules.
 */

export {
  legacyPaymentQueryKeys,
  useLegacyPaymentList,
  useLegacyPaymentById,
} from './payment';
