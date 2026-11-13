/** Header name expected by backend CriticalActionMiddleware. */
export const CRITICAL_ACTION_APPROVAL_HEADER = 'X-Critical-Action-Approval';

export type CriticalActionType =
  | 'SchlussbelegCreation'
  | 'TenantDeletion'
  | 'TenantArchive'
  | 'LicenseChange'
  | 'CurrencyChange'
  | 'CountryChange'
  | 'DeleteAllProducts'
  | 'DecommissionRegister'
  | 'BackupDisable'
  | 'FiscalExportDelete'
  | 'UserRoleChange'
  | 'MassPermissionUpdate';
