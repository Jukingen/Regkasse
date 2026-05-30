'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { Button, Modal, message } from 'antd';
import { useResetDemoCategories } from '@/api/admin/categories';
import { useI18n } from '@/i18n';
import { technicalConsole } from '@/shared/dev/technicalConsole';

export function ResetCategoriesButton() {
    const { t } = useI18n();
    const resetMutation = useResetDemoCategories();

    const handleReset = () => {
        Modal.confirm({
            title: t('common.categories.reset.confirmTitle'),
            content: t('common.categories.reset.confirmContent'),
            okText: t('common.categories.reset.confirmOk'),
            cancelText: t('common.buttons.cancel'),
            onOk: async () => {
                try {
                    const result = await resetMutation.mutateAsync();
                    message.success(
                        t('common.categories.reset.success', { count: result.resetCount }),
                    );
                } catch (error) {
                    technicalConsole.error('Demo category reset failed', error);
                    message.error(t('common.categories.reset.error'));
                    throw error;
                }
            },
        });
    };

    return (
        <Button
            icon={<ReloadOutlined />}
            loading={resetMutation.isPending}
            onClick={handleReset}
        >
            {t('common.categories.reset.button')}
        </Button>
    );
}
