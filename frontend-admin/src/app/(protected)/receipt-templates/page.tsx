'use client';

import React, { useState } from 'react';
import { Card, Typography, Button, Space, Input, Select, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import ReceiptTemplatesTable from '@/features/receipt-templates/components/ReceiptTemplatesTable';
import ReceiptPreviewModal from '@/features/receipt-templates/components/ReceiptPreviewModal';
import { useReceiptTemplateQueries } from '@/features/receipt-templates/hooks/useReceiptTemplateQueries';

const { Title } = Typography;

export default function ReceiptTemplatesPage() {
    const router = useRouter();
    const { useList, useListByLanguage, useListByType, useDelete, invalidateList } =
        useReceiptTemplateQueries();

    const [filterMode, setFilterMode] = useState<'all' | 'language' | 'type'>('all');
    const [filterValue, setFilterValue] = useState<string>('');
    const [previewId, setPreviewId] = useState<string | null>(null);

    const { data: allTemplates, isLoading: loadingAll } = useList({
        query: { enabled: filterMode === 'all' },
    });

    const { data: languageTemplates, isLoading: loadingLang } = useListByLanguage(filterValue, {
        query: { enabled: filterMode === 'language' && !!filterValue },
    });

    const { data: typeTemplates, isLoading: loadingType } = useListByType(filterValue, {
        query: { enabled: filterMode === 'type' && !!filterValue },
    });

    const { mutate: deleteTemplate } = useDelete({
        mutation: {
            onSuccess: () => {
                message.success('Template deleted');
                invalidateList();
            },
            onError: (error: Error) => {
                message.error(`Delete failed: ${error.message}`);
            },
        },
    });

    const data =
        filterMode === 'language'
            ? languageTemplates
            : filterMode === 'type'
                ? typeTemplates
                : allTemplates;

    const loading = loadingAll || loadingLang || loadingType;

    const handleDelete = (id: string) => {
        deleteTemplate({ id });
    };

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={3} style={{ margin: 0 }}>
                    Receipt Templates
                </Title>
                <Link href="/receipt-templates/new">
                    <Button type="primary" icon={<PlusOutlined />}>
                        New Template
                    </Button>
                </Link>
            </div>

            <Space style={{ marginBottom: 16 }}>
                <Select
                    value={filterMode}
                    onChange={(val) => {
                        setFilterMode(val);
                        setFilterValue('');
                    }}
                    style={{ width: 150 }}
                >
                    <Select.Option value="all">All Templates</Select.Option>
                    <Select.Option value="language">By Language</Select.Option>
                    <Select.Option value="type">By Type</Select.Option>
                </Select>

                {filterMode === 'language' && (
                    <Input
                        placeholder="e.g. en, de, tr"
                        value={filterValue}
                        onChange={(e) => setFilterValue(e.target.value)}
                        style={{ width: 200 }}
                    />
                )}

                {filterMode === 'type' && (
                    <Input
                        placeholder="e.g. sale, refund"
                        value={filterValue}
                        onChange={(e) => setFilterValue(e.target.value)}
                        style={{ width: 200 }}
                    />
                )}
            </Space>

            <ReceiptTemplatesTable
                data={data || []}
                loading={loading}
                onDelete={handleDelete}
                onPreview={setPreviewId}
            />

            <ReceiptPreviewModal
                templateId={previewId}
                onClose={() => setPreviewId(null)}
            />
        </Card>
    );
}
