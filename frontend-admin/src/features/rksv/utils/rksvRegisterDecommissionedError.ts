import { isAxiosError } from 'axios';

export const RKSV_REGISTER_DECOMMISSIONED = 'RKSV_REGISTER_DECOMMISSIONED';

/** True when special-receipt creation was rejected because the register has a Schlussbeleg. */
export function isRksvRegisterDecommissionedError(error: unknown): boolean {
  if (!isAxiosError(error)) return false;
  const data = error.response?.data;
  if (!data || typeof data !== 'object') return false;
  const body = data as { code?: unknown; message?: unknown };
  if (body.code === RKSV_REGISTER_DECOMMISSIONED) return true;
  return (
    typeof body.message === 'string' &&
    body.message.toLowerCase().includes('permanently decommissioned')
  );
}
