import requests

BASE_URL = "http://localhost:5184"
LOGIN_ENDPOINT = "/api/Auth/login"
LOGOUT_ENDPOINT = "/api/Auth/logout"
ME_ENDPOINT = "/api/Auth/me"
TIMEOUT = 30

def test_post_api_auth_logout_with_bearer_token():
    # Seeded admin credentials as per instruction
    login_payload = {
        "emailOrUsername": "admin@admin.com",
        "password": "Admin123!"
    }
    headers = {"Content-Type": "application/json"}

    try:
        # Login to get bearer token
        login_resp = requests.post(
            BASE_URL + LOGIN_ENDPOINT,
            json=login_payload,
            headers=headers,
            timeout=TIMEOUT
        )
        assert login_resp.status_code == 200, f"Login failed with status {login_resp.status_code}"
        token = login_resp.json().get("accessToken")
        assert token, "No accessToken received on login"
        auth_headers = {
            "Authorization": f"Bearer {token}"
        }

        # Confirm GET /api/Auth/me returns 200 (authenticated user profile)
        me_resp_before_logout = requests.get(
            BASE_URL + ME_ENDPOINT,
            headers=auth_headers,
            timeout=TIMEOUT
        )
        assert me_resp_before_logout.status_code == 200, "GET /api/Auth/me before logout did not return 200"

        # POST /api/Auth/logout with valid bearer token
        logout_resp = requests.post(
            BASE_URL + LOGOUT_ENDPOINT,
            headers=auth_headers,
            timeout=TIMEOUT
        )
        assert logout_resp.status_code == 200, f"Logout failed with status {logout_resp.status_code}"

        # After logout, GET /api/Auth/me should return 401 Unauthorized
        me_resp_after_logout = requests.get(
            BASE_URL + ME_ENDPOINT,
            headers=auth_headers,
            timeout=TIMEOUT
        )
        assert me_resp_after_logout.status_code == 401, \
            f"Expected 401 after logout but got {me_resp_after_logout.status_code}"

    except (requests.RequestException, AssertionError) as e:
        raise e

test_post_api_auth_logout_with_bearer_token()
