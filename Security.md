# Security Policy

This is a small, self-hosted personal-inventory app. It isn't under active security
auditing, but reports are welcome and will be looked at.

## Supported Versions

There are no formal releases yet — only the `main` branch is maintained. Please make
sure you're running the latest commit before reporting an issue.

## Reporting a Vulnerability

If you find a security issue (auth bypass, injection, path traversal in file
upload/serving, secret exposure, etc.), please **do not open a public issue**.

Instead, report it privately via **GitHub's private vulnerability reporting**:

1. Go to the **Security** tab of this repository
2. Click **"Report a vulnerability"**
3. Describe the issue, steps to reproduce, and potential impact

If that's not available, open a normal issue asking for a private contact channel,
without including exploit details.

I'll aim to acknowledge reports within a few days. This is a hobby project maintained
in spare time, so please be patient — but real security reports will be prioritized
over feature requests.

## Known Security Considerations for Self-Hosters

Since this app is meant to be self-hosted, a few things are **your** responsibility
when deploying it, not bugs in the code:

- **Change the default admin password.** `appsettings.json` ships with a placeholder
  (`Auth:AdminPassword`) — replace it before exposing the app beyond `localhost`.
- **Never commit real API keys.** `Groq:ApiKey` and `SerpApi:ApiKey` should be set via
  `appsettings.Development.json`, environment variables, or a secrets manager — not
  committed to git. See the `.gitignore` for what's excluded by default.
- **Run behind HTTPS** if exposing this beyond your own machine — camera capture
  requires it in the browser anyway, and it protects the admin login and API keys in
  transit.
- **File uploads** (item photos, attachments) are saved under `wwwroot/uploads/` and
  `wwwroot/attachments/` with server-generated filenames — don't disable that filename
  generation, and don't serve `wwwroot/` with directory listing enabled.
- If you fork this and add authentication changes, keep in mind the app currently uses
  a single shared admin password, not per-user accounts — treat it as a
  single-operator tool, not multi-tenant software.