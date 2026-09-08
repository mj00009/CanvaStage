# Canva setup

CanvaStage uses the Canva Connect APIs to list and download presentations. Each Windows user who runs CanvaStage must configure and authorize a Canva integration.

## Important security limitation

Canva’s current security guidance recommends a web application with a secure backend for Connect API integrations. It specifically does not recommend storing client credentials in desktop applications.

CanvaStage is intended for a self-managed church computer and shared operator account. It reduces local exposure by protecting the Client Secret and OAuth tokens with Windows DPAPI, but this architecture is not a substitute for Canva’s recommended server-side model. Do not publish the credentials, copy them into issue reports, or reuse them for another integration.

## Before you begin

You need:

- access to the church Canva account;
- multi-factor authentication enabled on that account;
- permission to create an integration in the Canva Developer Portal;
- CanvaStage running under the Windows account that will operate it.

Canva currently offers Public integrations and Enterprise-only Private integrations. A loopback redirect is supported for local development, but it cannot remain when a Public integration is submitted for Canva review. CanvaStage’s local desktop flow is therefore intended for a self-managed integration, not a generally published Canva Marketplace integration.

## Create and configure the integration

1. Open CanvaStage and select **Presentation**.
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
12. Choose **Refresh** under **Choose a Canva presentation**. If presentations appear, setup is complete.

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
