# Canva setup

CanvaStage uses the Canva Connect APIs to list and download presentations. Each Windows user who runs CanvaStage must configure and authorize a Canva integration.

## Important security limitation

Canva's [REST API security recommendations](https://www.canva.dev/docs/apps/rest-apis/security-recommendations/) say integrations must be associated with a web app and that desktop apps are unsupported because they are difficult places to protect client credentials.

CanvaStage is a self-managed local deployment on a church computer and shared operator account. Windows DPAPI reduces local exposure of the Client Secret and OAuth tokens; it does not make this a Canva-supported desktop integration. Do not publish the credentials, copy them into issue reports, or reuse them for another integration.

## Before you begin

You need:

- access to the church Canva account;
- multi-factor authentication enabled on that account;
- permission to create an integration in the Canva Developer Portal;
- CanvaStage running under the Windows account that will operate it.

Canva now unifies its developer surfaces under the Developers SDK. New REST API setup uses **Your apps → Outside Canva**; older integrations can still expose the previous configuration screens. Private apps require Canva Enterprise. Public distribution requires review, and loopback redirects are restricted to development/testing and must be removed afterward. CanvaStage's local flow is self-managed, not a Canva-supported public desktop integration. See the [current quickstart](https://www.canva.dev/docs/apps/quickstart/).

## Create and configure the integration

1. Open CanvaStage and select **Settings & Diagnostics → Canva connection**. In v0.11.3 this card is always visible. On v0.11.2, expand **Canva connection — one-time setup** instead.
2. Choose **Copy redirect URL**. The current URL is:

   ```text
   http://127.0.0.1:27843/callback/
   ```

   Use the value displayed by the installed app if it ever differs from this document.

3. Open [Your apps in the Canva Developer Portal](https://www.canva.com/developers/apps) and sign in.
4. Choose **Create an app**, enter a name, choose the appropriate audience, and follow Canva's developer terms.
5. Open **Outside Canva → Start integrating**. Under **Configuration → Integration methods**, confirm **Canva REST APIs** is enabled.
6. Under **Credentials**, copy the **Client ID**.
7. Choose **Generate secret** and save the **Client Secret** immediately. Canva does not show it again.
8. In the REST API **Scopes**, enable **Read** only for design:meta and design:content:

   - `design:meta:read`
   - `design:content:read`

9. Open **Outside Canva → Redirect URLs** and enter the exact CanvaStage redirect URL in **URL 1** (or another authorized redirect field). Keep `127.0.0.1`, not `localhost`. Return navigation is not required.
10. Return to CanvaStage. Enter the Client ID and Client Secret, then choose **Save configuration**.
11. Choose **Connect Canva**. Complete the authorization prompt in the browser and return to CanvaStage.
12. Open **Setup → Advanced → 1. Load the Canva deck** and choose **Refresh**. Apply the intended presentation and choose the Extended confidence display in step 2. After this first-time setup, **Setup → Simple → Start setup** reuses those choices and downloads content only when necessary.

CanvaStage uses OAuth 2.0 Authorization Code with PKCE. The browser returns the authorization result to CanvaStage through the loopback address; it does not require an internet-accessible server on the Windows computer.

For an existing integration, the legacy route can be **Your integrations → Configure your integration → Scopes / Authentication → Authorized redirects**. Use the equivalent fields there. Do not recreate a working saved integration simply because the portal labels changed. Do not copy the starter kit's broader scopes, return-navigation configuration, or example redirect: CanvaStage needs only its two Read scopes and displayed loopback URL.

## Reopening CanvaStage

The Client Secret and tokens are saved for the current Windows user. **Saved securely** should appear when the app is reopened, and the secret does not need to be entered again.

Use **Replace saved secret** only after generating a replacement in the Developer Portal. Use **Disconnect** to revoke the Canva connection when possible and remove all locally protected Canva credentials.

## Common setup problems

### The browser cannot return to CanvaStage

- Confirm the authorized redirect matches the displayed URL exactly, including the trailing slash.
- Close another application that may be using port 27843.
- Start CanvaStage normally rather than as administrator.
- Retry **Connect Canva**.

### Canva rejects the requested access

Confirm both required scopes are enabled in the Developer Portal. Adding a scope in CanvaStage’s authorization request does not enable it in the integration configuration.

### No presentations appear

- Confirm the connected account can open the presentation.
- Choose **Test connection**, then **Refresh**.
- Shared designs must be accessible to the connected account.

### The connection works only for one Windows account

This is expected. DPAPI protects the credentials for the Windows user that saved them. Configure CanvaStage again when using a different Windows account.

## Official Canva references

- [Creating and configuring REST API apps](https://www.canva.dev/docs/apps/quickstart/)
- [Authentication](https://www.canva.dev/docs/apps/rest-apis/authentication/)
- [Security recommendations](https://www.canva.dev/docs/apps/rest-apis/security-recommendations/)
- [Canva API and App Developer Terms](https://www.canva.com/policies/canva-developer-terms/)
