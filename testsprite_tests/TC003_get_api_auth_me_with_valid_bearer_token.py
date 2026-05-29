import requests

BASE_URL = "http://localhost:5184"
LOGIN_ENDPOINT = "/api/Auth/login"
ME_ENDPOINT = "/api/Auth/me"
TIMEOUT = 30

def test_get_api_auth_me_with_valid_bearer_token():
    login_url = BASE_URL + LOGIN_ENDPOINT
    me_url = BASE_URL + ME_ENDPOINT
    login_payload = {
        "emailOrUsername": "admin@admin.com",
        "password": "Admin123!"
    }
    try:
        # Login to obtain bearer token
        login_response = requests.post(login_url, json=login_payload, timeout=TIMEOUT)
        assert login_response.status_code == 200, f"Login failed with status {login_response.status_code}"
        login_data = login_response.json()
        access_token = login_data.get("accessToken")
        assert access_token, "Access token not found in login response"

        headers = {"Authorization": f"Bearer {access_token}"}
        # Call /api/Auth/me with valid token
        me_response = requests.get(me_url, headers=headers, timeout=TIMEOUT)
        assert me_response.status_code == 200, f"/api/Auth/me call failed with status {me_response.status_code}"
        me_data = me_response.json()
        assert isinstance(me_data, dict), "Authenticated user profile is not a JSON object"
        assert "email" in me_data and "username" in me_data, "Authenticated user profile missing expected fields"
    except requests.RequestException as e:
        assert False, f"Request failed: {e}"

test_get_api_auth_me_with_valid_bearer_token()
