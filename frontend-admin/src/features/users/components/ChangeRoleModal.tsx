'use client';

import { useEffect, useMemo, useState } from 'react';
import { Alert, Checkbox, Modal, Space, Tooltip, Typography } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';

import { useI18n } from '@/i18n';
import {
    getRoleChangePreserveAvailability,
    isSameRoleName,
    normalizeRoleName,
} from '@/features/users/utils/roleChangePreservePolicy';

export type ChangeRoleModalProps = {
    open: boolean;
    previousRole: string;
    newRole: string;
    /** Tenant role endpoint required for preserve; when false, checkbox stays hidden. */
    hasTenantContext?: boolean;
    confirmLoading?: boolean;
    onCancel: () => void;
    onConfirm: (preservePreviousPermissions: boolean) => void;
};

export function ChangeRoleModal(props: ChangeRoleModalProps) {
    if (!props.open) return null;
    return <ChangeRoleModalContent {...props} />;
}

function ChangeRoleModalContent({
    open,
    previousRole,
    newRole,
    hasTenantContext = true,
    confirmLoading = false,
    onCancel,
    onConfirm,
}: ChangeRoleModalProps) {
    const { t } = useI18n();
    const [preservePreviousPermissions, setPreservePreviousPermissions] = useState(false);

    const availability = useMemo(
        () => getRoleChangePreserveAvailability(previousRole, newRole),
        [previousRole, newRole],
    );
    const canPreserve = availability === 'available' && hasTenantContext;
    const isSameRole = isSameRoleName(previousRole, newRole);
    const displayPreviousRole = normalizeRoleName(previousRole) || '—';
    const displayNewRole = normalizeRoleName(newRole) || '—';

    useEffect(() => {
        if (!open) return;
        setPreservePreviousPermissions(false);
    }, [open, previousRole, newRole]);

    return (
        <Modal
            title={t('users.roleChange.modalTitle')}
            open={open}
            onCancel={onCancel}
            onOk={() => onConfirm(canPreserve ? preservePreviousPermissions : false)}
            okText={t('users.roleChange.confirm')}
            cancelText={t('users.roleChange.cancel')}
            confirmLoading={confirmLoading}
            okButtonProps={{ disabled: isSameRole }}
            destroyOnHidden
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                {isSameRole ? (
                    <Alert type="info" showIcon title={t('users.roleChange.sameRoleTitle')} description={t('users.roleChange.sameRoleDescription')} />
                ) : (
                    <Typography.Paragraph style={{ marginBottom: 0 }}>
                        {t('users.roleChange.description', {
                            oldRole: displayPreviousRole,
                            newRole: displayNewRole,
                        })}
                    </Typography.Paragraph>
                )}

                {availability === 'no_previous_role' ? (
                    <Alert type="info" showIcon title={t('users.roleChange.noPreviousRoleTitle')} description={t('users.roleChange.noPreviousRoleDescription')} />
                ) : null}

                {availability === 'superadmin_source' ? (
                    <Alert type="warning" showIcon title={t('users.roleChange.superAdminWarningTitle')} description={t('users.roleChange.superAdminWarningDescription')} />
                ) : null}

                {canPreserve ? (
                    <Space align="start">
                        <Checkbox
                            checked={preservePreviousPermissions}
                            onChange={(event) => setPreservePreviousPermissions(event.target.checked)}
                        >
                            {t('users.roleChange.preserveCheckbox')}
                        </Checkbox>
                        <Tooltip title={t('users.roleChange.preserveTooltip')}>
                            <InfoCircleOutlined style={{ color: 'rgba(0,0,0,0.45)', marginTop: 2 }} />
                        </Tooltip>
                    </Space>
                ) : null}
            </Space>
        </Modal>
    );
}
