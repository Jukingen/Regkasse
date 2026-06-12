'use client';

import { Collapse, List, Table, Typography } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import {
    CLOSED_REGISTER_REASON_KEYS,
    RKSV_SHIFT_RULE_KEYS,
    type ClosedReasonKey,
    type RksvShiftRuleKey,
} from '@/features/cash-registers/utils/registerClosedContext';

type RksvRuleRow = {
    key: RksvShiftRuleKey;
    rule: string;
    description: string;
};

export function CashRegisterShiftRksvGuide() {
    const { t } = useI18n();

    const rksvRows: RksvRuleRow[] = RKSV_SHIFT_RULE_KEYS.map((key) => ({
        key,
        rule: t(`cashRegisters.shiftGuidance.rksvRules.${key}.rule`),
        description: t(`cashRegisters.shiftGuidance.rksvRules.${key}.description`),
    }));

    return (
        <Collapse
            style={{ marginBottom: 16 }}
            items={[
                {
                    key: 'shift-rksv-guide',
                    label: (
                        <Typography.Text strong>
                            <InfoCircleOutlined style={{ marginInlineEnd: 8 }} />
                            {t('cashRegisters.shiftGuidance.pageCollapseTitle')}
                        </Typography.Text>
                    ),
                    children: (
                        <div>
                            <Typography.Title level={5} style={{ marginTop: 0 }}>
                                {t('cashRegisters.shiftGuidance.closedSectionTitle')}
                            </Typography.Title>
                            <Typography.Paragraph type="secondary">
                                {t('cashRegisters.shiftGuidance.closedSectionIntro')}
                            </Typography.Paragraph>
                            <List
                                size="small"
                                dataSource={[...CLOSED_REGISTER_REASON_KEYS]}
                                renderItem={(reasonKey: ClosedReasonKey) => (
                                    <List.Item>
                                        <Typography.Text>
                                            {t(`cashRegisters.shiftGuidance.closedReasons.${reasonKey}`)}
                                        </Typography.Text>
                                    </List.Item>
                                )}
                            />

                            <Typography.Title level={5}>
                                {t('cashRegisters.shiftGuidance.rksvRulesSectionTitle')}
                            </Typography.Title>
                            <Table<RksvRuleRow>
                                size="small"
                                pagination={false}
                                rowKey="key"
                                dataSource={rksvRows}
                                columns={[
                                    {
                                        title: t('cashRegisters.shiftGuidance.rksvRulesColumnRule'),
                                        dataIndex: 'rule',
                                        width: '32%',
                                        render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
                                    },
                                    {
                                        title: t('cashRegisters.shiftGuidance.rksvRulesColumnDescription'),
                                        dataIndex: 'description',
                                    },
                                ]}
                            />
                        </div>
                    ),
                },
            ]}
        />
    );
}
