import { DemoRequestForm } from '@/components/DemoRequestForm';
import { FAQ, FEATURES, HERO, INDUSTRIES, PRICING } from '@/content/landing';

export default function HomePage() {
  return (
    <>
      <header className="site-header">
        <div className="wrap">
          <a className="logo" href="#top">
            Regkasse
          </a>
          <nav className="nav" aria-label="Hauptnavigation">
            <a href="#funktionen">Funktionen</a>
            <a href="#branchen">Branchen</a>
            <a href="#preise">Preise</a>
            <a href="#demo">Demo</a>
            <a href="https://admin.regkasse.at/login">Login</a>
          </nav>
        </div>
      </header>
      <main id="top">
        <section className="hero">
          <div className="wrap">
            <h1>{HERO.headline}</h1>
            <p>{HERO.subheadline}</p>
            <div className="cta-row">
              <a className="button" href="#demo">
                {HERO.primaryCta}
              </a>
              <a className="button secondary" href="#funktionen">
                {HERO.secondaryCta}
              </a>
            </div>
          </div>
        </section>
        <section className="section" id="funktionen">
          <div className="wrap">
            <h2>Funktionen</h2>
            <div className="grid features">
              {FEATURES.map((feature) => (
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
            <h2>Branchen</h2>
            <div className="grid industries">
              {INDUSTRIES.map((industry) => (
                <article className="card tile" key={industry.id}>
                  <span className="icon" aria-hidden="true">
                    {industry.icon}
                  </span>
                  <div>
                    <h3>{industry.name}</h3>
                    <p>{industry.description}</p>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>
        <section className="section" id="preise">
          <div className="wrap">
            <h2>Preise</h2>
            <div className="grid pricing">
              {PRICING.map((tier) => (
                <article className="card" key={tier.name}>
                  <h3>{tier.name}</h3>
                  <p className="price">{tier.price}</p>
                  <p>{tier.detail}</p>
                  <div className="cta-row">
                    <a className="button" href="#demo">
                      14 Tage kostenlos testen
                    </a>
                  </div>
                  <p className="note">Alle Preise zzgl. USt.</p>
                </article>
              ))}
            </div>
          </div>
        </section>
        <section className="section">
          <div className="wrap">
            <h2>Demo anfordern</h2>
            <DemoRequestForm />
          </div>
        </section>
        <section className="section">
          <div className="wrap faq">
            <h2>Fragen</h2>
            {FAQ.map((item) => (
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
          <nav aria-label="Rechtliches">
            <a href="/impressum">Impressum</a>
            <a href="/datenschutz">Datenschutz</a>
            <a href="/agb">AGB</a>
          </nav>
        </div>
      </footer>
    </>
  );
}
