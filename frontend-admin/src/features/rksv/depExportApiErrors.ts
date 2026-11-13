import { registerApiErrorCodeTranslation } from '@/shared/errors/apiErrorCodeRegistry';

let registered = false;

/** Map DEP download/history API `code` values to FA i18n keys. */
export function ensureDepExportApiErrorTranslations(): void {
  if (registered) return;
  registered = true;
  registerApiErrorCodeTranslation(
    'RKSV_DEP_EXPORT_EXPIRED',
    'rksvHub.depExportPage.exportExpired'
  );
  registerApiErrorCodeTranslation(
    'RKSV_DEP_EXPORT_FILE_NOT_FOUND',
    'rksvHub.depExportPage.downloadFailed'
  );
  registerApiErrorCodeTranslation(
    'RKSV_DEP_EXPORT_TOKEN_EXPIRED',
    'rksvHub.depExportPage.exportExpired'
  );
}
