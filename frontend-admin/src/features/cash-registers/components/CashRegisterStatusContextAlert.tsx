'use client';

import { Alert, List, Typography } from 'antd';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import {
    closedContextMessageKey,
    inferClosedRegisterContext,
    isClosedRegister,
} from '@/features/cash-registers/utils/registerClosedContext';
import { rawRegisterStatus, REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

const OPEN_PREREQUISITE_KEYS = [
    'startbelegFirstOpen',
    'noStartbelegOnReopen',
    'monatsbelegProduction',
    'singleCashier',
] as const;

export type CashRegisterStatusContextAlertProps = {
    register: CashRegister;
    showOpenPrerequisites?: boolean;
};

export function CashRegisterStatusContextAlert({
    register,
    showOpenPrerequisites = false,
}: CashRegisterStatusContextAlertProps) {
    const { t } = useI18n();
    const status = rawRegisterStatus(register);

    if (status === REGISTER_STATUS.open) {
        return (
            <Alert
                type="success"
                showIcon
                title={t('cashRegisters.shiftGuidance.detailOpenTitle')}
                description={t('cashRegisters.shiftGuidance.detailOpenDescription')}
                style={{ marginBottom: 16 }}
            />
        );
    }

    if (!isClosedRegister(register)) {
        return null;
    }

    const context = inferClosedRegisterContext(register);

    return (
        <Alert
            type="info"
            showIcon
            title={t('cashRegisters.shiftGuidance.detailClosedTitle')}
            description={
                <div>
                    <Typography.Paragraph style={{ marginBottom: showOpenPrerequisites ? 12 : 0 }}>
                        {context ? t(closedContextMessageKey(context)) : t('cashRegisters.shiftGuidance.detailContextGeneric')}
                    </Typography.Paragraph>
                    {showOpenPrerequisites ? (
                        <>
                            <Typography.Text strong>
                                {t('cashRegisters.shiftGuidance.openPrerequisitesTitle')}
                            </Typography.Text>
                            <List
                                size="small"
                                dataSource={[...OPEN_PREREQUISITE_KEYS]}
                                renderItem={(key) => (
                                    <List.Item style={{ paddingBlock: 4, border: 'none' }}>
                                        <Typography.Text type="secondary">
                                            {t(`cashRegisters.shiftGuidance.openPrerequisites.${key}`)}
                                        </Typography.Text>
                                    </List.Item>
                                )}
                            />
                        </>
                    ) : null}
                </div>
            }
            style={{ marginBottom: 16 }}
        />
    );
}
