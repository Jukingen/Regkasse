import requests

BASE_URL = "http://localhost:5184"
LOGIN_ENDPOINT = "/api/Auth/login"
TIMEOUT = 30

def test_post_api_auth_login_with_invalid_credentials():
    url = BASE_URL + LOGIN_ENDPOINT
    headers = {
        "Content-Type": "application/json"
    }
    # Invalid credentials payload
    payload = {
        "loginIdentifier": "invaliduser@example.com",
        "password": "WrongPassword123!",
        "clientApp": "admin"
    }
    try:
        response = requests.post(url, json=payload, headers=headers, timeout=TIMEOUT)
    except requests.RequestException as e:
        assert False, f"Request to {LOGIN_ENDPOINT} failed: {e}"
    assert response.status_code in (400, 401), \
        f"Expected status 400 or 401, got {response.status_code}"
    try:
        body = response.json()
    except ValueError:
        body = {}
    # Verify presence of a login failure indication in response body (message or error)
    message = body.get("message") or body.get("error") or ""
    assert message.strip() != "", \
        f"Expected login failure message in response body, got empty message"

test_post_api_auth_login_with_invalid_credentials()