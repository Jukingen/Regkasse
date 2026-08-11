# Archived: multi-step CreateTenantWizard

This folder is **not imported** by the app. It was archived because
`CreateTenantWizard.tsx` (single-step) and `CreateTenantWizard/index.tsx`
shared the same module path and caused a resolution conflict.

Single-step tenant creation wizard is the active implementation.
Multi-step version removed from the live import graph to reduce confusion.

Do not reintroduce a `CreateTenantWizard/` directory next to
`CreateTenantWizard.tsx` without renaming one of them first.
