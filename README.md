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

## How to run it: pick ONE option

There are **two independent ways** to run the app. You only need one. Each has its own section below, and **each uses different ports**:

| | [Option 1: Docker](#option-1-run-with-docker) (recommended) | [Option 2: .NET SDK, no Docker](#option-2-run-locally-with-the-net-sdk-no-docker) |
|---|---|---|
| You need | Git + Docker | Git + .NET 10 SDK |
| Start command | `docker compose up --build -d` | `dotnet run --project ECommercePlus` |
| **Open this URL** | **[https://localhost:8443](https://localhost:8443)** | **[https://localhost:7268](https://localhost:7268)** |
| HTTP port (redirects to HTTPS) | 8080 | 5213 |
| Database location | Docker volume `ecommerceplus-data` | `ECommercePlus/App_Data/` folder |
| HTTPS certificate | Self-signed (browser warns once) | .NET dev certificate (trusted) |

> ⚠️ Don't mix them up: **8443 is only for Docker**, and **7268 is only for `dotnet run`**. Both can run at the same time; each has its own separate database.

Supported on Windows, macOS (Intel and Apple Silicon), and Linux. All commands work in bash, zsh, and PowerShell.

### Get the code (both options)

Requires [Git](https://git-scm.com/downloads).

```bash
git clone https://github.com/joe-knd/ECommercePlus.git
cd ECommercePlus
```

All commands below are run from this folder (the repository root, which contains `compose.yaml`).

---

## Option 1: Run with Docker

### 1.1 Requirements

| Tool | Version | Download |
|---|---|---|
| Docker Desktop (Windows / macOS) or Docker Engine (Linux) | Docker 24+ with **Compose v2** (`docker compose`, not `docker-compose`) | [docker.com](https://docs.docker.com/get-docker/) |

**You don't need the .NET SDK.** The Dockerfile builds the app inside the official .NET 10 SDK image and runs it in the smaller ASP.NET runtime image. Ports **8443** and **8080** must be free.

### 1.2 Start

```bash
docker compose up --build -d
```

- `--build` builds the image from the source code. Use it the first time and whenever the code changes.
- `-d` (detached) runs the container in the background and gives you the terminal back. Leave out `-d` to see the logs live in the terminal; Ctrl+C then stops the app.

The first build takes a few minutes. The app is ready when `docker compose logs` shows `ECommercePlus is ready. Open https://localhost:8443 in your browser.`

### 1.3 Open the app

Open **[https://localhost:8443](https://localhost:8443)**. Plain [http://localhost:8080](http://localhost:8080) redirects there.

- The container creates a **self-signed certificate** for `localhost` the first time it starts, so the browser warns *"Your connection isn't private"* once. Click **Advanced → Continue to localhost (unsafe)** in Edge/Chrome, or **Show Details → visit this website** in Safari. In Chrome, if there is no *Continue* link, click anywhere on the page and type `thisisunsafe`. To avoid the warning, see [1.7](#17-optional-use-the-trusted-net-dev-certificate-in-docker).
- Use **`localhost`** in the address. The log line `Now listening on: https://[::]:8443` shows what the server listens on (`[::]` means "all network interfaces"). It isn't a URL you can open.

Then [sign in as the admin](#signing-in-as-the-admin-both-options).

### 1.4 Stop, restart, and reset

| Goal | Command |
|---|---|
| Build and start (first time, or after code changes) | `docker compose up --build -d` |
| Start again without rebuilding | `docker compose up -d` |
| Check that it's running | `docker compose ps` |
| Follow the logs (Ctrl+C stops following, not the app) | `docker compose logs -f` |
| Stop (data is kept) | `docker compose down` |
| Stop and **erase all data** (database, keys, certificate) | `docker compose down -v` |
| Start over with a fresh database | `docker compose down -v` then `docker compose up -d` |

Data is stored in the named volume `ecommerceplus-data`, so it survives `docker compose down` and rebuilds. Only `down -v` erases it.

### 1.5 Without Compose (plain Docker)

```bash
docker build -f ECommercePlus/Dockerfile -t ecommerceplus .
docker run --rm -p 8443:8443 -p 8080:8080 -v ecommerceplus-data:/app/App_Data ecommerceplus
```

### 1.6 Troubleshooting (Docker)

| Symptom | Cause and fix |
|---|---|
| Nothing at `https://localhost:7268` | 7268 is the port for Option 2. With Docker, open [https://localhost:8443](https://localhost:8443). |
| `ERR_HTTP2_PROTOCOL_ERROR` or **400 Bad Request** | You opened `https://[::]:8443` or `http://[::]:8080` (copied from the log). `[::]` is a listen address, not a valid host. Open [https://localhost:8443](https://localhost:8443). |
| *Your connection isn't private* / `NET::ERR_CERT_AUTHORITY_INVALID` | Expected with the self-signed certificate. Continue as described in [1.3](#13-open-the-app), or use [1.7](#17-optional-use-the-trusted-net-dev-certificate-in-docker). |
| *This site can't be reached* / connection refused | The container isn't running or is still starting. Check `docker compose ps` and `docker compose logs -f`, and wait for the *is ready* line. |
| *Port is already allocated* | Another program uses 8443 or 8080. Stop it, or change the left side of the mappings in `compose.yaml` (e.g. `"9443:8443"`) and open that port instead. |
| Admin password `P@ssw0rd!123` isn't accepted | The volume is from an older run that already has an admin. Run `docker compose down -v` and start again. |

### 1.7 Optional: use the trusted .NET dev certificate in Docker

Skip this unless the browser warning bothers you. It's the only Docker step that needs the .NET SDK on the host. Export the dev certificate once, then start with the `compose.devcert.yaml` override instead of the command in [1.2](#12-start). Use **https://localhost:8443** as usual.

macOS / Linux (bash, zsh):

```bash
dotnet dev-certs https -ep ~/.aspnet/https/ecommerceplus.pfx -p <choose-a-password>
dotnet dev-certs https --trust
CERT_PASSWORD=<choose-a-password> docker compose -f compose.yaml -f compose.devcert.yaml up --build -d
```

Windows (PowerShell):

```powershell
dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\ecommerceplus.pfx" -p <choose-a-password>
dotnet dev-certs https --trust
$env:CERT_PASSWORD = "<choose-a-password>"
docker compose -f compose.yaml -f compose.devcert.yaml up --build -d
```

On Linux, `dotnet dev-certs https --trust` only trusts the certificate for some browsers and tools. See the .NET docs for your distribution.

---

## Option 2: Run locally with the .NET SDK (no Docker)

### 2.1 Requirements

| Tool | Version | Download |
|---|---|---|
| .NET SDK | **10.0** (`dotnet --version` shows `10.0.x`) | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |

**You don't need Docker.** Ports **7268** and **5213** must be free.

### 2.2 Trust the development certificate (once per machine)

```bash
dotnet dev-certs https --trust
```

This creates and trusts the local .NET HTTPS development certificate, so the browser shows no warning. On Linux it only trusts it for some browsers and tools; see the .NET docs for your distribution.

### 2.3 Start

```bash
dotnet run --project ECommercePlus
```

The app is ready when the console shows `ECommercePlus is ready. Open https://localhost:7268 in your browser.`

**You don't need to run migrations.** Pending EF Core migrations, including the Identity tables, are applied automatically at startup, followed by the admin and CSV seeding.

### 2.4 Open the app

Open **[https://localhost:7268](https://localhost:7268)**. Plain [http://localhost:5213](http://localhost:5213) redirects there.

Then [sign in as the admin](#signing-in-as-the-admin-both-options).

### 2.5 Stop and reset

- **Stop:** press Ctrl+C in the terminal running `dotnet run`.
- **Start over with a fresh database:** stop the app, delete the `ECommercePlus/App_Data` folder, and run it again.
  - macOS / Linux: `rm -rf ECommercePlus/App_Data`
  - Windows (PowerShell): `Remove-Item -Recurse -Force ECommercePlus\App_Data`

### 2.6 Troubleshooting (local run)

| Symptom | Cause and fix |
|---|---|
| Nothing at `https://localhost:8443` | 8443 is the port for Option 1 (Docker). With `dotnet run`, open [https://localhost:7268](https://localhost:7268). |
| *Your connection isn't private* | The dev certificate isn't trusted. Run `dotnet dev-certs https --trust` and restart the browser. |
| `The current .NET SDK does not support targeting .NET 10.0` | Install the .NET 10 SDK and check with `dotnet --version`. |
| *Address already in use* | Another program uses 7268 or 5213. Stop it, or change `applicationUrl` in `ECommercePlus/Properties/launchSettings.json`. |
| Admin password `P@ssw0rd!123` isn't accepted | The local database is from an older run. Stop the app and delete `ECommercePlus/App_Data` (see [2.5](#25-stop-and-reset)). |

---

## Signing in as the admin (both options)

On first start, the app creates the `Admin` role and this account:

| Email | Temporary password |
|---|---|
| `admin@ecommerceplus.local` | `P@ssw0rd!123` |

You must **change the password at first sign-in**. The default is set in `appsettings.json` (`AdminSeed:Password`) and can be overridden with the `AdminSeed__Password` / `AdminSeed__Email` environment variables. If you set `AdminSeed__Password` to an empty value, a random password is generated instead and written to `App_Data/initial-admin-password.txt` (the file is deleted after the first password change).

## Running the tests

Requires the .NET 10 SDK. From the repository root:

```bash
dotnet test
```

There are 93 tests: unit tests for import, content safety (script and SQL-injection detection), search, CRUD, checkout, payments, and temporary passwords, plus integration tests that boot the full app over HTTPS with `WebApplicationFactory` (sign-in, forced password change, role checks, the HTTP→HTTPS redirect, and Secure cookies).

## Optional: EF Core tooling (only when you change the data model)

This is only for developers who change the entities; it isn't needed to run the app. The last argument after `migrations add` is a descriptive name you choose for the new migration (`AddProductBrand` below is just an example). The existing migrations are `InitialCreate` and `AddIdentity`.

```bash
dotnet tool restore
dotnet ef migrations add AddProductBrand --project ECommercePlus --output-dir Data/Migrations
```

The next app start applies it automatically.

---

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
| Health | `/health` | Liveness and database check |

Shopping (search, cart, checkout) doesn't require an account. The *Admin* menu appears only for users in the `Admin` role, and any admin URL returns *Access denied* for other signed-in users or redirects anonymous users to sign in.

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
- EF Core gives **migrations**, LINQ queries that are parameterized (which prevents SQL injection at the database level; on top of that, injection-like input is rejected by validation, see [Content safety](#content-safety-reject-script-and-sql-injection-content)), and a provider-agnostic model. Moving to PostgreSQL or SQL Server would mostly be a provider and connection-string change.
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
| `<script>alert('xss')</script>` name (`XS-001`) | **Rejected**: HTML/script content isn't allowed in product text. See [Content safety](#content-safety-reject-script-and-sql-injection-content). |
| `Robert'); DROP TABLE products;--` name (`SQL-001`) | **Rejected**: SQL-injection-like pattern. |
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

With the sample file on an empty database, the result is: **97 rows → 86 created, 6 rejected, 5 ignored**. Rejected: `XS-001` (script), `SQL-001` (SQL injection), `YM-015` (`free` price), `DL-007` (negative stock), `HD-099` and `WS-001` (empty name). Ignored: 2 blank rows and 3 superseded duplicates. This is covered by tests.

### Content safety: reject script and SQL-injection content
Product name, description, and category are checked by `ContentSafety` inside `ProductRules.Validate`. The same rules apply to **CSV import and to the admin create/edit forms**, so unsafe text can't get in either way. A rejected CSV row shows the field and the reason in the import report; the form shows the error next to the field.

| Rejected | Examples |
|---|---|
| HTML tags and comments | `<script>…`, `<img …>`, `</title>`, `<!-- -->` |
| Script vectors | `javascript:`, `vbscript:`, `data:text/html`, event handlers like `onerror=` / `onclick=`, HTML entities like `&#60;`, URL-encoded `%3C` |
| SQL-injection patterns | `DROP/TRUNCATE/ALTER TABLE`, `UNION SELECT`, `INSERT INTO`, `DELETE FROM`, `UPDATE … SET`, `'); …`, `; DROP …`, `' OR '1'='1`, `'--`, `;--`, `/* … */`, `EXEC(`, `xp_…` |
| Control characters | Invisible characters other than tab and line breaks |

The patterns are deliberately narrow so normal product text still works: apostrophes (`Kids' toy`), inch marks (`27" monitor; IPS`), `&`, `%`, `<`/`>` used as comparisons (`< 5 USD`), `™`, and everyday words like *select*, *update*, or *drop* in a sentence are all accepted. The test suite covers both lists.

This is **defense in depth**, not the only protection: all queries stay parameterized, all output is HTML-encoded by Razor, and the Content-Security-Policy blocks inline scripts. Even if a pattern were missed, it would be stored and shown as plain text, never executed.

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
- **Temporary passwords**: the seeded admin starts with a well-known default (`P@ssw0rd!123`) for an easy first run. Users created or reset by an admin get a random password that meets the policy and is shown once. A `must_change_password` claim and a middleware restrict the user to *Change password* until they set their own.
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
| Script / SQL-like text | Store as-is and rely only on output encoding, or strip the bad parts (sanitize) | Output encoding alone is safe for this app, but the data could later reach other systems (exports, emails, reports) that don't encode. Silently stripping changes the data without anyone noticing. Rejecting with a clear reason is the most transparent. |
| Cart storage | DB-persisted cart / client cookie | A DB cart is needed for multi-device carts, which requires user accounts. A cookie-only cart could be tampered with. A session holds only IDs and quantities and is re-validated on every request. |
| Payment | Real sandbox (Stripe test mode) | Not required, and it would need API keys. The `IPaymentGateway` boundary makes swapping it in a single class. |
| Auth | Duende IdentityServer / OpenIddict / Entra ID | An identity server is only worth it with several clients or APIs that need tokens. ASP.NET Core Identity with cookies covers one MVC app. |
| Auth | Random admin password only (no default) | Safer, but it makes the first run harder. The default password is only usable until the first sign-in forces a change, and it can be overridden or disabled via configuration. In a real deployment, set `AdminSeed__Password` from a secret store. |
| HTTPS in Docker | HTTP only behind a TLS-terminating proxy | That's the common production setup, but the challenge runs the container directly, so the app serves HTTPS itself. |

## Known limitations / next steps
- Customers check out as guests, so there is no "my orders" page across sessions. Next step: optional customer accounts with orders linked to the user.
- No email confirmation or self-service password reset (admins reset passwords). Next step: an `IEmailSender` and Identity's token providers.
- The session uses an in-memory store, so carts are lost on restart and don't work across multiple instances. Next step: a distributed cache (Redis) or DB-backed carts.
- SQLite allows only one writer at a time. That's fine for a single instance. For horizontal scaling, move to PostgreSQL.
- Payment is charged inside the stock-reservation transaction, which is acceptable with an in-process fake. With a real provider, use authorize/capture plus an outbox or saga so the database lock isn't held during a network call.
- Single currency (USD) and no taxes or shipping costs.
- Data Protection keys are stored unencrypted on the volume (logged as a warning). In production, protect them with a certificate or a key vault.



