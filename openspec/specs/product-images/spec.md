# product-images Specification

## Purpose
Lets Admin and Manager users attach several photos to a product, optionally per variant, stored as files in a configured object storage (Cloudflare R2 or a local folder) and referenced from the database, with a server-computed cover image.

## Requirements

### Requirement: Product images are stored as references
The database SHALL store, per image, the owning product, an optional variant, the storage object key, the public URL, the content type, the size in bytes, the sort order and the creation time. Image files SHALL NOT be stored in the database. The sort orders of one product's images SHALL always be 0, 1, 2 … without gaps after every image write.

#### Scenario: Three uploads
- **WHEN** an Admin uploads three images to a product that has none
- **THEN** the product detail lists three images with sort orders 0, 1 and 2, each with a URL that serves the uploaded file

#### Scenario: Gap closed after removal
- **WHEN** a product has images at sort orders 0, 1, 2 and the image at 1 is removed
- **THEN** the remaining images have sort orders 0 and 1 in their previous relative order

### Requirement: Image upload
`POST /api/products/{id}/images` SHALL accept one multipart file in the form field `file` and an optional form field `variantId`. Success SHALL return 201 with the new image (`id`, `variantId`, `url`, `contentType`, `sizeBytes`, `sortOrder`). The new image SHALL be placed after the product's existing images. An unknown product id SHALL return 404.

#### Scenario: Shared image
- **WHEN** an Admin uploads `front.jpg` without `variantId` to a product with two images
- **THEN** the response is 201 with `variantId` null and `sortOrder` 2

#### Scenario: Unknown product
- **WHEN** an Admin uploads an image to a product id that does not exist
- **THEN** the response is 404 and no file is stored

### Requirement: Image file rules
The server SHALL accept only JPEG, PNG and WebP files, recognized from the file content signature and not from the file name or the declared content type. A file that is missing, empty, larger than 5 MB (5,242,880 bytes) or of another type SHALL be rejected with 400 and an error for `file`, and nothing SHALL be stored.

#### Scenario: Renamed text file
- **WHEN** an Admin uploads a text file named `photo.jpg`
- **THEN** the response is 400 with an error for `file`

#### Scenario: Too large
- **WHEN** an Admin uploads a PNG of 6,000,000 bytes (larger than 5 MB, request still under 6 MiB)
- **THEN** the response is 400 with an error for `file` and the product's images are unchanged

#### Scenario: WebP accepted
- **WHEN** an Admin uploads a valid WebP file named `image.bin`
- **THEN** the response is 201 with `contentType` `image/webp`

### Requirement: Upload request size limit
The server SHALL reject an upload request whose body is larger than 6 MiB (6,291,456 bytes) with 400 before the form is read, and nothing SHALL be stored. The error MAY be reported under an empty key instead of `file`.

#### Scenario: Request body over the limit
- **WHEN** an Admin uploads a 7 MiB file
- **THEN** the response is 400 before the form is read and the product's images are unchanged

### Requirement: Image limit per product
A product SHALL have at most 20 images, counting shared and variant images together. An upload that would exceed the limit SHALL return 409 ProblemDetails and store nothing. The check SHALL hold when several uploads for the same product run at the same time.

#### Scenario: Twenty-first image
- **WHEN** a product has 20 images and an Admin uploads another one
- **THEN** the response is 409 and the product still has 20 images

#### Scenario: Concurrent uploads at the limit
- **WHEN** a product has 19 images and two uploads for it arrive at the same time
- **THEN** exactly one succeeds and the other returns 409

### Requirement: Variant images
An image MAY belong to one variant of the same product; an image without a variant is shared by all variants. A `variantId` that is not a variant of the product SHALL be rejected with 400 and an error for `variantId` (on upload) or `items[i].variantId` (on layout). Inactive variants MAY have images.

#### Scenario: Variant of another product
- **WHEN** an Admin uploads an image with the id of a variant that belongs to a different product
- **THEN** the response is 400 with an error for `variantId` and nothing is stored

#### Scenario: Image for a variant
- **WHEN** an Admin uploads an image with the id of the product's "Đen" variant
- **THEN** the image's `variantId` is that variant's id

### Requirement: Image removal
`DELETE /api/products/{id}/images/{imageId}` SHALL remove the image and return 204. An unknown product, an unknown image, or an image of another product SHALL return 404. The stored file SHALL be deleted after the database change is committed; a failure to delete the file SHALL NOT fail the request.

#### Scenario: Remove an image
- **WHEN** an Admin deletes one of a product's three images
- **THEN** the response is 204 and the product detail lists two images

#### Scenario: Image of another product
- **WHEN** an Admin deletes an image id through a different product's route
- **THEN** the response is 404 and both products keep their images

### Requirement: Image layout
`PUT /api/products/{id}/images/layout` with `items` (each `id` and optional `variantId`) SHALL set the order of the product's images to the order of `items` and set each image's variant, atomically, and return 200 with the full image list. `items` SHALL list every current image of the product exactly once; otherwise the response SHALL be 400 with an error for `items` and nothing SHALL change.

#### Scenario: Move an image to the front
- **WHEN** a product has images A, B, C and the layout sends B, A, C
- **THEN** the response lists B, A, C with sort orders 0, 1, 2

#### Scenario: Missing image in the layout
- **WHEN** a product has images A, B, C and the layout sends only A and B
- **THEN** the response is 400 with an error for `items` and the order is unchanged

#### Scenario: Re-assign a variant
- **WHEN** the layout sends image A with the "Trắng" variant id instead of no variant
- **THEN** image A belongs to the "Trắng" variant

### Requirement: Cover image
The product's `imageUrl` SHALL be the URL of the first shared image in sort order; when the product has only variant images it SHALL be the URL of the first image; when it has no images it SHALL be null. The server SHALL update it in the same transaction as every image upload, removal and layout change. Clients SHALL NOT set it directly.

#### Scenario: Shared image first
- **WHEN** a product's images in order are "Đen" image A, shared image B, shared image C
- **THEN** the product's `imageUrl` is the URL of B

#### Scenario: Last image removed
- **WHEN** the only image of a product is removed
- **THEN** the product's `imageUrl` is null and listings show the placeholder

### Requirement: Image write authorization
Upload, removal and layout endpoints SHALL require a valid bearer token whose role is `Admin` or `Manager`. A request without a valid token SHALL return 401 and a request from another role SHALL return 403, without changing data. Images SHALL be readable through their public URL without a token.

#### Scenario: Sales staff upload
- **WHEN** a user with role `SalesStaff` uploads an image
- **THEN** the response is 403 and nothing is stored

#### Scenario: Missing token
- **WHEN** a client deletes an image without a bearer token
- **THEN** the response is 401 and the image still exists

### Requirement: Upload never leaves half-saved images
When a file has been stored but the database change fails or is rejected, the server SHALL delete the stored file before answering. When the storage cannot be reached, the server SHALL return 503 ProblemDetails and SHALL NOT add a database row. A database row SHALL never point to a file that the server failed to store.

#### Scenario: Limit reached after the file was stored
- **WHEN** an upload stores its file and then finds the product already has 20 images
- **THEN** the response is 409 and the stored file is deleted

#### Scenario: Storage down
- **WHEN** the object storage refuses the upload
- **THEN** the response is 503 and the product's images are unchanged

### Requirement: Configurable image storage
The server SHALL store image files in Cloudflare R2 when R2 storage is configured, and otherwise in a local folder served by the API under `/uploads`. Storage credentials SHALL be read from configuration outside source control and SHALL NOT appear in any response or log. Missing R2 settings while R2 is selected SHALL stop the server at startup with a message naming the missing setting.

#### Scenario: No R2 configuration
- **WHEN** the API starts without R2 settings and an Admin uploads an image
- **THEN** the image URL is a relative `/uploads/...` path that the API serves

#### Scenario: Incomplete R2 configuration
- **WHEN** R2 is selected but the secret access key is missing
- **THEN** the API does not start and the log names the missing setting without printing any secret
