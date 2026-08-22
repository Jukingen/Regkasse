'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Select, Space, Typography } from 'antd';
import { useMemo, useState } from 'react';

import {
  adminCashRegisterDetailQueryKey,
  assignCashRegisterUser,
  cashRegisterListQueryKey,
} from '@/features/cash-registers/api/cashRegisters';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

export type CashRegisterAssignedUserFieldProps = {
  registerId: string;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  /** Editing requires `cash_register.manage`; everyone else sees the current assignment as text. */
  canEdit: boolean;
  /** A decommissioned register can no longer be assigned. */
  disabled?: boolean;
};

/** `null` is a real value here ("no assignment"), so the Select uses `undefined` for "nothing picked yet". */
const UNASSIGNED = undefined;

/**
 * Shows and edits `cash_registers.assigned_user_id` — the cashier a register is reserved for.
 * This only scopes who sees the register in the POS picker; payment rights stay tied to the open shift.
 */
export function CashRegisterAssignedUserField({
  registerId,
  assignedUserId,
  assignedUserName,
  canEdit,
  disabled = false,
}: CashRegisterAssignedUserFieldProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();

  const persisted = assignedUserId?.trim() || UNASSIGNED;

  // Reset the pending edit whenever the server value moves (own save, refetch, or another admin).
  const [draft, setDraft] = useState<string | undefined>(persisted);
  const [syncedWith, setSyncedWith] = useState<string | undefined>(persisted);
  if (syncedWith !== persisted) {
    setSyncedWith(persisted);
    setDraft(persisted);
  }

  const cashiersQuery = useUsersList(
    { role: 'Cashier', isActive: true, pageSize: 100 },
    { enabled: canEdit, staleTime: 60_000 }
  );

  const options = useMemo(() => {
    const rows = cashiersQuery.data?.items ?? [];
    const mapped = rows
      .filter((u) => Boolean(u.id))
      .map((u) => ({
        value: u.id as string,
        label:
          [u.firstName, u.lastName].filter(Boolean).join(' ').trim() ||
          u.userName ||
          u.email ||
          (u.id as string),
      }));

    // The current assignee may sit outside the first page, or may no longer be a Cashier —
    // without this the Select would render a bare guid.
    if (persisted && !mapped.some((o) => o.value === persisted)) {
      mapped.unshift({ value: persisted, label: assignedUserName?.trim() || persisted });
    }
    return mapped;
  }, [assignedUserName, cashiersQuery.data, persisted]);

  const mutation = useMutation({
    mutationFn: (userId: string | null) => assignCashRegisterUser(registerId, userId),
    onSuccess: async (_data, userId) => {
      notify.successKey(
        userId
          ? 'cashRegisters.detail.assignedUserSaved'
          : 'cashRegisters.detail.assignedUserCleared'
      );
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: adminCashRegisterDetailQueryKey(registerId) }),
        queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
        queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] }),
      ]);
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'CashRegisterDetail.assignUser',
        fallbackKey: 'cashRegisters.detail.assignedUserFailed',
      });
    },
  });

  if (!canEdit) {
    return <>{assignedUserName?.trim() || t('cashRegisters.detail.assignedUserNone')}</>;
  }

  return (
    <Space orientation="vertical" size={4} style={{ width: '100%' }}>
      <Space wrap>
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          style={{ minWidth: 220 }}
          value={draft}
          options={options}
          loading={cashiersQuery.isLoading}
          disabled={disabled || mutation.isPending}
          placeholder={t('cashRegisters.detail.assignedUserPlaceholder')}
          aria-label={t('cashRegisters.detail.assignedUser')}
          onChange={(value?: string) => setDraft(value ?? UNASSIGNED)}
        />
        <Button
          type="primary"
          size="small"
          disabled={disabled || draft === persisted}
          loading={mutation.isPending}
          onClick={() => mutation.mutate(draft ?? null)}
        >
          {t('common.buttons.save')}
        </Button>
      </Space>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {persisted
          ? t('cashRegisters.detail.assignedUserHint')
          : t('cashRegisters.detail.assignedUserHintUnassigned')}
      </Typography.Text>
    </Space>
  );
}
