import requests

BASE_URL = "http://localhost:5184"
LOGIN_URL = f"{BASE_URL}/api/Auth/login"
ADMIN_USERS_URL = f"{BASE_URL}/api/admin/users"
TIMEOUT = 30

ADMIN_CREDENTIALS = {
    "emailOrUsername": "admin@admin.com",
    "password": "Admin123!"
}


def login_get_token():
    resp = requests.post(LOGIN_URL, json=ADMIN_CREDENTIALS, timeout=TIMEOUT)
    resp.raise_for_status()
    data = resp.json()
    access_token = data.get("accessToken")
    if not access_token:
        raise AssertionError("Login did not return accessToken")
    return access_token


def fetch_admin_users(access_token, tenant_id):
    headers = {
        "Authorization": f"Bearer {access_token}",
        "X-Tenant-ID": tenant_id  # Assuming tenant context is passed this way
    }
    resp = requests.get(ADMIN_USERS_URL, headers=headers, timeout=TIMEOUT)
    resp.raise_for_status()
    return resp.json()


def test_tc303_tenant_context_isolation_admin_users_list():
    access_token = login_get_token()

    tenant_a = "TenantA"
    tenant_b = "TenantB"

    users_tenant_a = fetch_admin_users(access_token, tenant_a)
    users_tenant_b = fetch_admin_users(access_token, tenant_b)

    ids_tenant_a = {u.get("id") for u in users_tenant_a}
    ids_tenant_b = {u.get("id") for u in users_tenant_b}

    intersection = ids_tenant_a.intersection(ids_tenant_b)
    assert intersection == set(), f"Cross-tenant user leakage found: {intersection}"

    assert isinstance(users_tenant_a, list), "Tenant A users list response is not a list"
    assert isinstance(users_tenant_b, list), "Tenant B users list response is not a list"


test_tc303_tenant_context_isolation_admin_users_list()