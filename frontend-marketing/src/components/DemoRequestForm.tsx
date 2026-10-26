'use client';

import { useState } from 'react';
import { INDUSTRIES } from '@/content/landing';

type Notice = { tone: 'ok' | 'error'; text: string };

function apiBase(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();
  const base = configured && configured.length > 0 ? configured : 'http://localhost:5184';
  return base.replace(/\/+$/, '');
}

export function DemoRequestForm() {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [company, setCompany] = useState('');
  const [industry, setIndustry] = useState<string>(INDUSTRIES[0].id);
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setNotice(null);
    try {
      const response = await fetch(`${apiBase()}/api/public/demo-requests`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ name, email, company, industry, message }),
      });
      if (response.status === 201) {
        setNotice({ tone: 'ok', text: 'Vielen Dank. Wir haben Ihre Demo-Anfrage erhalten.' });
        setName('');
        setEmail('');
        setCompany('');
        setMessage('');
        return;
      }
      if (response.status === 429) {
        setNotice({
          tone: 'error',
          text: 'Zu viele Anfragen. Bitte versuchen Sie es in einer Stunde erneut.',
        });
        return;
      }
      if (response.status === 400) {
        setNotice({ tone: 'error', text: 'Bitte prüfen Sie Ihre Angaben.' });
        return;
      }
      setNotice({
        tone: 'error',
        text: 'Die Anfrage konnte nicht gesendet werden. Bitte versuchen Sie es später erneut.',
      });
    } catch {
      setNotice({
        tone: 'error',
        text: 'Die Anfrage konnte nicht gesendet werden. Bitte versuchen Sie es später erneut.',
      });
    } finally {
      setPending(false);
    }
  }

  return (
    <form className="form" id="demo" onSubmit={onSubmit}>
      <label>
        Name
        <input name="name" autoComplete="name" required value={name} onChange={(event) => setName(event.target.value)} />
      </label>
      <label>
        E-Mail
        <input
          name="email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </label>
      <label>
        Firma
        <input name="company" autoComplete="organization" value={company} onChange={(event) => setCompany(event.target.value)} />
      </label>
      <label>
        Branche
        <select name="industry" required value={industry} onChange={(event) => setIndustry(event.target.value)}>
          {INDUSTRIES.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Nachricht
        <textarea name="message" rows={4} value={message} onChange={(event) => setMessage(event.target.value)} />
      </label>
      <button className="button" type="submit" disabled={pending}>
        {pending ? 'Wird gesendet…' : 'Demo anfordern'}
      </button>
      {notice ? (
        <p className={notice.tone === 'ok' ? 'toast' : 'toast error'} role="status">
          {notice.text}
        </p>
      ) : null}
    </form>
  );
}
