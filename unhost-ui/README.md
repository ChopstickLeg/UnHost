# UnHost UI

Vue 3, Tailwind CSS v4, and shadcn-vue. Black theme, locally bundled fonts.

## Development

```powershell
npm.cmd ci
npm.cmd run dev
```

Vite runs on port 54902 and proxies `/api` to `http://localhost:5189`. Start the API separately. Override the target with `$env:UNHOST_API_URL` when needed. Use HTTPS for deployed instances.

Vue Router provides separate `/setup`, `/login`, and `/dashboard` pages. Visiting `/` selects the appropriate page using `/api/status` and the current session. Unknown URLs show a 404 page. Setup creates the operator account with the configured bootstrap secret, then returns to login. Sessions use the server's HttpOnly cookie.

ASP.NET Core serves the frontend for direct page loads and refreshes. Missing API endpoints and static assets still return server 404s.

## Build

```powershell
npm.cmd run build
```

Type-checks and builds into `Unhost.API/wwwroot` for the .NET container.

Shared components are in `src/components/ui`; theme tokens are in `src/assets/main.css`. Add components with `npm.cmd exec -- shadcn-vue add <component>`. License notices ship in `public/licenses`.
