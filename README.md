# ECommercePlus

A small e-commerce application built with **ASP.NET Core MVC (.NET 10)**, **EF Core**, and **SQLite**. It covers:

- **Product CRUD** (admin UI), with server-side validation and optimistic concurrency.
- **CSV import** with a row-by-row report showing what was created, updated, unchanged, rejected, or ignored.
- **Product search**: free text, category, price range, in-stock filter, sorting, and pagination.
- **Purchasing**: session cart, checkout, a **fake payment gateway**, and order history.
- **ASP.NET Core Identity**: a seeded `Admin` role and admin account with a temporary password, plus a small **Users** admin section. Everything under *Admin* requires the `Admin` role.
- **HTTPS only**: plain HTTP requests are redirected to HTTPS, and every cookie is `Secure`.
- **Docker**: one container, with the SQLite database kept in a volume.

> **Sample CSV:** The example file *"LoanPro Code Challenge E-Commerce"* was downloaded on **2026-09-30**.
> It is included at [`ECommercePlus/SeedData/sample-products.csv`](ECommercePlus/SeedData/sample-products.csv) and imported automatically the first time the app starts with an empty database.

---

## Quick start

### Option 1: Docker (recommended)

Requirements: Docker Desktop / Docker Engine with Compose v2.

```bash
docker compose up --build
```

Open <https://localhost:8443>. Plain <http://localhost:8080> redirects there.

- Unless you mount a certificate, the container creates a **self-signed certificate** for `localhost` on first start (kept on the volume), so your browser shows a warning you'll need to accept once. To use your trusted .NET dev certificate instead:

  ```bash
  dotnet dev-certs https -ep ~/.aspnet/https/ecommerceplus.pfx -p <choose-a-password>
  dotnet dev-certs https --trust
  CERT_PASSWORD=<choose-a-password> docker compose -f compose.yaml -f compose.devcert.yaml up --build
  ```
- Data is stored in the named volume `ecommerceplus-data`, so it survives restarts.
- To start over with a fresh database, run `docker compose down -v`.

Without Compose:

```bash
docker build -f ECommercePlus/Dockerfile -t ecommerceplus .
docker run --rm -p 8443:8443 -p 8080:8080 -v ecommerceplus-data:/app/App_Data ecommerceplus
```

### Option 2: Run locally with the .NET SDK

Requirements: [.NET SDK 10.0](https://dotnet.microsoft.com/download).

```bash
dotnet dev-certs https --trust
dotnet run --project ECommercePlus
```

Open <https://localhost:7268>. <http://localhost:5213> redirects there. `dotnet dev-certs https --trust` only needs to run once per machine; it creates and trusts the local development certificate.

- The SQLite file is created at `ECommercePlus/App_Data/ecommerce.db`. Delete the folder to reset.
- **You don't need to run migrations.** Pending EF Core migrations, including the Identity tables, are applied automatically at startup, followed by the admin and CSV seeding.

### Signing in as the admin

On first start with an empty database, the app creates the `Admin` role and the account **`admin@ecommerceplus.local`** with a random temporary password. The password is never logged. It is written to a file that only the app user can read:

```bash
cat ECommercePlus/App_Data/initial-admin-password.txt                                   # local
docker compose exec ecommerceplus cat /app/App_Data/initial-admin-password.txt          # Docker
```

To choose the temporary password yourself, set `AdminSeed__Password` (and optionally `AdminSeed__Email`) as environment variables before the first start. In both cases you must **change the password at first sign-in**, and the file is deleted once you do.

### Run the tests

```bash
dotnet test
```

There are 61 tests: unit tests for import, search, CRUD, checkout, payments, and temporary passwords, plus integration tests that boot the full app over HTTPS with `WebApplicationFactory` (sign-in, forced password change, role checks, the HTTP→HTTPS redirect, and Secure cookies).

### Optional: EF Core tooling (only when you change the data model)

This is for developers changing the entities. It isn't needed to run the app. `<Name>` is a descriptive name you choose for the new migration, for example `AddProductBrand`. The existing migrations are `InitialCreate` and `AddIdentity`.

```bash
dotnet tool restore
dotnet ef migrations add AddProductBrand --project ECommercePlus --output-dir Data/Migrations
```

The next app start applies it automatically.

---

## Using the app

| Area | URL | What it does |
|---|---|---|
| Shop | `/Shop` | Browse and search the catalog, view details, add to cart |
| Cart | `/Cart` | Change quantities, remove items, go to checkout |
| Checkout | `/Checkout` | Customer and shipping details plus a fake card payment |
| Order receipt | `/Orders/Details/{number}` | Shown after checkout. Visible only to the browser session that placed the order, or to an admin |
| Sign in / Change password | `/Account/Login`, `/Account/ChangePassword` | Cookie sign-in with ASP.NET Core Identity |
| Admin → Manage products | `/Products` | List, search, create, edit, and delete products *(Admin role)* |
| Admin → Import CSV | `/Products/Import` | Upload a CSV and see the import report *(Admin role)* |
| Admin → Orders | `/Orders` | All recent orders *(Admin role)* |
| Admin → Users | `/Users` | Create users (with a generated temporary password), reset passwords, grant or revoke Admin, lock or unlock, delete *(Admin role)* |

Shopping (search, cart, checkout) doesn't require an account. The *Admin* menu appears only for users in the `Admin` role, and any admin URL returns *Access denied* for other signed-in users or redirects anonymous users to sign in.
| Health | `/health` | Liveness and database check |

**Fake payment test cards** (any future expiry date and any 3–4 digit CVV):

| Card number | Result |
|---|---|
| `4242 4242 4242 4242` (or any Luhn-valid number) | Approved |
| `4000 0000 0000 0002` | Declined |
| `4000 0000 0000 9995` | Insufficient funds |
| Number that fails the Luhn check, or an expired date | Declined (validation) |

---

## Architecture

```
ECommercePlus/
├── Domain/            Entities (Product, Order, OrderItem), ProductInput, ProductRules (validation + normalization), OperationResult
├── Data/              AppDbContext, value converters, migrations, DatabaseInitializer (migrate + seed)
├── Services/
│   ├── Products/      ProductService: CRUD + search (paged, filtered, sorted)
│   ├── Import/        ProductCsvImporter + ImportReport
│   ├── Cart/          CartService over an ICartStore (session-backed)
│   ├── Checkout/      CheckoutService: stock reservation, payment, order creation (transactional)
│   └── Payments/      IPaymentGateway + FakePaymentGateway, Luhn helper
├── Identity/          AppUser, roles/policies, admin seeding, temporary passwords, must-change-password middleware
├── Controllers/       Thin MVC controllers (Shop, Products, Cart, Checkout, Orders, Account, Users, Home/Error)
├── ViewModels/        Form/view models (DataAnnotations for client-side validation)
├── Views/             Razor views (Bootstrap 5)
├── Infrastructure/    Security headers middleware, self-signed certificate fallback, session order history, money formatting
└── SeedData/          The sample CSV
ECommercePlus.Tests/   xUnit tests (SQLite in-memory + WebApplicationFactory)
```

Controllers only handle HTTP. Business rules live in services that are registered as interfaces, so the storage, cart, and payment provider can each be replaced without touching the UI.

---

## Decisions and rationale

### Stack: ASP.NET Core MVC with server-rendered Razor
- One deployable unit that includes the UI. There is no separate SPA build, which keeps the Docker image and the reviewer's setup simple.
- Server rendering gives built-in **antiforgery protection** (enforced globally), **HTML encoding by default**, and works without JavaScript. jQuery Validation adds client-side checks on top.

### Database: SQLite through EF Core
- It is a **local, file-based SQL database**, as the challenge requires. Nothing extra needs to be installed or run, and it sits on a Docker volume.
- EF Core gives **migrations**, LINQ queries that are parameterized (which prevents SQL injection, e.g. `Robert'); DROP TABLE products;--` is stored as a plain name), and a provider-agnostic model. Moving to PostgreSQL or SQL Server would mostly be a provider and connection-string change.
- **Money handling:** SQLite has no real decimal type, and EF cannot sort or compare `decimal` columns server-side on SQLite. Prices are kept as `decimal` in C# and stored as **integer cents** through a value converter, so filtering and sorting run in the database and are exact. Weight is stored as integer grams for the same reason.
- **Constraints:** unique index on `Sku`, a CHECK constraint so `Stock >= 0`, and indexes on name, category, and order date.

### Product model
- **SKU is the business key.** It is unique, case-insensitive (normalized to upper case), and limited to letters, digits, `-` and `_`. The CSV import upserts by SKU.
- **Category is free text, not an enum.** The sample file has 18 different categories, and adding an enum value would mean a code change for every new one. The UI suggests existing categories (datalist and search dropdown). An empty category becomes `Uncategorized`.
- `weight_kg` is **optional** because one sample row has no weight. `0` is allowed (digital gift card).
- **Optimistic concurrency:** each product has a `Version` GUID concurrency token. If two admins edit the same product, the second save gets a "modified by someone else" message instead of silently overwriting the first.
- Validation lives in one place, `ProductRules`, and is shared by the admin form and the CSV importer. The form also has DataAnnotations for client-side feedback.

### CSV import: lenient on format, strict on meaning
The sample file deliberately contains bad data. Each case is handled explicitly and shows up in the import report:

| Case in sample | Handling |
|---|---|
| `$29.99` | Accepted. The leading `$` is removed and an info note is added. |
| `free` as price | **Rejected**: the value isn't a number. Guessing `0` could give away stock by accident. |
| `-5` stock | **Rejected**: stock can't be negative. |
| Empty name / whitespace-only name | **Rejected**: name is required. |
| `<script>alert('xss')</script>` name | Stored as-is and **always HTML-encoded on output** (plus a warning). Encoding on output is safer than trying to sanitize input. |
| SQL-injection-like name | Stored as plain text. All queries are parameterized. |
| Duplicate SKU in the same file (`RS-001`, `BS-021` ×3) | **The last row wins** (later rows look like updates). Earlier rows are reported as superseded. |
| SKU that already exists in the DB | **Updated** (upsert). Identical rows count as *unchanged*, so re-importing is idempotent. |
| Empty category | Set to `Uncategorized` (warning). |
| Missing `weight_kg` | Stored as null. |
| `0.00` price | Accepted with a warning (e.g. "Mystery Box"). |
| Fully blank rows (`,,,,,,`) | Ignored. |
| Quoted commas/quotes, Unicode (`—`, `™`) | Read correctly by CsvHelper (RFC 4180) as UTF-8. |

More import rules:
- Required columns are `name, sku, price, stock`. Headers are case-insensitive and can be in any order. Unknown columns produce a warning. A missing required column stops the whole import.
- Invalid rows are skipped. All valid rows are written in **one transaction**, so a file is never half-applied because of a database error.
- Limits: 5 MB upload, 50,000 rows, `.csv` extension only.

With the sample file on an empty database, the result is: **97 rows → 88 created, 4 rejected, 5 ignored** (2 blank rows and 3 superseded duplicates). This is covered by tests.

### Search
- Case-insensitive substring search across name, SKU, description, and category, combined with category, min/max price, and in-stock filters, five sort options, and pagination.
- Wildcard characters typed by the user (`%`, `_`) are escaped, so searching for `100%` matches literally.
- All search state is in the query string, so results can be bookmarked and shared.

### Purchasing and the fake payment
- **Cart** is stored in the server-side session as product ID → quantity. Prices are **never** taken from the client. The cart and checkout always read current prices and stock from the database.
- **Checkout runs in a single database transaction:**
  1. Each line is reserved with an atomic conditional update: `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`. Two shoppers can't buy the last unit at the same time, and stock can never go negative.
  2. The total is calculated from database prices.
  3. The payment gateway is charged.
  4. The order is saved and the transaction committed. If a step fails (no stock, declined card), the transaction rolls back and stock is restored.
- **Idempotency:** each checkout form carries a one-time token stored with the order (unique index). If the form is submitted twice, the existing order is returned instead of charging again.
- **Order lines keep a snapshot** of product name, SKU, and unit price. Deleting a product later sets the line's `ProductId` to null but keeps the order history.
- **`FakePaymentGateway`** implements `IPaymentGateway`. It checks Luhn, expiry, and CVV, and supports Stripe-style test cards for declines. Only the **last 4 digits** are stored. Card number and CVV are never saved or logged, and they are cleared when the form is shown again after an error.

### Authentication and authorization: ASP.NET Core Identity
- The `AppDbContext` extends `IdentityDbContext<AppUser>`, so users and roles live in the same SQLite database, added by the `AddIdentity` migration. There is no external identity server; cookie authentication is enough for a single app.
- **Policy-based authorization**: an `AdminOnly` policy (requires the `Admin` role) protects `ProductsController`, `UsersController`, and the order list.
- **Temporary passwords**: the seeded admin and any user created or reset by an admin get a random password that meets the policy and is shown once. A `must_change_password` claim and a middleware restrict the user to *Change password* until they set their own.
- **Safety rails in user admin**: admins can't delete, lock, or demote themselves, and the last active admin can't be removed. Role changes and locks rotate the security stamp, and cookies are re-validated every minute, so revoked access takes effect quickly.
- Password policy: at least 12 characters with upper case, lower case, digit, and symbol. Lockout after 5 failed attempts for 15 minutes. Sign-in errors don't reveal whether an account exists.

### HTTPS
- `UseHttpsRedirection` is always on, and HSTS is on outside Development (browsers ignore HSTS for `localhost`).
- Auth, session, antiforgery, and TempData cookies use `SecurePolicy = Always`, plus `HttpOnly` and `SameSite`.
- Certificates: locally, the .NET dev certificate. In Docker, a mounted certificate (`Kestrel__Certificates__Default__*`) if provided. Otherwise the app generates a self-signed `localhost` certificate on the volume so HTTPS works out of the box. In production, TLS would typically end at a reverse proxy or load balancer with a real certificate, and `ForwardedHeaders` would be enabled.

### Security and operational concerns
- A global `AutoValidateAntiforgeryToken` filter protects every POST.
- Security headers: a strict **Content-Security-Policy** (no inline scripts or styles), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, and `Permissions-Policy`.
- Friendly 404 and 500 pages via status-code re-execution. `/health` checks the database.
- The container runs as the **non-root** `app` user. Data Protection keys (used for antiforgery and session cookies) are saved on the volume, so cookies stay valid across container restarts.
- Logs are structured through `ILogger` for imports, product changes, orders, and payments (no card data).

---

## Alternatives considered

| Topic | Alternative | Why not (for this scope) |
|---|---|---|
| UI | React/Angular SPA + REST API | Twice the build tooling and deployment work, and the CRUD/search/checkout requirements don't need it. The service layer could sit behind a REST API later without changes. |
| UI | Blazor Server | Needs a persistent SignalR connection per user. MVC is simpler to scale and easier to test with plain HTTP. |
| DB | PostgreSQL / SQL Server in docker-compose | Better for concurrent writes and full-text search, but adds a second container. The requirement asked for a *local* DB, and EF Core keeps switching cheap. |
| DB | NoSQL (LiteDB/MongoDB) | Orders, stock reservation, and unique SKUs benefit from relational constraints and transactions. |
| Search | SQLite FTS5 / Elasticsearch | Better ranking and stemming. `LIKE` with indexes is enough for this catalog size and much simpler. FTS5 would be the next step. |
| Category | C# enum or separate `Categories` table | An enum needs a deploy for every new category. A table with admin CRUD is the natural next step and is out of scope here. |
| Price storage | `REAL`/`TEXT` columns | `REAL` loses precision. `TEXT` can't be compared or sorted numerically in SQLite. Integer cents is exact and queryable. |
| CSV duplicates | First row wins / reject the whole file | The sample suggests later rows are updates, and rejecting a whole file over one duplicate is unfriendly. Every duplicate is reported so nothing happens silently. |
| CSV `free` price | Treat as `0.00` | Too risky for a price field. Rejecting it and reporting it is the safe default. |
| Cart storage | DB-persisted cart / client cookie | A DB cart is needed for multi-device carts, which requires user accounts. A cookie-only cart could be tampered with. A session holds only IDs and quantities and is re-validated on every request. |
| Payment | Real sandbox (Stripe test mode) | Not required, and it would need API keys. The `IPaymentGateway` boundary makes swapping it in a single class. |
| Auth | Duende IdentityServer / OpenIddict / Entra ID | An identity server is only worth it with several clients or APIs that need tokens. ASP.NET Core Identity with cookies covers one MVC app. |
| Auth | Fixed admin password in `appsettings.json` | A known password in source control is a security risk. A random one-time password plus a forced change is safer. |
| HTTPS in Docker | HTTP only behind a TLS-terminating proxy | That's the common production setup, but the challenge runs the container directly, so the app serves HTTPS itself. |

## Known limitations / next steps
- Customers check out as guests, so there is no "my orders" page across sessions. Next step: optional customer accounts with orders linked to the user.
- No email confirmation or self-service password reset (admins reset passwords). Next step: an `IEmailSender` and Identity's token providers.
- The session uses an in-memory store, so carts are lost on restart and don't work across multiple instances. Next step: a distributed cache (Redis) or DB-backed carts.
- SQLite allows only one writer at a time. That's fine for a single instance. For horizontal scaling, move to PostgreSQL.
- Payment is charged inside the stock-reservation transaction, which is acceptable with an in-process fake. With a real provider, use authorize/capture plus an outbox or saga so the database lock isn't held during a network call.
- Single currency (USD) and no taxes or shipping costs.
- Data Protection keys are stored unencrypted on the volume (logged as a warning). In production, protect them with a certificate or a key vault.

## Use of AI
An AI coding assistant helped write this project. As requested, code comments were removed, including the auto-generated headers in EF migrations.
