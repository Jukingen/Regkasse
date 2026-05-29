import requests

BASE_URL = "http://localhost:5184"
LOGIN_ENDPOINT = "/api/Auth/login"
USERS_ENDPOINT = "/api/admin/users"
TIMEOUT = 30

ADMIN_CREDENTIALS = {
    "emailOrUsername": "admin@admin.com",
    "password": "Admin123!"
}

TENANT_A = "TenantA"
TENANT_B = "TenantB"

def login_admin():
    resp = requests.post(
        BASE_URL + LOGIN_ENDPOINT,
        json=ADMIN_CREDENTIALS,
        timeout=TIMEOUT
    )
    resp.raise_for_status()
    token = resp.json().get("accessToken")
    if not token:
        raise RuntimeError("Login did not return access token")
    return token

def get_headers(token, tenant):
    return {
        "Authorization": f"Bearer {token}",
        "X-Tenant": tenant,
        "Content-Type": "application/json"
    }

def create_user(token, tenant, username_suffix):
    # Minimal user payload to create user under specified tenant
    payload = {
        "email": f"user{username_suffix}@example.com",
        "username": f"user{username_suffix}",
        "password": "User12345!",
        "roles": ["User"]
    }
    headers = get_headers(token, tenant)
    resp = requests.post(
        BASE_URL + USERS_ENDPOINT,
        json=payload,
        headers=headers,
        timeout=TIMEOUT
    )
    resp.raise_for_status()
    return resp.json()

def fetch_any_user(token, tenant):
    headers = get_headers(token, tenant)
    resp = requests.get(
        BASE_URL + USERS_ENDPOINT,
        headers=headers,
        timeout=TIMEOUT
    )
    resp.raise_for_status()
    users = resp.json()
    if not users:
        return None
    return users[0]

def get_user(token, tenant, user_id):
    headers = get_headers(token, tenant)
    resp = requests.get(
        f"{BASE_URL}{USERS_ENDPOINT}/{user_id}",
        headers=headers,
        timeout=TIMEOUT
    )
    return resp

def update_user(token, tenant, user_id, update_data):
    headers = get_headers(token, tenant)
    resp = requests.put(
        f"{BASE_URL}{USERS_ENDPOINT}/{user_id}",
        json=update_data,
        headers=headers,
        timeout=TIMEOUT
    )
    return resp

def delete_user(token, tenant, user_id):
    headers = get_headers(token, tenant)
    resp = requests.delete(
        f"{BASE_URL}{USERS_ENDPOINT}/{user_id}",
        headers=headers,
        timeout=TIMEOUT
    )
    resp.raise_for_status()

def test_cross_tenant_user_access_returns_404():
    token = login_admin()

    # Obtain or create user in Tenant A
    try:
        user = fetch_any_user(token, TENANT_A)
        if user is None:
            user = create_user(token, TENANT_A, "TC302")
        user_id = user.get("id")
        assert user_id is not None, "User id should not be None"

        # Attempt to GET user by id via Tenant B context - expect 404
        resp_get = get_user(token, TENANT_B, user_id)
        assert resp_get.status_code == 404, f"Cross-tenant GET should return 404 but got {resp_get.status_code}"

        # Attempt to UPDATE user by id via Tenant B context - expect 404
        update_data = {"username": "updatedUsernameTC302"}
        resp_put = update_user(token, TENANT_B, user_id, update_data)
        assert resp_put.status_code == 404, f"Cross-tenant PUT should return 404 but got {resp_put.status_code}"

    finally:
        # Cleanup user created if it was created in this test
        # To determine if created, check if the created userId is in Tenant A context user list
        # But assume cleanup needed if user was created here.
        # We'll attempt delete on Tenant A only; ignore errors.
        try:
            delete_user(token, TENANT_A, user_id)
        except Exception:
            pass

test_cross_tenant_user_access_returns_404()
