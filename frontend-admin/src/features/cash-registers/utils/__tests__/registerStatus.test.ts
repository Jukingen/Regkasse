import { describe, expect, it } from 'vitest';
import {
    REGISTER_STATUS,
    canDecommissionRegister,
    isDecommissionedRegister,
    registerStatusEmoji,
} from '@/features/cash-registers/utils/registerStatus';

describe('registerStatus', () => {
    it('allows decommission only when closed', () => {
        expect(canDecommissionRegister(REGISTER_STATUS.closed)).toBe(true);
        expect(canDecommissionRegister(REGISTER_STATUS.open)).toBe(false);
        expect(canDecommissionRegister(REGISTER_STATUS.decommissioned)).toBe(false);
    });

    it('detects decommissioned status', () => {
        expect(isDecommissionedRegister(REGISTER_STATUS.decommissioned)).toBe(true);
        expect(isDecommissionedRegister(REGISTER_STATUS.open)).toBe(false);
    });

    it('maps emoji for primary statuses', () => {
        expect(registerStatusEmoji(REGISTER_STATUS.open)).toBe('🟢');
        expect(registerStatusEmoji(REGISTER_STATUS.closed)).toBe('🔴');
        expect(registerStatusEmoji(REGISTER_STATUS.decommissioned)).toBe('⚫');
    });
});
