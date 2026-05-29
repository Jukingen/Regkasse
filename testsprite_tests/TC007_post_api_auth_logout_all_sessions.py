import requests

BASE_URL = "http://localhost:5184"
LOGIN_ENDPOINT = "/api/Auth/login"
LOGOUT_ALL_ENDPOINT = "/api/Auth/logout-all"
AUTH_ME_ENDPOINT = "/api/Auth/me"

ADMIN_CREDENTIALS = {
    "emailOrUsername": "admin@admin.com",
    "password": "Admin123!"
}

TIMEOUT = 30

def test_post_api_auth_logout_all_sessions():
    # Step 1: Login to get bearer token (Tenant A)
    login_resp = requests.post(
        BASE_URL + LOGIN_ENDPOINT,
        json=ADMIN_CREDENTIALS,
        timeout=TIMEOUT
    )
    assert login_resp.status_code == 200, f"Login failed with status {login_resp.status_code}"
    login_data = login_resp.json()
    assert "accessToken" in login_data, "accessToken missing in login response"
    token = login_data["accessToken"]

    headers = {"Authorization": f"Bearer {token}"}

    # Step 2: Verify GET /api/Auth/me returns 200 before logout-all
    me_resp_before = requests.get(
        BASE_URL + AUTH_ME_ENDPOINT,
        headers=headers,
        timeout=TIMEOUT
    )
    assert me_resp_before.status_code == 200, f"GET /api/Auth/me failed with status {me_resp_before.status_code} before logout-all"

    try:
        # Step 3: POST /api/Auth/logout-all to logout all sessions
        logout_all_resp = requests.post(
            BASE_URL + LOGOUT_ALL_ENDPOINT,
            headers=headers,
            timeout=TIMEOUT
        )
        assert logout_all_resp.status_code == 200, f"Logout-all failed with status {logout_all_resp.status_code}"

        # Step 4: Verify GET /api/Auth/me with old token returns 401 after logout-all
        me_resp_after = requests.get(
            BASE_URL + AUTH_ME_ENDPOINT,
            headers=headers,
            timeout=TIMEOUT
        )
        assert me_resp_after.status_code == 401, f"GET /api/Auth/me did not return 401 after logout-all, got {me_resp_after.status_code}"
    finally:
        # No cleanup required as logout-all clears sessions
        pass

test_post_api_auth_logout_all_sessions()
