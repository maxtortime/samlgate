# CLAUDE.md

Guidance for Claude Code (and humans) working in this repository.

## What this is

samlgate is a single-binary CLI that replaces `saml2aws login` (Browser provider): it drives an installed
Chromium-based browser over the DevTools Protocol, intercepts the IdP's SAMLResponse POST to the AWS ACS URL,
calls STS `AssumeRoleWithSAML` (AWS SDK, unsigned), and writes a saml2aws-compatible profile to `~/.aws/credentials`.

## Build / test

```bash
dotnet build samlgate.slnx
dotnet test samlgate.slnx                     # includes a real headless-browser capture test when Edge/Chrome exists
dotnet publish src/Samlgate -c Release -r win-x64 -o out   # NativeAOT, must stay warning-free (CI enforces it)
```

.NET 10, C# latest. The solution file is `.slnx`. NativeAOT publish needs Visual Studio 2022 (Community is fine)
with the "Desktop development with C++" workload; the linker is located through `vswhere.exe`.

**Supported platform: Windows only** (x64/ARM64 releases, verified on Windows 11 x64). The macOS/Linux branches in
`BrowserLocator` and `AppPaths` are kept as a starting point but are untested; CI and releases are Windows-only
until those platforms are properly supported (macOS also needs the Safari/WKWebView path).

## Layout

- `src/Samlgate.Core` — all logic, `IsAotCompatible`.
  - `Browser/` — `BrowserLocator` (find Edge/Chrome/Brave/Chromium), `CdpConnection` (minimal DevTools client,
    flattened sessions), `BrowserSamlCapture` (launch with a dedicated profile → Fetch-intercept the ACS POST).
  - `Aws/` — `SamlAssertion`, `AcsEndpoints` (aws / aws-cn / aws-us-gov), `StsClient` (AWSSDK.SecurityToken), `CredentialsFile`.
  - `Config/` — `~/.samlgate` with fallback to `~/.saml2aws` (same key names), `StateStore` (last role per account).
  - `LoginFlow` — capture → choose role → STS → write.
- `src/Samlgate` — the CLI (`samlgate` executable): `Cli` (System.CommandLine command tree), commands, role prompt,
  `exec` resolver.
- `tests/Samlgate.Core.Tests` — xUnit.

## Rules

- **Write as little code as possible.** Prefer a well-maintained library over hand-written infrastructure
  (argument parsing, AWS calls, …). NativeAOT is here to ship a single binary, not to minimise its size.
- **Stay NativeAOT-safe.** Libraries must publish without trim/AOT warnings. No reflection-based `JsonSerializer`;
  use `JsonNode`/`Utf8JsonWriter`.
- **stdout is data, stderr is chatter.** `status` and `credential-process` output go to stdout; progress, prompts
  and errors go to stderr (credential_process consumers parse stdout).
- **Never let the ACS request reach AWS**, and never start navigating before `Fetch.enable` is in place
  (a still-valid IdP session can reach the ACS within milliseconds).
- Close the browser with `Browser.close` before killing it — killing can lose the IdP cookies that make
  later logins silent.
- User-facing messages and code comments are English only. `SatelliteResourceLanguages=en` stays:
  System.CommandLine's translations are partial (mixed-language help). If localization is ever needed, use plain
  resx + `ResourceManager` (verified NativeAOT-safe; resources are embedded in the executable,
  `UseSystemResourceKeys` must stay `false`), not `IStringLocalizer`, and never localize stdout data.
- Always use braces, even for one-line blocks (IDE0011 is a warning, and CI publishes with warnings as errors).
- No organisation-specific values (tenant IDs, app IDs, account IDs) in code, tests or docs — use placeholders.

## Open items

- **`CdpConnection` still hand-written, pending review.** Only candidate is PuppeteerSharp (can drive an installed
  Chrome/Edge, could also shrink `BrowserSamlCapture`); adopt it only if it publishes AOT-warning-free and keeps
  `Fetch.enable` in place before any navigation, popups/new tabs included. Playwright (needs its Node driver) and
  Selenium CDP (needs a WebDriver session) don't fit a single binary.

## Decided: stays hand-written

- **`IniFile`/`CredentialsFile`** — AWSSDK.Core `SharedCredentialsFile` can't replace them: `CredentialProfile.Properties`
  is internal, so saml2aws fields (`x_security_token_expires`, `x_principal_arn`) can't be read or written without
  reflection; `RegisterProfile` merges into the section instead of replacing it (stale keys such as an old
  `aws_security_token` survive) and reflows formatting.
- **`SamlAssertion`/`AcsEndpoints`** — the SDK's `Amazon.SecurityToken.SAML.SAMLAssertion` has an internal constructor
  (built only by the ADFS flow), drops the partition from `RoleSet` keys and ignores SessionDuration; the SDK has no
  signin/ACS host metadata.
- **`RolePrompt`** — 45 lines; a prompt library (e.g. Spectre.Console) would need redirecting to stderr and
  re-creating the "Enter = last role" default, for little gain.
