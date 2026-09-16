import { customInstance } from '@/lib/axios';

export type AutoTagesabschlussOpenOrdersPolicy =
  | 'NotifyAndContinue'
  | 'Block'
  | 'ForceWithWarning';

export type AutoTagesabschlussSettings = {
  enabled: boolean;
  hourVienna: number;
  minuteVienna: number;
  openOrdersPolicy: AutoTagesabschlussOpenOrdersPolicy;
  promptCashCount: boolean;
};

type SettingsApi = {
  enabled?: boolean;
  Enabled?: boolean;
  hourVienna?: number;
  HourVienna?: number;
  minuteVienna?: number;
  MinuteVienna?: number;
  openOrdersPolicy?: string;
  OpenOrdersPolicy?: string;
  promptCashCount?: boolean;
  PromptCashCount?: boolean;
};

function normalizePolicy(value: string | undefined): AutoTagesabschlussOpenOrdersPolicy {
  if (value === 'Block' || value === 'NotifyAndContinue') return value;
  return 'ForceWithWarning';
}

function mapSettings(dto: SettingsApi): AutoTagesabschlussSettings {
  return {
    enabled: dto.enabled ?? dto.Enabled ?? true,
    hourVienna: dto.hourVienna ?? dto.HourVienna ?? 3,
    minuteVienna: dto.minuteVienna ?? dto.MinuteVienna ?? 0,
    openOrdersPolicy: normalizePolicy(dto.openOrdersPolicy ?? dto.OpenOrdersPolicy),
    promptCashCount: dto.promptCashCount ?? dto.PromptCashCount ?? true,
  };
}

export async function getAutoTagesabschlussSettings(): Promise<AutoTagesabschlussSettings> {
  const raw = await customInstance<SettingsApi>({
    url: '/api/Tagesabschluss/auto-close-settings',
    method: 'GET',
  });
  return mapSettings(raw ?? {});
}

export async function putAutoTagesabschlussSettings(
  body: AutoTagesabschlussSettings
): Promise<AutoTagesabschlussSettings> {
  const raw = await customInstance<SettingsApi>({
    url: '/api/Tagesabschluss/auto-close-settings',
    method: 'PUT',
    data: body,
    headers: { 'Content-Type': 'application/json' },
  });
  return mapSettings(raw ?? body);
}
