'use client';

import { useEffect } from 'react';
import { App, Modal, Alert, Form, Input, Typography } from 'antd';

import { useUpdateOwnUsernameMutation } from '@/features/user/api/updateOwnUsername';
import { ProfileUsernamePolicyAlert } from '@/features/user/components/ProfileUsernamePolicyAlert';
import type { UsernameChangePolicy } from '@/features/user/hooks/useUsernameChangePolicy';
import { useUsernameChangePolicy } from '@/features/user/hooks/useUsernameChangePolicy';
import {
    createLoginUserNameRules,
    LOGIN_USERNAME_MAX_LENGTH,
    NOTES_MAX_LENGTH,
} from '@/features/users/constants/validation';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';

type FormValues = {
    newUsername: string;
    reason?: string;
};

export type SelfServiceUsernameModalProps = {
    open: boolean;
    currentUsername: string;
    userEmail?: string | null;
    usernamePolicy?: UsernameChangePolicy;
    onClose: () => void;
    onSuccess: (newUsername: string) => void;
};

export function SelfServiceUsernameModal(props: SelfServiceUsernameModalProps) {
    if (!props.open) {
        return null;
    }
    return <SelfServiceUsernameModalContent {...props} />;
}

function SelfServiceUsernameModalContent({
    open,
    currentUsername,
    userEmail,
    usernamePolicy: usernamePolicyProp,
    onClose,
    onSuccess,
}: SelfServiceUsernameModalProps) {
    const { message } = App.useApp();
    const { t } = useI18n();
    const [form] = Form.useForm<FormValues>();
    const updateOwnUsername = useUpdateOwnUsernameMutation();
    const { data: fetchedPolicy, isLoading: isPolicyLoading } = useUsernameChangePolicy();
    const usernamePolicy = usernamePolicyProp ?? fetchedPolicy;
    const canChangeUsername = usernamePolicy?.canChange !== false;

    const usernameRules = createLoginUserNameRules({
        required: t('users.username.validation.required'),
        min: t('users.username.validation.min'),
        max: t('users.username.validation.max'),
        pattern: t('users.username.validation.pattern'),
    });

    useEffect(() => {
        if (!open) return;
        form.setFieldsValue({
            newUsername: currentUsername,
            reason: undefined,
        });
    }, [open, currentUsername, form]);

    const handleSubmit = async () => {
        try {
            const values = await form.validateFields();
            const trimmed = values.newUsername.trim();
            if (trimmed === currentUsername.trim()) {
                onClose();
                return;
            }

            const result = await updateOwnUsername.mutateAsync({
                newUsername: trimmed,
                reason: values.reason?.trim() || undefined,
            });
            message.success(t('users.username.updateSuccess'));
            onSuccess(result.newUsername?.trim() || trimmed);
            form.resetFields();
            onClose();
        } catch (error) {
            const normalized = normalizeApiError(error);
            if (normalized.httpStatus === 409) {
                message.error(t('users.username.conflict'));
                return;
            }
            if (normalized.code === 'BUSINESS_RULE' && normalized.rawMessage?.trim()) {
                message.error(normalized.rawMessage.trim());
                return;
            }
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    fallbackKey: 'users.username.updateFailed',
                    logContext: 'SelfServiceUsernameModal.update',
                }),
            );
        }
    };

    return (
        <Modal
            title={t('users.username.editTitle')}
            open={open}
            onCancel={onClose}
            onOk={() => void handleSubmit()}
            okText={t('common.buttons.save')}
            cancelText={t('common.buttons.cancel')}
            confirmLoading={updateOwnUsername.isPending}
            okButtonProps={{ disabled: !canChangeUsername }}
            destroyOnHidden
        >
            <ProfileUsernamePolicyAlert
                policy={usernamePolicy}
                isLoading={!usernamePolicyProp && isPolicyLoading}
            />

            <Alert
                type="warning"
                showIcon
                title={t('profile.username.reloginTitle')}
                description={t('profile.username.reloginDescription')}
                style={{ marginBottom: 16 }}
            />

            <Form form={form} layout="vertical" preserve={false}>
                <Form.Item label={t('users.username.currentLabel')}>
                    <Input value={currentUsername.trim() || '—'} disabled autoComplete="off" />
                    {userEmail?.trim() ? (
                        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
                            {userEmail.trim()}
                        </Typography.Text>
                    ) : null}
                </Form.Item>

                <Form.Item
                    name="newUsername"
                    label={t('users.username.newLabel')}
                    rules={usernameRules}
                >
                    <Input
                        placeholder={t('users.username.placeholder')}
                        maxLength={LOGIN_USERNAME_MAX_LENGTH}
                        autoComplete="off"
                    />
                </Form.Item>

                <Form.Item
                    name="reason"
                    label={t('users.username.reasonLabel')}
                    help={t('users.username.reasonHelp')}
                >
                    <Input.TextArea
                        rows={3}
                        maxLength={NOTES_MAX_LENGTH}
                        showCount
                        placeholder={t('users.username.reasonPlaceholder')}
                    />
                </Form.Item>
            </Form>

            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('common.auth.loginIdentifierCaseHint')}
            </Typography.Text>
        </Modal>
    );
}
