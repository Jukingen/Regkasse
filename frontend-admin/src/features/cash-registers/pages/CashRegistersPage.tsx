'use client';

/**
 * Cash register management UI building blocks.
 * Super-admin route wires these in `app/(protected)/admin/cash-registers/page.tsx`.
 */
export { BulkDecommissionBar } from '@/features/cash-registers/components/BulkDecommissionBar';
export { BulkDecommissionModal } from '@/features/cash-registers/components/BulkDecommissionModal';
export { CashRegisterGrid } from '@/features/cash-registers/components/CashRegisterGrid';
export { CashRegisterQuickActions } from '@/features/cash-registers/components/CashRegisterQuickActions';
export { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
export { RegisterDetailModal } from '@/features/cash-registers/components/RegisterDetailModal';
export { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
export { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
