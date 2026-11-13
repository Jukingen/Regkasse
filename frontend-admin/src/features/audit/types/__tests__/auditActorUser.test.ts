import {
  resolveAuditActorDisplayName,
  resolveAuditActorUser,
} from '@/features/audit/types/auditActorUser';
import type { AuditLogEntryDto } from '@/api/generated/model';

describe('auditActorUser helpers', () => {
  it('prefers nested user.displayName', () => {
    const row: AuditLogEntryDto = {
      userId: 'u1',
      actorDisplayName: 'Legacy',
      user: {
        id: 'u1',
        userName: 'anna',
        email: 'a@b.c',
        displayName: 'Anna Schmidt',
        role: 'Manager',
      },
    };
    expect(resolveAuditActorDisplayName(row)).toBe('Anna Schmidt');
    expect(resolveAuditActorUser(row)?.email).toBe('a@b.c');
  });

  it('falls back to actorDisplayName and userId', () => {
    const row: AuditLogEntryDto = {
      userId: 'deleted',
      actorDisplayName: 'Former Cashier',
      userRole: 'Cashier',
    };
    expect(resolveAuditActorDisplayName(row)).toBe('Former Cashier');
    expect(resolveAuditActorUser(row)).toEqual({
      id: 'deleted',
      userName: undefined,
      email: undefined,
      displayName: 'Former Cashier',
      role: 'Cashier',
    });
  });
});
