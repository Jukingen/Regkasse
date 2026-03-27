'use client';

import React, { useMemo } from 'react';
import { Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { ReceiptTaxLineDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';
import { useI18n } from '@/i18n';

const { Text } = Typography;

interface ReceiptTaxSummaryProps {
    taxLines: ReceiptTaxLineDto[];
    subTotal: number;
    taxTotal: number;
    grandTotal: number;
}

/**
 * Vergi tablosu ve alt toplamlar.
 */
export default function ReceiptTaxSummary({
    taxLines,
    subTotal,
    taxTotal,
    grandTotal,
}: ReceiptTaxSummaryProps) {
    const { t } = useI18n();

    const columns: ColumnsType<ReceiptTaxLineDto> = useMemo(
        () => [
            {
                title: t('receipts.detail.taxTable.colTaxRate'),
                dataIndex: 'taxRate',
                key: 'taxRate',
                render: (val: number) => `${val}%`,
            },
            {
                title: t('receipts.detail.taxTable.colNet'),
                dataIndex: 'netAmount',
                key: 'netAmount',
                align: 'right',
                render: (val: number) => formatEUR(val),
            },
            {
                title: t('receipts.detail.taxTable.colTax'),
                dataIndex: 'taxAmount',
                key: 'taxAmount',
                align: 'right',
                render: (val: number) => formatEUR(val),
            },
            {
                title: t('receipts.detail.taxTable.colGross'),
                dataIndex: 'grossAmount',
                key: 'grossAmount',
                align: 'right',
                render: (val: number) => formatEUR(val),
            },
        ],
        [t],
    );

    return (
        <Table<ReceiptTaxLineDto>
            columns={columns}
            dataSource={taxLines}
            rowKey="lineId"
            pagination={false}
            size="small"
            summary={() => (
                <Table.Summary fixed>
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0}>
                            <Text strong>{t('receipts.detail.taxTable.footerSubTotal')}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1} align="right">
                            <Text>{formatEUR(subTotal)}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={2} />
                        <Table.Summary.Cell index={3} />
                    </Table.Summary.Row>
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0}>
                            <Text strong>{t('receipts.detail.taxTable.footerTaxTotal')}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1} />
                        <Table.Summary.Cell index={2} align="right">
                            <Text>{formatEUR(taxTotal)}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={3} />
                    </Table.Summary.Row>
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0}>
                            <Text strong style={{ fontSize: 15 }}>{t('receipts.detail.taxTable.footerGrandTotal')}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1} />
                        <Table.Summary.Cell index={2} />
                        <Table.Summary.Cell index={3} align="right">
                            <Text strong style={{ fontSize: 15 }}>{formatEUR(grandTotal)}</Text>
                        </Table.Summary.Cell>
                    </Table.Summary.Row>
                </Table.Summary>
            )}
        />
    );
}
