'use client';

/**
 * RKSV hub FinanzOnline ortam etiketi: strict parse + Alert + Badge (tek hook ile bir kez parse).
 * Türkçe: /rksv sayfasında yeniden kullanım; env yalnızca shared config üzerinden.
 */
import { Alert, Tag, Tooltip, Typography } from 'antd';
import { type CSSProperties, useEffect, useMemo } from 'react';

import { useI18n } from '@/i18n/I18nProvider';
import {
  type RksvEnvironmentDevParseDebug,
  type StrictParsedRksvPublicEnvironment,
  buildRksvEnvironmentDevParseDebug,
  getRksvEnvironmentAlertType,
  getRksvEnvironmentBadgeColor,
  getRksvEnvironmentBannerKeys,
  getRksvEnvironmentDisplayLabelKey,
  logRksvEnvironmentDevParseDebug,
  parseStrictRksvPublicEnvironment,
  warnRksvPublicEnvironmentInConsole,
} from '@/shared/config/rksvEnvironment';

export function useRksvFinanzOnlineEnvironment(): {
  parsed: StrictParsedRksvPublicEnvironment;
  devParseDebug: RksvEnvironmentDevParseDebug | null;
} {
  const parsed = useMemo(() => parseStrictRksvPublicEnvironment(), []);
  const devParseDebug = useMemo(() => buildRksvEnvironmentDevParseDebug(), []);
  useEffect(() => {
    warnRksvPublicEnvironmentInConsole(parsed);
    logRksvEnvironmentDevParseDebug(devParseDebug);
  }, [parsed, devParseDebug]);
  return { parsed, devParseDebug };
}

type ParsedProp = { parsed: StrictParsedRksvPublicEnvironment };

type AlertProps = ParsedProp & {
  style?: CSSProperties;
  devParseDebug?: RksvEnvironmentDevParseDebug | null;
};

export function RksvFinanzOnlineEnvironmentBadge({ parsed }: ParsedProp) {
  const { t } = useI18n();
  const labelKey = getRksvEnvironmentDisplayLabelKey(parsed);
  const color = getRksvEnvironmentBadgeColor(parsed);
  return (
    <Tooltip title={t('rksvHub.env.buildTimeBadgeTooltip')}>
      <Tag color={color} data-rksv-environment-state={parsed.state}>
        {t(labelKey)}
      </Tag>
    </Tooltip>
  );
}

export function RksvFinanzOnlineEnvironmentAlert({ parsed, style, devParseDebug }: AlertProps) {
  const { t } = useI18n();
  const keys = getRksvEnvironmentBannerKeys(parsed);
  const params = keys.i18nParams ?? undefined;
  return (
    <div data-rksv-environment-state={parsed.state} data-rksv-environment-callout="true">
      <Alert
        showIcon
        type={getRksvEnvironmentAlertType(parsed)}
        title={t(keys.messageKey, params)}
        description={t(keys.descriptionKey, params)}
        style={style}
      />
      {devParseDebug ? (
        <Typography.Paragraph
          type="secondary"
          style={{ marginTop: 8, marginBottom: 0, fontFamily: 'monospace', fontSize: 12 }}
          data-rksv-env-dev-debug="true"
        >
          [dev] raw={devParseDebug.rawJson} trimmed={JSON.stringify(devParseDebug.trimmed)}{' '}
          normalized=
          {JSON.stringify(devParseDebug.normalizedUpper)} state={devParseDebug.parsedState} present=
          {String(devParseDebug.processEnvKeyPresent)} source={devParseDebug.source}
        </Typography.Paragraph>
      ) : null}
    </div>
  );
}
