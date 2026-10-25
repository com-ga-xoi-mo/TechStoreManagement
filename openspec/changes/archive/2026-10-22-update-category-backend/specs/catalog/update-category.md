# UC-CAT-03: Update Category

The normative delta for this use case is [category-catalog/spec.md](../category-catalog/spec.md). It extends the existing `category-catalog` capability without renaming or duplicating it.

This requested entry point covers `PUT /api/categories/{id}`: editable fields, UTC timestamps, name and Vietnamese slug validation, uniqueness, parent existence, cycle prevention, cancellation, and Admin authorization.

OpenSpec discovers capability deltas by the filename `spec.md`. Keep requirements in the linked canonical file so validation, synchronization, and archive apply them to `openspec/specs/category-catalog/spec.md`.

The implementation approach is in [design.md](../../design.md); the Red -> Green -> Refactor sequence and seven required test names are in [tasks.md](../../tasks.md).
