import { apiClient } from './config';
import {
  parsePosCashRegisterContextDto,
  type PosCashRegisterContextDto,
} from '../../utils/posCashRegisterReadinessParse';

export type { PosCashRegisterContextDto };

/** POST /api/pos/cash-register/ensure-ready */
export async function postEnsurePosCashRegisterReady(): Promise<PosCashRegisterContextDto> {
  const raw = await apiClient.post<unknown>('/pos/cash-register/ensure-ready', {});
  return parsePosCashRegisterContextDto(raw);
}

export { parsePosCashRegisterContextDto } from '../../utils/posCashRegisterReadinessParse';
