"""Shared auth test credentials from backend UserSeedData (admin@admin.com / Admin123!)."""

BASE_URL = "http://localhost:5184"
TIMEOUT = 30

# Seeded in backend/Data/UserSeedData.cs
LOGIN_IDENTIFIER = "admin@admin.com"
LOGIN_PASSWORD = "Admin123!"
CLIENT_APP = "admin"


def login_payload(login_identifier: str | None = None, password: str | None = None) -> dict:
    return {
        "loginIdentifier": login_identifier or LOGIN_IDENTIFIER,
        "password": password if password is not None else LOGIN_PASSWORD,
        "clientApp": CLIENT_APP,
    }


def extract_access_token(response_json: dict) -> str | None:
    return (
        response_json.get("token")
        or response_json.get("accessToken")
        or response_json.get("access_token")
    )
