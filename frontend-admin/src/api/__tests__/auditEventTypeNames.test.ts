import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

import { ActivityEventType } from '@/api/generated/model/activityEventType';
import { AuditEventType } from '@/api/generated/model/auditEventType';

describe('AuditEventType generated names', () => {
  it('uses C# member names for country and QR-Rechnung audit values', () => {
    expect(AuditEventType.TenantCountryChanged).toBe(97);
    expect(AuditEventType.QrRechnungPayloadBuilt).toBe(111);
    expect(AuditEventType.TenantCreatedWithCountry).toBe(96);
  });

  it('AuditEventType_Generated_Ts_File_Exports_PascalCase', () => {
    expect(AuditEventType.TenantCountryChanged).toBe(97);
    expect(AuditEventType.TenantCreatedWithCountry).toBe(96);
    expect(AuditEventType.EinvoiceValidated).toBe(108);
    expect(AuditEventType.EinvoiceSubmissionFailed).toBe(110);
    expect(AuditEventType.QrRechnungPayloadBuilt).toBe(111);
    expect(AuditEventType.PeppolParticipantRegistered).toBe(113);
    expect(Object.keys(AuditEventType).some((key) => key.startsWith('NUMBER_'))).toBe(false);
  });
});

describe('ActivityEventType generated names', () => {
  it('ActivityEventType_Generated_Ts_File_Exists', () => {
    const file = path.resolve('src/api/generated/model/activityEventType.ts');
    expect(fs.existsSync(file)).toBe(true);
    const source = fs.readFileSync(file, 'utf8');
    expect(source).toMatch(/export const ActivityEventType = \{/);
    expect(source).not.toMatch(/NUMBER_\d+/);
    expect(ActivityEventType.UserCreated).toBe('UserCreated');
    expect(ActivityEventType.TenantCountryChanged).toMatch(/^[A-Z]/);
    expect(ActivityEventType.PeppolParticipantRegistered).toBe('PeppolParticipantRegistered');
  });
});
