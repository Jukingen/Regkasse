# DEVELOPMENT LOG

## [Date: 2024-07-05] Authentication Improvements and Bug Fixes

### Backend (ASP.NET Core)
- Enhanced JWT token generation to include both user ID and email in claims (`sub`, `nameid`, `name`, `user_id`, `user_email`).
- Updated `AuthController.GetCurrentUser` to attempt user lookup by multiple claims: user ID, custom user_id, email, custom email, and name.
- Added detailed logging for all JWT claims and lookup attempts to aid debugging.
- Improved error messages and logging for user not found scenarios.
- Updated `RefreshToken` logic to match the new claim-based lookup.

### Frontend (React Native Expo)
- Updated `authService` to use correct login and refresh token endpoints and payloads.
- Synced frontend user interface to match backend user object structure.
- Fixed login and refresh token logic in `AuthContext` to match backend expectations.
- Ensured all authentication requests use `email` instead of `username`.
- Improved error handling and state management in authentication flow.

### General
- Verified admin user exists in the database and can be found by both ID and email.
- Added defensive checks for all user lookup scenarios.
- Confirmed that `/auth/me` endpoint now works reliably after login.
- All changes committed with detailed English commit messages for traceability.

--- 