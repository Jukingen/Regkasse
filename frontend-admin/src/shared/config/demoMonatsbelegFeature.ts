import {
  RksvPublicEnvironmentState,
  parseStrictRksvPublicEnvironment,
} from '@/shared/config/rksvEnvironment';

export function parseDemoAutoSuggestMonatsbelegEnv(raw: string | undefined): boolean {
  if (raw === undefined || raw === '') return false;
  const value = raw.trim().toLowerCase();
  return value === 'true' || value === '1' || value === 'yes' || value === 'on';
}

export function isDemoAutoSuggestMonatsbelegEnabled(): boolean {
  const flagEnabled = parseDemoAutoSuggestMonatsbelegEnv(
    process.env.NEXT_PUBLIC_DEMO_AUTO_SUGGEST_MONATSBELEG
  );
  if (!flagEnabled) return false;

  const isDevelopment = process.env.NODE_ENV === 'development';
  if (!isDevelopment) return false;

  const parsedRksvEnv = parseStrictRksvPublicEnvironment();
  return parsedRksvEnv.state === RksvPublicEnvironmentState.TEST;
}
