'use client';

import { useMemo, useState, type CSSProperties } from 'react';
import type { PublicTenantMenu, PublicTenantMenuItem, WebsiteStatus } from '@/lib/publicApi';
import { placeOnlineOrder } from '@/lib/onlineOrderApi';
import { MenuDisplay } from '@/components/MenuDisplay';

type CartLine = {
  productId: string;
  name: string;
  price: number;
  quantity: number;
};

type Props = {
  tenantSlug: string;
  menu: PublicTenantMenu;
  status: WebsiteStatus | null;
  loadingStatus: boolean;
};

/**
 * Website/app cart + checkout. Blocked when working hours say canOrder=false.
 * Never import into POS/FA.
 */
export function OnlineOrderPanel({ tenantSlug, menu, status, loadingStatus }: Props) {
  const canOrder = status?.canOrder === true;
  const [cart, setCart] = useState<CartLine[]>([]);
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const total = useMemo(
    () => cart.reduce((sum, line) => sum + line.price * line.quantity, 0),
    [cart],
  );

  const addItem = (item: PublicTenantMenuItem) => {
    if (!canOrder) return;
    setFeedback(null);
    setError(null);
    setCart((prev) => {
      const existing = prev.find((l) => l.productId === item.id);
      if (existing) {
        return prev.map((l) =>
          l.productId === item.id ? { ...l, quantity: Math.min(99, l.quantity + 1) } : l,
        );
      }
      return [
        ...prev,
        { productId: item.id, name: item.name, price: item.price, quantity: 1 },
      ];
    });
  };

  const clearCart = () => setCart([]);

  const submit = async () => {
    setFeedback(null);
    setError(null);
    if (!canOrder) {
      setError(status?.message?.trim() || 'Online-Bestellungen sind derzeit nicht möglich.');
      return;
    }
    if (cart.length === 0) {
      setError('Bitte wählen Sie mindestens einen Artikel.');
      return;
    }
    if (name.trim().length < 2 || phone.replace(/\D/g, '').length < 6) {
      setError('Name und Telefonnummer sind erforderlich.');
      return;
    }

    setSubmitting(true);
    try {
      const result = await placeOnlineOrder({
        tenant: tenantSlug,
        customerName: name.trim(),
        customerPhone: phone.trim(),
        notes: notes.trim() || undefined,
        orderType: 'takeaway',
        paymentMethod: 'cash',
        source: 'web',
        items: cart.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      });

      if (result.closedByHours) {
        setError(result.message || result.error || 'Restaurant derzeit geschlossen.');
        clearCart();
        return;
      }
      if (!result.succeeded) {
        setError(result.error || result.message || 'Bestellung fehlgeschlagen.');
        return;
      }

      setFeedback(
        `Bestellung ${result.orderNumber ?? ''} eingegangen` +
          (typeof result.total === 'number'
            ? ` · ${new Intl.NumberFormat('de-AT', { style: 'currency', currency: menu.currency }).format(result.total)}`
            : ''),
      );
      clearCart();
      setNotes('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <MenuDisplay
        menu={menu}
        canOrder={canOrder && !loadingStatus}
        closeTime={status?.closeTime}
        onAddItem={addItem}
      />

      {canOrder ? (
        <section
          aria-label="Warenkorb"
          style={{
            border: '1px solid #e2e8f0',
            borderRadius: 12,
            padding: '1rem 1.1rem',
            background: '#f8fafc',
          }}
        >
          <h2 style={{ margin: '0 0 0.75rem', fontSize: '1.1rem' }}>Warenkorb</h2>
          {cart.length === 0 ? (
            <p style={{ margin: 0, color: '#64748b', fontSize: '0.9rem' }}>
              Noch keine Artikel — tippen Sie auf „+ Bestellen“.
            </p>
          ) : (
            <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
              {cart.map((line) => (
                <li
                  key={line.productId}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.35rem 0',
                    borderBottom: '1px solid #e2e8f0',
                    fontSize: '0.9rem',
                  }}
                >
                  <span>
                    {line.quantity}× {line.name}
                  </span>
                  <span style={{ fontWeight: 600 }}>
                    {(line.price * line.quantity).toFixed(2)} €
                  </span>
                </li>
              ))}
            </ul>
          )}

          <div style={{ marginTop: '1rem', display: 'grid', gap: '0.65rem' }}>
            <label style={{ display: 'grid', gap: 4, fontSize: '0.85rem' }}>
              Name
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoComplete="name"
                style={inputStyle}
              />
            </label>
            <label style={{ display: 'grid', gap: 4, fontSize: '0.85rem' }}>
              Telefon
              <input
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                autoComplete="tel"
                style={inputStyle}
              />
            </label>
            <label style={{ display: 'grid', gap: 4, fontSize: '0.85rem' }}>
              Hinweis (optional)
              <input
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                style={inputStyle}
              />
            </label>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginTop: 4,
              }}
            >
              <strong>
                Summe:{' '}
                {new Intl.NumberFormat('de-AT', {
                  style: 'currency',
                  currency: menu.currency,
                }).format(total)}
              </strong>
              <button
                type="button"
                disabled={submitting || cart.length === 0}
                onClick={() => void submit()}
                style={{
                  border: 'none',
                  borderRadius: 10,
                  padding: '0.65rem 1rem',
                  background: 'var(--primary, #0f172a)',
                  color: '#fff',
                  fontWeight: 700,
                  cursor: submitting || cart.length === 0 ? 'not-allowed' : 'pointer',
                  opacity: submitting || cart.length === 0 ? 0.55 : 1,
                }}
              >
                {submitting ? 'Senden…' : 'Bestellung absenden'}
              </button>
            </div>
            {status?.closeTime ? (
              <p style={{ margin: 0, fontSize: '0.8rem', color: '#64748b' }}>
                Online-Bestellung möglich bis {status.closeTime}
                {status.openTime ? ` (heute ab ${status.openTime})` : ''}
              </p>
            ) : null}
          </div>
        </section>
      ) : null}

      {error ? (
        <p role="alert" style={{ margin: 0, color: '#b45309', fontSize: '0.9rem' }}>
          {error}
        </p>
      ) : null}
      {feedback ? (
        <p role="status" style={{ margin: 0, color: '#15803d', fontSize: '0.9rem' }}>
          {feedback}
        </p>
      ) : null}
    </div>
  );
}

const inputStyle: CSSProperties = {
  border: '1px solid #cbd5e1',
  borderRadius: 8,
  padding: '0.5rem 0.65rem',
  fontSize: '1rem',
};
