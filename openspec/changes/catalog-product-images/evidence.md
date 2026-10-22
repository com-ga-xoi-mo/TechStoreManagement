# Catalog product images — verification evidence

Status: implementation and automated verification complete; owner checks 20.2 and 20.3 pending. Task 20.4 remains open until the owner's results are recorded and the final evidence commit is made.

Recorded at: 2026-10-23T01:51:21+07:00.
Implementation commit: `f5fec6d` (gallery/viewer), on `feat/catalog-product-management`. Local commits only; nothing pushed and no pull request opened.

## Automated checks

| Check | Observed result |
|---|---|
| Baseline solution build | Passed |
| Baseline backend / client unit tests | 383 / 616 passed |
| Final `dotnet build TechStore.sln` | Passed, 0 errors |
| Final backend unit tests | 447 passed, 0 failed, 0 skipped |
| Final client unit tests | 680 passed, 0 failed, 0 skipped |
| `openspec validate catalog-product-images --strict` | Valid; two expected INFO messages about prerequisite archives |
| `git diff --check` | Passed |

Build emits an EF Core Relational assembly version conflict warning (10.0.4 versus 10.0.12); this warning was also present in the baseline. It did not fail the build or unit tests. These counts do not certify desktop bindings or real R2 access.

## Migration and schema

Owner approved changing `idx_product_images_variant_id` to `(variant_id, product_id)` on 2026-10-23. D1, task 4.2, EF configuration and DBML agree. The unapplied initial migration was removed with EF tooling and regenerated as `20261022182049_AddProductImages`.

Generated Up creates only `product_images`, two foreign keys (one composite), three check constraints and three secondary indexes; no existing table is dropped. The migration was applied successfully. PostgreSQL `\d product_images` confirmed columns, checks, cascading foreign keys and the three secondary indexes, in addition to the primary key index.

DBML brace balance was checked; no DBML parser or dbdiagram.io import was run.

## Live API checks (local storage)

API was built and run from `/tmp/catalog-images-live/api`, port 5177. Smoke uploads used `backend/samples/pixel.png`; throwaway data was removed after checks.

| Request/check | Observed result |
|---|---|
| Upload PNG | 201; URL starts `/uploads/products/` |
| Public GET image, without bearer token | 200; `Content-Type: image/png`, `X-Content-Type-Options: nosniff` |
| Upload text named JPG | 400 with `file` validation error |
| Upload without token | 401 |
| Save complete layout | 200 |
| Product detail | 200; one image and matching cover URL |
| Delete image | 204; file removed, images empty and cover null |
| Delete throwaway product with two images | 204; both files removed; DB image row count 0 |

Deleted throwaway product: `9cbe6b92-6db7-4233-9eb1-2c1e8c63449d`. Real concurrent requests and live SalesStaff authorization were not exercised by these smoke checks; the service race recheck and authorization metadata are covered by unit tests.

## Desktop owner checks (task 20.2)

The client started against port 5177 and logged a Shell viewport of 1024×640. No screenshot probe for Catalog pages was available. Image preview uses option 1, `BitmapImage.SetSource` with `AsRandomAccessStream`, which compiles. Actual rendered images and theme switching remain unverified.

Uno logged a RemoteControl endpoint warning and X11 presenter warnings. No desktop interaction result is inferred from startup.

For owner review, the backend now runs from the repository on port 5177, using `backend/uploads`. The client is running with `TECHSTORE_API_BASE_URL=http://localhost:5177/`.

| Item | Expected (full checklist: tasks.md 20.2) | Owner result |
|---|---|---|
| 1 | Empty `0/20`; multi-file picker; valid image previews; rejected filename shown | Pending |
| 2 | Thumbnail selection, cover, left/right and removal work | Pending |
| 3 | Variant assignment/count/rename follow rows; removing unsaved row makes image shared | Pending |
| 4 | Save order, variant filter and listing cover match | Pending |
| 5 | Cancel warns; saving edit persists; removed file gone; missing file shows placeholder without blocking save | Pending |
| 6 | Read-only storage yields 503 after product save; retry uploads successfully without duplicate product | Pending; coordinate backend restart before and after this item |
| 7 | Light/Dark keeps thumbnails, labels and selected border readable | Pending |
| 8 | Deleting the test product removes its files | Pending |

Do not mark task 20.2 complete until the owner confirms every item. Start with items 1–5; before item 6 switch the backend's local root to a scratch read-only folder; restore the normal backend before items 7–8.

## R2 owner check (task 20.3)

Pending. No owner keys or real R2 bucket were used. Unit checks and SDK compilation do not establish that R2 uploads/deletes work. If the owner has no bucket, record the owner's explicit decision before accepting the specified exception. Never put credentials in chat, this evidence or git.

## Hand-off

When the owner decides to share the branch, relay the following to Dev 3 (Inventory), Dev 4 (Orders) and Dev 6 (seeder/analytics):

- New `product_images` table and `AddProductImages` migration. Teammates with unmerged migrations need to regenerate them after pulling to avoid snapshot conflicts.
- `CreateProductRequest` and `UpdateProductRequest` no longer contain `ImageUrl`; `ProductDto` adds `Images`.
- `products.image_url` remains the cover, written only by the server.
- `IProductRepository.GetWithVariantsAsync` is unchanged.
- Deleting an unused product now removes its images too.

Images work with local storage by default. To enable R2, create a bucket, enable its public development URL or custom domain, create a bucket-scoped Object Read & Write API token, then set `ObjectStorage:Provider=R2` and `ObjectStorage:R2:{AccountId,AccessKeyId,SecretAccessKey,BucketName,PublicBaseUrl}` using backend user-secrets or environment variables. Exact commands are in `backend/Catalog/README.md`; keys remain server-side.

Archive order: `catalog-product-management-api`, then `catalog-product-management-ui`, then `catalog-product-images`. Do not archive this change until the owner checks and final evidence are complete. Pushing or opening a PR remains the owner's decision.
