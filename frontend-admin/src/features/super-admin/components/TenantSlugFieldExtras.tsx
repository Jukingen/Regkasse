'use client';

/**
 * Live address preview, availability status, and clickable slug suggestions.
 */
import { CheckCircleOutlined, CloseCircleOutlined, LinkOutlined } from '@ant-design/icons';
import { Space, Spin, Tag, Typography } from 'antd';
import React from 'react';

import type { SlugAvailabilityUi } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { getTenantSlugPreviewSegment } from '@/features/super-admin/lib/tenantSlug';
import { useI18n } from '@/i18n';
import styles from '@/styles/tenant-form.module.css';

export type TenantSlugFieldExtrasProps = {
  slugValue: string | undefined;
  baseDomain: string;
  portalUrl?: string | null;
  availabilityUi: SlugAvailabilityUi;
  suggestions?: string[];
  onSelectSuggestion?: (slug: string) => void;
};

export function TenantSlugFieldExtras({
  slugValue,
  baseDomain,
  portalUrl,
  availabilityUi,
  suggestions = [],
  onSelectSuggestion,
}: TenantSlugFieldExtrasProps) {
  const { t } = useI18n();
  const previewSegment = getTenantSlugPreviewSegment(slugValue);

  return (
    <div>
      {availabilityUi === 'checking' ? (
        <p className={`${styles.availability} ${styles.availabilityChecking}`}>
          <Spin size="small" style={{ marginRight: 8 }} />
          {t('tenants.create.fields.slug.checkingShort')}
        </p>
      ) : null}
      {availabilityUi === 'available' ? (
        <p className={`${styles.availability} ${styles.availabilityAvailable}`}>
          <CheckCircleOutlined aria-hidden style={{ marginRight: 6 }} />
          {t('tenants.create.fields.slug.availableShort')}
        </p>
      ) : null}
      {availabilityUi === 'taken' ? (
        <p className={`${styles.availability} ${styles.availabilityTaken}`}>
          <CloseCircleOutlined aria-hidden style={{ marginRight: 6 }} />
          {t('tenants.create.fields.slug.takenShort')}
        </p>
      ) : null}

      {availabilityUi === 'taken' && suggestions.length > 0 ? (
        <div className={styles.suggestionsBlock}>
          <Typography.Text type="secondary" className={styles.suggestionsLabel}>
            {t('tenants.create.fields.slug.suggestionsLabel')}
          </Typography.Text>
          <Space size={[8, 8]} wrap className={styles.suggestionsChips}>
            {suggestions.map((slug) => (
              <Tag
                key={slug}
                className={styles.suggestionChip}
                onClick={() => onSelectSuggestion?.(slug)}
                role="button"
                tabIndex={0}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    onSelectSuggestion?.(slug);
                  }
                }}
              >
                {slug}
              </Tag>
            ))}
          </Space>
        </div>
      ) : null}

      {previewSegment && availabilityUi === 'available' ? (
        <p className={styles.preview}>
          <LinkOutlined aria-hidden style={{ marginRight: 6 }} />
          {portalUrl ? (
            <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer" strong>
              {previewSegment}.{baseDomain}
            </Typography.Link>
          ) : (
            <Typography.Text strong>
              {previewSegment}.{baseDomain}
            </Typography.Text>
          )}
        </p>
      ) : null}
    </div>
  );
}
