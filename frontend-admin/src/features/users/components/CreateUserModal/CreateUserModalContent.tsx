'use client';

import { useQuery } from '@tanstack/react-query';
import { Button, Form, Modal, Tabs } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import type { CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { QuickUserSuccessModal } from '@/features/super-admin/components/QuickUserSuccessModal';
import { getQuickUsernamePattern } from '@/features/super-admin/lib/quickUserPreview';
import { type CreateUserResult, fetchUsernameSuggestion } from '@/features/users/api/users';
import { TenantSelector } from '@/features/users/components/TenantSelector';
import { UserTenantAssignmentModal } from '@/features/users/components/UserTenantAssignmentModal';
import { useTenantAssignmentModal } from '@/features/users/hooks/useTenantAssignmentModal';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';
import { queryCacheDynamic } from '@/lib/query/queryCachePolicy';

import { CreateUserNormalForm } from './CreateUserNormalForm';
import { CreateUserPasswordSuccessModal } from './CreateUserPasswordSuccessModal';
import { CreateUserQuickForm } from './CreateUserQuickForm';
import { buildQuickCreateRoleOptions, buildTenantCreateRoleOptions } from './roleOptions';
import type {
  CreateUserFormValues,
  CreateUserModalProps,
  CreateUserQuickFormValues,
} from './types';

export function CreateUserModalContent({
  open,
  confirmLoading = false,
  onClose,
  onComplete,
  onSubmit,
  isSuperAdmin = false,
  tenantId: fixedTenantId,
  tenantRows = [],
  tenantsLoading = false,
  showOwnerToggle = false,
  variant = 'usersPage',
  initialValues,
  allowDeferredTenantAssignment = false,
  onAssignTenants,
  quickMode,
}: CreateUserModalProps) {
  const notify = useNotify();
  const { t } = useI18n();
  const [form] = Form.useForm<CreateUserFormValues>();
  const [quickForm] = Form.useForm<CreateUserQuickFormValues>();
  const [passwordResult, setPasswordResult] = useState<CreateUserResult | null>(null);
  const [password, setPassword] = useState('');
  const [tenantAssignmentResult, setTenantAssignmentResult] = useState<CreateUserResult | null>(
    null
  );
  const [submitting, setSubmitting] = useState(false);
  const [assignmentSubmitting, setAssignmentSubmitting] = useState(false);
  const [quickResult, setQuickResult] = useState<CreateQuickUserResult | null>(null);
  const [quickSubmitting, setQuickSubmitting] = useState(false);
  const [activeTab, setActiveTab] = useState<'normal' | 'quick'>('normal');
  const tenantAssignmentModal = useTenantAssignmentModal();
  const { visible: tenantAssignmentVisible, closeModal: closeTenantAssignmentModal } =
    tenantAssignmentModal;

  const showTenantPicker = isSuperAdmin && !fixedTenantId;
  const canDeferTenantAssignment =
    showTenantPicker && !fixedTenantId && allowDeferredTenantAssignment && !!onAssignTenants;
  const canDeferQuickTenantAssignment =
    showTenantPicker &&
    !fixedTenantId &&
    allowDeferredTenantAssignment &&
    !!onAssignTenants &&
    !!quickMode?.onSubmitWithoutTenant;

  const roleOptions = useMemo(() => buildTenantCreateRoleOptions(t), [t]);
  const quickRoleOptions = useMemo(() => buildQuickCreateRoleOptions(t), [t]);
  const tenantById = useMemo(() => new Map(tenantRows.map((row) => [row.id, row])), [tenantRows]);

  const createUserTenantField = showTenantPicker ? (
    <Form.Item
      name="tenantId"
      label={t('users.create.tenant')}
      rules={
        canDeferTenantAssignment
          ? []
          : [{ required: true, message: t('users.create.tenantRequired') }]
      }
    >
      <TenantSelector tenants={tenantRows} loading={tenantsLoading} />
    </Form.Item>
  ) : null;

  const modalTitle = useMemo(() => {
    if (fixedTenantId) {
      const tenant = tenantById.get(fixedTenantId);
      if (tenant) {
        return variant === 'tenantDetail'
          ? t('users.create.titleAddForTenant', { name: tenant.name, slug: tenant.slug })
          : t('users.create.titleForTenant', { name: tenant.name, slug: tenant.slug });
      }
    }
    return t('users.create.title');
  }, [fixedTenantId, tenantById, variant, t]);

  useEffect(() => {
    if (!open) {
      form.resetFields();
      quickForm.resetFields();
      setPasswordResult(null);
      setTenantAssignmentResult(null);
      closeTenantAssignmentModal();
      setQuickResult(null);
      setActiveTab('normal');
      setPassword('');
      return;
    }
    form.setFieldsValue({
      role: 'Manager',
      isOwner: false,
      tenantId: fixedTenantId,
      ...initialValues,
    });
    quickForm.setFieldsValue({
      role: 'Manager',
      ...(fixedTenantId ? { tenantId: fixedTenantId } : {}),
    });
    setActiveTab('normal');
  }, [open, form, quickForm, fixedTenantId, initialValues, closeTenantAssignmentModal]);

  useEffect(() => {
    if (passwordResult?.generatedPassword) {
      setPassword(passwordResult.generatedPassword);
      return;
    }
    setPassword('');
  }, [passwordResult]);

  const createFormModalOpen = open && !passwordResult && !quickResult && !tenantAssignmentVisible;
  const quickFormConnected = createFormModalOpen && Boolean(quickMode) ? quickForm : undefined;
  const watchedQuickRole = Form.useWatch('role', quickFormConnected) ?? 'Manager';
  const watchedQuickTenantId = Form.useWatch('tenantId', quickFormConnected) ?? fixedTenantId;
  const quickPreviewTenant = watchedQuickTenantId
    ? tenantById.get(watchedQuickTenantId)
    : undefined;
  const quickPreviewSlug =
    quickPreviewTenant?.slug ?? (canDeferQuickTenantAssignment ? 'platform' : 'tenant');
  const quickPreviewName = quickPreviewTenant?.name ?? fixedTenantId ?? '';
  const quickUsernamePattern = getQuickUsernamePattern(watchedQuickRole);
  const { data: usernameSuggestion } = useQuery({
    queryKey: ['admin', 'username-suggestion', watchedQuickRole],
    queryFn: () => fetchUsernameSuggestion(watchedQuickRole),
    enabled: open && activeTab === 'quick' && Boolean(watchedQuickRole),
    // Dynamic: suggestion is role-scoped; invalidate implicitly when role changes via queryKey.
    ...queryCacheDynamic,
  });
  const quickUsernameAlternates = useMemo(() => {
    if (!usernameSuggestion?.availableNumbers?.length) return null;
    const prefix = usernameSuggestion.suggestedUsername.replace(/\d+$/, '');
    return usernameSuggestion.availableNumbers.map((n) => `${prefix}${n}`).join(', ');
  }, [usernameSuggestion]);
  const quickEmailPreview = t('tenants.users.quick.emailPreview', {
    role: watchedQuickRole.toLowerCase(),
    random: 'a3f9k2',
    slug: quickPreviewSlug,
  });

  const handleClose = () => {
    form.resetFields();
    quickForm.resetFields();
    setPasswordResult(null);
    setTenantAssignmentResult(null);
    tenantAssignmentModal.closeModal();
    setQuickResult(null);
    setActiveTab('normal');
    setPassword('');
    onClose();
  };

  const handleFinish = async (values: CreateUserFormValues) => {
    const tenantId = fixedTenantId ?? values.tenantId;
    if (!tenantId && !canDeferTenantAssignment) {
      form.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
      return;
    }
    setSubmitting(true);
    const usedDeferredCreate = canDeferTenantAssignment && !tenantId;
    const submitPayload: CreateUserFormValues = {
      email: values.email.trim(),
      firstName: values.firstName?.trim() || undefined,
      lastName: values.lastName?.trim() || undefined,
      role: values.role,
      isOwner: showOwnerToggle ? Boolean(values.isOwner) : false,
      ...(usedDeferredCreate ? {} : tenantId ? { tenantId } : {}),
    };
    try {
      const result = await onSubmit(submitPayload);
      if (result?.success && result.generatedPassword) {
        if (usedDeferredCreate && onAssignTenants) {
          if (tenantId) {
            try {
              await onAssignTenants(result.userId, [tenantId]);
              setPasswordResult(result);
            } catch {
              setTenantAssignmentResult(result);
              tenantAssignmentModal.openModal({
                userId: result.userId,
                userEmail: values.email.trim(),
                initialSelectedTenantIds: [tenantId],
              });
            }
            return;
          }
          setTenantAssignmentResult(result);
          tenantAssignmentModal.openModal({
            userId: result.userId,
            userEmail: values.email.trim(),
            initialSelectedTenantIds: [],
          });
          return;
        }
        setPasswordResult(result);
      }
    } catch {
      /* parent shows error toast */
    } finally {
      setSubmitting(false);
    }
  };

  const handleTenantAssignmentSave = async (selectedTenantIds: string[]) => {
    if (!tenantAssignmentModal.userId || !tenantAssignmentResult || !onAssignTenants) return;
    setAssignmentSubmitting(true);
    try {
      await onAssignTenants(tenantAssignmentModal.userId, selectedTenantIds);
      setPasswordResult(tenantAssignmentResult);
      setTenantAssignmentResult(null);
      tenantAssignmentModal.closeModal();
    } catch {
      /* parent shows error toast */
    } finally {
      setAssignmentSubmitting(false);
    }
  };

  const handleQuickFinish = async (values: CreateUserQuickFormValues) => {
    if (!quickMode) return;
    const tenantId = fixedTenantId ?? values.tenantId;
    if (!tenantId && !canDeferQuickTenantAssignment) {
      quickForm.setFields([{ name: 'tenantId', errors: [t('users.create.tenantRequired')] }]);
      return;
    }
    setQuickSubmitting(true);
    try {
      if (tenantId) {
        const result = await quickMode.onSubmit({
          role: values.role,
          tenantId,
        });
        if (result?.success) {
          setQuickResult(result);
        }
        return;
      }
      if (quickMode.onSubmitWithoutTenant) {
        const result = await quickMode.onSubmitWithoutTenant({
          role: values.role,
        });
        if (result?.success && result.generatedPassword) {
          setTenantAssignmentResult(result);
          tenantAssignmentModal.openModal({
            userId: result.userId,
            userEmail: result.email,
            initialSelectedTenantIds: [],
          });
        }
      }
    } catch {
      /* parent shows error toast */
    } finally {
      setQuickSubmitting(false);
    }
  };

  const copyPassword = async () => {
    if (!password) {
      notify.errorKey('users.password.noPasswordToCopy');
      return;
    }
    const copied = await copyTextToClipboard(password);
    if (copied) {
      notify.successKey('tenants.provisioning.copySuccess');
    } else {
      notify.errorKey('tenants.provisioning.copyFailed');
    }
  };

  const closePasswordModal = () => {
    setPasswordResult(null);
    setTenantAssignmentResult(null);
    tenantAssignmentModal.closeModal();
    onComplete?.();
    onClose();
  };

  const closeQuickResult = () => {
    setQuickResult(null);
    onComplete?.();
    onClose();
  };

  const handleGenerateAnotherQuickUser = () => {
    setQuickResult(null);
    setActiveTab('quick');
    quickForm.setFieldsValue({
      role: 'Manager',
      ...(fixedTenantId ? { tenantId: fixedTenantId } : {}),
    });
  };

  const loading =
    activeTab === 'quick' ? quickSubmitting : confirmLoading || submitting || assignmentSubmitting;

  const hiddenFormAnchors =
    open && !createFormModalOpen ? (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        {quickMode ? <Form form={quickForm} style={{ display: 'none' }} preserve /> : null}
      </>
    ) : null;

  const normalForm = (
    <CreateUserNormalForm
      form={form}
      onFinish={handleFinish}
      roleOptions={roleOptions}
      tenantField={createUserTenantField}
      showOwnerToggle={showOwnerToggle}
      t={t}
    />
  );

  const quickFormContent = quickMode ? (
    <CreateUserQuickForm
      form={quickForm}
      onFinish={handleQuickFinish}
      showTenantPicker={showTenantPicker}
      canDeferTenantAssignment={canDeferQuickTenantAssignment}
      tenantRows={tenantRows}
      tenantsLoading={tenantsLoading}
      roleOptions={quickRoleOptions}
      suggestedUsername={usernameSuggestion?.suggestedUsername}
      usernamePatternFallback={quickUsernamePattern}
      usernameAlternates={quickUsernameAlternates}
      emailPreview={quickEmailPreview}
      watchedTenantId={watchedQuickTenantId}
      t={t}
    />
  ) : null;

  return (
    <>
      {hiddenFormAnchors}
      <Modal
        title={modalTitle}
        open={createFormModalOpen}
        onCancel={handleClose}
        closable
        maskClosable
        width={600}
        forceRender
        footer={[
          <Button key="cancel" onClick={handleClose}>
            {t('common.buttons.cancel')}
          </Button>,
          <Button
            key="submit"
            type="primary"
            loading={loading}
            onClick={() => {
              if (activeTab === 'quick' && quickMode) {
                quickForm.submit();
                return;
              }
              form.submit();
            }}
          >
            {activeTab === 'quick' && quickMode
              ? t('tenants.users.quick.generate')
              : t('users.create.submit')}
          </Button>,
        ]}
      >
        {quickMode ? (
          <Tabs
            activeKey={activeTab}
            destroyOnHidden={false}
            onChange={(key) => setActiveTab(key as 'normal' | 'quick')}
            items={[
              {
                key: 'normal',
                label: t('users.create.tabs.normal'),
                children: normalForm,
              },
              {
                key: 'quick',
                label: t('users.create.tabs.quick'),
                children: quickFormContent,
              },
            ]}
          />
        ) : (
          normalForm
        )}
      </Modal>

      {open && tenantAssignmentVisible && tenantAssignmentResult ? (
        <UserTenantAssignmentModal
          open
          userEmail={tenantAssignmentModal.userEmail}
          currentTenants={tenantAssignmentModal.userTenants}
          allTenants={tenantRows}
          confirmLoading={assignmentSubmitting}
          cancelText={t('common.buttons.close')}
          onClose={() => {
            setPasswordResult(tenantAssignmentResult);
            setTenantAssignmentResult(null);
            tenantAssignmentModal.closeModal();
          }}
          onSave={handleTenantAssignmentSave}
          initialSelectedTenantIds={tenantAssignmentModal.initialSelectedTenantIds}
        />
      ) : null}

      <CreateUserPasswordSuccessModal
        open={open && !!passwordResult}
        result={passwordResult}
        password={password}
        onCopyPassword={() => void copyPassword()}
        onClose={closePasswordModal}
        t={t}
      />

      {quickMode ? (
        <QuickUserSuccessModal
          open={open && !!quickResult}
          result={quickResult}
          role={watchedQuickRole}
          tenantName={quickPreviewName}
          tenantSlug={quickPreviewSlug}
          onClose={closeQuickResult}
          onGenerateAnother={handleGenerateAnotherQuickUser}
        />
      ) : null}
    </>
  );
}
