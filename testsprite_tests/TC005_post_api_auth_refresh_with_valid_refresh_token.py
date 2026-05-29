import requests

BASE_URL = "http://localhost:5184"
LOGIN_URL = f"{BASE_URL}/api/Auth/login"
REFRESH_URL = f"{BASE_URL}/api/Auth/refresh"
ME_URL = f"{BASE_URL}/api/Auth/me"
TIMEOUT = 30

def test_post_api_auth_refresh_with_valid_refresh_token():
    login_payload = {
        "email": "admin@admin.com",
        "password": "Admin123!"
    }
    try:
        # Step 1: Login to get access and refresh tokens
        login_resp = requests.post(LOGIN_URL, json=login_payload, timeout=TIMEOUT)
        assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
        login_data = login_resp.json()
        assert "accessToken" in login_data and "refreshToken" in login_data, "Tokens missing in login response"

        refresh_token = login_data["refreshToken"]

        # Step 2: Use refresh token to get new access token
        refresh_payload = {"refreshToken": refresh_token}
        refresh_resp = requests.post(REFRESH_URL, json=refresh_payload, timeout=TIMEOUT)
        assert refresh_resp.status_code == 200, f"Refresh token failed: {refresh_resp.text}"
        refresh_data = refresh_resp.json()
        assert "accessToken" in refresh_data, "Access token missing in refresh response"

        new_access_token = refresh_data["accessToken"]

        # Step 3: Use new access token to get current user profile
        headers = {"Authorization": f"Bearer {new_access_token}"}
        me_resp = requests.get(ME_URL, headers=headers, timeout=TIMEOUT)
        assert me_resp.status_code == 200, f"GET /api/Auth/me failed with new token: {me_resp.text}"
        me_data = me_resp.json()
        assert "email" in me_data and me_data["email"].lower() == "admin@admin.com", "User profile mismatch"

    except requests.RequestException as e:
        assert False, f"Request failed: {str(e)}"

test_post_api_auth_refresh_with_valid_refresh_token()
