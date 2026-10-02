'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Input, Select, Space, Switch, Table, Tabs, Tag, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

import {
  cloneVerticalProfile,
  createVerticalProfile,
  deleteVerticalProfile,
  listTenantsByVerticalProfile,
  listVerticalProfileTenants,
  listVerticalProfiles,
  updateVerticalProfile,
  updateVerticalProfileFeatures,
  type VerticalProfileRecord,
  type VerticalProfileTenantSummary,
} from './api';
import {
  buildProfilePreview,
  FIELD_ENTITIES,
  VERTICAL_FEATURE_GROUPS,
  VERTICAL_LAYOUTS,
} from './profilePreview';

type FieldMap = Record<string, string[]>;
type EditorMode = 'edit' | 'create' | 'clone';

const CATALOG_KEY = ['admin', 'vertical-profiles', 'catalog'] as const;

function asFeatures(value: Record<string, boolean> | undefined): Record<string, boolean> {
  return { ...(value ?? {}) };
}

function asFields(value: Record<string, string[]> | undefined): FieldMap {
  const next: FieldMap = { customer: [], product: [], order: [] };
  for (const entity of FIELD_ENTITIES) {
    next[entity] = Array.isArray(value?.[entity]) ? [...value[entity]] : [];
  }
  return next;
}

function profileLabel(
  profile: Pick<VerticalProfileRecord, 'id' | 'name'>,
  t: (key: string) => string
): string {
  if (profile.name.startsWith('verticalProfiles.') || profile.name.startsWith('admin.')) {
    return t(profile.name);
  }
  return profile.name || profile.id;
}

export function VerticalConfigHub() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState('list');
  const [mode, setMode] = useState<EditorMode>('edit');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [slug, setSlug] = useState('');
  const [name, setName] = useState('');
  const [cloneFrom, setCloneFrom] = useState<string | undefined>();
  const [features, setFeatures] = useState<Record<string, boolean>>({});
  const [requiredFields, setRequiredFields] = useState<FieldMap>(asFields(undefined));
  const [optionalFields, setOptionalFields] = useState<FieldMap>(asFields(undefined));
  const [layout, setLayout] = useState<string>('standard');
  const [fieldDraft, setFieldDraft] = useState<Record<string, string>>({});
  const [deleteBlock, setDeleteBlock] = useState<VerticalProfileTenantSummary[] | null>(null);
  const [matrixWarning, setMatrixWarning] = useState<string | null>(null);

  const profilesQuery = useQuery({
    queryKey: CATALOG_KEY,
    queryFn: listVerticalProfiles,
  });
  const groupsQuery = useQuery({
    queryKey: ['admin', 'vertical-profiles', 'by-profile'],
    queryFn: listTenantsByVerticalProfile,
    enabled: tab === 'assignments',
  });
  const tenantsQuery = useQuery({
    queryKey: ['admin', 'vertical-profiles', selectedId, 'tenants'],
    queryFn: () => listVerticalProfileTenants(selectedId ?? ''),
    enabled: tab === 'editor' && Boolean(selectedId) && mode === 'edit',
  });

  const profiles = useMemo(() => profilesQuery.data ?? [], [profilesQuery.data]);
  const selected = profiles.find((profile) => profile.id === selectedId) ?? null;
  const featureKeys = useMemo(() => {
    const known = VERTICAL_FEATURE_GROUPS.flatMap((group) => [...group.features]);
    const extra = Object.keys(features).filter((key) => !known.some((item) => item === key));
    return { known, extra };
  }, [features]);

  const removedFeatures = useMemo(() => {
    if (!selected || mode !== 'edit') return [];
    return Object.entries(selected.posFeatures)
      .filter(([key, enabled]) => enabled && features[key] === false)
      .map(([key]) => key);
  }, [features, mode, selected]);

  const preview = buildProfilePreview(features, layout);
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'vertical-profiles'] });
  };

  const openEditor = (profile: VerticalProfileRecord, nextMode: EditorMode) => {
    setMode(nextMode);
    setSelectedId(profile.id);
    setSlug(nextMode === 'edit' ? profile.id : '');
    setName(nextMode === 'edit' ? profile.name : '');
    setCloneFrom(nextMode === 'clone' ? profile.id : undefined);
    setFeatures(asFeatures(profile.posFeatures));
    setRequiredFields(asFields(profile.requiredFields));
    setOptionalFields(asFields(profile.optionalFields));
    setLayout(profile.posLayout || 'standard');
    setTab('editor');
  };

  const startBlank = () => {
    setMode('create');
    setSelectedId(null);
    setSlug('');
    setName('');
    setCloneFrom(undefined);
    setFeatures(Object.fromEntries(featureKeys.known.map((key) => [key, false])));
    setRequiredFields(asFields(undefined));
    setOptionalFields(asFields(undefined));
    setLayout('standard');
    setTab('editor');
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      const body = {
        name,
        posFeatures: features,
        requiredFields,
        optionalFields,
        posLayout: layout,
      };
      if (mode === 'create') {
        return createVerticalProfile({ ...body, id: slug, cloneFrom });
      }
      if (mode === 'clone' && selectedId) {
        return cloneVerticalProfile(selectedId, { id: slug, name });
      }
      if (!selectedId) throw new Error('missing profile');
      return updateVerticalProfile(selectedId, body);
    },
    onSuccess: async (result) => {
      notify.success(t('admin.verticalProfiles.saveSuccess'));
      if (result.affectedTenants.length > 0) {
        notify.warning(t('admin.verticalProfiles.featureRemovalWarning'), {
          mode: 'notification',
          description: result.affectedTenants.map((tenant) => tenant.name).join(', '),
        });
      }
      await refresh();
      setSelectedId(result.profile.id);
      setMode('edit');
    },
    onError: () => notify.error(t('admin.verticalProfiles.saveError')),
  });

  const requestDelete = async (profile: VerticalProfileRecord) => {
    const tenants = await listVerticalProfileTenants(profile.id);
    if (tenants.length > 0) {
      setDeleteBlock(tenants);
      return;
    }
    modal.confirm({
      title: t('admin.verticalProfiles.deleteConfirm'),
      onOk: async () => {
        await deleteVerticalProfile(profile.id);
        notify.success(t('admin.verticalProfiles.deleteSuccess'));
        await refresh();
      },
    });
  };

  const toggleMatrix = async (profile: VerticalProfileRecord, feature: string, enabled: boolean) => {
    if (!enabled && profile.posFeatures[feature] && profile.tenantCount > 0) {
      const tenants = await listVerticalProfileTenants(profile.id);
      setMatrixWarning(
        `${t('admin.verticalProfiles.featureRemovalWarning')}: ${tenants.map((tenant) => tenant.name).join(', ')}`
      );
    }
    await updateVerticalProfileFeatures(profile.id, { ...profile.posFeatures, [feature]: enabled });
    await refresh();
  };

  const addField = (bucket: 'required' | 'optional', entity: string) => {
    const draftKey = `${bucket}.${entity}`;
    const value = (fieldDraft[draftKey] ?? '').trim();
    if (!value) return;
    const setter = bucket === 'required' ? setRequiredFields : setOptionalFields;
    setter((current) => ({
      ...current,
      [entity]: current[entity]?.includes(value) ? current[entity] : [...(current[entity] ?? []), value],
    }));
    setFieldDraft((current) => ({ ...current, [draftKey]: '' }));
  };

  const featureLabel = (feature: string) => {
    const key = `admin.verticalProfiles.features.${feature}`;
    const translated = t(key);
    return translated === key ? feature : translated;
  };

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      {profilesQuery.isError ? (
        <Alert type="error" showIcon title={t('admin.verticalProfiles.loadError')} />
      ) : null}
      {deleteBlock ? (
        <Alert
          type="warning"
          showIcon
          title={t('admin.verticalProfiles.deleteBlockedTitle')}
          description={deleteBlock.map((tenant) => tenant.name).join(', ')}
        />
      ) : null}
      {matrixWarning ? <Alert type="warning" showIcon title={matrixWarning} /> : null}

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'list',
            label: t('admin.verticalProfiles.tabs.list'),
            children: (
              <Space orientation="vertical" size={12} style={{ width: '100%' }}>
                <Button type="primary" onClick={startBlank}>
                  {t('admin.verticalProfiles.actions.create')}
                </Button>
                <Table
                  rowKey="id"
                  loading={profilesQuery.isLoading}
                  pagination={false}
                  dataSource={profiles}
                  locale={{ emptyText: t('admin.verticalProfiles.empty') }}
                  columns={[
                    {
                      title: t('admin.verticalProfiles.columns.id'),
                      dataIndex: 'id',
                      render: (id: string) => <span data-testid={`profile-id-${id}`}>{id}</span>,
                    },
                    {
                      title: t('admin.verticalProfiles.columns.name'),
                      render: (_value, profile: VerticalProfileRecord) => profileLabel(profile, t),
                    },
                    {
                      title: t('admin.verticalProfiles.columns.featureCount'),
                      dataIndex: 'featureCount',
                    },
                    {
                      title: t('admin.verticalProfiles.columns.tenantCount'),
                      dataIndex: 'tenantCount',
                    },
                    {
                      title: t('admin.verticalProfiles.columns.source'),
                      dataIndex: 'source',
                      render: (source: string) =>
                        t(`admin.verticalProfiles.source.${source}`) || source,
                    },
                    {
                      title: t('admin.verticalProfiles.columns.actions'),
                      render: (_value, profile: VerticalProfileRecord) => (
                        <Space>
                          <Button
                            aria-label={`${t('admin.verticalProfiles.actions.edit')} ${profile.id}`}
                            onClick={() => openEditor(profile, 'edit')}
                          >
                            {t('admin.verticalProfiles.actions.edit')}
                          </Button>
                          <Button
                            aria-label={`${t('admin.verticalProfiles.actions.clone')} ${profile.id}`}
                            onClick={() => openEditor(profile, 'clone')}
                          >
                            {t('admin.verticalProfiles.actions.clone')}
                          </Button>
                          <Button
                            aria-label={`${t('admin.verticalProfiles.actions.viewTenants')} ${profile.id}`}
                            onClick={() => setTab('assignments')}
                          >
                            {t('admin.verticalProfiles.actions.viewTenants')}
                          </Button>
                          {profile.source === 'custom' ? (
                            <Button
                              danger
                              aria-label={`${t('admin.verticalProfiles.actions.delete')} ${profile.id}`}
                              onClick={() => void requestDelete(profile)}
                            >
                              {t('admin.verticalProfiles.actions.delete')}
                            </Button>
                          ) : null}
                        </Space>
                      ),
                    },
                  ]}
                />
              </Space>
            ),
          },
          {
            key: 'editor',
            label: t('admin.verticalProfiles.tabs.editor'),
            children: (
              <Space orientation="vertical" size={16} style={{ width: '100%' }}>
                {mode !== 'edit' ? (
                  <Input
                    aria-label={t('admin.verticalProfiles.editor.slug')}
                    placeholder={t('admin.verticalProfiles.editor.slug')}
                    value={slug}
                    onChange={(event) => setSlug(event.target.value)}
                  />
                ) : null}
                <Input
                  aria-label={t('admin.verticalProfiles.editor.name')}
                  placeholder={t('admin.verticalProfiles.editor.name')}
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                />
                {mode === 'create' ? (
                  <Select
                    aria-label={t('admin.verticalProfiles.editor.cloneFrom')}
                    allowClear
                    placeholder={t('admin.verticalProfiles.editor.cloneFromNone')}
                    value={cloneFrom}
                    onChange={setCloneFrom}
                    options={profiles.map((profile) => ({
                      value: profile.id,
                      label: profileLabel(profile, t),
                    }))}
                  />
                ) : null}
                <Select
                  aria-label={t('admin.verticalProfiles.editor.layout')}
                  value={layout}
                  onChange={setLayout}
                  options={VERTICAL_LAYOUTS.map((value) => ({
                    value,
                    label: t(`admin.verticalProfiles.layouts.${value}`),
                  }))}
                />
                {VERTICAL_FEATURE_GROUPS.map((group) => (
                  <Card
                    key={group.id}
                    size="small"
                    title={t(`admin.verticalProfiles.features.groups.${group.id}`)}
                  >
                    {group.features.map((feature) => (
                      <div key={feature}>
                        <Switch
                          aria-label={featureLabel(feature)}
                          checked={Boolean(features[feature])}
                          onChange={(checked) =>
                            setFeatures((current) => ({ ...current, [feature]: checked }))
                          }
                        />{' '}
                        {featureLabel(feature)}
                      </div>
                    ))}
                  </Card>
                ))}
                {featureKeys.extra.length > 0 ? (
                  <Card size="small" title={t('admin.verticalProfiles.features.groups.other')}>
                    {featureKeys.extra.map((feature) => (
                      <div key={feature}>
                        <Switch
                          aria-label={feature}
                          checked={Boolean(features[feature])}
                          onChange={(checked) =>
                            setFeatures((current) => ({ ...current, [feature]: checked }))
                          }
                        />{' '}
                        {feature}
                      </div>
                    ))}
                  </Card>
                ) : null}
                {removedFeatures.length > 0 && (tenantsQuery.data?.length ?? 0) > 0 ? (
                  <Alert
                    type="warning"
                    showIcon
                    title={t('admin.verticalProfiles.featureRemovalWarning')}
                    description={tenantsQuery.data?.map((tenant) => tenant.name).join(', ')}
                  />
                ) : null}
                {(['required', 'optional'] as const).map((bucket) => {
                  const values = bucket === 'required' ? requiredFields : optionalFields;
                  const title =
                    bucket === 'required'
                      ? t('admin.verticalProfiles.editor.requiredFields')
                      : t('admin.verticalProfiles.editor.optionalFields');
                  return (
                    <Card key={bucket} size="small" title={title}>
                      {FIELD_ENTITIES.map((entity) => (
                        <div key={entity}>
                          <Typography.Text>
                            {t(`admin.verticalProfiles.entities.${entity}`)}
                          </Typography.Text>
                          <div>
                            {(values[entity] ?? []).map((field) => (
                              <Tag
                                key={field}
                                closable
                                onClose={() => {
                                  const setter =
                                    bucket === 'required' ? setRequiredFields : setOptionalFields;
                                  setter((current) => ({
                                    ...current,
                                    [entity]: current[entity].filter((item) => item !== field),
                                  }));
                                }}
                              >
                                {field}
                              </Tag>
                            ))}
                          </div>
                          <Space>
                            <Input
                              aria-label={`${title} ${entity}`}
                              value={fieldDraft[`${bucket}.${entity}`] ?? ''}
                              placeholder={t('admin.verticalProfiles.editor.fieldPlaceholder')}
                              onChange={(event) =>
                                setFieldDraft((current) => ({
                                  ...current,
                                  [`${bucket}.${entity}`]: event.target.value,
                                }))
                              }
                            />
                            <Button onClick={() => addField(bucket, entity)}>
                              {t('admin.verticalProfiles.editor.addField')}
                            </Button>
                          </Space>
                        </div>
                      ))}
                    </Card>
                  );
                })}
                <Card size="small" title={t('admin.verticalProfiles.preview.title')}>
                  <p>
                    {t('admin.verticalProfiles.preview.enables')}{' '}
                    {preview.enabled.length > 0
                      ? preview.enabled.map((key) => t(key)).join(', ')
                      : t('admin.verticalProfiles.preview.enablesEmpty')}
                  </p>
                  <p>
                    {t('admin.verticalProfiles.preview.posScreens')}{' '}
                    {preview.screens.length > 0
                      ? preview.screens.map((key) => t(key)).join(', ')
                      : t('admin.verticalProfiles.preview.posScreensEmpty')}
                  </p>
                  <p>
                    {t('admin.verticalProfiles.preview.faTabs')}{' '}
                    {preview.tabs.length > 0
                      ? preview.tabs.map((key) => t(key)).join(', ')
                      : t('admin.verticalProfiles.preview.faTabsEmpty')}
                  </p>
                </Card>
                <Button type="primary" loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
                  {t('admin.verticalProfiles.actions.save')}
                </Button>
              </Space>
            ),
          },
          {
            key: 'assignments',
            label: t('admin.verticalProfiles.tabs.assignments'),
            children: (
              <Space orientation="vertical" size={12} style={{ width: '100%' }}>
                {(groupsQuery.data ?? []).map((group) => (
                  <Card key={group.profileId} size="small" title={`${group.profileId} (${group.tenantCount})`}>
                    {group.tenants.length === 0 ? (
                      <Typography.Text type="secondary">
                        {t('admin.verticalProfiles.assignments.empty')}
                      </Typography.Text>
                    ) : (
                      group.tenants.map((tenant) => (
                        <div key={tenant.id}>
                          <a href={`/admin/tenants/${tenant.id}?tab=verticalProfile`}>{tenant.name}</a>
                          <Typography.Text type="secondary"> ({tenant.overridesCount})</Typography.Text>
                        </div>
                      ))
                    )}
                  </Card>
                ))}
              </Space>
            ),
          },
          {
            key: 'matrix',
            label: t('admin.verticalProfiles.tabs.matrix'),
            children: (
              <Table
                rowKey="feature"
                pagination={false}
                dataSource={VERTICAL_FEATURE_GROUPS.flatMap((group) =>
                  group.features.map((feature) => ({ feature }))
                )}
                columns={[
                  {
                    title: t('admin.verticalProfiles.editor.features'),
                    dataIndex: 'feature',
                    render: (feature: string) => featureLabel(feature),
                  },
                  ...profiles.map((profile) => ({
                    title: profile.id,
                    key: profile.id,
                    render: (_value: unknown, row: { feature: string }) => (
                      <Switch
                        aria-label={`${profile.id} ${row.feature}`}
                        checked={Boolean(profile.posFeatures[row.feature])}
                        onChange={(checked) => void toggleMatrix(profile, row.feature, checked)}
                      />
                    ),
                  })),
                ]}
              />
            ),
          },
        ]}
      />
    </Space>
  );
}
