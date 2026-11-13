'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import React, { useCallback, useEffect, useMemo, useState } from 'react';

import {
  applyTaxGroupToProducts,
  type TaxGroupAdmin,
} from '@/features/tax/api/taxGroups';
import { taxHistoryQueryKey } from '@/features/tax/api/taxHistory';
import { contrastingTextColor } from '@/features/tax/utils/taxGroupButtonColors';
import {
  pushRecentTaxGroupId,
  readRecentTaxGroupIds,
} from '@/features/tax/utils/recentTaxGroups';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useTaxGroups } from '@/hooks/useTaxGroups';
import { useI18n } from '@/i18n';

export type TaxQuickActionsProps = {
  /** Selected product ids to assign the chosen tax group to. */
  selectedProductIds: string[];
  /** When false, controls stay visible but disabled. */
  canManage?: boolean;
  style?: React.CSSProperties;
};

export function TaxQuickActions({
  selectedProductIds,
  canManage = true,
  style,
}: TaxQuickActionsProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { tenantId } = useCurrentTenant();
  const { data: taxGroups, isLoading } = useTaxGroups();
  const [recentIds, setRecentIds] = useState<string[]>(() => readRecentTaxGroupIds(tenantId));

  useEffect(() => {
    setRecentIds(readRecentTaxGroupIds(tenantId));
  }, [tenantId]);

  const activeGroups = useMemo(
    () => (taxGroups ?? []).filter((g) => g.isActive),
    [taxGroups]
  );

  const recentGroups = useMemo(() => {
    const byId = new Map(activeGroups.map((g) => [g.id, g]));
    return recentIds.map((id) => byId.get(id)).filter((g): g is TaxGroupAdmin => !!g);
  }, [activeGroups, recentIds]);

  const applyMutation = useMutation({
    mutationFn: applyTaxGroupToProducts,
    onSuccess: (result, vars) => {
      setRecentIds(pushRecentTaxGroupId(vars.taxGroupId, tenantId));
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      void queryClient.invalidateQueries({ queryKey: taxHistoryQueryKey });
      if (result.updatedProducts > 0) {
        notify.successKey('products.actions.quickTax.success', {
          count: result.updatedProducts,
          rate: result.newRate,
        });
      } else {
        notify.info(t('products.actions.quickTax.unchanged'));
      }
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TaxQuickActions.apply',
        fallbackKey: 'products.actions.quickTax.failed',
      });
    },
  });

  const applyTaxToSelected = useCallback(
    (group: TaxGroupAdmin) => {
      if (!canManage) return;
      if (selectedProductIds.length === 0) {
        notify.warning(t('products.actions.quickTax.selectProductsFirst'));
        return;
      }

      modal.confirm({
        title: t('products.actions.quickTax.confirmTitle'),
        content: t('products.actions.quickTax.confirmContent', {
          count: selectedProductIds.length,
          name: group.name,
          rate: group.rate,
        }),
        okText: t('products.actions.quickTax.confirmOk'),
        cancelText: t('common.buttons.cancel'),
        onOk: () =>
          applyMutation.mutateAsync({
            taxGroupId: group.id,
            productIds: selectedProductIds,
            reason: 'Quick tax assign',
          }),
      });
    },
    [applyMutation, canManage, modal, notify, selectedProductIds, t]
  );

  const disabled = !canManage || applyMutation.isPending;

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%', ...style }}>
      <Card title={t('products.actions.quickTax.selectorTitle')} size="small" loading={isLoading}>
        {selectedProductIds.length === 0 ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            title={t('products.actions.quickTax.selectHint')}
          />
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
            {t('products.actions.quickTax.applyHint', { count: selectedProductIds.length })}
          </Typography.Text>
        )}
        <Space wrap size={[8, 8]}>
          {activeGroups.map((group) => {
            const bg = group.color || undefined;
            const fg = contrastingTextColor(group.color);
            return (
              <Button
                key={group.id}
                size="small"
                disabled={disabled}
                loading={applyMutation.isPending && applyMutation.variables?.taxGroupId === group.id}
                onClick={() => applyTaxToSelected(group)}
                style={
                  bg
                    ? {
                        backgroundColor: bg,
                        borderColor: bg,
                        color: fg,
                      }
                    : undefined
                }
              >
                {group.icon ? `${group.icon} ` : ''}
                {group.rate}%
              </Button>
            );
          })}
        </Space>
      </Card>

      {recentGroups.length > 0 ? (
        <Card title={t('products.actions.quickTax.recentTitle')} size="small">
          <Space wrap size={[8, 8]}>
            {recentGroups.map((group) => (
              <Tag
                key={group.id}
                color={group.color || undefined}
                style={{
                  cursor: disabled ? 'not-allowed' : 'pointer',
                  opacity: disabled ? 0.6 : 1,
                  userSelect: 'none',
                }}
                onClick={() => {
                  if (!disabled) applyTaxToSelected(group);
                }}
              >
                {group.icon ? `${group.icon} ` : ''}
                {group.rate}%
              </Tag>
            ))}
          </Space>
        </Card>
      ) : null}
    </Space>
  );
}
