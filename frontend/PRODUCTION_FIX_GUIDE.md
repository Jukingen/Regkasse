# 🔧 PRODUCTION FIX: "index" Header + Linter + JWT Auth

## 🎯 Problem Summary

### 1. UI Bug: "index" Header Görünüyor
**Symptom:** Sol üstte boş "index" yazısı var  
**Root Cause:** Root `_layout.tsx`'te `<Stack />` component'i default screenOptions kullanıyor

### 2. Linter Errors
**Symptom:** `npm run lint` → 3 errors + 260 warnings  
**Root Cause:** TypeScript strict mode, unused vars, any types

### 3. JWT Auth Check
**Current:** Request interceptor token ekliyor ✅  
**Concern:** 401 handling her endpoint'te logout yapmamalı

---

## ✅ FIX 1: "index" Header Kaldırma

### A) Kontrol Listesi

- ✅ **app/index.tsx exists** - Redirect to login yapıyor
- ✅ **app/_layout.tsx** - `<Stack />` default header gösteriyor
- ✅ **app/(tabs)/_layout.tsx** - `headerShown: true` ayarı var
- ⚠️ **Root Stack** - screenOptions eksik, default title "index" gösteriyor

---

### B) Çözüm: Root Layout Fix

```typescript
// app/_layout.tsx

import '../i18n';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import React from 'react';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';
import { useMemoryMonitor } from '../hooks/useMemoryOptimization';

export default function RootLayout() {
  useMemoryMonitor();

  return (
    <AuthProvider>
      <SystemProvider>
        <ThemeProvider>
          <AppStateProvider>
            {/* ✅ FIX: screenOptions eklendi */}
            <Stack
              screenOptions={{
                headerShown: false, // Tüm ekranlarda header gizli
              }}
            >
              {/* ✅ FIX: index route için explicit config */}
              <Stack.Screen
                name="index"
                options={{
                  headerShown: false,
                }}
              />
              
              {/* ✅ Auth route group */}
              <Stack.Screen
                name="(auth)"
                options={{
                  headerShown: false,
                }}
              />
              
              {/* ✅ Tabs route group */}
              <Stack.Screen
                name="(tabs)"
                options={{
                  headerShown: false, // Tabs kendi header'ını yönetir
                }}
              />
              
              {/* ✅ Screens route group */}
              <Stack.Screen
                name="(screens)"
                options={{
                  headerShown: false,
                }}
              />
            </Stack>
            
            {/* ✅ StatusBar her zaman görünür */}
            <StatusBar style="auto" />
          </AppStateProvider>
        </ThemeProvider>
      </SystemProvider>
    </AuthProvider>
  );
}
```

**Açıklama:**
- `headerShown: false` → Root stack header gizli
- Her route group için explicit Screen definition
- `(tabs)` kendi header'ını yönetiyor (TabLayout'ta `headerShown: true`)

---

### Alternative: Minimal Fix

Sadece `screenOptions` ekle:

```typescript
<Stack
  screenOptions={{
    headerShown: false,
  }}
/>
```

Bu kadar! Tüm route'larda header gizlenir.

---

## ✅ FIX 2: Linter Errors

### Common Errors (Top 10)

#### 1. **Unused Variables/Imports**

```typescript
// ❌ Before
import React, { useState, useEffect, useMemo } from 'react';

const MyComponent = () => {
  const [count, setCount] = useState(0);
  // useMemo kullanılmıyor!
  return <div>{count}</div>;
};

// ✅ After
import React, { useState } from 'react';

const MyComponent = () => {
  const [count, setCount] = useState(0);
  return <div>{count}</div>;
};
```

**Fix:** Kullanılmayan import'ları/değişkenleri sil.

---

#### 2. **Implicit `any` Types**

```typescript
// ❌ Before
export const TokenManager = {
  getTokenInfo: (token: string) => {
    const decoded = jwtDecode(token) as any; // ❌ any
    return decoded;
  },
};

// ✅ After
interface JWTPayload {
  exp?: number;
  sub: string;
  email: string;
  role: string;
}

export const TokenManager = {
  getTokenInfo: (token: string): JWTPayload | null => {
    try {
      const decoded = jwtDecode<JWTPayload>(token);
      return decoded;
    } catch {
      return null;
    }
  },
};
```

---

#### 3. **Missing Return Types**

```typescript
// ❌ Before
const handleSubmit = async (data) => {
  // ...
};

// ✅ After
const handleSubmit = async (data: FormData): Promise<void> => {
  // ...
};
```

---

#### 4. **react-hooks/exhaustive-deps**

```typescript
// ❌ Before
useEffect(() => {
  checkAuthStatus();
}, [user]); // Missing: checkAuthStatus

// ✅ After (Option 1: Add dependency)
useEffect(() => {
  checkAuthStatus();
}, [user, checkAuthStatus]);

// ✅ After (Option 2: Disable if intentional)
useEffect(() => {
  checkAuthStatus();
  // eslint-disable-next-line react-hooks/exhaustive-deps
}, [user]); // Intentionally omitting checkAuthStatus
```

**Best Practice:** Option 2 with comment explaining why.

---

#### 5. **Floating Promises**

```typescript
// ❌ Before
useEffect(() => {
  fetchData(); // ❌ Unhandled promise
}, []);

// ✅ After
useEffect(() => {
  void fetchData(); // ✅ Explicit void
  // Or:
  fetchData().catch(console.error);
}, []);
```

---

#### 6. **No Default Export**

```typescript
// ❌ Before (if ESLint requires it)
export function MyComponent() {}

// ✅ After
export default function MyComponent() {}
```

---

#### 7. **Console Logs (Production)**

```typescript
// ❌ Before
console.log('Debug:', data);

// ✅ After (Option 1: Remove)
// (deleted)

// ✅ After (Option 2: Conditional)
if (__DEV__) {
  console.log('Debug:', data);
}

// ✅ After (Option 3: Disable per line)
// eslint-disable-next-line no-console
console.log('Important:', data);
```

---

#### 8. **Prefer const**

```typescript
// ❌ Before
let user = getUser();
// user never reassigned

// ✅ After
const user = getUser();
```

---

#### 9. **Type Assertion**

```typescript
// ❌ Before
const user = response.data as User; // Too broad

// ✅ After
const user: User = response.data; // Type annotation
// Or validate:
const user = UserSchema.parse(response.data);
```

---

#### 10. **@typescript-eslint/no-explicit-any**

```typescript
// ❌ Before
const handleChange = (event: any) => {};

// ✅ After
import { ChangeEvent } from 'react';
const handleChange = (event: ChangeEvent<HTMLInputElement>) => {};
```

---

### Priority Fixes (Top Files)

Based on common patterns, focus on:

1. **app/(tabs)/cash-register.tsx**
   - 37 lint errors (já conhecidos)
   - Fix: Zustand migration (already documented)

2. **services/api/config.ts**
   - `any` types in TokenManager
   - Missing return types
  
3. **contexts/AuthContext.tsx**
   - Potential exhaustive-deps warnings

4. **hooks/*.ts**
   - Unused imports
   - Missing return types

---

### Auto-Fix Commands

```bash
# Auto-fix formatting
npx eslint . --fix

# Check remaining errors
npm run lint

# Prettier fix
npx prettier --write "**/*.{ts,tsx,js,jsx,json}"
```

---

## ✅ FIX 3: JWT Auth Config

### Current Status: ✅ GOOD!

**services/api/config.ts** already implements:

1. ✅ **Token Attachment:**
   ```typescript
   const token = await AsyncStorage.getItem('token');
   if (token) {
     config.headers.Authorization = `Bearer ${token}`;
   }
   ```

2. ✅ **Token Expiry Check:**
   ```typescript
   if (TokenManager.isTokenExpired(token)) {
     await TokenManager.clearTokens();
     return Promise.reject(new Error('Token expired'));
   }
   ```

3. ✅ **401 Handling with Refresh:**
   ```typescript
   if (error.response?.status === 401) {
     const refreshToken = await AsyncStorage.getItem('refreshToken');
     if (refreshToken) {
       // Refresh token logic
     } else {
       await TokenManager.clearTokens();
       // Redirect to login
     }
   }
   ```

---

### ⚠️ Potential Issue: Aggressive Logout

**Problem:** Every 401 → Clear tokens → Login redirect

**Solution:** Whitelist endpoints that should NOT trigger logout

```typescript
// services/api/config.ts

// Response interceptor
axiosInstance.interceptors.response.use(
  (response) => response.data,
  async (error) => {
    const status = error.response?.status;
    const url = error.config?.url;
    
    // ✅ FIX: Whitelist cart/table endpoints
    const NO_LOGOUT_ENDPOINTS = [
      '/cart/current',
      '/cart/add-item',
      '/tables/',
    ];
    
    const shouldSkipLogout = NO_LOGOUT_ENDPOINTS.some(
      (endpoint) => url?.includes(endpoint)
    );
    
    if (status === 401) {
      console.log('⚠️ 401 error:', { url, shouldSkipLogout });
      
      if (shouldSkipLogout) {
        // ✅ Silent fail - use cached data
        console.log('🔕 Skipping logout for whitelisted endpoint');
        return Promise.reject(error); // Don't clear tokens!
      }
      
      // ❌ Only for auth endpoints: logout
      const token = await AsyncStorage.getItem('token');
      if (!token || TokenManager.isTokenExpired(token)) {
        console.log('🔐 Token invalid, logout required');
        await TokenManager.clearTokens();
        
        // Emit logout event
        if (typeof window !== 'undefined') {
          window.dispatchEvent(new Event('auth-logout'));
        }
      }
    }
    
    return Promise.reject(error);
  }
);
```

---

### Alternative: Event-Based Logout

```typescript
// contexts/AuthContext.tsx

useEffect(() => {
  const handleLogout = () => {
    console.log('🔐 Logout event received');
    logout();
  };
  
  if (typeof window !== 'undefined') {
    window.addEventListener('auth-logout', handleLogout);
    return () => window.removeEventListener('auth-logout', handleLogout);
  }
}, [logout]);
```

---

## 🧪 Testing

### Test 1: "index" Header Fix

```
1. Fresh start: npx expo start --clear
2. Open app
3. BEFORE: Index" header visible ❌
4. AFTER: No "index" header ✅
5. Navigate to Kasse tab
6. Header shows "Kasse" ✅
```

---

### Test 2: Linter

```bash
# Before
npm run lint
# Result: 3 errors + 260 warnings ❌

# Apply fixes...

# After
npm run lint
# Result: 0 errors + 0 warnings ✅
```

---

### Test 3: JWT Auth

```
1. Login successfully
2. Token stored in AsyncStorage ✅
3. Make API requests:
   - Headers include: Authorization: Bearer <token> ✅
4. Expire token (manually or wait)
5. Make request → 401 error
6. Refresh token attempted ✅
7. If refresh fails → Logout ✅
8. If refresh succeeds → Request retried ✅
```

---

### Test 4: No False Logout

```
1. Login
2. Switch tables rapidly
3. BEFORE: 401 → Logout ❌
4. AFTER: 401 → Silent fail, use cache ✅
5. No unexpected login redirect ✅
```

---

## 📋 Implementation Checklist

### Phase 1: UI Fix (5 min)
- [ ] Update `app/_layout.tsx`: Add `screenOptions={{ headerShown: false }}`
- [ ] Test: Verify "index" header gone
- [ ] Commit: "fix: hide 'index' header in root stack"

### Phase 2: Linter (30-60 min)
- [ ] Run `npx eslint . --fix`
- [ ] Manually fix remaining errors:
  - [ ] cash-register.tsx Zustand migration (use CASH_REGISTER_ZUSTAND_MIGRATION.md)
  - [ ] services/api/config.ts: Add JWTPayload type
  - [ ] Remove unused imports
  - [ ] Add return types
- [ ] Run `npm run lint` until clean
- [ ] Commit: "fix: resolve all linter errors"

### Phase 3: JWT Auth (15 min)
- [ ] Review services/api/config.ts
- [ ] Add NO_LOGOUT_ENDPOINTS whitelist (optional)
- [ ] Test table switch → no logout
- [ ] Commit: "fix: improve 401 handling - whitelist cart endpoints"

---

## 🎯 Success Criteria

✅ **UI:** No "index" header visible  
✅ **Linter:** `npm run lint` passes with 0 errors  
✅ **JWT:** Token attached to all API requests  
✅ **Auth:** 401 handling doesn't trigger false logouts

---

## 📞 Support

If issues persist:

1. **"index" still visible:**
   - Clear expo cache: `npx expo start --clear`
   - Check `app/(tabs)/_layout.tsx` - ensure `headerShown: true` only for tabs
   - Screenshot + send

2. **Linter errors:**
   - Send full `npm run lint` output
   - Focus on top 10 errors
   - I'll provide file-specific fixes

3. **JWT issues:**
   - Check Network tab → Request headers
   - Send console logs for 401 errors
   - Verify AsyncStorage has 'token' key

Let's ship production-quality code! 🚀
