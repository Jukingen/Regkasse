'use client';

import { useCallback, useMemo, useState, type ReactNode } from 'react';
import {
    PlayCircleOutlined,
    StopOutlined,
    FileTextOutlined,
    DollarOutlined,
    WarningOutlined,
    ShoppingOutlined,
    ThunderboltOutlined,
    CheckCircleOutlined,
    TeamOutlined,
    CloudServerOutlined,
} from '@ant-design/icons';
import { Alert, Button, FloatButton, Modal, Space, Tag } from 'antd';
import { useRouter } from 'next/navigation';

import { useBackupAttention } from '@/features/backup/hooks/useBackupAttention';
import { FA_QUICK_CASH_REGISTER_QUERY_PARAM } from '@/features/cash-registers/constants/quickSwitch';
import { usePendingMonatsbeleg } from '@/features/rksv/hooks/usePendingMonatsbeleg';
import { useOpenShifts } from '@/features/shifts/hooks/useOpenShifts';
import { useShiftManagement } from '@/features/shifts/hooks/useShiftManagement';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPosAppOpenUrl } from '@/lib/posAppUrl';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

type QuickActionKey = 'open-shift' | 'close-shift';

type QuickActionItem = {
    key: string;
    icon: ReactNode;
    label: string;
    onClick: () => void;
    visible: boolean;
    badge?: number;
    type?: 'warning';
};

export function QuickActions() {
    const router = useRouter();
    const { t } = useI18n();
    const { message } = useAntdApp();
    const { hasPermission } = usePermissions();
    const { tenantSlug } = useCurrentTenant();
    const { selectedRegister } = useCashRegisterSelection({
        autoSelect: true,
        persistSelection: true,
    });
    const { openShift, closeShift, isShiftOpen, isLoading } = useShiftManagement(selectedRegister?.id);
    const { data: pendingMonatsbeleg = [] } = usePendingMonatsbeleg();
    const { data: openShifts = [] } = useOpenShifts(selectedRegister?.id);
    const { needsAttention: backupNeedsAttention } = useBackupAttention();

    const [modalOpen, setModalOpen] = useState(false);
    const [action, setAction] = useState<QuickActionKey | null>(null);

    const canManageShifts = hasPermission(PERMISSIONS.SHIFT_MANAGE);
    const canViewReports = hasPermission(PERMISSIONS.REPORT_VIEW);
    const canViewCashRegisters = hasPermission(PERMISSIONS.CASHREGISTER_MANAGE);
    const canViewDailyClosing = hasPermission(PERMISSIONS.DAILY_CLOSING_VIEW);
    const canViewShifts = hasPermission(PERMISSIONS.SHIFT_VIEW);
    const canViewBackup = hasPermission(PERMISSIONS.SETTINGS_VIEW);
    const posUrl = buildPosAppOpenUrl(tenantSlug);

    const handleAction = (key: QuickActionKey) => {
        setAction(key);
        setModalOpen(true);
    };

    const navigateWithRegister = useCallback(
        (path: string) => {
            if (selectedRegister?.id) {
                router.push(
                    `${path}?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(selectedRegister.id)}`,
                );
                return;
            }
            router.push(path);
        },
        [router, selectedRegister?.id],
    );

    const actions = useMemo((): QuickActionItem[] => {
        const openShiftCount = openShifts.length;

        return [
            {
                key: 'open-shift',
                icon: <PlayCircleOutlined />,
                label: t('quickActions.actions.openShift'),
                onClick: () => handleAction('open-shift'),
                visible: Boolean(canManageShifts && !isShiftOpen && selectedRegister),
            },
            {
                key: 'close-shift',
                icon: <StopOutlined />,
                label: t('quickActions.actions.closeShift'),
                onClick: () => handleAction('close-shift'),
                visible: Boolean(canManageShifts && isShiftOpen && selectedRegister),
            },
            {
                key: 'daily-closing',
                icon: <CheckCircleOutlined />,
                label: t('quickActions.actions.dailyClosing'),
                onClick: () => navigateWithRegister('/tagesabschluss'),
                visible: Boolean(canViewDailyClosing && selectedRegister),
            },
            {
                key: 'daily-report',
                icon: <FileTextOutlined />,
                label: t('quickActions.actions.dailyReport'),
                onClick: () => navigateWithRegister('/reporting/tagesbericht'),
                visible: Boolean(canViewReports && selectedRegister),
            },
            {
                key: 'open-shifts',
                icon: <TeamOutlined />,
                label:
                    openShiftCount > 0
                        ? t('quickActions.actions.openShiftsCount', { count: openShiftCount })
                        : t('quickActions.actions.openShifts'),
                onClick: () => router.push('/shifts'),
                visible: canViewShifts,
                badge: openShiftCount > 0 ? openShiftCount : undefined,
            },
            {
                key: 'cash-balance',
                icon: <DollarOutlined />,
                label: t('quickActions.actions.cashBalance'),
                onClick: () => navigateWithRegister('/kassenverwaltung'),
                visible: Boolean(canViewCashRegisters && selectedRegister),
            },
            {
                key: 'monatsbeleg',
                icon: <WarningOutlined />,
                label:
                    pendingMonatsbeleg.length > 0
                        ? t('quickActions.actions.monatsbelegMissing', {
                              count: pendingMonatsbeleg.length,
                          })
                        : t('quickActions.actions.monatsbeleg'),
                onClick: () => router.push(RKSV_SONDERBELEGE_PATH),
                visible: pendingMonatsbeleg.length > 0,
                badge: pendingMonatsbeleg.length,
                type: 'warning',
            },
            {
                key: 'backup-failed',
                icon: <CloudServerOutlined />,
                label: t('quickActions.actions.backupFailed'),
                onClick: () => router.push('/settings/backup-dr'),
                visible: Boolean(canViewBackup && backupNeedsAttention),
                type: 'warning',
            },
            {
                key: 'pos',
                icon: <ShoppingOutlined />,
                label: t('quickActions.actions.openPos'),
                onClick: () => {
                    if (!posUrl) return;
                    window.open(posUrl, '_blank', 'noopener,noreferrer');
                },
                visible: Boolean(posUrl),
            },
        ];
    }, [
        backupNeedsAttention,
        canManageShifts,
        canViewBackup,
        canViewCashRegisters,
        canViewDailyClosing,
        canViewReports,
        canViewShifts,
        isShiftOpen,
        openShifts.length,
        pendingMonatsbeleg.length,
        posUrl,
        router,
        selectedRegister,
        t,
        navigateWithRegister,
    ]);

    const visibleActions = actions.filter((item) => item.visible);

    const handleConfirm = async () => {
        if (!selectedRegister?.id) {
            return;
        }

        try {
            if (action === 'open-shift') {
                await openShift(selectedRegister.id);
                message.success(t('quickActions.messages.shiftOpened'));
            } else if (action === 'close-shift') {
                await closeShift(selectedRegister.id);
                message.success(t('quickActions.messages.shiftClosed'));
            }
            setModalOpen(false);
        } catch {
            // API errors are surfaced by useShiftManagement mutation handlers.
        }
    };

    if (visibleActions.length === 0) {
        return null;
    }

    return (
        <>
            <FloatButton.Group
                trigger="click"
                type="primary"
                icon={<ThunderboltOutlined />}
                tooltip={t('quickActions.title')}
                style={{ right: 24, bottom: 24 }}
            >
                {visibleActions.map((item) => (
                    <FloatButton
                        key={item.key}
                        icon={item.icon}
                        onClick={item.onClick}
                        type={item.type === 'warning' ? 'primary' : 'default'}
                        badge={item.badge ? { count: item.badge } : undefined}
                        tooltip={item.label}
                    />
                ))}
            </FloatButton.Group>

            <Modal
                title={
                    action === 'open-shift'
                        ? t('quickActions.modals.openShift.title')
                        : t('quickActions.modals.closeShift.title')
                }
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                destroyOnHidden
                footer={[
                    <Button key="cancel" onClick={() => setModalOpen(false)}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button
                        key="confirm"
                        type="primary"
                        danger={action === 'close-shift'}
                        loading={isLoading}
                        onClick={() => void handleConfirm()}
                    >
                        {action === 'open-shift'
                            ? t('quickActions.modals.openShift.confirm')
                            : t('quickActions.modals.closeShift.confirm')}
                    </Button>,
                ]}
            >
                <Space direction="vertical" style={{ width: '100%' }}>
                    <Alert
                        title={
                            action === 'open-shift'
                                ? t('quickActions.modals.openShift.description')
                                : t('quickActions.modals.closeShift.description')
                        }
                        description={
                            action === 'close-shift'
                                ? t('quickActions.modals.closeShift.warning')
                                : undefined
                        }
                        type={action === 'close-shift' ? 'warning' : 'info'}
                        showIcon
                    />
                    {selectedRegister ? (
                        <div>
                            <Tag color="blue">{selectedRegister.location || selectedRegister.registerNumber}</Tag>
                            <span style={{ marginLeft: 8, color: '#64748b' }}>
                                {selectedRegister.registerNumber}
                            </span>
                        </div>
                    ) : null}
                </Space>
            </Modal>
        </>
    );
}
