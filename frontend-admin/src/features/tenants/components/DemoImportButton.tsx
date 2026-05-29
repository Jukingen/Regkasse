'use client';

import { useState } from 'react';
import { Button } from 'antd';
import { ImportOutlined } from '@ant-design/icons';

import { DemoImportModal } from '@/features/tenants/components/DemoImportModal';

export type DemoImportButtonProps = {
    /** Super-admin tenant detail: target tenant id. Omit on products page (current tenant context). */
    tenantId?: string;
    tenantName: string;
    onSuccess?: () => void;
};

export function DemoImportButton({ tenantId, tenantName, onSuccess }: DemoImportButtonProps) {
    const [isModalOpen, setIsModalOpen] = useState(false);

    return (
        <>
            <Button icon={<ImportOutlined />} onClick={() => setIsModalOpen(true)} type="default">
                Demo Produkte importieren
            </Button>

            <DemoImportModal
                open={isModalOpen}
                tenantId={tenantId}
                tenantName={tenantName}
                onClose={() => setIsModalOpen(false)}
                onSuccess={() => {
                    setIsModalOpen(false);
                    onSuccess?.();
                }}
            />
        </>
    );
}
