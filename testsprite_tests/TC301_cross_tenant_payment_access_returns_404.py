import requests

BASE_URL = "http://localhost:5184"
LOGIN_URL = f"{BASE_URL}/api/Auth/login"
PAYMENTS_URL = f"{BASE_URL}/api/admin/payments"
TIMEOUT = 30

ADMIN_CREDENTIALS = {
    "email-or-username": "admin@admin.com",
    "password": "Admin123!"
}

TENANT_A = "TenantA"
TENANT_B = "TenantB"

def login():
    resp = requests.post(LOGIN_URL, json=ADMIN_CREDENTIALS, timeout=TIMEOUT)
    resp.raise_for_status()
    data = resp.json()
    token = data.get("accessToken") or data.get("access_token") or data.get("token")
    assert token is not None, "Failed to retrieve access token on login"
    return token

def create_payment(token, tenant):
    headers = {
        "Authorization": f"Bearer {token}",
        "X-Tenant": tenant,
        "Content-Type": "application/json"
    }
    # Using minimal body to create a payment; API schema not detailed so using placeholder
    payment_data = {
        "amount": 100,
        "currency": "USD",
        "description": "Test payment for cross-tenant access test"
    }
    resp = requests.post(PAYMENTS_URL, json=payment_data, headers=headers, timeout=TIMEOUT)
    resp.raise_for_status()
    return resp.json()

def get_payment(token, tenant, payment_id):
    headers = {
        "Authorization": f"Bearer {token}",
        "X-Tenant": tenant,
    }
    url = f"{PAYMENTS_URL}/{payment_id}"
    resp = requests.get(url, headers=headers, timeout=TIMEOUT)
    return resp

def delete_payment(token, tenant, payment_id):
    headers = {
        "Authorization": f"Bearer {token}",
        "X-Tenant": tenant
    }
    url = f"{PAYMENTS_URL}/{payment_id}"
    resp = requests.delete(url, headers=headers, timeout=TIMEOUT)
    resp.raise_for_status()

def test_cross_tenant_payment_access_returns_404():
    token = login()
    payment = None
    payment_id = None
    try:
        # Create payment under Tenant A
        payment = create_payment(token, TENANT_A)
        payment_id = payment.get("id") or payment.get("paymentId")
        assert payment_id is not None, "Payment creation response missing id"

        # Attempt to access payment under Tenant B context
        resp = get_payment(token, TENANT_B, payment_id)
        # Assert status code 404 for cross-tenant access (not 403)
        assert resp.status_code == 404, f"Expected 404 for cross-tenant access but got {resp.status_code}, response: {resp.text}"
    finally:
        # Cleanup: delete payment under Tenant A if created
        if payment_id:
            try:
                delete_payment(token, TENANT_A, payment_id)
            except Exception:
                pass

test_cross_tenant_payment_access_returns_404()