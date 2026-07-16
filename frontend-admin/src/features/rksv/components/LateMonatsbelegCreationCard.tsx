'use client';

import { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, Form, Input, InputNumber, Select } from 'antd';
import { ClockCircleOutlined } from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useCreateMonatsbeleg } from '@/features/rksv/hooks/useCreateMonatsbeleg';
import {
    isMonatsbelegWarningResponse,
    warningSeverityToAlertType,
} from '@/features/rksv/types/monatsbelegWarning';
import {
    formatViennaYearMonth,
    getMonthDifference,
    getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';
import { formatMonatsbelegMonthNameDe } from '@/features/rksv/utils/monatsbelegMissingMonths';
import { MonatsbelegLateSuccessModal } from '@/features/rksv/components/MonatsbelegLateSuccessModal';
import {
    toMonatsbelegLateSuccessResult,
    type MonatsbelegLateSuccessResult,
} from '@/features/rksv/types/createMonatsbelegResponseExtended';

const OTHER_REASON_VALUE = 'Anderer Grund';

const LATE_REASON_PRESETS = [
    'Ich habe vergessen, den Monatsbeleg zu erstellen',
    'Technisches Problem / Systemausfall',
    'Personeller Wechsel / Neue Mitarbeiter',
    OTHER_REASON_VALUE,
] as const;

type LateMonatsbelegFormValues = {
    year: number;
    month: number;
    reason: string;
    customReason?: string;
};

export type LateMonatsbelegCreationCardProps = {
    cashRegisterId?: string;
    canCreate?: boolean;
    disabled?: boolean;
    onSuccess?: () => void;
};

function buildPastMonthPreviewMessage(monthDiff: number): string {
    if (monthDiff === 1) {
        return 'Monatsbeleg für den Vormonat wird erstellt. Dies ist zulässig.';
    }
    if (monthDiff <= 6) {
        return `Monatsbeleg für ${monthDiff} Monate zurück wird erstellt. FinanzOnline akzeptiert dies in der Regel.`;
    }
    return `Monatsbeleg für ${monthDiff} Monate zurück wird erstellt. Dies könnte bei einer Betriebsprüfung hinterfragt werden. Nur nach Rücksprache mit einem Steuerberater fortfahren.`;
}

export function LateMonatsbelegCreationCard({
    cashRegisterId,
    canCreate = true,
    disabled = false,
    onSuccess,
}: LateMonatsbelegCreationCardProps) {
    const { message, modal } = useAntdApp();
    const createMonatsbeleg = useCreateMonatsbeleg();
    const { year: viennaYear, month: viennaMonth } = useMemo(() => getViennaCalendarYearMonth(), []);

    const initialValues = useMemo((): LateMonatsbelegFormValues => {
        const month = viennaMonth === 1 ? 12 : viennaMonth - 1;
        const year = viennaMonth === 1 ? viennaYear - 1 : viennaYear;
        return {
            year,
            month,
            reason: LATE_REASON_PRESETS[0],
            customReason: '',
        };
    }, [viennaMonth, viennaYear]);

    const [form] = Form.useForm<LateMonatsbelegFormValues>();
    const [forceMode, setForceMode] = useState(false);
    const [successOpen, setSuccessOpen] = useState(false);
    const [successResult, setSuccessResult] = useState<MonatsbelegLateSuccessResult | null>(null);
    const selectedYear = Form.useWatch('year', form) ?? initialValues.year;

    const monthOptions = useMemo(
        () =>
            Array.from({ length: 12 }, (_, idx) => {
                const value = idx + 1;
                const isCurrentOrFuture =
                    typeof selectedYear === 'number' &&
                    selectedYear === viennaYear &&
                    value >= viennaMonth;
                return {
                    value,
                    label: formatMonatsbelegMonthNameDe(value),
                    disabled: isCurrentOrFuture,
                };
            }),
        [selectedYear, viennaMonth, viennaYear],
    );

    const resolveReason = useCallback((values: LateMonatsbelegFormValues): string => {
        if (values.reason === OTHER_REASON_VALUE) {
            return values.customReason?.trim() ?? '';
        }
        return values.reason.trim();
    }, []);

    const submitCreation = useCallback(
        async (values: LateMonatsbelegFormValues, withForce: boolean) => {
            if (!cashRegisterId?.trim()) {
                message.warning('Bitte zuerst eine Kasse auswählen.');
                return;
            }

            const { year, month } = values;
            const monthDiff = getMonthDifference(year, month);
            if (monthDiff <= 0) {
                message.error(
                    'Monatsbeleg kann nur für abgeschlossene (vergangene) Kalendermonate erstellt werden.',
                );
                return;
            }

            const effectiveReason =
                resolveReason(values) ||
                `Nachträglich erstellter Monatsbeleg für ${formatViennaYearMonth(year, month)}`;

            try {
                const response = await createMonatsbeleg.mutateAsync({
                    data: {
                        cashRegisterId: cashRegisterId.trim(),
                        year,
                        month,
                        reason: effectiveReason,
                    },
                    force: withForce || monthDiff > 0,
                });
                setSuccessResult(toMonatsbelegLateSuccessResult(response, { year, month }));
                setSuccessOpen(true);
                form.resetFields();
                form.setFieldsValue(initialValues);
                setForceMode(false);
                onSuccess?.();
            } catch (error: unknown) {
                if (!isAxiosError(error)) {
                    message.error('Fehler beim Erstellen des Monatsbelegs');
                    return;
                }

                const data = error.response?.data;
                if (isMonatsbelegWarningResponse(data) && !withForce) {
                    const warningMessage =
                        data.warningMessage?.trim() || buildPastMonthPreviewMessage(data.monthDiff ?? monthDiff);
                    const severity = warningSeverityToAlertType(data.severity);

                    modal.confirm({
                        title: 'Monatsbeleg nachträglich erstellen',
                        content: (
                            <div>
                                <p style={{ marginBottom: 0 }}>{warningMessage}</p>
                                <p style={{ marginTop: 12, marginBottom: 0 }}>
                                    Dieser Monatsbeleg wird für den ausgewählten Zeitraum erstellt, aber mit dem
                                    heutigen Datum versehen (keine Rückdatierung).
                                </p>
                                <p style={{ marginTop: 12, marginBottom: 0, color: '#ff4d4f' }}>
                                    Möchten Sie fortfahren?
                                </p>
                            </div>
                        ),
                        okText: 'Nachträglich erstellen',
                        okButtonProps: {
                            danger: severity === 'error',
                            type: 'primary',
                        },
                        cancelText: 'Abbrechen',
                        onOk: async () => {
                            setForceMode(true);
                            await submitCreation(values, true);
                        },
                    });
                    return;
                }

                const apiError =
                    typeof data === 'object' &&
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
            form,
            initialValues,
            message,
            modal,
            onSuccess,
            resolveReason,
        ],
    );

    const handleFinish = useCallback(
        (values: LateMonatsbelegFormValues) => {
            void submitCreation(values, forceMode);
        },
        [forceMode, submitCreation],
    );

    if (!canCreate) return null;

    return (
        <Card
            title="Monatsbeleg nachträglich erstellen (verspätet)"
            style={{ marginBottom: 16, borderColor: '#eab308' }}
        >
            <Alert
                type="info"
                showIcon
                title="Hinweis zur nachträglichen Erstellung"
                description={
                    <div>
                        <p style={{ marginBottom: 8 }}>
                            Dieser Monatsbeleg wird für den ausgewählten Zeitraum erstellt, aber mit dem heutigen
                            Datum versehen.
                        </p>
                        <p style={{ marginBottom: 8 }}>
                            Der abgedeckte Zeitraum wird korrekt hinterlegt und als <strong>verspätet</strong>{' '}
                            markiert. Bei einer Prüfung ist die verspätete Erstellung transparent nachvollziehbar.
                        </p>
                        <p style={{ color: '#dc2626', fontSize: 13, marginBottom: 0 }}>
                            Dieser Vorgang wird im Audit-Log protokolliert.
                        </p>
                    </div>
                }
            />

            {!cashRegisterId ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginTop: 16 }}
                    title="Kasse erforderlich"
                    description="Bitte wählen Sie oben eine Kasse aus, bevor Sie einen Monatsbeleg nachholen."
                />
            ) : null}

            <Form<LateMonatsbelegFormValues>
                form={form}
                layout="inline"
                initialValues={initialValues}
                onFinish={handleFinish}
                style={{ marginTop: 16, rowGap: 12 }}
            >
                <Form.Item
                    name="year"
                    label="Jahr"
                    rules={[{ required: true, message: 'Jahr ist erforderlich' }]}
                >
                    <InputNumber min={2020} max={viennaYear} disabled={disabled || !cashRegisterId} />
                </Form.Item>

                <Form.Item
                    name="month"
                    label="Monat"
                    rules={[{ required: true, message: 'Monat ist erforderlich' }]}
                >
                    <Select
                        style={{ width: 140 }}
                        disabled={disabled || !cashRegisterId}
                        options={monthOptions}
                    />
                </Form.Item>

                <Form.Item
                    name="reason"
                    label="Grund der Verspätung"
                    rules={[{ required: true, message: 'Bitte einen Grund wählen' }]}
                >
                    <Select
                        style={{ minWidth: 280 }}
                        placeholder="Bitte wählen Sie einen Grund"
                        disabled={disabled || !cashRegisterId}
                        options={LATE_REASON_PRESETS.map((value) => ({ value, label: value }))}
                    />
                </Form.Item>

                <Form.Item noStyle shouldUpdate={(prev, next) => prev.reason !== next.reason}>
                    {({ getFieldValue }) =>
                        getFieldValue('reason') === OTHER_REASON_VALUE ? (
                            <Form.Item
                                name="customReason"
                                label="Eigener Grund"
                                rules={[
                                    { required: true, message: 'Bitte einen Grund angeben' },
                                    { min: 10, message: 'Mindestens 10 Zeichen' },
                                ]}
                                style={{ minWidth: 280 }}
                            >
                                <Input.TextArea
                                    rows={2}
                                    maxLength={450}
                                    disabled={disabled || !cashRegisterId}
                                    placeholder="Bitte den Grund für die verspätete Erstellung beschreiben"
                                />
                            </Form.Item>
                        ) : null
                    }
                </Form.Item>

                <Form.Item>
                    <Button
                        type="primary"
                        htmlType="submit"
                        danger
                        icon={<ClockCircleOutlined />}
                        loading={createMonatsbeleg.isPending}
                        disabled={disabled || !cashRegisterId}
                    >
                        Monatsbeleg jetzt nachholen
                    </Button>
                </Form.Item>
            </Form>

            {forceMode ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginTop: 12 }}
                    title="Bestätigung erforderlich"
                    description="Der Monatsbeleg wird für einen vergangenen Zeitraum nachträglich erstellt."
                />
            ) : null}

            <MonatsbelegLateSuccessModal
                open={successOpen}
                result={successResult}
                onClose={() => setSuccessOpen(false)}
            />
        </Card>
    );
}
