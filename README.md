# UnHost

UnHost is a self-hosted application deployment platform for Docker Swarm, built to explore .NET Native AOT.

See the [roadmap](docs/roadmap.md) for the first release and follow-up work, and the [glossary](CONTEXT.md) for project terminology.

## Tests and CI

Install .NET 11, Node.js 24, and the UI dependencies, then run the C# unit and integration tests from the repository root:

```powershell
npm.cmd ci --prefix unhost-ui
dotnet test UnHost.slnx --configuration Release -p:ShouldRunNpmInstall=false -p:ShouldRunBuildScript=false --filter "Category!=Smoke" --logger "trx;LogFileName=backend.trx" --results-directory TestResults
npm.cmd run build --prefix unhost-ui
npm.cmd run test:ci --prefix unhost-ui
```

The C# suite uses xUnit, the real Minimal API through `WebApplicationFactory`, and isolated SQLite databases. It checks bootstrap validation and concurrency, authentication, lockout, CSRF, migration history, and conflicting database writes. `UnHost:DataDirectory` selects the directory for the database and cookie keys, and defaults to the working directory. Each test installation uses its own temporary directory.

The TypeScript suite uses Vitest. Auth-client unit tests mock HTTP responses. Vue integration tests mount the real app and auth client to check setup, login, session restoration, duplicate submission, errors, and logout. Tests are included in the TypeScript type check. Use `npm.cmd run test:watch --prefix unhost-ui` during development.

Run the published Linux Native AOT smoke test with Docker Desktop in Linux container mode:

```powershell
docker build --tag unhost-ci:local .
$env:UNHOST_SMOKE_IMAGE = 'unhost-ci:local'
dotnet test UnHost.slnx --configuration Release -p:ShouldRunNpmInstall=false -p:ShouldRunBuildScript=false --filter "Category=Smoke" --logger "trx;LogFileName=smoke.trx" --results-directory TestResults
```

The smoke test starts a temporary container and volume. It checks the bundled UI and assets, bootstrap and password errors, account creation, protected API access, login, logout, and operator/session persistence after deleting and recreating the container. It removes its container and volume when finished. This HTTP-level session check replays the issued cookie directly.

Run the Playwright browser tests against the same published image:

```powershell
npm.cmd exec --prefix unhost-ui -- playwright install chromium
$env:UNHOST_SMOKE_IMAGE = 'unhost-ci:local'
npm.cmd run test:e2e --prefix unhost-ui
```

Playwright starts a fresh container and volume for each test and drives headless Chromium through the actual UI. It checks bootstrap and password errors, account creation, rejected sign-in, anonymous access, login, reload, session restoration after container recreation, and logout. The browser manages cookies normally and all API requests reach the Native AOT executable. No responses or application modules are mocked. Each test cleans up its container and volume. Reports go to `TestResults/browser.xml` and `TestResults/playwright-report`; failed tests retain screenshots, traces, and container logs in the report artifacts. Browser tests are included in the TypeScript type check.

[CI](.github/workflows/ci.yml) runs on pull requests, pushes to `main`, and manual dispatch. C# and TypeScript checks must pass before it builds the Linux x64 Native AOT image and runs the HTTP smoke tests and Playwright browser tests against it. Successful runs upload the test reports and a `unhost-linux-x64` artifact containing the executable and UI. Pushes to `main` also publish the exact tested image to `ghcr.io/chopstickleg/unhost:latest` and `ghcr.io/chopstickleg/unhost:sha-<full-commit-sha>`, using the workflow's `GITHUB_TOKEN`. Pull requests do not publish images. The repository must allow GitHub Actions and grant its token package write access for publication.

## Run locally with Docker Compose

Use Docker Desktop with Linux containers. From the repository root, create a local bootstrap secret once:

```powershell
New-Item -ItemType Directory -Force .secrets | Out-Null
if (-not (Test-Path .secrets/bootstrap.txt)) {
    $secretBytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($secretBytes)
    [System.IO.File]::WriteAllText("$PWD/.secrets/bootstrap.txt", [Convert]::ToBase64String($secretBytes))
}
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). Compose builds the existing Dockerfile, including the Vue UI, and runs the API in Development mode. Set `$env:UNHOST_PORT = '8081'` before starting to use another port. HTTP access is bound to loopback for local development; use HTTPS for remote access.

On a fresh volume, create the operator account from another PowerShell window. Replace the example password before running:

```powershell
$body = @{
    email = 'admin@example.com'
    password = 'Replace-With-Your-Own-Password1!'
    bootstrapSecret = (Get-Content .secrets/bootstrap.txt -Raw).TrimEnd("`r", "`n")
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8080/api/auth/setup' -ContentType 'application/json' -Body $body
```

Then sign in through the UI. The secret stays out of Git and the image build context and mounts read-only through a Compose secret.

Re-run `docker compose up --build` after code changes. This setup runs the published Native AOT executable, so it does not provide hot reload or managed debugger attachment. Use `docker compose logs -f unhost` to follow logs. `docker compose down` stops the app and keeps the SQLite database and cookie keys in the `unhost-data` volume. To erase local data and repeat first-run setup, use `docker compose down --volumes`.

## First-run setup

Mount a persistent volume at `/data`. UnHost applies the SQLite schema at startup and stores the database and cookie keys on that volume.

Before setup, generate a random bootstrap secret and save it outside the checkout. Mount it as a read-only file that the container user can read. Set `UnHost__BootstrapSecretFile` to the path inside the container. Send the secret as `bootstrapSecret` alongside `email` and `password` in a JSON request to `POST /api/auth/setup` over HTTPS. After setup succeeds, remove the secret mount and its configuration.

## Add a database migration

From the repository root, restore the pinned EF Core CLI and scaffold a migration after changing the Infrastructure model:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add AddClusters --project Unhost.Infrastructure --startup-project Unhost.Infrastructure --output-dir Database/Migrations
```

Export the SQL for that single step. Replace `InitialCreate` with the previous migration name and `<migration-id>` with the full generated filename stem, including its timestamp.

```powershell
dotnet tool run dotnet-ef migrations script InitialCreate AddClusters --no-transactions --project Unhost.Infrastructure --startup-project Unhost.Infrastructure --output "Unhost.Infrastructure/Database/Migrations/Sql/<migration-id>.sql"
```

Review and check in the migration, model snapshot, and SQL together. The native app embeds the SQL, applies it inside a transaction, and uses EF's migration history to resume at the next pending migration. Use `0` as the starting migration when exporting the initial script.

If you manually provisioned a development database before the initial migration existed, use a fresh volume or back up and baseline the existing database before starting this version.
