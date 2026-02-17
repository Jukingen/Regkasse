'use client';

import React from 'react';
import { Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { ReceiptTaxLineDto } from '@/features/receipts/types/receipts';
import { formatEUR } from '@/shared/utils/currency';

const { Text } = Typography;

interface ReceiptTaxSummaryProps {
    taxLines: ReceiptTaxLineDto[];
    subTotal: number;
    taxTotal: number;
    grandTotal: number;
}

const columns: ColumnsType<ReceiptTaxLineDto> = [
    {
        title: 'Tax Rate',
        dataIndex: 'taxRate',
        key: 'taxRate',
        render: (val: number) => `${val}%`,
    },
    {
        title: 'Net Amount',
        dataIndex: 'netAmount',
        key: 'netAmount',
        align: 'right',
        render: (val: number) => formatEUR(val),
    },
    {
        title: 'Tax Amount',
        dataIndex: 'taxAmount',
        key: 'taxAmount',
        align: 'right',
        render: (val: number) => formatEUR(val),
    },
    {
        title: 'Gross Amount',
        dataIndex: 'grossAmount',
        key: 'grossAmount',
        align: 'right',
        render: (val: number) => formatEUR(val),
    },
];

/**
 * Tax breakdown table with summary footer showing sub/tax/grand totals.
 */
export default function ReceiptTaxSummary({
    taxLines,
    subTotal,
    taxTotal,
    grandTotal,
}: ReceiptTaxSummaryProps) {
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
                            <Text strong>Sub Total</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1} align="right">
                            <Text>{formatEUR(subTotal)}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={2} />
                        <Table.Summary.Cell index={3} />
                    </Table.Summary.Row>
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0}>
                            <Text strong>Tax Total</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={1} />
                        <Table.Summary.Cell index={2} align="right">
                            <Text>{formatEUR(taxTotal)}</Text>
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={3} />
                    </Table.Summary.Row>
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0}>
                            <Text strong style={{ fontSize: 15 }}>Grand Total</Text>
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
