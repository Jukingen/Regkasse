# Fix Report: EF Core Tracking Conflict

## Issue
The backend returned a 500 Internal Server Error when attempting to update a Product:
> "The instance of entity type 'Product' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked."

## Root Cause
In `GenericRepository.UpdateAsync`:
1.  `existingEntity` was retrieved using `GetByIdAsync(entity.Id)`, which attaches it to the EF Core context for tracking.
2.  `_dbSet.Update(entity)` was then called with the *new* entity instance (from the API body), which attempts to attach *it* to the context as well.
3.  Since EF Core cannot track two different C# objects representing the same database row (same Primary Key), it threw the exception.

## Fix Implemented
Detach the `existingEntity` after verifying it exists. This removes it from the ChangeTracker, allowing the new `entity` instance to be attached and saved without conflict.

```csharp
// Before:
// var existingEntity = await GetByIdAsync(entity.Id);
// ... checks ...
// _dbSet.Update(entity); // Conflict!

// After:
var existingEntity = await GetByIdAsync(entity.Id);
if (existingEntity == null) throw ...;

// EF Core tracking hatasını önlemek için mevcut entity'i detach et
_context.Entry(existingEntity).State = EntityState.Detached;

entity.UpdatedAt = DateTime.UtcNow;
entity.CreatedAt = existingEntity.CreatedAt;

_dbSet.Update(entity); // Safe!
await _context.SaveChangesAsync();
```

## Status
-   [x] Analysis Complete
-   [x] Fix Applied to `GenericRepository.cs`
-   [ ] User Verification Pending
