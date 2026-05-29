import requests

BASE_URL = "http://localhost:5184"
TIMEOUT = 30

def test_post_api_auth_login_with_valid_credentials():
    url = f"{BASE_URL}/api/Auth/login"
    headers = {
        "Content-Type": "application/json"
    }
    payload = {
        "emailOrUsername": "admin@admin.com",
        "password": "Admin123!"
    }
    try:
        response = requests.post(url, json=payload, headers=headers, timeout=TIMEOUT)
        assert response.status_code == 200, f"Expected 200, got {response.status_code}"
        json_resp = response.json()
        assert "token" in json_resp and isinstance(json_resp["token"], str) and json_resp["token"], "Missing or invalid token"
        assert "session" in json_resp and isinstance(json_resp["session"], dict), "Missing or invalid session data"
    except requests.exceptions.RequestException as e:
        assert False, f"Request failed: {e}"

test_post_api_auth_login_with_valid_credentials()
