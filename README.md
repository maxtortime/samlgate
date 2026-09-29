# samlgate

**AWS credentials from a browser-based SAML sign-in — a drop-in replacement for `saml2aws login` with the Browser provider.**

[한국어](README.ko.md)

samlgate opens your IdP sign-in (Microsoft Entra ID, Okta, Google Workspace, …) in a real browser, catches the
SAML assertion on its way to AWS, exchanges it for temporary credentials with STS, and writes them to
`~/.aws/credentials` in the same format saml2aws uses. It is a single ~7 MB native binary with no runtime,
no Playwright and no browser-driver downloads.

It is built for teams that:

- work in **one or a few AWS accounts handed to them by a parent organisation or partner**, so
  **IAM Identity Center is not an option**, and
- manage people in **Microsoft Entra ID** using the *AWS Single-Account Access* enterprise app (SAML),
  where sign-in means a browser, MFA and Conditional Access — not a username/password prompt in a terminal.

## How it works

SAML has no `localhost` callback. After sign-in the IdP makes the browser `POST` the `SAMLResponse` to the fixed AWS
endpoint `https://signin.aws.amazon.com/saml`. samlgate:

1. starts Edge/Chrome with a **dedicated profile** and the DevTools Protocol enabled,
2. intercepts exactly that ACS request (aws, aws-cn and aws-us-gov partitions) — it never reaches AWS,
3. answers it with a local "you can close this window" page and closes the browser,
4. calls STS `AssumeRoleWithSAML` and writes the profile.

Because the dedicated profile keeps the IdP's cookies, **you sign in once**; after that, renewing expired
credentials just flashes a browser window for a few seconds. (Answer **"Yes" to "Stay signed in?"** on the first
Entra sign-in — samlgate closes the browser every time, so a session-only cookie would be lost.)

## Install

> **Windows only for now.** Builds are published for Windows x64 and ARM64; it has been tested on Windows 11 x64.
> macOS and Linux are not supported yet (see [Roadmap](#roadmap)).

Download `samlgate-<version>-win-x64.zip` (or `win-arm64`) from
[Releases](https://github.com/platpharm/samlgate/releases) and extract `samlgate.exe` into a folder on your `PATH`.

To build from source: `dotnet publish src/Samlgate -c Release -r win-x64` (.NET 10 SDK and the Visual Studio
"Desktop development with C++" workload for NativeAOT).

**Requirements:** Microsoft Edge, which ships with Windows — or Google Chrome, Brave or Chromium.

## Quick start

```bash
# 1. Save your IdP-initiated sign-in URL (see "Finding the sign-in URL" below)
samlgate configure --url "https://launcher.myapps.microsoft.com/api/signin/<app-id>?tenantId=<tenant-id>"

# 2. Sign in — writes the [saml] profile to ~/.aws/credentials
samlgate login

# 3. Use it
aws sts get-caller-identity --profile saml
samlgate status
```

Already using saml2aws? Skip step 1 — samlgate reads the same account from `~/.saml2aws` when `~/.samlgate` does
not have it.

### Finding the sign-in URL (Microsoft Entra ID)

Entra admin center → **Enterprise applications** → your AWS app → **Properties** → **User access URL**. Users can
also right-click the app tile in [My Apps](https://myapps.microsoft.com) and copy the link.

## Commands

| Command | |
|---|---|
| `samlgate login` | Sign in if the stored credentials expire within 5 minutes (or `--force`), then print the session. |
| `samlgate status` | Print the session; exit code `0` if valid, `1` otherwise. Handy in scripts. |
| `samlgate configure --url …` | Create/update an account in `~/.samlgate`. |
| `samlgate credential-process` | Print credentials as [`credential_process`](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sourcing-external.html) JSON, signing in if needed. |
| `samlgate exec -- <cmd> …` | Run a command with `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`/`AWS_SESSION_TOKEN` set. |

Common options: `-a/--idp-account <name>` (config section, default `default`), `-p/--profile <name>` (credentials
profile, default `saml`), `--role <arn>`, `--region`, `--session-duration <seconds>`, `--browser edge|chrome|brave|chromium`,
`--browser-path <exe>`, `--timeout <seconds>`. Run `samlgate <command> --help` for the full list.

### Roles

The SAML response lists every AWS IAM role your user is assigned in the IdP app. With one role samlgate uses it;
with several it asks (and remembers your choice per account). Pin one with `role_arn` / `--role`.

### Using credential_process

Point a **different** profile at samlgate (a profile that also has static keys in `~/.aws/credentials` would use
those instead):

```ini
# ~/.aws/config
[profile work]
credential_process = samlgate credential-process -a default
region = ap-northeast-2
```

When no terminal is attached, samlgate cannot ask for a role — set `role_arn`, or run `samlgate login` once so it
remembers your choice.

## Configuration

`~/.samlgate` (override with `SAMLGATE_CONFIG`) is an INI file with one section per account. Key names match
saml2aws, so you can copy `~/.saml2aws` as-is; unknown keys are ignored.

| Key | Default | |
|---|---|---|
| `url` | — | IdP-initiated sign-in URL. saml2aws `provider = AzureAD` + `app_id` is converted automatically. |
| `aws_profile` | `saml` | Credentials profile to write. |
| `role_arn` | — | Role to assume without asking. |
| `region` | partition default (`us-east-1`) | STS region. |
| `aws_session_duration` | assertion's `SessionDuration`, else 3600 | Falls back to 3600 if the role's maximum is lower. |
| `credentials_file` | `~/.aws/credentials` | Also honours `AWS_SHARED_CREDENTIALS_FILE`. |
| `browser_type` | Edge | `edge`/`msedge`, `chrome`, `brave`, `chromium`. |
| `browser_executable_path` | auto-detected | |
| `target_url` | AWS ACS URLs | Additional URL to intercept (custom ACS). |

The credentials profile contains `aws_access_key_id`, `aws_secret_access_key`, `aws_session_token`,
`aws_security_token`, `x_principal_arn` and `x_security_token_expires`, exactly like saml2aws.

`SAMLGATE_DATA_DIR` moves the browser profiles and remembered roles (default `%LOCALAPPDATA%\samlgate`).

## Security notes

- The SAML assertion is only sent to AWS STS. The intercepted ACS request is answered locally.
- The browser profile is separate from your everyday profile and lives in the data directory; delete it to sign out.
- The credentials file is written atomically.
- DevTools listens on a random port bound to `127.0.0.1` only while samlgate is running.
- No telemetry.

## Roadmap

- macOS, including the Safari engine when no Chromium browser is installed (native WKWebView window)
- Linux
- Firefox via WebDriver BiDi
- Try a headless sign-in first when the IdP session is still valid (no window at all)

## Development

```bash
dotnet test samlgate.slnx          # unit tests + a headless-browser capture test if Edge/Chrome is installed
dotnet run --project src/Samlgate -- help
```

`src/Samlgate.Core` holds everything testable (config, SAML parsing, STS, credentials file, DevTools capture);
`src/Samlgate` is the thin CLI (System.CommandLine). STS is called through AWSSDK.SecurityToken.

## License

[MIT](LICENSE)
