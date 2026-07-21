'use client';
import {
  ClockCircleOutlined,
  CloudSyncOutlined,
  EnvironmentOutlined,
  EyeOutlined,
  FileProtectOutlined,
  MinusCircleOutlined,
  SafetyOutlined,
  ShopOutlined,
  StopOutlined,
  UserOutlined,
  WalletOutlined,
} from '@ant-design/icons';
import { Button, Card, Col, Empty, Row, Tag, Tooltip, Typography } from 'antd';
import type { ReactNode } from 'react';

import type { CashRegister } from '@/api/generated/model';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import {
  canDecommissionRegister,
  isDecommissionedRegister,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

import styles from './CashRegisterGrid.module.css';

export type CashRegisterGridProps = {
  registers: CashRegister[];
  loading?: boolean;
  canCreate?: boolean;
  canManage?: boolean;
  totalRegisterCount?: number;
  canDecommission: boolean;
  /** @deprecated Use CashRegisterStatusBadge. */
  statusLabel?: (status: number | undefined) => string;
  onEdit: (register: CashRegister) => void;
  onDecommission: (register: CashRegister) => void;
};

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function asEnhanced(record: CashRegister): EnhancedCashRegister {
  return record as EnhancedCashRegister;
}

function resolveCashierName(record: EnhancedCashRegister): string | null {
  const fromApi = record.currentCashierName?.trim();
  if (fromApi) {
    return fromApi;
  }
  return record.currentUser?.userName?.trim() || record.currentUserId?.trim() || null;
}

function renderLoadingCards() {
  return Array.from({ length: 8 }, (_, index) => (
    <Col xs={24} sm={12} xl={8} xxl={6} key={`loading-${index}`}>
      <Card loading className={styles.loadingCard} />
    </Col>
  ));
}

export function CashRegisterGrid({
  registers,
  loading = false,
  canCreate = false,
  canManage = false,
  totalRegisterCount = 0,
  canDecommission,
  onEdit,
  onDecommission,
}: CashRegisterGridProps) {
  const { t, formatLocale } = useI18n();
  const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);

  const emptyDescription =
    totalRegisterCount === 0
      ? canCreate
        ? t('cashRegisters.emptyCanCreate')
        : t('cashRegisters.emptyContactAdmin')
      : t('cashRegisters.empty');

  if (loading && registers.length === 0) {
    return <Row gutter={[16, 16]}>{renderLoadingCards()}</Row>;
  }

  if (registers.length === 0) {
    return <Empty description={emptyDescription} />;
  }

  return (
    <Row gutter={[16, 16]}>
      {registers.map((register) => {
        const enhanced = asEnhanced(register);
        const status = rawRegisterStatus(register);
        const decommissioned = isDecommissionedRegister(status);
        const registerId = register.id?.trim();
        const offlineHref = registerId
          ? `/admin/tse/offline-transactions?cashRegisterId=${encodeURIComponent(registerId)}`
          : '/admin/tse/offline-transactions';
        const canStilllegen = canDecommission && !decommissioned && canDecommissionRegister(status);

        const actions: ReactNode[] = [];

        if (canManage) {
          actions.push(
            <Tooltip title={t('cashRegisters.actions.view')} key="view">
              <Button
                type="text"
                icon={<EyeOutlined />}
                aria-label={t('cashRegisters.actions.view')}
                onClick={() => onEdit(register)}
              />
            </Tooltip>
          );
        }

        if (decommissioned) {
          actions.push(
            <Tooltip
              title={t('cashRegisters.decommission.restoreTooltip')}
              key="decommission-disabled"
            >
              <Button
                type="text"
                icon={<MinusCircleOutlined />}
                aria-label={t('cashRegisters.actions.restore')}
                disabled
              />
            </Tooltip>
          );
        } else if (canDecommission) {
          actions.push(
            <Tooltip
              title={
                !canDecommissionRegister(status)
                  ? t('cashRegisters.decommission.mustCloseFirst')
                  : t('cashRegisters.actions.decommission')
              }
              key="decommission"
            >
              <Button
                type="text"
                danger
                icon={<StopOutlined />}
                aria-label={t('cashRegisters.actions.decommission')}
                disabled={!canStilllegen}
                onClick={() => onDecommission(register)}
              />
            </Tooltip>
          );
        }

        actions.push(
          <Tooltip title={t('cashRegisters.actions.tseHealth')} key="tse">
            <Button
              type="text"
              icon={<SafetyOutlined />}
              aria-label={t('cashRegisters.actions.tseHealth')}
              href="/rksv/status"
            />
          </Tooltip>
        );

        if ((enhanced.offlineQueueCount ?? 0) > 0) {
          actions.push(
            <Tooltip
              title={t('cashRegisters.offlineQueue.tooltip', {
                count: enhanced.offlineQueueCount ?? 0,
              })}
              key="offline"
            >
              <Button
                type="text"
                icon={<CloudSyncOutlined />}
                aria-label={t('cashRegisters.actions.offlineQueue')}
                href={offlineHref}
              />
            </Tooltip>
          );
        }

        if (canOpenSonderbelege) {
          actions.push(
            <Tooltip title={t('cashRegisters.actions.specialReceipts')} key="special">
              <Button
                type="text"
                icon={<FileProtectOutlined />}
                aria-label={t('cashRegisters.actions.specialReceipts')}
                href="/rksv/sonderbelege?focus=schlussbeleg"
              />
            </Tooltip>
          );
        }

        return (
          <Col xs={24} sm={12} xl={8} xxl={6} key={register.id ?? register.registerNumber}>
            <Card
              hoverable
              className={`${styles.registerCard}${decommissioned ? ` ${styles.decommissionedCard}` : ''}`}
              actions={actions}
            >
              <Card.Meta
                avatar={<ShopOutlined style={{ fontSize: 28, color: '#1677ff' }} />}
                title={
                  <span className={styles.metaTitle}>
                    {register.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}
                  </span>
                }
                description={
                  <span className={styles.metaDescription}>
                    <EnvironmentOutlined />
                    {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
                  </span>
                }
              />

              <div className={styles.statusRow}>
                <CashRegisterStatusBadge register={register} />
                <Tag>
                  {register.isActive === false
                    ? t('common.categories.table.inactive')
                    : t('common.categories.table.active')}
                </Tag>
              </div>

              <div className={styles.statusRow}>
                <TseHealthBadge status={enhanced.tseHealthStatus} />
                {(enhanced.offlineQueueCount ?? 0) > 0 ? (
                  <Tag color="orange">
                    {t('cashRegisters.offlineQueue.label', {
                      count: enhanced.offlineQueueCount ?? 0,
                    })}
                  </Tag>
                ) : null}
              </div>

              <div className={styles.details}>
                <div>
                  <Typography.Text className={styles.detailLabel}>
                    <WalletOutlined /> {t('cashRegisters.detail.currentBalance')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {isFiniteNumber(register.currentBalance)
                      ? formatCurrency(register.currentBalance, formatLocale)
                      : FORMAT_EMPTY_DISPLAY}
                  </Typography.Text>
                </div>

                <div>
                  <Typography.Text className={styles.detailLabel}>
                    <ClockCircleOutlined />{' '}
                    {decommissioned
                      ? t('cashRegisters.detail.decommissionedAt')
                      : t('cashRegisters.detail.lastBalanceUpdate')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {formatDateTime(
                      decommissioned ? register.decommissionedAtUtc : register.lastBalanceUpdate,
                      formatLocale
                    )}
                  </Typography.Text>
                </div>

                <div>
                  <Typography.Text className={styles.detailLabel}>
                    <UserOutlined /> {t('cashRegisters.detail.currentCashier')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {resolveCashierName(enhanced) ?? FORMAT_EMPTY_DISPLAY}
                  </Typography.Text>
                </div>

                <div>
                  <Typography.Text className={styles.detailLabel}>
                    <ClockCircleOutlined /> {t('cashRegisters.detail.lastSyncAtUtc')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {enhanced.lastSyncAtUtc
                      ? formatDateTime(enhanced.lastSyncAtUtc, formatLocale)
                      : FORMAT_EMPTY_DISPLAY}
                  </Typography.Text>
                </div>

                <div>
                  <Typography.Text className={styles.detailLabel}>
                    {t('cashRegisters.detail.lastMonatsbelegUtc')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {enhanced.lastMonatsbelegUtc
                      ? formatDateTime(enhanced.lastMonatsbelegUtc, formatLocale)
                      : FORMAT_EMPTY_DISPLAY}
                  </Typography.Text>
                </div>

                <div>
                  <Typography.Text className={styles.detailLabel}>
                    {t('cashRegisters.detail.lastJahresbelegUtc')}
                  </Typography.Text>
                  <Typography.Text className={styles.detailValue}>
                    {enhanced.lastJahresbelegUtc
                      ? formatDateTime(enhanced.lastJahresbelegUtc, formatLocale)
                      : FORMAT_EMPTY_DISPLAY}
                  </Typography.Text>
                </div>
              </div>
            </Card>
          </Col>
        );
      })}
    </Row>
  );
}
