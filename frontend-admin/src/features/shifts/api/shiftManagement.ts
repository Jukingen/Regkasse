import {
  postApiCashRegisterIdClose,
  postApiCashRegisterIdOpen,
} from '@/api/generated/cash-register/cash-register';
import { listCashRegistersByTenant } from '@/features/cash-registers/api/cashRegisters';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

export type ShiftStatusDto = {
  registerId: string;
  isOpen: boolean;
  currentBalance: number;
  registerNumber?: string;
  location?: string;
};

export const shiftStatusQueryKey = (registerId?: string) =>
  ['shift', 'status', registerId?.trim() || '__none__'] as const;

export async function fetchShiftStatus(registerId: string): Promise<ShiftStatusDto> {
  const trimmedId = registerId.trim();
  const registers = await listCashRegistersByTenant();
  const register = registers.find((row) => row.id === trimmedId);

  if (!register) {
    throw new Error('REGISTER_NOT_FOUND');
  }

  return {
    registerId: register.id,
    isOpen: register.status === REGISTER_STATUS.open,
    currentBalance: register.currentBalance ?? 0,
    registerNumber: register.registerNumber,
    location: register.location,
  };
}

export async function openCashRegisterShift(registerId: string) {
  return postApiCashRegisterIdOpen(registerId.trim(), { openingBalance: 0 });
}

export async function closeCashRegisterShift(registerId: string, closingBalance: number) {
  return postApiCashRegisterIdClose(registerId.trim(), { closingBalance });
}
