import {
  getUserPreferences,
  updateUserPreferences,
  type UserPreferencesApiResponse,
  type SaveUserPreferencesApiRequest,
} from '@/lib/personalization/userPreferencesApi';

export type { UserPreferencesApiResponse, SaveUserPreferencesApiRequest };
export { getUserPreferences, updateUserPreferences };
