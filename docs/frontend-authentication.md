# Frontend authentication

## Session storage

OpsManager uses two token types with different storage rules:

- The short-lived JWT access token is kept only in the frontend's in-memory
  session store. It is never written to `localStorage`, `sessionStorage`, or a
  JavaScript-readable cookie.
- The rotating refresh token is issued by the API as a persistent `HttpOnly`
  cookie. The browser stores and sends it; frontend JavaScript cannot read it.

This provides a long-lived login without making bearer credentials available
to injected scripts. The default refresh lifetime is 30 days and can be
configured with `Jwt__RefreshTokenDays`.

## Login and reload flow

Login and registration requests use `credentials: "include"`, allowing the
browser to accept the API's refresh cookie. The returned access token is placed
in memory.

After a page reload, the memory store is empty. The auth provider calls the
realm-specific refresh endpoint:

- Tenant: `POST /api/v1/auth/refresh`
- Platform: `POST /api/v1/platform/auth/refresh`

The browser attaches the `HttpOnly` cookie. The API validates and rotates the
refresh token, returns a new access token, and replaces the cookie. Concurrent
refresh attempts are deduplicated. Tenant sessions then load `/auth/me` to
hydrate the current user, organization, role, departments, and subscription
access.

When an authenticated API call returns 401, the same refresh-and-retry flow is
attempted once. A failed refresh clears the in-memory identity and route guards
return the user to the appropriate login screen.

## Cookie and deployment requirements

The refresh cookie is:

- `HttpOnly`
- `Secure` outside Development
- `SameSite=Strict`
- scoped to `/api/v1`
- persistent until the refresh-token expiry

All frontend API requests use `credentials: "include"`. Credentialed CORS must
allow the exact frontend origin. In local development, use the same hostname
for both applications, such as `http://localhost:3000` and
`http://localhost:5291`; mixing `localhost` and `127.0.0.1` prevents a Strict
cookie from being sent.

Production must use HTTPS. If the frontend and API are intentionally deployed
cross-site rather than as same-site hosts, the cookie and CSRF design must be
reviewed before changing `SameSite`.

## Logout

Logout sends the refresh cookie to the realm-specific logout endpoint. The API
revokes the stored refresh-token session and expires the cookie. The frontend
clears its in-memory access token even if the network request fails.
