import type { Metadata } from 'next';
import { jsonLdScript, buildMetadata } from '@/seo/metadata';
import './globals.css';

export const metadata: Metadata = buildMetadata();

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="de">
      <body>
        <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: jsonLdScript() }} />
        {children}
      </body>
    </html>
  );
}
