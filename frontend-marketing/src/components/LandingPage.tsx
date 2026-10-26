'use client';

import { useState } from 'react';
import { DemoRequestForm } from '@/components/DemoRequestForm';
import { INDUSTRIES } from '@/content/landing';
import { formatEuroPeriod, PUBLISHED_TIERS, tierAmount } from '@/i18n/pricing';
import { LANGS } from '@/i18n/types';
import { MarketingI18nProvider, useTranslations } from '@/i18n/useTranslations';

function priceLabel(monthlyEur: number | null, annual: boolean, perMonth: string, perYear: string, custom: string): string {
  if (monthlyEur == null) return custom;
  const period = annual ? perYear : perMonth;
  return formatEuroPeriod(tierAmount(monthlyEur, annual), period);
}

function LandingView() {
  const { lang, setLang, t } = useTranslations();
  const [annual, setAnnual] = useState(false);

  return (
    <>
      <header className="site-header">
        <div className="wrap">
          <a className="logo" href="#top">
            Regkasse
          </a>
          <nav className="nav" aria-label={t.nav.label}>
            <a href="#funktionen">{t.nav.features}</a>
            <a href="#branchen">{t.nav.industries}</a>
            <a href="#preise">{t.nav.pricing}</a>
            <a href="#demo">{t.nav.demo}</a>
            <a href="https://admin.regkasse.at/login">{t.nav.login}</a>
            <div className="lang-switch" role="group" aria-label={t.languageLabel}>
              {LANGS.map((code) => (
                <button
                  key={code}
                  type="button"
                  data-lang={code}
                  aria-pressed={lang === code}
                  onClick={() => setLang(code)}
                >
                  {code}
                </button>
              ))}
            </div>
          </nav>
        </div>
      </header>
      <main id="top">
        <section className="hero">
          <div className="wrap">
            <h1 data-testid="hero-headline">{t.hero.headline}</h1>
            <p>{t.hero.subheadline}</p>
            <div className="cta-row">
              <a className="button" href="#demo">
                {t.hero.primaryCta}
              </a>
              <a className="button secondary" href="#funktionen">
                {t.hero.secondaryCta}
              </a>
            </div>
          </div>
        </section>
        <section className="section" id="produkt-demo" data-testid="product-demo">
          <div className="wrap">
            <h2>{t.video.title}</h2>
            <div className="video-frame">
              <p>{t.video.body}</p>
            </div>
          </div>
        </section>
        <section className="section" id="funktionen">
          <div className="wrap">
            <h2>{t.featuresTitle}</h2>
            <div className="grid features">
              {t.features.map((feature) => (
                <article className="card" key={feature.title}>
                  <h3>{feature.title}</h3>
                  <p>{feature.text}</p>
                </article>
              ))}
            </div>
          </div>
        </section>
        <section className="section" id="branchen">
          <div className="wrap">
            <h2>{t.industriesTitle}</h2>
            <div className="grid industries">
              {INDUSTRIES.map((industry) => (
                <article className="card tile" key={industry.id}>
                  <span className="icon" aria-hidden="true">
                    {industry.icon}
                  </span>
                  <div>
                    <h3>{t.industries[industry.id]?.name}</h3>
                    <p>{t.industries[industry.id]?.description}</p>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>
        <section className="section" id="preise">
          <div className="wrap">
            <h2>{t.pricingTitle}</h2>
            <label className="billing-toggle">
              <input
                type="checkbox"
                data-testid="billing-annual"
                checked={annual}
                onChange={(event) => setAnnual(event.target.checked)}
              />
              {t.pricing.annualToggle}
            </label>
            <div className="grid pricing">
              {PUBLISHED_TIERS.map((tier) => {
                const copy = t.pricing.tiers[tier.id];
                return (
                  <article className="card" key={tier.id}>
                    <h3>{copy.name}</h3>
                    <p className="price" data-testid={`price-${tier.id}`}>
                      {priceLabel(tier.monthlyEur, annual, t.pricing.perMonth, t.pricing.perYear, t.pricing.custom)}
                    </p>
                    <p>{copy.detail}</p>
                    <div className="cta-row">
                      <a className="button" href="#demo">
                        {t.pricing.trialCta}
                      </a>
                    </div>
                  </article>
                );
              })}
            </div>
            <p className="note" data-testid="vat-note">
              {t.pricing.vatNote}
            </p>
          </div>
        </section>
        <section className="section">
          <div className="wrap">
            <h2>{t.demoTitle}</h2>
            <DemoRequestForm />
          </div>
        </section>
        <section className="section">
          <div className="wrap faq" data-testid="faq">
            <h2>{t.faqTitle}</h2>
            {t.faq.map((item) => (
              <details key={item.question}>
                <summary>{item.question}</summary>
                <p>{item.answer}</p>
              </details>
            ))}
          </div>
        </section>
      </main>
      <footer className="site-footer">
        <div className="wrap">
          <span>Regkasse</span>
          <nav aria-label={t.nav.legal}>
            <a href="/impressum">Impressum</a>
            <a href="/datenschutz">Datenschutz</a>
            <a href="/agb">AGB</a>
          </nav>
        </div>
      </footer>
    </>
  );
}

export function LandingPage() {
  return (
    <MarketingI18nProvider>
      <LandingView />
    </MarketingI18nProvider>
  );
}
