export default function HomePage() {
  return (
    <main style={{ padding: '3rem 1.5rem', maxWidth: 40 * 16, margin: '0 auto' }}>
      <h1 style={{ fontSize: '1.75rem', marginBottom: '0.75rem' }}>Regkasse Sites</h1>
      <p style={{ color: '#64748b', lineHeight: 1.5 }}>
        Öffnen Sie die Mandanten-Website unter <code>/[slug]</code> — z.&nbsp;B.{' '}
        <code>/demo-cafe</code>.
      </p>
    </main>
  );
}
