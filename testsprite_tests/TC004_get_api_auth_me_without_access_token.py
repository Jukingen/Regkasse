import requests

BASE_URL = "http://localhost:5184"
TIMEOUT = 30

def test_get_api_auth_me_without_access_token():
    url = f"{BASE_URL}/api/Auth/me"
    try:
        response = requests.get(url, timeout=TIMEOUT)
        assert response.status_code == 401, f"Expected 401 Unauthorized but got {response.status_code}"
        # Optionally, check response content/message for prompt to login
        # Example: assert "login" in response.text.lower()
    except requests.RequestException as e:
        raise AssertionError(f"Request to {url} failed: {e}")

test_get_api_auth_me_without_access_token()