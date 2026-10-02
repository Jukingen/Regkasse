import { getOfflineOrderManager } from '@/services/offline/offlineOrderManager';
import { syncPendingPaymentQueue } from '@/services/payment/pendingPaymentQueue';

/** Retry both local queues without trapping the sale UI. */
export async function retryOfflineQueuesNow(): Promise<void> {
  await getOfflineOrderManager().retryPendingAndFailedOrders();
  await syncPendingPaymentQueue();
}
