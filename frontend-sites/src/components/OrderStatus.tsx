'use client';

import type { CSSProperties } from 'react';
import type { WebsiteStatus } from '@/lib/publicApi';
import { useWebsiteStatus } from '@/hooks/useWebsiteStatus';

const bannerBase: CSSProperties = {
  display: 'flex',
  flexDirection: 'column',
  gap: '0.25rem',
  padding: '0.9rem 1.05rem',
  borderRadius: 12,
  marginBottom: '1.25rem',
  fontSize: '0.95rem',
  lineHeight: 1.45,
};

type Props = {
  /** When provided by parent (TenantWebsite), avoids a second poll. */
  status?: WebsiteStatus | null;
  loading?: boolean;
  /** Standalone mode: fetch status by slug when `status` is omitted. */
  tenantSlug?: string;
};

/**
 * Website open/order banner. Blocks online-order UX when closed — never POS/FA.
 */
export function OrderStatusWidget({
  status: statusProp,
  loading: loadingProp,
  tenantSlug,
}: Props) {
  // Hooks must run unconditionally; empty slug is a no-op when parent supplies status.
  const fetched = useWebsiteStatus(tenantSlug ?? '');
  const status = statusProp !== undefined ? statusProp : fetched.data;
  const loading = loadingProp !== undefined ? loadingProp : fetched.loading;

  if (loading && !status) {
    return (
      <div
        role="status"
        style={{
          ...bannerBase,
          background: 'rgba(148, 163, 184, 0.12)',
          border: '1px solid #e2e8f0',
          color: '#64748b',
        }}
      >
        <span>Öffnungszeiten werden geladen…</span>
      </div>
    );
  }

  if (!status?.isOpen) {
    const detail =
      status?.message?.trim() ||
      (status?.openTime
        ? `Öffnet um ${status.openTime}`
        : 'Online-Bestellungen sind derzeit nicht möglich.');
    return (
      <div
        className="closed-banner"
        role="status"
        aria-live="polite"
        style={{
          ...bannerBase,
          background: 'rgba(148, 163, 184, 0.15)',
          border: '1px solid #cbd5e1',
          color: '#334155',
        }}
      >
        <strong>
          {status?.isSpecial ? '⛔ Sondertag — geschlossen' : '⛔ Heute geschlossen'}
        </strong>
        <span>{detail}</span>
        {status?.openTime && status?.closeTime ? (
          <span style={{ fontSize: '0.8rem', color: '#64748b' }}>
            Reguläre Zeiten: {status.openTime} – {status.closeTime}
          </span>
        ) : null}
        <span style={{ fontSize: '0.8rem', color: '#64748b', marginTop: 4 }}>
          Die Speisekarte können Sie weiterhin einsehen.
          {status?.isSpecial
            ? ' Feiertag/Sondertag ist aktiv.'
            : ' Feiertage/Sondertage sind berücksichtigt.'}
        </span>
      </div>
    );
  }

  if (!status.canOrder) {
    return (
      <div
        className="warning-banner"
        role="status"
        aria-live="polite"
        style={{
          ...bannerBase,
          background: 'rgba(245, 158, 11, 0.12)',
          border: '1px solid rgba(245, 158, 11, 0.4)',
          color: '#92400e',
        }}
      >
        <strong>
          {status.isSpecial ? '⚠ Sondertag — Bestellung nicht möglich' : '⚠ Bestellung nicht möglich'}
        </strong>
        <span>
          {status.closeTime
            ? `Letzte Online-Bestellung vor ${status.closeTime}`
            : status.message?.trim() || 'Online-Bestellungen vor Schließung gestoppt'}
        </span>
      </div>
    );
  }

  return (
    <div
      className="open-banner"
      role="status"
      style={{
        ...bannerBase,
        background: 'rgba(34, 197, 94, 0.12)',
        border: '1px solid rgba(34, 197, 94, 0.35)',
        color: 'var(--primary, #0f172a)',
      }}
    >
      <strong>{status.isSpecial ? 'Sondertag — geöffnet' : 'Geöffnet'}</strong>
      <span>
        {status.closeTime
          ? `Online-Bestellung möglich — bis ${status.closeTime}`
          : status.message?.trim() || 'Online-Bestellung möglich'}
      </span>
    </div>
  );
}

export { OrderStatusWidget as OrderStatus };
