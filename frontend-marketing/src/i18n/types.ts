export const LANGS = ['de', 'en', 'tr'] as const;

export type Lang = (typeof LANGS)[number];

export type IndustryCopy = {
  name: string;
  description: string;
};

export type TierCopy = {
  name: string;
  detail: string;
};

export type FaqItem = {
  question: string;
  answer: string;
};

export type Catalog = {
  languageLabel: string;
  nav: {
    label: string;
    features: string;
    industries: string;
    pricing: string;
    demo: string;
    login: string;
    legal: string;
  };
  hero: {
    headline: string;
    subheadline: string;
    primaryCta: string;
    secondaryCta: string;
  };
  video: {
    title: string;
    body: string;
  };
  featuresTitle: string;
  features: { title: string; text: string }[];
  industriesTitle: string;
  industries: Record<string, IndustryCopy>;
  pricingTitle: string;
  pricing: {
    annualToggle: string;
    trialCta: string;
    vatNote: string;
    perMonth: string;
    perYear: string;
    custom: string;
    tiers: {
      starter: TierCopy;
      professional: TierCopy;
      enterprise: TierCopy;
    };
  };
  demoTitle: string;
  form: {
    name: string;
    email: string;
    company: string;
    industry: string;
    message: string;
    submit: string;
    pending: string;
    success: string;
    rateLimit: string;
    invalid: string;
    failed: string;
  };
  faqTitle: string;
  faq: FaqItem[];
};
