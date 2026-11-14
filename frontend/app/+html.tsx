import { ScrollViewStyleReset } from 'expo-router/html';
import type { ReactNode } from 'react';

import { POS_WEB_DOCUMENT } from '../utils/posWebDocument';

export default function Root({ children }: { children: ReactNode }) {
  return (
    <html lang={POS_WEB_DOCUMENT.lang}>
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />
        <title>{POS_WEB_DOCUMENT.title}</title>
        <meta name="description" content={POS_WEB_DOCUMENT.description} />
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{
            __html: JSON.stringify({
              '@context': 'https://schema.org',
              '@type': 'WebApplication',
              name: POS_WEB_DOCUMENT.title,
              applicationCategory: 'BusinessApplication',
              operatingSystem: 'Web',
              inLanguage: POS_WEB_DOCUMENT.lang,
              description: POS_WEB_DOCUMENT.description,
            }),
          }}
        />
        <ScrollViewStyleReset />
      </head>
      <body>{children}</body>
    </html>
  );
}
