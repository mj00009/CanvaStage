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

Canva currently offers Public integrations and Enterprise-only Private integrations. A Public integration can be used in draft state for individual use and testing; submission for review is required to make it broadly available. A loopback redirect can be used for local development, but it cannot remain when a Public integration is submitted for review. CanvaStage's local flow is intended for a self-managed deployment, not a broadly published Canva integration.

## Create and configure the integration

1. Open CanvaStage and select **Settings & Diagnostics**. Expand **Canva connection — one-time setup**.

   In the unreleased #34 UI follow-up, this section is an always-visible **Canva connection** card and no expansion is needed. v0.11.2 still uses the expandable section.
2. Choose **Copy redirect URL**. The current URL is:

   ```text
   http://127.0.0.1:27843/callback/
   ```

   Use the value displayed by the installed app if it ever differs from this document.

3. Open the [Canva Developer Portal](https://www.canva.dev/developers/integrations) and sign in.
4. Open **Your integrations**, choose **Create an integration**, accept Canva’s developer terms, and choose the integration type available and appropriate for the church account.
5. On **Configure your integration**, enter an identifying name.
6. Copy the **Client ID**.
7. Choose **Generate secret** and save the **Client Secret** immediately. Canva does not show it again.
8. Open **Scopes** and enable only:

   - `design:meta:read`
   - `design:content:read`

9. Open **Authentication**. Under **Authorized redirects**, add the exact CanvaStage redirect URL. Use `127.0.0.1`; Canva does not allow `localhost` for this local redirect.
10. Return to CanvaStage. Enter the Client ID and Client Secret, then choose **Save configuration**.
11. Choose **Connect Canva**. Complete the authorization prompt in the browser and return to CanvaStage.
12. Open **Setup → 1. Load the Canva deck** and choose **Refresh**. If presentations appear, setup is complete.

CanvaStage uses OAuth 2.0 Authorization Code with PKCE. The browser returns the authorization result to CanvaStage through the loopback address; it does not require an internet-accessible server on the Windows computer.

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

- [Creating integrations](https://www.canva.dev/docs/connect/creating-integrations/)
- [Authentication](https://www.canva.dev/docs/connect/authentication/)
- [Connect API scopes](https://www.canva.dev/docs/connect/appendix/scopes/)
- [Security recommendations](https://www.canva.dev/docs/connect/guidelines/security/)
- [Canva API and App Developer Terms](https://www.canva.com/policies/canva-developer-terms/)
