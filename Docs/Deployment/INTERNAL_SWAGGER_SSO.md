# Internal Swagger + Corporate SSO (Aerospace-Style)

HelloblueGK treats Swagger as **internal documentation** in production:

- **Not public** — unauthenticated requests to `/swagger` receive `401` or an SSO redirect
- **Corporate SSO** — optional Azure AD / Okta via OpenID Connect
- **VPN / Zero Trust** — optional extra layer in front of Render (recommended for defense/aerospace programs)

---

## Current behavior (no SSO configured yet)

| Environment | Swagger access |
|-------------|----------------|
| **Production** | Requires JWT — login at `/api/v1/Auth/login`, paste token in Swagger **Authorize** |
| **Development** | Public at `http://localhost:5000/swagger` when `Documentation:AllowPublicInDevelopment` is `true` |

---

## Option A — App-level SSO (Azure AD / Entra ID)

### 1. Register an app in Azure Portal

1. **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `HelloblueGK Internal Docs`
3. Redirect URI (Web):
   - `https://hellobluegk.onrender.com/swagger/oauth2-redirect.html`
   - `https://hellobluegk.onrender.com/api/v1/Account/sso-callback`
4. Create a **client secret** (Certificates & secrets)
5. Note: **Tenant ID**, **Client ID**, **Client secret**

### 2. Set Render environment variables

On **HelloblueGK** → **Environment**, add:

| Key | Value |
|-----|--------|
| `Authentication__OpenIdConnect__Enabled` | `true` |
| `Authentication__OpenIdConnect__Authority` | `https://login.microsoftonline.com/YOUR-TENANT-ID/v2.0` |
| `Authentication__OpenIdConnect__ClientId` | Your Azure app client ID |
| `Authentication__OpenIdConnect__ClientSecret` | Your Azure app secret |
| `Authentication__OpenIdConnect__Audience` | Same as Client ID (or API app ID URI) |
| `Authentication__OpenIdConnect__ApiScope` | `api://YOUR-CLIENT-ID/.default` |

Save and redeploy.

### 3. Access internal docs

1. Open `https://hellobluegk.onrender.com/api/v1/Account/login`
2. Sign in with your corporate Microsoft account
3. You are redirected to `/swagger`
4. Click **Authorize** in Swagger to use OAuth2 for **Try it out**

### 4. Logout

`https://hellobluegk.onrender.com/api/v1/Account/logout`

---

## Option B — VPN / Zero Trust in front of Render (recommended for aerospace)

Render does not provide a traditional corporate VPN. Teams typically add **Zero Trust** at the edge:

### Cloudflare Access (common pattern)

1. Put your domain behind Cloudflare
2. **Zero Trust** → **Access** → **Applications** → protect `hellobluegk.yourdomain.com`
3. Require identity (Google Workspace, Azure AD, Okta)
4. Only authenticated staff reach Render; Swagger stays internal even before app SSO

### Tailscale

1. Run Tailscale on engineer laptops
2. Use a **subnet router** or **Tailscale Funnel** with ACLs so only your tailnet reaches the service

This matches how many aerospace teams gate internal engineering tools without exposing `*.onrender.com` broadly.

---

## Recommended stack (aerospace-style)

```
Engineer laptop
    → Corporate SSO (Cloudflare Access or Tailscale)
        → Render (HelloblueGK)
            → App SSO for /swagger (Azure AD OIDC)
                → JWT / OAuth for API Try it out
```

Layer 1 (edge) = who can reach the host  
Layer 2 (app) = who can open Swagger  
Layer 3 (API) = who can call simulations / launches

---

## Okta (instead of Azure AD)

Use the same env vars; set `Authority` to your Okta issuer, e.g.:

`https://YOUR-ORG.okta.com/oauth2/default`

Redirect URIs must match your Okta app settings.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `/swagger` returns 401 | Expected without SSO/JWT — use `/api/v1/Account/login` or JWT Authorize |
| SSO redirect loop | Check redirect URIs in IdP match exactly |
| `Account/login` returns 404 | `Authentication__OpenIdConnect__Enabled` is not `true` |
| Try it out fails after SSO | Click Swagger **Authorize** and complete OAuth2 flow |

---

## Security notes

- Do **not** re-enable public registration in production for convenience
- Provision API users through admin process; use SSO only for documentation access unless you map SSO users to app accounts
- Rotate `ClientSecret` on the same schedule as other production secrets
