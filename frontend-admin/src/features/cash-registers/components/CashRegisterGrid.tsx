'use client';
import type { ReactNode } from 'react';
import {
    CheckCircleOutlined,
    ClockCircleOutlined,
    EnvironmentOutlined,
    EyeOutlined,
    FileProtectOutlined,
    LockOutlined,
    MinusCircleOutlined,
    ShopOutlined,
    StopOutlined,
    ToolOutlined,
    UserOutlined,
    WalletOutlined,
} from '@ant-design/icons';
import { Button, Card, Col, Empty, Row, Tag, Tooltip, Typography } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    rawRegisterStatus,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';
import styles from './CashRegisterGrid.module.css';

export type CashRegisterGridProps = {
    registers: CashRegister[];
    loading?: boolean;
    canCreate?: boolean;
    canManage?: boolean;
    totalRegisterCount?: number;
    canDecommission: boolean;
    statusLabel: (status: number | undefined) => string;
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
};

function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

function renderLoadingCards() {
    return Array.from({ length: 8 }, (_, index) => (
        <Col xs={24} sm={12} xl={8} xxl={6} key={`loading-${index}`}>
            <Card loading className={styles.loadingCard} />
        </Col>
    ));
}

function statusBadgeClass(status: number | undefined): string {
    switch (status) {
        case REGISTER_STATUS.open:
            return `${styles.statusBadge} ${styles.statusOpen}`;
        case REGISTER_STATUS.closed:
            return `${styles.statusBadge} ${styles.statusClosed}`;
        case REGISTER_STATUS.decommissioned:
            return `${styles.statusBadge} ${styles.statusDecommissioned}`;
        case REGISTER_STATUS.maintenance:
            return `${styles.statusBadge} ${styles.statusMaintenance}`;
        case REGISTER_STATUS.disabled:
        default:
            return `${styles.statusBadge} ${styles.statusDisabled}`;
    }
}

function statusIcon(status: number | undefined) {
    switch (status) {
        case REGISTER_STATUS.open:
            return <CheckCircleOutlined />;
        case REGISTER_STATUS.closed:
            return <LockOutlined />;
        case REGISTER_STATUS.decommissioned:
            return <StopOutlined />;
        case REGISTER_STATUS.maintenance:
            return <ToolOutlined />;
        case REGISTER_STATUS.disabled:
        default:
            return <MinusCircleOutlined />;
    }
}

export function CashRegisterGrid({
    registers,
    loading = false,
    canCreate = false,
    canManage = false,
    totalRegisterCount = 0,
    canDecommission,
    statusLabel,
    onEdit,
    onDecommission,
}: CashRegisterGridProps) {
    const { t, formatLocale } = useI18n();

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
                const status = rawRegisterStatus(register);
                const decommissioned = isDecommissionedRegister(status);
                const canStilllegen =
                    canDecommission &&
                    !decommissioned &&
                    canDecommissionRegister(status);

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
                        </Tooltip>,
                    );
                }

                if (decommissioned) {
                    actions.push(
                        <Tooltip title={t('cashRegisters.decommission.restoreTooltip')} key="decommission-disabled">
                            <Button
                                type="text"
                                icon={<MinusCircleOutlined />}
                                aria-label={t('cashRegisters.actions.restore')}
                                disabled
                            />
                        </Tooltip>,
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
                        </Tooltip>,
                    );
                }

                actions.push(
                    <Tooltip title={t('cashRegisters.actions.specialReceipts')} key="special">
                        <Button
                            type="text"
                            icon={<FileProtectOutlined />}
                            aria-label={t('cashRegisters.actions.specialReceipts')}
                            href="/rksv/sonderbelege?focus=schlussbeleg"
                        />
                    </Tooltip>,
                );

                return (
                    <Col xs={24} sm={12} xl={8} xxl={6} key={register.id ?? register.registerNumber}>
                        <Card
                            hoverable
                            className={`${styles.registerCard}${decommissioned ? ` ${styles.decommissionedCard}` : ''}`}
                            actions={actions}
                        >
                            <Card.Meta
                                avatar={<ShopOutlined style={{ fontSize: 28, color: '#1677ff' }} />}
                                title={<span className={styles.metaTitle}>{register.registerNumber?.trim() || FORMAT_EMPTY_DISPLAY}</span>}
                                description={
                                    <span className={styles.metaDescription}>
                                        <EnvironmentOutlined />
                                        {register.location?.trim() || FORMAT_EMPTY_DISPLAY}
                                    </span>
                                }
                            />

                            <div className={styles.statusRow}>
                                <span className={statusBadgeClass(status)}>
                                    {statusIcon(status)}
                                    {statusLabel(status)}
                                </span>
                                <Tag>{register.isActive === false ? t('common.categories.table.inactive') : t('common.categories.table.active')}</Tag>
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
                                            formatLocale,
                                        )}
                                    </Typography.Text>
                                </div>

                                <div>
                                    <Typography.Text className={styles.detailLabel}>
                                        <UserOutlined /> {t('cashRegisters.detail.currentUser')}
                                    </Typography.Text>
                                    <Typography.Text className={styles.detailValue}>
                                        {register.currentUser?.userName?.trim() ||
                                            register.currentUserId?.trim() ||
                                            FORMAT_EMPTY_DISPLAY}
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
