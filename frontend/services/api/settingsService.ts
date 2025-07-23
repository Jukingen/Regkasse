import { API_BASE_URL } from './config';

// Kullanıcı ayarlarını getir
export const getUserSettings = async () => {
  const response = await fetch(`${API_BASE_URL}/api/settings/user`, {
    method: 'GET',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
  });
  if (!response.ok) throw new Error(await response.text());
  return await response.json();
};

// Kullanıcı ayarlarını güncelle
export const updateUserSettings = async (settings: any) => {
  const response = await fetch(`${API_BASE_URL}/api/settings/user`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(settings),
  });
  if (!response.ok) throw new Error(await response.text());
  return await response.json();
};

// Kullanıcı ayarlarını sıfırla
export const resetUserSettings = async () => {
  const response = await fetch(`${API_BASE_URL}/api/settings/user/reset`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
  });
  if (!response.ok) throw new Error(await response.text());
  return await response.json();
};

// Kullanıcı dilini güncelle
export const updateUserLanguage = async (language: string) => {
  const response = await fetch(`${API_BASE_URL}/api/settings/user`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ language }),
  });
  if (!response.ok) throw new Error(await response.text());
  return await response.json();
}; 