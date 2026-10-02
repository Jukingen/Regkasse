'use client';

import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Input, InputNumber, Row, Select, Space, Switch, Tag, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import {
  getApiAdminTenantsTenantIdVerticalProfile,
  getApiAdminVerticalProfiles,
  putApiAdminTenantsTenantIdVerticalProfile,
} from '@/api/generated/admin/admin';
import type {
  EffectiveVerticalProfileDto,
  UpdateTenantVerticalProfileRequest,
  VerticalProfileDto,
} from '@/api/generated/model';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type EntityFieldGroup = 'customer' | 'product';
type FeatureValues = Record<string, boolean>;
type FieldValues = Record<EntityFieldGroup, string[]>;
type FieldInputs = Record<EntityFieldGroup, string>;
type JsonObject = Record<string, unknown>;
type EffectiveVerticalProfileView = EffectiveVerticalProfileDto & {
  taxiTariffPerKm?: number | null;
};

const EMPTY_FIELD_INPUTS: FieldInputs = { customer: '', product: '' };
const KNOWN_PROFILE_NAME_KEYS: Record<string, string> = {
  gastronomy: 'verticalProfiles.gastronomy.name',
  'gastronomy-tables': 'verticalProfiles.gastronomyTables.name',
  'hair-salon': 'verticalProfiles.hairSalon.name',
  'mobile-services': 'verticalProfiles.mobileServices.name',
  'handy-shop': 'verticalProfiles.handyShop.name',
  taxi: 'verticalProfiles.taxi.name',
  'ticket-sales': 'verticalProfiles.ticketSales.name',
  beherbergung: 'verticalProfiles.beherbergung.name',
  vet: 'verticalProfiles.vet.name',
};
const KNOWN_FEATURE_LABEL_KEYS: Record<string, string> = {
  tables: 'tenants.verticalProfile.features.tables',
  kitchenDisplay: 'tenants.verticalProfile.features.kitchenDisplay',
  patientRecord: 'tenants.verticalProfile.features.patientRecord',
  serviceDuration: 'tenants.verticalProfile.features.serviceDuration',
  appointment: 'tenants.verticalProfile.features.appointment',
  imeiTracking: 'tenants.verticalProfile.features.imeiTracking',
  routeTracking: 'tenants.verticalProfile.features.routeTracking',
  roomTracking: 'tenants.verticalProfile.features.roomTracking',
  ticketScan: 'tenants.verticalProfile.features.ticketScan',
};
const ENTITY_FIELD_LABEL_KEYS: Record<EntityFieldGroup, string> = {
  customer: 'tenants.verticalProfile.customerFields',
  product: 'tenants.verticalProfile.productFields',
};
const ENTITY_FIELD_PLACEHOLDER_KEYS: Record<EntityFieldGroup, string> = {
  customer: 'tenants.verticalProfile.customerFieldPlaceholder',
  product: 'tenants.verticalProfile.productFieldPlaceholder',
};

function asObject(value: unknown): JsonObject {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as JsonObject)
    : {};
}

function toFeatureValues(value: unknown): FeatureValues {
  return Object.fromEntries(
    Object.entries(asObject(value)).filter((entry): entry is [string, boolean] => {
      return typeof entry[1] === 'boolean';
    })
  );
}

function toFieldValues(value: unknown): FieldValues {
  const object = asObject(value);
  const read = (key: EntityFieldGroup): string[] =>
    Array.isArray(object[key])
      ? object[key].filter((field): field is string => typeof field === 'string')
      : [];
  return { customer: read('customer'), product: read('product') };
}

function profileNameKey(profile: Pick<VerticalProfileDto, 'id' | 'name'>): string {
  return (profile.id && KNOWN_PROFILE_NAME_KEYS[profile.id]) || profile.name || profile.id || '';
}

function featureLabelKey(feature: string): string {
  return KNOWN_FEATURE_LABEL_KEYS[feature] || feature;
}

export interface TenantVerticalProfileEditorProps {
  tenantId: string;
}

export function TenantVerticalProfileEditor({ tenantId }: TenantVerticalProfileEditorProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [selectedProfileId, setSelectedProfileId] = useState('');
  const [featureValues, setFeatureValues] = useState<FeatureValues | null>(null);
  const [fieldValues, setFieldValues] = useState<FieldValues | null>(null);
  const [preservedOverrides, setPreservedOverrides] = useState<JsonObject | null>(null);
  const [fieldInputs, setFieldInputs] = useState<FieldInputs>(EMPTY_FIELD_INPUTS);
  const [taxiTariffPerKm, setTaxiTariffPerKm] = useState<number | null>(null);

  const profilesQuery = useQuery({
    queryKey: ['admin', 'vertical-profiles'],
    queryFn: () => getApiAdminVerticalProfiles(),
    enabled: Boolean(tenantId),
  });
  const effectiveQuery = useQuery({
    queryKey: ['admin', 'tenants', tenantId, 'vertical-profile'],
    queryFn: () => getApiAdminTenantsTenantIdVerticalProfile(tenantId),
    enabled: Boolean(tenantId),
  });

  const profiles = useMemo(() => profilesQuery.data ?? [], [profilesQuery.data]);
  const effectiveProfileId = selectedProfileId || effectiveQuery.data?.profileId || '';
  const effectiveFeatureValues =
    featureValues ?? toFeatureValues(effectiveQuery.data?.posFeatures);
  const effectiveFieldValues = fieldValues ?? toFieldValues(effectiveQuery.data?.optionalFields);
  const effectiveOverrides =
    preservedOverrides ?? asObject(effectiveQuery.data?.overrides);
  const effectiveView = effectiveQuery.data as EffectiveVerticalProfileView | undefined;
  const effectiveTariff =
    taxiTariffPerKm ??
    (typeof effectiveView?.taxiTariffPerKm === 'number' ? effectiveView.taxiTariffPerKm : null);
  const selectedProfile = useMemo(
    () => profiles.find((profile) => profile.id === effectiveProfileId),
    [profiles, effectiveProfileId]
  );

  const selectProfile = (profileId: string) => {
    const profile = profiles.find((candidate) => candidate.id === profileId);
    setSelectedProfileId(profileId);

    if (profileId === effectiveQuery.data?.profileId) {
      setFeatureValues(toFeatureValues(effectiveQuery.data.posFeatures));
      setFieldValues(toFieldValues(effectiveQuery.data.optionalFields));
      setPreservedOverrides(asObject(effectiveQuery.data.overrides));
      return;
    }

    setFeatureValues(toFeatureValues(profile?.posFeatures));
    setFieldValues(toFieldValues(profile?.optionalFields));
    setPreservedOverrides({});
  };

  const saveMutation = useMutation({
    mutationFn: (request: UpdateTenantVerticalProfileRequest) =>
      putApiAdminTenantsTenantIdVerticalProfile(tenantId, request),
    onSuccess: (effective: EffectiveVerticalProfileDto) => {
      notify.success(t('tenants.verticalProfile.saveSuccess'));
      queryClient.setQueryData(
        ['admin', 'tenants', tenantId, 'vertical-profile'],
        effective
      );
      void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
    },
    onError: () => notify.error(t('tenants.verticalProfile.saveError')),
  });

  const addField = (entity: EntityFieldGroup) => {
    const value = fieldInputs[entity].trim();
    if (!value || effectiveFieldValues[entity].includes(value)) return;

    setFieldValues((current) => ({
      ...(current ?? effectiveFieldValues),
      [entity]: [...(current ?? effectiveFieldValues)[entity], value],
    }));
    setFieldInputs((current) => ({ ...current, [entity]: '' }));
  };

  const removeField = (entity: EntityFieldGroup, field: string) => {
    setFieldValues((current) => ({
      ...(current ?? effectiveFieldValues),
      [entity]: (current ?? effectiveFieldValues)[entity].filter(
        (candidate) => candidate !== field
      ),
    }));
  };

  const save = () => {
    if (!effectiveProfileId) return;
    saveMutation.mutate({
      profileId: effectiveProfileId,
      taxiTariffPerKm: effectiveTariff,
      overrides: {
        ...effectiveOverrides,
        posFeatures: effectiveFeatureValues,
        optionalFields: effectiveFieldValues,
      },
    } as UpdateTenantVerticalProfileRequest);
  };

  const featureKeys = Object.keys(asObject(selectedProfile?.posFeatures)).sort();
  const loading = profilesQuery.isLoading || effectiveQuery.isLoading;
  const loadFailed = profilesQuery.isError || effectiveQuery.isError;

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      {loadFailed ? (
        <Alert
          type="error"
          showIcon
          title={t('tenants.verticalProfile.loadError')}
        />
      ) : null}

      <Card loading={loading} title={t('tenants.verticalProfile.selectorTitle')}>
        <Typography.Paragraph type="secondary">
          {t('tenants.verticalProfile.selectorHelp')}
        </Typography.Paragraph>
        <Select
          aria-label={t('tenants.verticalProfile.profileLabel')}
          value={effectiveProfileId || undefined}
          placeholder={t('tenants.verticalProfile.profilePlaceholder')}
          onChange={selectProfile}
          options={profiles.map((profile) => ({
            value: profile.id,
            label: t(profileNameKey(profile)),
          }))}
          style={{ width: '100%' }}
        />
      </Card>

      <Card title={t('tenants.verticalProfile.overrideTitle')} loading={loading}>
        <Space orientation="vertical" size={20} style={{ width: '100%' }}>
          <div>
            <Typography.Title level={5}>
              {t('tenants.verticalProfile.posFeaturesTitle')}
            </Typography.Title>
            {featureKeys.length === 0 ? (
              <Typography.Text type="secondary">
                {t('tenants.verticalProfile.noFeatures')}
              </Typography.Text>
            ) : (
              <Row gutter={[16, 12]}>
                {featureKeys.map((feature) => (
                  <Col xs={24} md={12} key={feature}>
                    <Space>
                      <Switch
                        aria-label={t(featureLabelKey(feature))}
                        checked={effectiveFeatureValues[feature] ?? false}
                        onChange={(checked) =>
                          setFeatureValues((current) => ({
                            ...(current ?? effectiveFeatureValues),
                            [feature]: checked,
                          }))
                        }
                      />
                      <Typography.Text>
                        {t(featureLabelKey(feature))}
                      </Typography.Text>
                    </Space>
                  </Col>
                ))}
              </Row>
            )}
          </div>

          <div>
            <Typography.Title level={5}>
              {t('tenants.verticalProfile.customFieldsTitle')}
            </Typography.Title>
            <Row gutter={[16, 16]}>
              {(['customer', 'product'] as const).map((entity) => (
                <Col xs={24} md={12} key={entity}>
                  <Card
                    size="small"
                    title={t(ENTITY_FIELD_LABEL_KEYS[entity])}
                  >
                    <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
                      <Input
                        aria-label={t(ENTITY_FIELD_PLACEHOLDER_KEYS[entity])}
                        placeholder={t(ENTITY_FIELD_PLACEHOLDER_KEYS[entity])}
                        value={fieldInputs[entity]}
                        onChange={(event) =>
                          setFieldInputs((current) => ({
                            ...current,
                            [entity]: event.target.value,
                          }))
                        }
                        onPressEnter={() => addField(entity)}
                      />
                      <Button
                        icon={<PlusOutlined />}
                        aria-label={t('tenants.verticalProfile.addField')}
                        onClick={() => addField(entity)}
                      >
                        {t('tenants.verticalProfile.addField')}
                      </Button>
                    </Space.Compact>
                    <Space size={[4, 8]} wrap>
                      {effectiveFieldValues[entity].map((field) => (
                        <Tag
                          key={field}
                          closable
                          onClose={() => removeField(entity, field)}
                        >
                          {field}
                        </Tag>
                      ))}
                      {effectiveFieldValues[entity].length === 0 ? (
                        <Typography.Text type="secondary">
                          {t('tenants.verticalProfile.noCustomFields')}
                        </Typography.Text>
                      ) : null}
                    </Space>
                  </Card>
                </Col>
              ))}
            </Row>
          </div>
        </Space>
      </Card>

      {effectiveProfileId === 'taxi' ? (
        <Card title={t('tenants.verticalProfile.taxiTariffTitle')} loading={loading}>
          <Typography.Paragraph type="secondary">
            {t('tenants.verticalProfile.taxiTariffHelp')}
          </Typography.Paragraph>
          <InputNumber
            aria-label={t('tenants.verticalProfile.taxiTariffLabel')}
            min={0}
            max={9999.99}
            step={0.1}
            precision={2}
            value={effectiveTariff}
            onChange={(value) => setTaxiTariffPerKm(typeof value === 'number' ? value : null)}
            style={{ width: '100%' }}
          />
        </Card>
      ) : null}

      <Button
        type="primary"
        loading={saveMutation.isPending}
        disabled={!effectiveProfileId || loadFailed}
        onClick={save}
      >
        {t('tenants.verticalProfile.save')}
      </Button>
    </Space>
  );
}
