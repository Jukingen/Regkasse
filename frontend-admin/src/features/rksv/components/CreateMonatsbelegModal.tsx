'use client';

import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Alert, Descriptions, Input, Modal, Tag, Typography } from 'antd';
import { ExclamationCircleOutlined, InfoCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useCreateMonatsbeleg } from '@/features/rksv/hooks/useCreateMonatsbeleg';
import {
    isMonatsbelegWarningResponse,
    warningSeverityToAlertType,
    type MonatsbelegWarningResponse,
} from '@/features/rksv/types/monatsbelegWarning';
import { formatViennaYearMonth, getMonthDifference } from '@/shared/utils/viennaCalendar';
import { MonatsbelegLateSuccessModal } from '@/features/rksv/components/MonatsbelegLateSuccessModal';
import {
    toMonatsbelegLateSuccessResult,
    type MonatsbelegLateSuccessResult,
} from '@/features/rksv/types/createMonatsbelegResponseExtended';

export type CreateMonatsbelegModalProps = {
    open: boolean;
    cashRegisterId: string;
    year: number;
    month: number;
    reason?: string;
    onClose: () => void;
    onSuccess: () => void;
};

type SeverityUi = {
    color: string;
    icon: ReactNode;
    title: string;
    alertType: 'info' | 'warning' | 'error';
};

function getSeverityConfig(monthDiff: number): SeverityUi {
    if (monthDiff <= 1) {
        return {
            color: 'blue',
            icon: <InfoCircleOutlined />,
            title: 'Info',
            alertType: 'info',
        };
    }
    if (monthDiff <= 6) {
        return {
            color: 'orange',
            icon: <WarningOutlined />,
            title: 'Warnung',
            alertType: 'warning',
        };
    }
    return {
        color: 'red',
        icon: <ExclamationCircleOutlined />,
        title: 'Achtung',
        alertType: 'error',
    };
}

function severityFromApi(severity: MonatsbelegWarningResponse['severity'], monthDiff: number): SeverityUi {
    const alertType = warningSeverityToAlertType(severity);
    if (alertType === 'info') {
        return {
            color: 'blue',
            icon: <InfoCircleOutlined />,
            title: 'Info',
            alertType: 'info',
        };
    }
    if (alertType === 'error') {
        return {
            color: 'red',
            icon: <ExclamationCircleOutlined />,
            title: 'Achtung',
            alertType: 'error',
        };
    }
    return getSeverityConfig(monthDiff);
}

function buildPastMonthPreviewMessage(monthDiff: number): string {
    if (monthDiff === 1) {
        return 'Monatsbeleg für den Vormonat wird erstellt. Dies ist zulässig.';
    }
    if (monthDiff <= 6) {
        return `Monatsbeleg für ${monthDiff} Monate zurück wird erstellt. FinanzOnline akzeptiert dies in der Regel.`;
    }
    return `Monatsbeleg für ${monthDiff} Monate zurück wird erstellt. Dies könnte bei einer Betriebsprüfung hinterfragt werden. Nur nach Rücksprache mit einem Steuerberater fortfahren.`;
}

function formatRegisterShort(registerId: string): string {
    const trimmed = registerId.trim();
    if (trimmed.length <= 8) return trimmed;
    return `${trimmed.slice(0, 8)}…`;
}

export function CreateMonatsbelegModal({
    open,
    cashRegisterId,
    year,
    month,
    reason,
    onClose,
    onSuccess,
}: CreateMonatsbelegModalProps) {
    const { message, modal } = useAntdApp();
    const createMonatsbeleg = useCreateMonatsbeleg();
    const [forceMode, setForceMode] = useState(false);
    const [warning, setWarning] = useState<{ message: string; severity: string } | null>(null);
    const [lateReason, setLateReason] = useState('');
    const [successOpen, setSuccessOpen] = useState(false);
    const [successResult, setSuccessResult] = useState<MonatsbelegLateSuccessResult | null>(null);

    const monthDiff = useMemo(() => getMonthDifference(year, month), [year, month]);
    const isCurrentMonth = monthDiff === 0;
    const isPastMonth = monthDiff > 0;
    const isFutureMonth = monthDiff < 0;
    const severityConfig = useMemo(() => getSeverityConfig(monthDiff), [monthDiff]);

    useEffect(() => {
        if (open) {
            setForceMode(false);
            setWarning(null);
            setLateReason('');
            setSuccessOpen(false);
            setSuccessResult(null);
        }
    }, [open, cashRegisterId, year, month]);

    const handleSubmit = useCallback(
        async (explicitForce?: boolean) => {
            const withForce = explicitForce ?? forceMode;
            if (isFutureMonth) {
                message.error('Monatsbeleg kann nicht für einen zukünftigen Kalendermonat erstellt werden.');
                return;
            }

            const effectiveReason = isPastMonth
                ? lateReason.trim() ||
                  reason?.trim() ||
                  `Nachträglich erstellter Monatsbeleg für ${formatViennaYearMonth(year, month)}`
                : reason?.trim() || `Monatsbeleg für ${formatViennaYearMonth(year, month)}`;

            try {
                const response = await createMonatsbeleg.mutateAsync({
                    data: {
                        cashRegisterId,
                        year,
                        month,
                        reason: effectiveReason,
                    },
                    force: withForce,
                });

                if (lateResult.isLateCreated || isPastMonth || withForce) {
                    setSuccessResult(lateResult);
                    setSuccessOpen(true);
                    onSuccess();
                    return;
                }

                message.success(`Monatsbeleg für ${formatViennaYearMonth(year, month)} erfolgreich erstellt`);
                onSuccess();
                onClose();
            } catch (error: unknown) {
                if (!isAxiosError(error)) {
                    message.error('Fehler beim Erstellen des Monatsbelegs');
                    return;
                }

                const data = error.response?.data;
                if (isMonatsbelegWarningResponse(data) && !withForce) {
                    const warningMessage =
                        data.warningMessage?.trim() ||
                        buildPastMonthPreviewMessage(data.monthDiff ?? monthDiff);
                    const severity = data.severity ?? severityConfig.alertType;
                    const confirmSeverity = severityFromApi(severity, data.monthDiff ?? monthDiff);

                    setWarning({ message: warningMessage, severity });

                    modal.confirm({
                        title: 'Monatsbeleg nachträglich erstellen',
                        icon: confirmSeverity.icon,
                        content: (
                            <div>
                                <p style={{ marginBottom: 0 }}>{warningMessage}</p>
                                <p style={{ marginTop: 12, marginBottom: 0 }}>
                                    Der Beleg wird mit dem realen aktuellen Datum erstellt und als verspätet markiert
                                    (keine Rückdatierung).
                                </p>
                                <p style={{ marginTop: 12, marginBottom: 0, color: '#ff4d4f' }}>
                                    Möchten Sie fortfahren?
                                </p>
                            </div>
                        ),
                        okText: 'Nachträglich erstellen',
                        okButtonProps: {
                            danger: confirmSeverity.alertType === 'error',
                            type: 'primary',
                        },
                        cancelText: 'Abbrechen',
                        onOk: async () => {
                            setForceMode(true);
                            await handleSubmit(true);
                        },
                    });
                    return;
                }

                const apiError =
                    typeof data === 'object' &&
                    data !== null &&
                    'error' in data &&
                    typeof (data as { error?: unknown }).error === 'string'
                        ? (data as { error: string }).error
                        : typeof data === 'object' &&
                            data !== null &&
                            'message' in data &&
                            typeof (data as { message?: unknown }).message === 'string'
                          ? (data as { message: string }).message
                          : null;
                message.error(apiError ?? 'Fehler beim Erstellen des Monatsbelegs');
            }
        },
        [
            cashRegisterId,
            createMonatsbeleg,
            forceMode,
            isFutureMonth,
            isPastMonth,
            lateReason,
            message,
            modal,
            month,
            monthDiff,
            onClose,
            onSuccess,
            reason,
            severityConfig.alertType,
            year,
        ],
    );

    const warningAlertType = warning
        ? warningSeverityToAlertType(warning.severity)
        : severityConfig.alertType;

    return (
        <>
            <Modal
                title={isPastMonth ? 'Monatsbeleg nachträglich erstellen' : 'Monatsbeleg erstellen'}
                open={open && !successOpen}
            onCancel={onClose}
            onOk={() => void handleSubmit()}
            confirmLoading={createMonatsbeleg.isPending}
            okText={isPastMonth ? 'Nachträglich erstellen' : 'Erstellen'}
            cancelText="Abbrechen"
            okButtonProps={{
                danger: isPastMonth && monthDiff > 6,
                disabled: isFutureMonth,
            }}
            width={500}
            destroyOnHidden
        >
            <Descriptions bordered column={1} size="small">
                <Descriptions.Item label="Kasse">{formatRegisterShort(cashRegisterId)}</Descriptions.Item>
                <Descriptions.Item label="Jahr">{year}</Descriptions.Item>
                <Descriptions.Item label="Monat">{month}</Descriptions.Item>
                <Descriptions.Item label="Status">
                    {isFutureMonth ? (
                        <Tag color="red">Zukünftiger Monat</Tag>
                    ) : isCurrentMonth ? (
                        <Tag color="green">Aktueller Monat</Tag>
                    ) : (
                        <Tag color={severityConfig.color}>
                            {monthDiff} {monthDiff === 1 ? 'Monat' : 'Monate'} überfällig
                        </Tag>
                    )}
                </Descriptions.Item>
            </Descriptions>

            {isFutureMonth ? (
                <Alert
                    type="error"
                    showIcon
                    title="Zukünftiger Monat nicht erlaubt"
                    description="Monatsbelege können nur für den aktuellen oder vergangene Kalendermonate erstellt werden."
                    style={{ marginTop: 16 }}
                />
            ) : null}

            {isPastMonth ? (
                <Alert
                    type="info"
                    title="Nachträgliche (verspätete) Erstellung"
                    description={
                        'Der Beleg wird mit dem tatsächlichen aktuellen Datum erstellt und von der TSE zum jetzigen Zeitpunkt signiert. ' +
                        `Der abgedeckte Zeitraum (${formatViennaYearMonth(year, month)}) wird korrekt hinterlegt und der Beleg als „verspätet erstellt" markiert. ` +
                        'Das Erstellungsdatum wird NICHT rückdatiert; die verspätete Erstellung ist bei einer Prüfung transparent nachvollziehbar.'
                    }
                    showIcon
                    style={{ marginTop: 16 }}
                />
            ) : null}

            {isPastMonth && !warning ? (
                <Alert
                    type={severityConfig.alertType}
                    title={severityConfig.title}
                    description={buildPastMonthPreviewMessage(monthDiff)}
                    showIcon
                    icon={severityConfig.icon}
                    style={{ marginTop: 16 }}
                />
            ) : null}

            {isPastMonth ? (
                <div style={{ marginTop: 16 }}>
                    <Typography.Text type="secondary">
                        Grund der nachträglichen Erstellung (wird im Audit-Log und am Beleg dokumentiert)
                    </Typography.Text>
                    <Input.TextArea
                        value={lateReason}
                        onChange={(e) => setLateReason(e.target.value)}
                        maxLength={450}
                        rows={2}
                        placeholder="z. B. Monatsbeleg wurde versehentlich nicht fristgerecht erstellt und wird nachgeholt."
                        style={{ marginTop: 8 }}
                    />
                </div>
            ) : null}

            {warning ? (
                <Alert
                    type={warningAlertType}
                    title="Bestätigung erforderlich"
                    description={warning.message}
                    showIcon
                    style={{ marginTop: 16 }}
                />
            ) : null}

            {forceMode ? (
                <Alert
                    type="info"
                    title="Nachträgliche Erstellung bestätigt"
                    description="Der Monatsbeleg wird für einen vergangenen Zeitraum nachträglich erstellt (reales Erstellungsdatum, als verspätet markiert). Dies wird im Audit-Log protokolliert."
                    showIcon
                    style={{ marginTop: 16 }}
                />
            ) : null}
            </Modal>

            <MonatsbelegLateSuccessModal
                open={successOpen}
                result={successResult}
                onClose={() => {
                    setSuccessOpen(false);
                    setSuccessResult(null);
                    onClose();
                }}
            />
        </>
    );
}
