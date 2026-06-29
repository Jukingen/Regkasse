export type { AdminOfflineOrderRowDto } from '@/api/generated/model/adminOfflineOrderRowDto';

/** UI status filter including "all". */
export type OfflineOrderStatus = 'pending' | 'synced' | 'failed' | 'expired' | 'all';
