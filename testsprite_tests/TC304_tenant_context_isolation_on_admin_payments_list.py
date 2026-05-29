import requests

BASE_URL = "http://localhost:5184"
LOGIN_URL = f"{BASE_URL}/api/Auth/login"
ADMIN_PAYMENTS_URL = f"{BASE_URL}/api/admin/payments"
TIMEOUT = 30

ADMIN_CREDENTIALS = {
    "loginIdentifier": "admin@admin.com",
    "password": "Admin123!",
    "clientApp": "admin"
}

TENANT_A_HEADER = {"X-Tenant-Id": "tenantA"}
TENANT_B_HEADER = {"X-Tenant-Id": "tenantB"}


def login_and_get_token():
    resp = requests.post(LOGIN_URL, json=ADMIN_CREDENTIALS, timeout=TIMEOUT)
    resp.raise_for_status()
    data = resp.json()
    token = data.get("accessToken") or data.get("access_token") or data.get("token")
    if not token:
        raise Exception("Login response missing access token")
    return token


def get_payments(token, tenant_headers):
    headers = {
        "Authorization": f"Bearer {token}",
        **tenant_headers
    }
    resp = requests.get(ADMIN_PAYMENTS_URL, headers=headers, timeout=TIMEOUT)
    resp.raise_for_status()
    return resp.json()


def test_TC304_tenant_context_isolation_on_admin_payments_list():
    token = login_and_get_token()

    payments_tenant_a = get_payments(token, TENANT_A_HEADER)
    payments_tenant_b = get_payments(token, TENANT_B_HEADER)

    assert isinstance(payments_tenant_a, list), "Expected payments list from Tenant A"
    assert isinstance(payments_tenant_b, list), "Expected payments list from Tenant B"

    # IDs or unique fields extraction - assuming each payment has 'id' field
    ids_a = {p.get("id") for p in payments_tenant_a if "id" in p}
    ids_b = {p.get("id") for p in payments_tenant_b if "id" in p}

    # They should not overlap
    overlap = ids_a.intersection(ids_b)
    assert not overlap, f"Cross-tenant payment data overlap detected: {overlap}"

    # Now verify cross-tenant payment access returns 404 for individual payment IDs from Tenant A accessed with Tenant B context
    # Test one example id if exists
    if ids_a:
        sample_payment_id = next(iter(ids_a))
        headers_b = {
            "Authorization": f"Bearer {token}",
            **TENANT_B_HEADER
        }
        single_payment_url = f"{BASE_URL}/api/admin/payments/{sample_payment_id}"
        resp = requests.get(single_payment_url, headers=headers_b, timeout=TIMEOUT)
        assert resp.status_code == 404, f"Expected 404 for cross-tenant payment access but got {resp.status_code}"

    # Similarly test cross-tenant payment access returns 404 for Tenant B payment id accessed with Tenant A context
    if ids_b:
        sample_payment_id_b = next(iter(ids_b))
        headers_a = {
            "Authorization": f"Bearer {token}",
            **TENANT_A_HEADER
        }
        single_payment_url_b = f"{BASE_URL}/api/admin/payments/{sample_payment_id_b}"
        resp = requests.get(single_payment_url_b, headers=headers_a, timeout=TIMEOUT)
        assert resp.status_code == 404, f"Expected 404 for cross-tenant payment access but got {resp.status_code}"


test_TC304_tenant_context_isolation_on_admin_payments_list()