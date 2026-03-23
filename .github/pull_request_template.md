## Localization Boundary Checklist

- [ ] I used literal translation keys only (no dynamic `t(...)` keys).
- [ ] I did not add `t(product.name)`, `t(category.name)`, or `t(cat.name)`.
- [ ] I did not add domain-like keys such as `products.<real_category_name>`.
- [ ] UI/operator copy changes are in locale JSON; domain/catalog data remains untouched.
- [ ] `de` entries exist for all new keys.
- [ ] Missing `en`/`tr` entries (if any) are intentional and documented.
- [ ] I ran `npm run i18n:validate` in the affected app(s).
- [ ] I ran `npm run i18n:boundary` in the affected app(s).
- [ ] I reviewed generated report files under `localization/out/reports`.

## Notes

- Explain any intentional missing translations or boundary exceptions here.

## CI Validation Summary

- frontend validate: pass / warning-only / fail
- frontend-admin validate: pass / fail
- boundary (frontend): pass / warning-only / fail
- boundary (frontend-admin): pass / fail
- JSON parse check: pass / fail
