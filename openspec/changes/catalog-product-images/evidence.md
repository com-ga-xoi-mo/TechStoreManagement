# Catalog product images — verification evidence

Status: complete. Implementation, automated checks, owner app check (20.2) and R2 check (20.3) are recorded; all tasks are ticked.

Final update: 2026-10-23T16:34:07+07:00. Last code commit: `673eabe` (`.env` loader), after `df6adfc` and `ac07bf7`; first recorded at 2026-10-23T01:51:21+07:00 with `65a8682`. Branch `feat/catalog-product-management`. Local commits only; nothing pushed and no pull request opened.

Final counts: backend 450 passed (447 + 3 `DotEnvFileTests`), client 689 passed, solution build 0 errors (the same 2 EF Relational version warnings as baseline), `openspec validate catalog-product-images --strict` valid.

## Automated checks

| Check | Observed result |
|---|---|
| Baseline solution build | Passed |
| Baseline backend / client unit tests | 383 / 616 passed |
| Solution build before task 18.7 follow-up | Passed, 0 errors |
| Desktop client build after task 18.7 | Passed, 0 errors, 0 warnings |
| Final backend unit tests | 447 passed, 0 failed, 0 skipped |
| Final client unit tests | 689 passed, 0 failed, 0 skipped |
| `openspec validate catalog-product-images --strict` | Valid; two expected INFO messages about prerequisite archives |
| `git diff --check` | Passed |

Build emits an EF Core Relational assembly version conflict warning (10.0.4 versus 10.0.12); this warning was also present in the baseline. It did not fail the build or unit tests. These counts do not certify desktop bindings or real R2 access.

## Review follow-up (tasks 18.7–18.8)

Verified at: 2026-10-23T12:10:00+07:00. Commit `65a8682` implements delete-404 continuation and the changed-elsewhere message for a mismatched image set. The added tests verify deletion failures other than 404 still propagate and the editor stays open when layout is stale. No automatic reload or retry was added.

Post-commit reruns: backend 447 passed; client 689 passed (+9 tests over the preceding implementation). `ProductEditorViewModel` is 742 lines (limit 760). OpenSpec strict validation is valid with the same two expected archive-order INFO messages.

The updated request-size contract was checked again on port 5177 against a newly generated, confirmed nonexistent product id:

| File size (bytes; multipart request is slightly larger) | HTTP | Validation error key |
|---|---|---|
| 5,767,168 | 400 | `file` |
| 6,100,000 | 400 | `file` |
| 7,340,032 | 400 | Empty key `""` |

These were rejected requests; they did not create product or image data. The owner checks below remain pending.

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

## Preview-frame picker adjustment

Owner confirmed that clicking the large image frame should open the picker, replacing the separate "+ Thêm" button. The frame now uses `AddImagesCommand`; selecting files still only changes the draft and uploads wait for Save product. The existing command disables adding at 20 images. Desktop build passed with 0 warnings and 0 errors; OpenSpec strict validation passed. Manual picker interaction and theme confirmation remain part of task 20.2.

## Hover hint verification

The large image frame now shows a downward-arrow-to-tray glyph and "Thêm ảnh" on pointer hover, using a theme-aware translucent backdrop. A temporary isolated Skia/X11 probe exercised the actual `ProductImageGalleryView` with XTest pointer motion:

| Check | Observed result |
|---|---|
| Pointer outside / inside / outside (Light) | Overlay opacity 0 / 1 / 0; icon color `#FF0F172A` |
| Pointer inside / outside (Dark) | Overlay opacity 1 / 0; icon color `#FFF8FAFC` |
| 20 images while pointer is inside | Overlay Visibility `Collapsed` |

The temporary probe source and entry-point modifications were removed. This verifies the hover indicator and its theme colors; it does not replace the owner's full picker/save checklist.

## Desktop owner checks (task 20.2)

The client started against port 5177 and logged a Shell viewport of 1024×640. No screenshot probe for Catalog pages was available. Image preview uses option 1, `BitmapImage.SetSource` with `AsRandomAccessStream`, which compiles. Actual rendered images and theme switching remain unverified.

Uno logged a RemoteControl endpoint warning and X11 presenter warnings. No desktop interaction result is inferred from startup.

For owner review, the backend now runs from the repository on port 5177, using `backend/uploads`. The client is running with `TECHSTORE_API_BASE_URL=http://localhost:5177/`.

| Item | Expected (full checklist: tasks.md 20.2) | Owner result |
|---|---|---|
| 1 | Empty `0/20`; click the large image frame for the multi-file picker (no separate add button); valid image previews; rejected filename shown | OK (see exception below) |
| 2 | Thumbnail selection, cover, left/right and removal work | OK |
| 3 | Variant assignment/count/rename follow rows; removing unsaved row makes image shared | OK |
| 4 | Save order, variant filter and listing cover match | OK |
| 5 | Cancel warns; saving edit persists; removed file gone; missing file shows placeholder without blocking save | OK |
| 6 | Read-only storage yields 503 after product save; retry uploads successfully without duplicate product | OK |
| 7 | Light/Dark keeps thumbnails, labels and selected border readable | OK |
| 8 | Deleting the test product removes its files | OK |

On 2026-10-23 the owner confirmed the whole checklist in one message ("ok chuẩn hết rồi"); no per-item notes were given, so the results above record that single confirmation.

**Exception (outside the app):** on Ubuntu 24.04 GNOME, Uno opens the `org.freedesktop.portal.FileChooser` dialog drawn by `xdg-desktop-portal-gnome` 46.2 / GTK 4.14. Ctrl+click only highlights files without selecting them (GTK issue #5669), so "Submit" stays disabled. Shift+click or picking several times works. Windows, macOS and KDE are not affected. The owner chose to keep the design.

## Button template follow-up

Verified at: 2026-10-23T14:43:58+07:00. The image picker now uses Button's `CommonStates` in its C# Markup control template. `PointerEntered`/`PointerExited` handlers and manual `GoToState` calls were removed. Because Uno Markup inserts `TemplateRoot`, the private Button subclass attaches the declared groups to that root in `OnApplyTemplate` before calling base; it contains no ViewModel synchronization or pointer handlers. No new D2 exception is required.

A temporary isolated Skia/X11 probe exercised the final template with native XTest pointer/button/Space events and a fake picker returning PNG bytes:

| Check | Observed result |
|---|---|
| Pointer outside / inside / pressed / outside | `Normal` / `PointerOver` / `Pressed` / `Normal`; overlay opacity 0 / 1 / 1 / 0 |
| Click and keyboard Space | Command invoked once per action; image count 0 → 1 → 2 and large preview source became `BitmapImage` |
| Light → Dark while hovered | Glyph color `#FF0F172A` → `#FFF8FAFC`; overlay remained visible |
| Fill gallery to 20 | `Disabled`, button disabled, overlay opacity 0 and Visibility Collapsed; preview source retained |

Temporary probe code and entry-point changes were removed. Desktop build and strict OpenSpec validation were rerun after cleanup. This does not certify real portal file selection, server upload, native Windows rendering, or complete owner task 20.2. Tracking remains 98/101.

## R2 owner check (task 20.3)

Checked on 2026-10-23 with the owner's bucket `tech-store` and its public r2.dev URL. The owner put the keys in the git-ignored `backend/.env` (mode 600); no key appears in chat, this evidence, git or the backend log. Key shapes were checked without printing them (Account ID and Access Key ID 32 hex characters, Secret 64).

| Backend | Request/check | Observed result |
|---|---|---|
| Port 5177, started with `source backend/.env` | Upload 75-byte PNG | 201; URL `https://pub-….r2.dev/products/<productId:N>/<key>.png` |
| | Public GET | 200 `image/png`, bytes identical to the upload |
| | Delete image | 204 |
| | Public GET after delete | 404 |
| Port 5179, plain `dotnet run` (task 19.9 loader) | Upload / public GET / delete / GET after delete | 201 with r2.dev URL / 200 / 204 / 404 |

Both test images were deleted, so the product returned to its previous images. The owner confirmed the Cloudflare configuration (bucket-scoped Object Read & Write token).

## Hand-off

When the owner decides to share the branch, relay the following to Dev 3 (Inventory), Dev 4 (Orders) and Dev 6 (seeder/analytics):

- New `product_images` table and `AddProductImages` migration. Teammates with unmerged migrations need to regenerate them after pulling to avoid snapshot conflicts.
- `CreateProductRequest` and `UpdateProductRequest` no longer contain `ImageUrl`; `ProductDto` adds `Images`.
- `products.image_url` remains the cover, written only by the server.
- `IProductRepository.GetWithVariantsAsync` is unchanged.
- Deleting an unused product now removes its images too.

Images work with local storage by default. To enable R2, create a bucket, enable its public development URL or custom domain, create a bucket-scoped Object Read & Write API token, then set `ObjectStorage:Provider=R2` and `ObjectStorage:R2:{AccountId,AccessKeyId,SecretAccessKey,BucketName,PublicBaseUrl}` using backend user-secrets, environment variables, or the git-ignored `backend/.env` that `dotnet run` loads in Development. Exact commands are in `backend/Catalog/README.md`; keys remain server-side. Never include `backend/.env` in a zipped submission.

Archive order: `catalog-product-management-api`, then `catalog-product-management-ui`, then `catalog-product-images`. Pushing or opening a PR remains the owner's decision.
