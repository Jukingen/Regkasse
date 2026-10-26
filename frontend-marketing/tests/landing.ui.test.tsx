// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { LandingPage } from '../src/components/LandingPage';
import { MARKETING_LANG_KEY } from '../src/i18n/catalogs';

async function mount() {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  await act(async () => {
    root.render(<LandingPage />);
  });
  return { container, root };
}

describe('marketing language and pricing', () => {
  let root: Root | null = null;

  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.lang = 'de';
  });

  afterEach(async () => {
    if (root) {
      await act(async () => {
        root?.unmount();
      });
      root = null;
    }
    document.body.innerHTML = '';
  });

  it('toggles de, en, and tr and updates the hero headline', async () => {
    const view = await mount();
    root = view.root;
    const headline = () => view.container.querySelector('[data-testid="hero-headline"]')?.textContent;

    expect(headline()).toBe('Die Kasse für jede Branche');

    await act(async () => {
      view.container.querySelector<HTMLButtonElement>('[data-lang="en"]')?.click();
    });
    expect(headline()).toBe('The cash register for every trade');
    expect(window.localStorage.getItem(MARKETING_LANG_KEY)).toBe('en');
    expect(document.documentElement.lang).toBe('en');

    await act(async () => {
      view.container.querySelector<HTMLButtonElement>('[data-lang="tr"]')?.click();
    });
    expect(headline()).toBe('Her sektör için kasa');
    expect(window.localStorage.getItem(MARKETING_LANG_KEY)).toBe('tr');

    await act(async () => {
      view.container.querySelector<HTMLButtonElement>('[data-lang="de"]')?.click();
    });
    expect(headline()).toBe('Die Kasse für jede Branche');
  });

  it('renders euro prices and the yearly two-month discount', async () => {
    const view = await mount();
    root = view.root;
    const price = (id: string) => view.container.querySelector(`[data-testid="price-${id}"]`)?.textContent;

    expect(price('starter')).toBe('€29/Monat');
    expect(price('professional')).toBe('€79/Monat');
    expect(price('enterprise')).toBe('Individuell');
    expect(view.container.querySelector('[data-testid="vat-note"]')?.textContent).toBe('Alle Preise zzgl. 20% USt.');

    await act(async () => {
      view.container.querySelector<HTMLInputElement>('[data-testid="billing-annual"]')?.click();
    });
    expect(price('starter')).toBe('€290/Jahr');
    expect(price('professional')).toBe('€790/Jahr');
    expect(price('enterprise')).toBe('Individuell');
  });

  it('shows 10 FAQ items in each language', async () => {
    const view = await mount();
    root = view.root;
    const count = () => view.container.querySelectorAll('[data-testid="faq"] details').length;

    expect(count()).toBe(10);
    await act(async () => {
      view.container.querySelector<HTMLButtonElement>('[data-lang="en"]')?.click();
    });
    expect(count()).toBe(10);
    expect(view.container.querySelector('[data-testid="faq"] summary')?.textContent).toBe('RKSV-compliant?');
    await act(async () => {
      view.container.querySelector<HTMLButtonElement>('[data-lang="tr"]')?.click();
    });
    expect(count()).toBe(10);
    expect(view.container.querySelector('[data-testid="faq"] summary')?.textContent).toBe('RKSV uyumlu mu?');
  });
});
