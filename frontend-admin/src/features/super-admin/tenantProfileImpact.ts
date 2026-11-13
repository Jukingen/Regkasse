import { customInstance } from '@/lib/axios';

export interface TenantProfileImpactWarning {
  code: string;
  count: number;
}

export interface TenantProfileImpact {
  currentProfileId: string;
  targetProfileId: string;
  counts: {
    customersWithPetData: number;
    paymentsWithPrescriptionReference: number;
    paymentsWithRouteFrom: number;
    soldImeis: number;
    appointments: number;
    rooms: number;
    folios: number;
    tickets: number;
  };
  warnings: TenantProfileImpactWarning[];
}

export function getTenantProfileImpact(tenantId: string, profileId: string) {
  return customInstance<TenantProfileImpact>({
    url: `/api/admin/tenants/${tenantId}/profile-impact`,
    method: 'GET',
    params: { profileId },
  });
}

const IMPACT_LABEL_KEYS: Record<string, string> = {
  customersWithPetData: 'tenants.verticalProfile.impactCustomersWithPetData',
  paymentsWithPrescriptionReference: 'tenants.verticalProfile.impactPaymentsWithPrescriptionReference',
  paymentsWithRouteFrom: 'tenants.verticalProfile.impactPaymentsWithRouteFrom',
  soldImeis: 'tenants.verticalProfile.impactSoldImeis',
  appointments: 'tenants.verticalProfile.impactAppointments',
  rooms: 'tenants.verticalProfile.impactRooms',
  folios: 'tenants.verticalProfile.impactFolios',
  tickets: 'tenants.verticalProfile.impactTickets',
};

export function impactWarningLabelKey(code: string): string {
  return IMPACT_LABEL_KEYS[code] ?? code;
}
