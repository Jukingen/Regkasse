import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { isBlockedPath, isExamplePath, scanFile, scanText } from './scan-secrets.mjs';

describe('isExamplePath', () => {
  it('allows tracked templates', () => {
    assert.equal(isExamplePath('backend/appsettings.Development.example.json'), true);
    assert.equal(isExamplePath('.env.example'), true);
    assert.equal(isExamplePath('.env.production.example'), true);
  });
});

describe('isBlockedPath', () => {
  it('blocks real appsettings and env files', () => {
    assert.equal(isBlockedPath('backend/appsettings.json'), true);
    assert.equal(isBlockedPath('backend/appsettings.Production.json'), true);
    assert.equal(isBlockedPath('frontend-admin/.env.local'), true);
    assert.equal(isBlockedPath('credentials.json'), true);
    assert.equal(isBlockedPath('backend/App_Data/license-dev/signing-private.pem'), true);
    assert.equal(isBlockedPath('certs/api.pfx'), true);
  });

  it('allows example templates', () => {
    assert.equal(isBlockedPath('backend/appsettings.example.json'), false);
    assert.equal(isBlockedPath('backend/appsettings.Development.example.json'), false);
    assert.equal(isBlockedPath('.env.example'), false);
    assert.equal(isBlockedPath('.env.production.example'), false);
  });
});

describe('scanText', () => {
  it('flags PEM private keys and connection-string passwords', () => {
    const pem = scanText('-----BEGIN PRIVATE KEY-----\nMIIB\n-----END PRIVATE KEY-----');
    assert.ok(pem.some((f) => f.id === 'private-key-pem'));

    const conn = scanText('Host=localhost;Password=SuperSecretDb1;Database=kasse_db');
    assert.ok(conn.some((f) => f.id === 'connection-string-password'));
  });

  it('flags Fiskaly keys and Stripe live keys', () => {
    const fiskaly = scanText(
      '{"ApiKey":"test_abc123xyz789_testapi","ApiSecret":"Abcdefghijklmnopqrstuvwxyz012345"}',
    );
    assert.ok(fiskaly.some((f) => f.id === 'fiskaly-api-key'));
    assert.ok(fiskaly.some((f) => f.id === 'fiskaly-api-secret'));

    const stripe = scanText(['sk', 'live', '51NotARealKeyButLooksLiveXX'].join('_'));
    assert.ok(stripe.some((f) => f.id === 'stripe-live-secret'));
  });

  it('does not treat POSTGRES_PASSWORD or C# Password properties as connection strings', () => {
    assert.deepEqual(scanText('POSTGRES_PASSWORD=not-a-connection-string'), []);
    assert.deepEqual(scanText('public string Password = request.Password;'), []);
  });

  it('allows placeholders, empty values, and documented seed passwords', () => {
    const ok = scanText(`{
      "ApiKey": "YOUR_FISKALY_API_KEY_HERE",
      "ApiSecret": "YOUR_FISKALY_API_SECRET_HERE",
      "SecretKey": "CHANGE_ME_DOCKER_DEV_JWT_SECRET_KEY_32CHARS_MIN",
      "Password": "postgres"
    }
    password: Admin123!
    DemoTenant1!
    Password=YOUR_PASSWORD
    Host=localhost;Password=postgres;Database=kasse`);
    assert.deepEqual(ok, []);
  });

  it('allows documented connection-string placeholders in quoted docs', () => {
    assert.deepEqual(
      scanText('dotnet user-secrets set "x" "Host=localhost;Username=postgres;Password=YOUR_PASSWORD"'),
      [],
    );
    assert.deepEqual(scanText("example: 'Host=localhost;Password=***',"), []);
    assert.deepEqual(scanText('Host=127.0.0.1;Username=openapi;Password=openapi"'), []);
  });

  it('does not span connection-string Password= across newlines', () => {
    const text = 'Host=localhost;Database=kasse_db;Username=postgres;Password=***\nJwtSettings__SecretKey=***';
    assert.deepEqual(scanText(text), []);
  });

  it('does not treat i18n-style labels as secrets when scanning skipped locale files', () => {
    const findings = scanFile(
      'frontend-admin/src/i18n/locales/de/settings.json',
      '{"currentPassword":"Aktuelles Passwort"}',
    );
    assert.deepEqual(findings, []);
  });
});
