# CraftorAI.Email

Shared email library for multi-service .NET applications, built on 
top of [Resend](https://resend.com). Centralizes transactional email 
logic (auth flows, subscription lifecycle, billing notifications) so 
every service consumes the same tested implementation instead of 
duplicating email code.

## Why this exists

In a multi-service backend, email logic (verification links, password 
resets, billing receipts) tends to get copy-pasted across projects. 
This package extracts it into a single versioned NuGet package, 
published privately via GitHub Packages, so every service references 
one source of truth and updates ship through a normal version bump 
instead of manual syncing.

## Installation

Add the private feed to your `nuget.config`:

\```xml
<packageSources>
  <add key="github" value="https://nuget.pkg.github.com/Bryanrd87/index.json" />
</packageSources>
\```

Authenticate with a GitHub Personal Access Token (`read:packages` 
scope) as your NuGet source credentials, then:

\```bash
dotnet add package CraftorAI.Email
\```

## Usage

\```csharp
// Program.cs
builder.Services.AddCraftorEmailServices(builder.Configuration);
\```

\```csharp
// Anywhere via DI
public class AuthController(IEmailService emailService)
{
    public async Task SendVerification(string email, string url)
        => await emailService.SendEmailVerificationAsync(email, url);
}
\```

## Tech Stack

- .NET 10
- Resend API (HTTP-based email delivery)
- Typed `HttpClient` via `IHttpClientFactory`
- Structured error handling (`EmailSendResult`: network, auth, rate 
  limit, invalid email — each independently retryable)

## CI/CD

Publishing is automated via GitHub Actions 
(`.github/workflows/publish-nuget.yml`): pushing a `v*` tag packs and 
publishes the library to GitHub Packages automatically.

## Design notes

- Typed `HttpClient` registration avoids socket exhaustion under load.
- Every send method returns a structured `EmailSendResponse` 
  (not just bool) so callers can distinguish retryable failures 
  (network, rate limit) from permanent ones (invalid email, auth).
- HTML templates are self-contained in the service to keep the 
  package dependency-free — no external template engine required.
