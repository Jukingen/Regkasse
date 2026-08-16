import { customInstance } from '@/lib/axios';

export type FiskalyFonAuthDto = {
  authenticated: boolean;
  authenticationStatus: string;
  participantId?: string | null;
  userId?: string | null;
  authenticatedAt?: string | null;
  error?: string | null;
};

export type FiskalyScuSetupDto = {
  scuId?: string | null;
  state: string;
  initializedAt?: string | null;
};

export type FiskalyCashRegisterSetupDto = {
  cashRegisterId: string;
  registerNumber?: string | null;
  location?: string | null;
  state: string;
  initializedAt?: string | null;
};

export type FiskalySetupStatusDto = {
  enabled: boolean;
  isConfigured: boolean;
  environment: string;
  fon: FiskalyFonAuthDto;
  scu: FiskalyScuSetupDto;
  cashRegisters: FiskalyCashRegisterSetupDto[];
};

export type AuthenticateFonRequest = {
  fonParticipantId: string;
  fonUserId: string;
  fonUserPin: string;
};

const SETUP_URL = '/api/admin/fiskaly/setup';

export async function getFiskalySetup(signal?: AbortSignal): Promise<FiskalySetupStatusDto> {
  return customInstance<FiskalySetupStatusDto>({
    url: SETUP_URL,
    method: 'GET',
    signal,
  });
}

export async function authenticateFiskalyFon(
  body: AuthenticateFonRequest,
  signal?: AbortSignal
): Promise<FiskalyFonAuthDto> {
  return customInstance<FiskalyFonAuthDto>({
    url: '/api/admin/fiskaly/fon/authenticate',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function initializeFiskalyScu(signal?: AbortSignal): Promise<FiskalyScuSetupDto> {
  return customInstance<FiskalyScuSetupDto>({
    url: '/api/admin/fiskaly/scu/initialize',
    method: 'POST',
    signal,
  });
}

export async function initializeFiskalyCashRegister(
  cashRegisterId: string,
  signal?: AbortSignal
): Promise<FiskalyCashRegisterSetupDto> {
  return customInstance<FiskalyCashRegisterSetupDto>({
    url: `/api/admin/fiskaly/cash-register/${cashRegisterId}/initialize`,
    method: 'POST',
    signal,
  });
}

export function isFiskalyResourceInitialized(state?: string | null): boolean {
  return (state ?? '').toUpperCase() === 'INITIALIZED';
}

export function isFiskalyFonAuthenticated(fon?: FiskalyFonAuthDto | null): boolean {
  return Boolean(fon?.authenticated) || (fon?.authenticationStatus ?? '').toUpperCase() === 'AUTHENTICATED';
}
