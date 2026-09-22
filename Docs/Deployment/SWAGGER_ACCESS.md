# Restricting Swagger in your own deployment

By default this application treats its Swagger UI as documentation for authenticated users rather
than as a public page. This guide describes how to configure that in a deployment you control.

Replace every placeholder — `api.example.com`, `YOUR-TENANT-ID`, `YOUR-CLIENT-ID` — with your own
values. Nothing here is specific to any particular hosted instance.

## Default behaviour

| Environment | Swagger access |
|-------------|----------------|
| Development | Served at `http://localhost:5000/swagger` when `Documentation:AllowPublicInDevelopment` is `true` |
| Production | Requires a bearer token; obtain one from `/api/v1/Auth/login` and use Swagger's **Authorize** button |

`/Health` is the only endpoint that is unauthenticated in either case.

## Optional: OpenID Connect

The application can sit behind any standards-compliant OpenID Connect provider. Configuration is
by environment variable, so no secret needs to enter a config file:

| Variable | Value |
|----------|-------|
| `Authentication__OpenIdConnect__Enabled` | `true` |
| `Authentication__OpenIdConnect__Authority` | Your issuer URL |
| `Authentication__OpenIdConnect__ClientId` | Your client ID |
| `Authentication__OpenIdConnect__ClientSecret` | Your client secret |
| `Authentication__OpenIdConnect__Audience` | Your API audience, often the client ID |
| `Authentication__OpenIdConnect__ApiScope` | The scope your API expects |

Issuer URLs follow each provider's own convention — for example
`https://login.microsoftonline.com/YOUR-TENANT-ID/v2.0` for Microsoft Entra ID, or
`https://YOUR-ORG.okta.com/oauth2/default` for Okta.

Register two redirect URIs with your provider, both on your own domain:

```
https://api.example.com/swagger/oauth2-redirect.html
https://api.example.com/api/v1/Account/sso-callback
```

Once enabled, `/api/v1/Account/login` begins the sign-in flow and `/api/v1/Account/logout` ends
it. Swagger's **Authorize** button then completes an OAuth2 flow for **Try it out**.

## Optional: restrict access at the network edge

Application-level sign-in controls who can open Swagger, but not who can reach the host. If you
need the second property, put an identity-aware proxy or a private network in front of the
service. Any of the common approaches work — an identity-aware access proxy in front of your
domain, or a mesh VPN with access controls so that only your own devices can route to the
service. Both are configured entirely outside this application.

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| `/swagger` returns 401 | Expected without a token or SSO session; sign in or use **Authorize** |
| SSO redirect loop | Redirect URIs registered with the provider do not match exactly |
| `Account/login` returns 404 | `Authentication__OpenIdConnect__Enabled` is not `true` |
| **Try it out** fails after signing in | Complete the OAuth2 flow via Swagger's **Authorize** button |

## Notes

- Keep public registration disabled in production; `Auth:AllowPublicRegistration` defaults to
  `false`.
- Signing in to read documentation does not by itself grant API access. Map provider identities to
  application accounts deliberately.
- Rotate the client secret on whatever schedule you apply to your other production secrets.
