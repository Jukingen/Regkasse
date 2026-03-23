'use client';

import React, { useEffect } from 'react';
import { message, Card, Button, Spin } from 'antd';
import { useRouter } from 'next/navigation';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission } from '@/shared/auth/permissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

import ReceiptTemplateForm from '@/features/receipt-templates/components/ReceiptTemplateForm';
import { useReceiptTemplates } from '@/features/receipt-templates/hooks/useReceiptTemplates';
import type { CreateReceiptTemplateRequest, UpdateReceiptTemplateRequest } from '@/api/generated/model';

export default function NewReceiptTemplatePage() {
    const router = useRouter();
    const { user, isInitialized } = useAuth();
    const canManage = hasPermission(user, PERMISSIONS.RECEIPT_TEMPLATE_MANAGE);

    useEffect(() => {
        if (isInitialized && !canManage) {
            router.replace('/receipt-templates');
        }
    }, [isInitialized, canManage, router]);
    const { useCreate, invalidateList } = useReceiptTemplates();

    const { mutate: createTemplate, isPending } = useCreate({
        mutation: {
            onSuccess: () => {
                message.success('Vorlage angelegt.');
                invalidateList();
                router.push('/receipt-templates');
            },
            onError: (error: Error) => {
                message.error(`Anlegen fehlgeschlagen: ${error.message}`);
            },
        },
    });

    const handleSubmit = (values: CreateReceiptTemplateRequest | UpdateReceiptTemplateRequest) => {
        // Strict typing: values from form should match CreateReceiptTemplateRequest
        createTemplate({ data: values as CreateReceiptTemplateRequest });
    };

    if (!isInitialized || !canManage) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
                <Spin size="large" tip="Zugriff wird geprüft…" />
            </div>
        );
    }

    return (
        <React.Fragment>
            <AdminPageHeader
                title="Neue Belegvorlage"
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_LABELS.receiptTemplates, href: '/receipt-templates' },
                    { title: 'Neu' },
                ]}
                actions={
                    <Button
                        icon={<ArrowLeftOutlined />}
                        onClick={() => router.push('/receipt-templates')}
                    >
                        Zurück
                    </Button>
                }
            />

            <Card>
                <ReceiptTemplateForm
                    onSubmit={handleSubmit}
                    loading={isPending}
                    mode="create"
                />
            </Card>
        </React.Fragment>
    );
}
