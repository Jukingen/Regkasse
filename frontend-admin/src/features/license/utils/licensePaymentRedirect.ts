/**
 * Optional external checkout / purchase URL for mandant license renewal.
 * When unset, FA falls back to Super Admin billing hub or support mailto.
 */
export function getConfiguredLicensePaymentUrl(): string | null {
  const raw = process.env.NEXT_PUBLIC_LICENSE_PAYMENT_URL?.trim();
  return raw && raw.length > 0 ? raw : null;
}

export type LicensePaymentRedirectTarget = {
  href: string;
  kind: 'external' | 'internal' | 'mailto';
};

export function resolveLicensePaymentRedirectTarget(options: {
  isSuperAdmin: boolean;
}): LicensePaymentRedirectTarget {
  const configured = getConfiguredLicensePaymentUrl();
  if (configured) {
    if (configured.startsWith('mailto:')) {
      return { href: configured, kind: 'mailto' };
    }
    if (/^https?:\/\//i.test(configured)) {
      return { href: configured, kind: 'external' };
    }
    return { href: configured.startsWith('/') ? configured : `/${configured}`, kind: 'internal' };
  }

  if (options.isSuperAdmin) {
    return { href: '/admin/billing', kind: 'internal' };
  }

  const subject = encodeURIComponent('Lizenzverlängerung');
  const body = encodeURIComponent(
    'Bitte um Ausstellung / Verlängerung einer Regkasse-Mandantenlizenz (Billing-Schlüssel).'
  );
  return {
    href: `mailto:support@regkasse.at?subject=${subject}&body=${body}`,
    kind: 'mailto',
  };
}

/** Navigates to payment / purchase (external checkout, FA billing, or support mail). */
export function redirectToLicensePayment(options: {
  isSuperAdmin: boolean;
  pushInternal: (href: string) => void;
}): void {
  const target = resolveLicensePaymentRedirectTarget({
    isSuperAdmin: options.isSuperAdmin,
  });
  if (target.kind === 'internal') {
    options.pushInternal(target.href);
    return;
  }
  if (typeof window !== 'undefined') {
    window.location.assign(target.href);
  }
}
