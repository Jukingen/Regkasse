import type { CSSProperties } from 'react';
import type { PublicTenantMenu, PublicTenantMenuItem } from '@/lib/publicApi';

function money(value: number, currency: string): string {
  try {
    return new Intl.NumberFormat('de-AT', { style: 'currency', currency }).format(value);
  } catch {
    return `€ ${value.toFixed(2)}`;
  }
}

const orderBtnBase: CSSProperties = {
  border: 'none',
  borderRadius: 8,
  padding: '0.4rem 0.7rem',
  fontWeight: 700,
  fontSize: '0.8rem',
  whiteSpace: 'nowrap',
};

export function MenuDisplay({
  menu,
  canOrder = false,
  closeTime = null,
  onAddItem,
}: {
  menu: PublicTenantMenu;
  /** When false, order buttons stay visible but disabled. */
  canOrder?: boolean;
  closeTime?: string | null;
  onAddItem?: (item: PublicTenantMenuItem) => void;
}) {
  const byCategory = new Map<string, typeof menu.items>();
  for (const item of menu.items) {
    const key = item.categoryName?.trim() || 'Weitere';
    const list = byCategory.get(key) ?? [];
    list.push(item);
    byCategory.set(key, list);
  }

  if (menu.items.length === 0) {
    return <p style={{ color: '#64748b' }}>Aktuell keine Speisen verfügbar.</p>;
  }

  return (
    <div style={{ display: 'grid', gap: '1.75rem' }}>
      <h2 style={{ margin: 0, fontSize: '1.35rem', color: 'var(--primary, #0f172a)' }}>
        Speisekarte
      </h2>

      {!canOrder ? (
        <p className="warning" style={{ margin: 0, fontSize: '0.875rem', color: '#92400e' }}>
          {closeTime
            ? `⚠ Letzte Online-Bestellung bis ${closeTime}`
            : '⚠ Online-Bestellung ist derzeit gestoppt'}
        </p>
      ) : null}

      {[...byCategory.entries()].map(([category, items]) => (
        <section key={category}>
          <h3
            style={{
              margin: '0 0 0.75rem',
              fontSize: '1.05rem',
              color: 'var(--accent, #38bdf8)',
              letterSpacing: '0.02em',
            }}
          >
            {category}
          </h3>
          <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {items.map((item) => {
              const handleAddToCart = () => {
                if (!canOrder || !onAddItem) return;
                onAddItem(item);
              };

              return (
                <li
                  key={item.id}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: '1rem',
                    padding: '0.65rem 0',
                    borderBottom: '1px solid #e2e8f0',
                  }}
                >
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 600 }}>{item.name}</div>
                    {item.description ? (
                      <div style={{ fontSize: '0.875rem', color: '#64748b', marginTop: 2 }}>
                        {item.description}
                      </div>
                    ) : null}
                  </div>
                  <div
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'flex-end',
                      gap: 4,
                      flexShrink: 0,
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.65rem',
                      }}
                    >
                      <div style={{ fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {money(item.price, menu.currency)}
                      </div>
                      <button
                        type="button"
                        onClick={handleAddToCart}
                        disabled={!canOrder}
                        className={!canOrder ? 'disabled' : ''}
                        aria-disabled={!canOrder}
                        title={
                          canOrder
                            ? 'In den Warenkorb'
                            : closeTime
                              ? `Bestellung gestoppt — letzte Bestellung bis ${closeTime}`
                              : 'Bestellung gestoppt'
                        }
                        style={{
                          ...orderBtnBase,
                          background: canOrder ? 'var(--accent, #38bdf8)' : '#e2e8f0',
                          color: canOrder ? '#0f172a' : '#64748b',
                          cursor: canOrder ? 'pointer' : 'not-allowed',
                          opacity: canOrder ? 1 : 0.85,
                        }}
                      >
                        {canOrder ? 'In den Warenkorb' : '🔒 Bestellung gestoppt'}
                      </button>
                    </div>
                    {!canOrder && closeTime ? (
                      <span
                        className="warning"
                        style={{ fontSize: '0.75rem', color: '#92400e' }}
                      >
                        ⚠ Letzte Bestellung bis {closeTime}
                      </span>
                    ) : null}
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}
