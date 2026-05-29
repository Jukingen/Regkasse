import requests

def test_get_api_health_without_authentication():
    base_url = "http://localhost:5184"
    url = f"{base_url}/api/health"
    try:
        response = requests.get(url, timeout=30)
        response.raise_for_status()
    except requests.RequestException as e:
        assert False, f"Request to {url} failed: {e}"
    assert response.status_code == 200, f"Expected status code 200, got {response.status_code}"
    content_type = response.headers.get('Content-Type', '')
    assert 'application/json' in content_type.lower(), f"Expected 'Content-Type' to include 'application/json', got '{content_type}'"
    assert response.text and response.text.strip(), f"Response body is empty"
    try:
        json_data = response.json()
    except ValueError as e:
        assert False, f"Response body is not valid JSON: {e}"
    assert isinstance(json_data, dict), f"Response JSON is not a dict: {json_data}"
    assert "status" in json_data, f"'status' key not in response JSON: {json_data}"
    status_value = json_data["status"]
    assert isinstance(status_value, str), f"'status' value is not a string: {status_value}"
    assert status_value.lower() == "healthy", f"Expected 'status' to be 'healthy', got '{status_value}'"

test_get_api_health_without_authentication()