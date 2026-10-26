export default function ComingSoonPage({ title }: { title: string }) {
  return (
    <main className="section">
      <div className="wrap">
        <h1>{title}</h1>
        <p>Diese Seite folgt in Kürze.</p>
        <p>
          <a href="/">Zur Startseite</a>
        </p>
      </div>
    </main>
  );
}
