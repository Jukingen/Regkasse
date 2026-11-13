type Translator = (key: string, options?: Record<string, string | number>) => string;

/** Maps a catalog flag name to its admin description. Keys are literal so i18n usage scan sees them. */
export function describeFeatureFlag(t: Translator, name: string): string {
  switch (name) {
    case 'EnableNewPaymentFlow':
      return t('featureFlags.descriptions.EnableNewPaymentFlow');
    case 'EnableDepExportV2':
      return t('featureFlags.descriptions.EnableDepExportV2');
    case 'EnableOnlineOrdersV2':
      return t('featureFlags.descriptions.EnableOnlineOrdersV2');
    case 'EnableAutoAusfall':
      return t('featureFlags.descriptions.EnableAutoAusfall');
    case 'Fiscal.RksvAt':
      return t('featureFlags.descriptions.fiscalRksvAt');
    case 'Fiscal.KassenSicherheitDe':
      return t('featureFlags.descriptions.fiscalKassenSicherheitDe');
    case 'Fiscal.MwstCh':
      return t('featureFlags.descriptions.fiscalMwstCh');
    case 'EInvoicing.Zugferd':
      return t('featureFlags.descriptions.eInvoicingZugferd');
    case 'EInvoicing.XRechnung':
      return t('featureFlags.descriptions.eInvoicingXRechnung');
    case 'EInvoicing.QrRechnung':
      return t('featureFlags.descriptions.eInvoicingQrRechnung');
    case 'EInvoicing.En16931':
      return t('featureFlags.descriptions.eInvoicingEn16931');
    case 'Vies.CheckEnabled':
      return t('featureFlags.descriptions.viesCheckEnabled');
    case 'EInvoicing.Peppol':
      return t('featureFlags.descriptions.eInvoicingPeppol');
    default:
      return name;
  }
}

export function isReservedFeatureFlag(source: string | null | undefined, name: string): boolean {
  return name === 'EInvoicing.Peppol' || (source ?? '').startsWith('reserved');
}
