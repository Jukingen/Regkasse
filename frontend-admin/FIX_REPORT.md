# Fix Report: Product Update 400 Error

## Root Cause
The Backend returned `400 Bad Request` because the `Unit` field was empty but required by the `[Required]` attribute on the `Product` model.

## Changes Implemented

### 1. ProductForm.tsx
-   **Validation**: Added `rules={[{ required: true }]}` to the `Unit` input field.
-   **Error Handling**: Updated `handleOk` to catch submission errors.
-   **Feedback**: Implemented logic to map Backend Validation Errors (e.g., `errors: { Unit: ["Required"] }`) to Ant Design Form fields. Now, specific fields will be highlighted with the exact error message from the server.

### 2. ProductsPage (page.tsx)
-   **Exception Propagation**: Updated `handleCreate` and `handleUpdate` to `throw err` instead of swallowing it. This ensures `ProductForm` receives the error response to display validation messages.

### 3. Payload Verification
-   The `mapUiProductToApi` utility already includes `unit: uiProduct.unit`, ensuring the data is sent correctly.

## how to Verify
1.  Open the Edit Modal for a product.
2.  Clear the "Unit" field.
3.  Click "Save".
4.  **Expected**: The form prevents submission and shows "Please enter unit".
5.  **Backend Verification**: If any other field is rejected by the backend, the form will now display that specific error on the corresponding field.
