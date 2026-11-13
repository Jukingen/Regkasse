import { apiClient } from './config';
import {
  parsePosCashRegisterContextDto,
  type PosCashRegisterContextDto,
} from '../../utils/posCashRegisterReadinessParse';

export type { PosCashRegisterContextDto };

/** GET /api/pos/cash-register/current — read-only readiness snapshot (no auto-open). */
export async function getPosCashRegisterCurrent(): Promise<PosCashRegisterContextDto> {
  const raw = await apiClient.get<unknown>('/pos/cash-register/current');
  return parsePosCashRegisterContextDto(raw);
}

/** POST /api/pos/cash-register/ensure-ready */
export async function postEnsurePosCashRegisterReady(): Promise<PosCashRegisterContextDto> {
  const raw = await apiClient.post<unknown>('/pos/cash-register/ensure-ready', {});
  return parsePosCashRegisterContextDto(raw);
}

export { parsePosCashRegisterContextDto } from '../../utils/posCashRegisterReadinessParse';
