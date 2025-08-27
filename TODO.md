# TODO List

## Completed Tasks ✅

### Backend Catalog Endpoint
- [x] Add backend endpoint GET /api/products/catalog returning categories with IDs and products with categoryId
- [x] Fix cryptographic using statements and property access issues
- [x] Generate deterministic category IDs from category names
- [x] Return products with CategoryId field for relationship mapping

### Frontend Catalog Integration
- [x] Update frontend Product type with optional categoryId and add getCatalog service
- [x] Update useProductsUnified to load via getCatalog and expose categoriesWithIds
- [x] Add fallback to separate endpoints if catalog fails
- [x] Add comprehensive error handling and logging

## Current Status

The catalog endpoint is now implemented and should resolve the 400 Bad Request error. The endpoint:

1. **Backend (`/api/products/catalog`)**:
   - Returns categories with stable IDs (deterministic GUIDs from names)
   - Returns products with `CategoryId` field linking to categories
   - Uses only existing Product model properties
   - Includes proper error handling

2. **Frontend**:
   - `getProductCatalog()` service function handles the response
   - `useProductsUnified` tries catalog first, falls back to separate endpoints
   - Comprehensive logging for debugging
   - Graceful error handling

## Next Steps

1. Test the catalog endpoint in the frontend
2. Verify that categories and products are properly loaded
3. Consider implementing ID-based category filtering if needed
4. Monitor performance and optimize if necessary

## Technical Details

- **Category ID Generation**: Uses SHA1 hash of normalized category name to create stable GUIDs
- **Response Format**: 
  ```json
  {
    "Categories": [{"Id": "guid", "Name": "string"}],
    "Products": [{"...", "CategoryId": "guid"}]
  }
  ```
- **Fallback Strategy**: If catalog fails, falls back to separate `/products/all` and `/products/categories` calls
- **Error Handling**: Comprehensive logging and graceful degradation
