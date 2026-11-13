'use client';

import { DownloadOutlined, FileProtectOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Row,
  Space,
  Spin,
  Statistic,
  Tooltip,
  Typography,
} from 'antd';
import { useMemo } from 'react';
import { useRouter } from 'next/navigation';

import { CashRegisterAssignedUserField } from '@/features/cash-registers/components/CashRegisterAssignedUserField';
import { CashierDisplay } from '@/features/cash-registers/components/CashierDisplay';
import { CashRegisterDetailHistory } from '@/features/cash-registers/components/CashRegisterDetailHistory';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { CashRegisterStatusContextAlert } from '@/features/cash-registers/components/CashRegisterStatusContextAlert';
import { LimitWarning } from '@/features/tenants/components/LimitWarning';
import { useCashRegisterActionHandler } from '@/features/cash-registers/hooks/useCashRegisterActionHandler';
import { useCashRegisterPermissions } from '@/features/cash-registers/hooks/useCashRegisterPermissions';
import { isOpenShiftHeldBy } from '@/features/cash-registers/utils/shiftOccupancy';
import {
  adminCashRegisterDetailQueryKey,
  getAdminCashRegisterById,
  toEnhancedCashRegister,
} from '@/features/cash-registers/api/cashRegisters';
import { fetchAdminPaymentsList } from '@/features/payments/api/adminPaymentsListQuery';
import { fetchAdminShiftOverview } from '@/features/shifts/api/shiftsOverview';
import {
  REGISTER_STATUS,
  isDecommissionedRegister,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { usePermissions } from '@/hooks/usePermissions';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import {
  KASSENVERWALTUNG_PATH,
  cashRegisterReportsPath,
} from '@/shared/cashRegisterRoutes';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { getHttpStatusFromError } from '@/lib/queryErrorHandling';

export type CashRegisterDetailProps = {
  registerId: string;
};

export function CashRegisterDetail({ registerId }: CashRegisterDetailProps) {
  const { t, formatLocale } = useI18n();
  const router = useRouter();
  const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
  const { user } = usePermissions();
  const id = registerId.trim();

  const registerQuery = useQuery({
    queryKey: adminCashRegisterDetailQueryKey(id),
    queryFn: () => getAdminCashRegisterById(id),
    enabled: Boolean(id),
    staleTime: 15_000,
  });

  const statsQuery = useQuery({
    queryKey: ['admin', 'cash-registers', id, 'stats'],
    queryFn: async () => {
      const [payments, shifts] = await Promise.all([
        fetchAdminPaymentsList({
          cashRegisterId: id,
          page: 1,
          pageNumber: 1,
          pageSize: 50,
          sortBy: 'CreatedAt',
          sortDirection: 'desc',
          includeTotalCount: true,
        }),
        fetchAdminShiftOverview({ cashRegisterId: id, limit: 100 }),
      ]);
      const shiftRows = [...(shifts.activeShifts ?? []), ...(shifts.shiftHistory ?? [])];
      const revenueFromPayments = (payments.items ?? []).reduce(
        (sum, row) => sum + (typeof row.totalAmount === 'number' ? row.totalAmount : 0),
        0
      );
      const revenueFromShifts = shiftRows.reduce(
        (sum, row) => sum + (Number.isFinite(row.totalSales) ? row.totalSales : 0),
        0
      );
      return {
        totalTransactions: payments.total ?? payments.items.length,
        totalRevenue: revenueFromShifts > 0 ? revenueFromShifts : revenueFromPayments,
        totalShifts: shiftRows.length,
        lastTransaction: payments.items[0]?.createdAt ?? null,
        paymentRows: payments.items,
      };
    },
    enabled: Boolean(id),
    staleTime: 15_000,
  });

  const register = useMemo(
    () => (registerQuery.data ? toEnhancedCashRegister(registerQuery.data) : null),
    [registerQuery.data]
  );

  const permissions = useCashRegisterPermissions(registerQuery.data ?? null);
  const status = register ? rawRegisterStatus(register) : undefined;
  const decommissioned = permissions.isDecommissioned || isDecommissionedRegister(status);
  const isClosed = status === REGISTER_STATUS.closed;
  const isOpen = status === REGISTER_STATUS.open;
  const holdsOpenShift = isOpenShiftHeldBy(register?.currentUserId, user?.id);
  const registerName =
    register?.registerNumber?.trim() || t('cashRegisters.detail.title');

  const { handleRegisterAction, shiftActionPending, canForceClose } = useCashRegisterActionHandler({
    onEdit: () => undefined,
    onDecommission: () => undefined,
    onHardDelete: () => undefined,
  });

  const closeDisabled =
    decommissioned ||
    !isOpen ||
    shiftActionPending ||
    !permissions.canClose ||
    (!holdsOpenShift && !canForceClose);

  const exportData = () => {
    const rows = statsQuery.data?.paymentRows ?? [];
    const csv = rowsToCsv([
      [
        t('receipts.table.colReceipt'),
        t('cashRegisters.detail.createdAt'),
        t('payments.table.colMethod'),
        t('cashRegisters.detail.statRevenue'),
        t('cashRegisters.detail.status'),
      ],
      ...rows.map((row) => [
        row.receiptNumber ?? '',
        row.createdAt ?? '',
        row.method ?? '',
        row.totalAmount ?? '',
        row.status ?? '',
      ]),
    ]);
    downloadCsvText(csv, `cash-register-${id}-payments.csv`);
  };

  if (!id) {
    return <Alert type="warning" showIcon title={t('cashRegisters.detail.notFound')} />;
  }

  if (registerQuery.isLoading) {
    return (
      <div style={{ textAlign: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  if (registerQuery.error || !register) {
    const statusCode = getHttpStatusFromError(registerQuery.error);
    const denied = statusCode === 403 || statusCode === 401;
    return (
      <Alert
        type="error"
        showIcon
        title={denied ? t('cashRegisters.permission.denied') : t('cashRegisters.detail.notFound')}
        description={
          registerQuery.error
            ? getUserFacingApiErrorMessage(t, registerQuery.error, {
                logContext: 'CashRegisterDetail.load',
                fallbackKey: denied
                  ? 'cashRegisters.permission.denied'
                  : 'cashRegisters.errors.loadFailed',
              })
            : undefined
        }
      />
    );
  }

  if (!permissions.canView) {
    return (
      <Alert type="error" showIcon title={t('cashRegisters.permission.denied')} />
    );
  }

  const openDisabled =
    decommissioned || !isClosed || shiftActionPending || !permissions.canOpen;
  const showActionsCard =
    permissions.canManageShifts || permissions.canViewReports || permissions.canExport;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('cashRegisters.detail.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('cashRegisters.pageTitle'), href: KASSENVERWALTUNG_PATH },
          { title: registerName },
        ]}
      />

      <CashRegisterStatusContextAlert register={register} showOpenPrerequisites={!decommissioned} />

      <LimitWarning limitKey="maxActiveRegistersPerUser" />

      <Card title={t('cashRegisters.detail.title')}>
        <Descriptions column={{ xs: 1, sm: 2, lg: 3 }} bordered size="small">
          <Descriptions.Item label={t('cashRegisters.detail.name')}>
            {register.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.description')}>
            {FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.location')}>
            {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.status')}>
            <CashRegisterStatusBadge register={register} />
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.currentCashier')}>
            <CashierDisplay
              user={register.currentUser}
              displayName={register.currentCashierName}
              userName={register.currentCashierUserName}
              email={register.currentCashierEmail}
            />
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.assignedUser')}>
            <CashRegisterAssignedUserField
              registerId={id}
              assignedUserId={registerQuery.data?.assignedUserId}
              assignedUserName={registerQuery.data?.assignedUserName}
              canEdit={permissions.canAssignUser}
              disabled={decommissioned}
            />
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.createdAt')}>
            {register.createdAt
              ? formatDateTime(register.createdAt, formatLocale)
              : FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
          <Descriptions.Item label={t('cashRegisters.detail.updatedAt')}>
            {register.updatedAt
              ? formatDateTime(register.updatedAt, formatLocale)
              : FORMAT_EMPTY_DISPLAY}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title={t('cashRegisters.detail.statistics')} loading={statsQuery.isLoading}>
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} lg={6}>
            <Statistic
              title={t('cashRegisters.detail.statTransactions')}
              value={statsQuery.data?.totalTransactions ?? 0}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Statistic
              title={t('cashRegisters.detail.statRevenue')}
              value={statsQuery.data?.totalRevenue ?? 0}
              formatter={(value) => formatCurrency(Number(value), formatLocale)}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Statistic
              title={t('cashRegisters.detail.statShifts')}
              value={statsQuery.data?.totalShifts ?? 0}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Statistic
              title={t('cashRegisters.detail.statLastTransaction')}
              value={
                statsQuery.data?.lastTransaction
                  ? formatDateTime(statsQuery.data.lastTransaction, formatLocale)
                  : FORMAT_EMPTY_DISPLAY
              }
            />
          </Col>
        </Row>
      </Card>

      <Card title={t('cashRegisters.detail.history')}>
        <CashRegisterDetailHistory registerId={id} />
      </Card>

      {showActionsCard ? (
      <Card title={t('cashRegisters.detail.actions')}>
        <Space wrap>
          {permissions.canManageShifts ? (
            <>
          <Tooltip
            title={
              decommissioned
                ? t('cashRegisters.permission.decommissioned')
                : undefined
            }
          >
            <span>
              <Button
                type="primary"
                disabled={openDisabled}
                loading={shiftActionPending}
                onClick={() => handleRegisterAction('open-shift', register)}
              >
                {t('cashRegisters.actions.openRegister')}
              </Button>
            </span>
          </Tooltip>
          <Tooltip
            title={
              decommissioned
                ? t('cashRegisters.permission.decommissioned')
                : isOpen && !holdsOpenShift && !canForceClose
                  ? t('cashRegisters.shift.closeHeldByOther')
                  : undefined
            }
          >
            <span>
              <Button
                disabled={closeDisabled}
                loading={shiftActionPending}
                onClick={() => handleRegisterAction('close-shift', register)}
              >
                {t('cashRegisters.actions.closeRegister')}
              </Button>
            </span>
          </Tooltip>
          {canOpenSonderbelege ? (
            <Tooltip
              title={
                decommissioned
                  ? t('cashRegisters.actions.decommissionedCannotCreateSpecialReceipts')
                  : undefined
              }
            >
              <span>
                <Button
                  icon={<FileProtectOutlined />}
                  disabled={decommissioned}
                  href={decommissioned ? undefined : '/rksv/sonderbelege'}
                >
                  {t('cashRegisters.actions.createSpecialReceipt')}
                </Button>
              </span>
            </Tooltip>
          ) : null}
            </>
          ) : null}
          {permissions.canViewReports ? (
            <Button onClick={() => router.push(cashRegisterReportsPath(id))}>
              {t('cashRegisters.actions.viewReports')}
            </Button>
          ) : null}
          {permissions.canExport ? (
            <Button icon={<DownloadOutlined />} onClick={exportData}>
              {t('cashRegisters.actions.exportData')}
            </Button>
          ) : null}
        </Space>
        {permissions.canViewReports ? (
          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
            <Button type="link" href={cashRegisterReportsPath(id)} style={{ paddingInline: 0 }}>
              {t('cashRegisters.detail.openReports')}
            </Button>
          </Typography.Paragraph>
        ) : null}
      </Card>
      ) : null}
    </Space>
  );
}
