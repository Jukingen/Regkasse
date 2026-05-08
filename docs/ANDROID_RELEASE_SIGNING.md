# Android release signing — Regkasse POS (no Play Console upload key)

This document describes a **standalone / sideload** workflow: create your own release keystore, use it with **EAS Build** via `credentials.json`, or sign an **APK manually** with `jarsigner`. It does **not** cover Google Play App Signing or Play-internal upload certificates.

> **Security:** Never commit `regkasse-release.keystore`, `credentials.json`, or passwords. Store backups of the keystore and passwords offline (password manager + encrypted backup). Losing the keystore blocks shipping updates with the same `applicationId`.

---

## 1. Prerequisites

- **JDK** (includes `keytool` and `jarsigner`), e.g. Temurin 17 or the JDK bundled with Android Studio.
- **Android SDK build-tools** (optional but recommended): `zipalign` and `apksigner` live under `$ANDROID_HOME/build-tools/<version>/`.

Verify:

```bash
keytool -help
jarsigner -help
```

---

## 2. Create the production keystore

Choose a secure directory **outside** git (this repo ignores `secrets/` under `frontend/`).

### 2.1 Commands (single RSA key, long validity)

From the directory where you want the keystore file (example: `frontend/secrets/`):

**PowerShell (Windows):**

```powershell
New-Item -ItemType Directory -Force -Path secrets | Out-Null
cd secrets
keytool -genkey -v -keystore regkasse-release.keystore -alias regkasse -keyalg RSA -keysize 2048 -validity 10000
```

**bash (macOS / Linux):**

```bash
mkdir -p secrets && cd secrets
keytool -genkey -v -keystore regkasse-release.keystore -alias regkasse -keyalg RSA -keysize 2048 -validity 10000
```

### 2.2 Interactive prompts

`keytool` will ask for:

- Keystore password (remember it → use as `keystorePassword` in `credentials.json`).
- Key password (can match keystore password → use as `keyPassword`; if different, store both separately).
- Distinguished name fields (CN, OU, O, L, ST, C). Use your **company** details; these are embedded in the certificate.

### 2.3 Confirm keystore contents (optional)

```bash
keytool -list -v -keystore regkasse-release.keystore -alias regkasse
```

---

## 3. Configure EAS Build — `credentials.json`

EAS reads **`credentials.json`** at the **Expo project root** (here: `frontend/`).

### 3.1 Copy the example and edit paths/passwords

```bash
cd frontend
copy credentials.json.example credentials.json
```

Edit `credentials.json`:

- **`keystorePath`**: path relative to `frontend/` (or absolute). Example keeps the keystore under `./secrets/regkasse-release.keystore`.
- **`keystorePassword`**: keystore password from step 2.
- **`keyAlias`**: must match `-alias` → **`regkasse`**.
- **`keyPassword`**: key password from step 2.

Example shape (do not commit real passwords):

```json
{
  "android": {
    "keystore": {
      "keystorePath": "./secrets/regkasse-release.keystore",
      "keystorePassword": "YOUR_KEYSTORE_PASSWORD",
      "keyAlias": "regkasse",
      "keyPassword": "YOUR_KEY_PASSWORD"
    }
  }
}
```

### 3.2 Ensure `eas.json` uses local credentials

The Regkasse `frontend/eas.json` **production** profile includes:

```json
"android": {
  "buildType": "apk",
  "credentialsSource": "local"
}
```

So EAS uses `credentials.json` instead of Expo-hosted Android credentials.

### 3.3 Build signed APK with EAS

```bash
cd frontend
npx eas-cli@latest login
npx eas-cli@latest build --platform android --profile production --local
```

Or cloud build (still **no** Play Store upload; you download the APK):

```bash
npx eas-cli@latest build --platform android --profile production
```

---

## 4. Manual APK signing with `jarsigner` (without EAS)

Use this when you already have an **unsigned** release APK (e.g. from Gradle `assembleRelease` with signing disabled, or an exported unsigned artifact).

### 4.1 Naming

- Input: `app-release-unsigned.apk`
- Output: `regkasse-release-aligned-signed.apk`

Adjust paths to match your machine.

### 4.2 Align (recommended before JAR signing)

Alignment reduces RAM use at install time. Use build-tools `zipalign`:

```bash
# Replace 36.0.0 with your installed build-tools version
zipalign -v -p 4 app-release-unsigned.apk regkasse-release-aligned.apk
```

If `zipalign` is not on `PATH`:

```bash
# Windows (example)
"%ANDROID_HOME%\build-tools\36.0.0\zipalign.exe" -v -p 4 app-release-unsigned.apk regkasse-release-aligned.apk
```

### 4.3 Sign with `jarsigner`

Use **SHA256withRSA** and **SHA-256** digest (widely accepted on current Android versions).

**PowerShell (single line):**

```powershell
jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA-256 -keystore secrets/regkasse-release.keystore regkasse-release-aligned.apk regkasse
```

**bash:**

```bash
jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA-256 \
  -keystore secrets/regkasse-release.keystore \
  regkasse-release-aligned.apk regkasse
```

- Final argument **`regkasse`** is the **key alias** (must match `keytool -alias`).

You will be prompted for the keystore password unless you use a protected automation setup (avoid plaintext passwords in CI).

### 4.4 Verify signature

```bash
jarsigner -verify -verbose -certs regkasse-release-aligned.apk
```

Rename the final APK as needed (e.g. `Regkasse-1.0.0-release.apk`).

### 4.5 Limitation (v1 vs v2 signing)

`jarsigner` applies **JAR signing (v1)**. Modern builds often use **`apksigner`** from Android build-tools for **APK Signature Scheme v2/v3**, which improves install security and performance. For sideloading, v1-only often still works on many devices; if install fails with “package appears invalid”, prefer signing with:

```bash
apksigner sign --ks secrets/regkasse-release.keystore --ks-key-alias regkasse --out regkasse-signed.apk regkasse-release-aligned.apk
apksigner verify --verbose regkasse-signed.apk
```

---

## 5. Install on device (unknown sources)

1. Copy the signed `.apk` to the device.
2. Enable **Install unknown apps** for your file browser / browser (Android 8+).
3. Open the APK and install.

Package name must match `app.json` → `expo.android.package` (`com.registrierkasse.cashregister`).

---

## 6. Checklist

| Step | Done |
|------|------|
| Keystore created with alias `regkasse`, RSA 2048, validity 10000 days | ☐ |
| Keystore + passwords backed up securely (not in git) | ☐ |
| `frontend/credentials.json` created from example, paths correct | ☐ |
| `credentials.json` + `*.keystore` **not** committed | ☐ |
| EAS build with `--profile production` produces installable APK | ☐ |
| Or: manual `zipalign` + `jarsigner` / `apksigner` verify passes | ☐ |

---

## 7. Related repo files

| File | Purpose |
|------|---------|
| `frontend/credentials.json.example` | Safe template (copy to `credentials.json`) |
| `frontend/eas.json` | `credentialsSource: "local"`, APK `buildType` |
| `frontend/app.json` | `android.package`, version, `runtimeVersion` |
| `frontend/.gitignore` | Ignores `credentials.json`, `*.keystore`, `secrets/` |

Official references:

- [Local credentials — Expo](https://docs.expo.dev/app-signing/local-credentials/)
- [Sign your app — Android](https://developer.android.com/studio/publish/app-signing)
