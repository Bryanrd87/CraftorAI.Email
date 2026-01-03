using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CraftorAI.Email.Configuration;
using CraftorAI.Email.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CraftorAI.Email.Services;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;
    private const string BaseUrl = "https://api.resend.com/emails";

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<ResendConfiguration> config,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    #region Core Send Methods

    public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody, string? plainTextBody = null)
    {
        var response = await SendEmailWithResultAsync(to, subject, htmlBody, plainTextBody);
        return response.IsSuccess;
    }

    public async Task<EmailSendResponse> SendEmailWithResultAsync(string to, string subject, string htmlBody, string? plainTextBody = null)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("Email service is disabled. Email to {Email} not sent.", to);
            return new EmailSendResponse
            {
                Result = EmailSendResult.ServiceDisabled,
                Message = "Email service is disabled in configuration"
            };
        }

        try
        {
            _logger.LogInformation("Sending email via Resend API to {Email}", to);

            var emailData = new
            {
                from = $"{_config.FromName} <{_config.FromEmail}>",
                to = new[] { to },
                subject,
                html = htmlBody,
                text = plainTextBody
            };

            var jsonContent = JsonSerializer.Serialize(emailData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(BaseUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Email sent successfully to {Email}", to);
                return new EmailSendResponse
                {
                    Result = EmailSendResult.Success,
                    Message = "Email sent successfully"
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);

                return response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or
                    System.Net.HttpStatusCode.Forbidden => new EmailSendResponse
                    {
                        Result = EmailSendResult.AuthenticationError,
                        Message = "Resend API authentication failed"
                    },
                    System.Net.HttpStatusCode.BadRequest => new EmailSendResponse
                    {
                        Result = EmailSendResult.InvalidEmail,
                        Message = "Invalid email request parameters"
                    },
                    System.Net.HttpStatusCode.TooManyRequests => new EmailSendResponse
                    {
                        Result = EmailSendResult.RateLimited,
                        Message = "Rate limit exceeded",
                        ShouldRetry = true
                    },
                    _ => new EmailSendResponse
                    {
                        Result = EmailSendResult.UnknownError,
                        Message = $"API error: {response.StatusCode}"
                    }
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error sending email to {Email}", to);
            return new EmailSendResponse
            {
                Result = EmailSendResult.NetworkError,
                Message = "Network error while contacting email service",
                ShouldRetry = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {Email}", to);
            return new EmailSendResponse
            {
                Result = EmailSendResult.UnknownError,
                Message = "Unexpected error occurred"
            };
        }
    }

    #endregion

    #region Auth Emails

    public Task<bool> SendEmailVerificationAsync(string email, string verificationUrl) =>
        SendEmailVerificationAsync(email, verificationUrl, 24);

    public async Task<bool> SendEmailVerificationAsync(string email, string verificationUrl, int expirationHours)
    {
        var subject = "Verify Your Email Address - CraftorAI";
        var htmlBody = GetEmailVerificationHtml(verificationUrl, expirationHours);
        var plainTextBody = GetEmailVerificationText(verificationUrl, expirationHours);
        return await SendEmailAsync(email, subject, htmlBody, plainTextBody);
    }

    public Task<EmailSendResponse> SendEmailVerificationWithResultAsync(string email, string verificationUrl) =>
        SendEmailVerificationWithResultAsync(email, verificationUrl, 24);

    public async Task<EmailSendResponse> SendEmailVerificationWithResultAsync(string email, string verificationUrl, int expirationHours)
    {
        var subject = "Verify Your Email Address - CraftorAI";
        var htmlBody = GetEmailVerificationHtml(verificationUrl, expirationHours);
        var plainTextBody = GetEmailVerificationText(verificationUrl, expirationHours);
        return await SendEmailWithResultAsync(email, subject, htmlBody, plainTextBody);
    }

    public Task<bool> SendPasswordResetAsync(string email, string resetUrl) =>
        SendPasswordResetAsync(email, resetUrl, 1);

    public async Task<bool> SendPasswordResetAsync(string email, string resetUrl, int expirationHours)
    {
        var subject = "Reset Your Password - CraftorAI";
        var htmlBody = GetPasswordResetHtml(resetUrl, expirationHours);
        var plainTextBody = GetPasswordResetText(resetUrl, expirationHours);
        return await SendEmailAsync(email, subject, htmlBody, plainTextBody);
    }

    public async Task<bool> SendTeamInvitationAsync(string email, string firstName, string organizationName, string inviterName, string role, string acceptInvitationUrl)
    {
        var subject = $"{inviterName} invited you to join {organizationName} on CraftorAI";
        var htmlBody = GetTeamInvitationHtml(firstName, organizationName, inviterName, role, acceptInvitationUrl);
        var plainTextBody = GetTeamInvitationText(firstName, organizationName, inviterName, role, acceptInvitationUrl);
        return await SendEmailAsync(email, subject, htmlBody, plainTextBody);
    }

    #endregion

    #region Subscription Lifecycle Emails

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var subject = "Welcome to CraftorAI!";
        var htmlBody = GetWelcomeHtml(toName);
        var plainTextBody = GetWelcomeText(toName);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendTrialEndingEmailAsync(string toEmail, string toName, int daysRemaining, string planName)
    {
        var subject = $"Your {planName} trial ends in {daysRemaining} day{(daysRemaining > 1 ? "s" : "")}";
        var htmlBody = GetTrialEndingHtml(toName, daysRemaining, planName);
        var plainTextBody = GetTrialEndingText(toName, daysRemaining, planName);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendTrialEndedEmailAsync(string toEmail, string toName, string planName)
    {
        var subject = $"Your {planName} trial has ended - Welcome to CraftorAI!";
        var htmlBody = GetTrialEndedHtml(toName, planName);
        var plainTextBody = GetTrialEndedText(toName, planName);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendPaymentFailedEmailAsync(string toEmail, string toName, string planName, decimal amount)
    {
        var subject = "Action Required: Payment Failed for Your CraftorAI Subscription";
        var htmlBody = GetPaymentFailedHtml(toName, planName, amount);
        var plainTextBody = GetPaymentFailedText(toName, planName, amount);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendSubscriptionCancelledEmailAsync(string toEmail, string toName, string planName, DateTime endDate)
    {
        var subject = "Your CraftorAI Subscription Has Been Cancelled";
        var htmlBody = GetSubscriptionCancelledHtml(toName, planName, endDate);
        var plainTextBody = GetSubscriptionCancelledText(toName, planName, endDate);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    #endregion

    #region Stripe Notification Emails

    public async Task SendCheckoutCompletedEmailAsync(string toEmail, string toName, string planName, decimal amount, string billingPeriod, DateTime nextBillingDate)
    {
        var subject = $"Welcome to {planName} - Your subscription is confirmed!";
        var htmlBody = GetCheckoutCompletedHtml(toName, planName, amount, billingPeriod, nextBillingDate);
        var plainTextBody = GetCheckoutCompletedText(toName, planName, amount, billingPeriod, nextBillingDate);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendInvoicePaidEmailAsync(string toEmail, string toName, string invoiceNumber, decimal amount, string planName, string? invoiceUrl)
    {
        var subject = $"Payment Receipt - Invoice #{invoiceNumber}";
        var htmlBody = GetInvoicePaidHtml(toName, invoiceNumber, amount, planName, invoiceUrl);
        var plainTextBody = GetInvoicePaidText(toName, invoiceNumber, amount, planName, invoiceUrl);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendInvoicePaymentFailedEmailAsync(string toEmail, string toName, string invoiceNumber, decimal amount, string planName)
    {
        var subject = "Action Required: Payment failed for your CraftorAI subscription";
        var htmlBody = GetInvoicePaymentFailedHtml(toName, invoiceNumber, amount, planName);
        var plainTextBody = GetInvoicePaymentFailedText(toName, invoiceNumber, amount, planName);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendSubscriptionCreatedEmailAsync(string toEmail, string toName, string planName, decimal amount, string billingPeriod, DateTime? trialEndDate)
    {
        var subject = trialEndDate.HasValue
            ? $"Your {planName} trial has started!"
            : $"Your {planName} subscription is active!";
        var htmlBody = GetSubscriptionCreatedHtml(toName, planName, amount, billingPeriod, trialEndDate);
        var plainTextBody = GetSubscriptionCreatedText(toName, planName, amount, billingPeriod, trialEndDate);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendSubscriptionUpdatedEmailAsync(string toEmail, string toName, string oldPlanName, string newPlanName, decimal newAmount, string billingPeriod)
    {
        var subject = "Your subscription has been updated";
        var htmlBody = GetSubscriptionUpdatedHtml(toName, oldPlanName, newPlanName, newAmount, billingPeriod);
        var plainTextBody = GetSubscriptionUpdatedText(toName, oldPlanName, newPlanName, newAmount, billingPeriod);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    public async Task SendSubscriptionCancelledByStripeEmailAsync(string toEmail, string toName, string planName, DateTime accessEndDate, string? reason)
    {
        var subject = "Your CraftorAI subscription has been cancelled";
        var htmlBody = GetSubscriptionCancelledByStripeHtml(toName, planName, accessEndDate, reason);
        var plainTextBody = GetSubscriptionCancelledByStripeText(toName, planName, accessEndDate, reason);
        await SendEmailAsync(toEmail, subject, htmlBody, plainTextBody);
    }

    #endregion

    #region HTML Templates - Shared Styles

    private static string GetBaseStyles() => @"
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f9fa;
        }
        .container {
            background-color: white;
            border-radius: 12px;
            padding: 40px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            border: 1px solid #e9ecef;
        }
        .header {
            text-align: center;
            margin-bottom: 32px;
            padding-bottom: 24px;
            border-bottom: 2px solid #f1f3f4;
        }
        .logo {
            font-size: 32px;
            font-weight: 800;
            color: #6366f1;
            margin: 0;
        }
        .button {
            display: inline-block;
            background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
            color: white !important;
            padding: 16px 32px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            box-shadow: 0 4px 12px rgba(99, 102, 241, 0.4);
        }
        .button-danger {
            background: linear-gradient(135deg, #dc2626 0%, #ef4444 100%);
            box-shadow: 0 4px 12px rgba(220, 38, 38, 0.4);
        }
        .button-success {
            background: linear-gradient(135deg, #059669 0%, #10b981 100%);
            box-shadow: 0 4px 12px rgba(5, 150, 105, 0.4);
        }
        .info-box {
            background-color: #f0f9ff;
            border-left: 4px solid #3b82f6;
            padding: 16px;
            border-radius: 6px;
            margin: 20px 0;
        }
        .warning-box {
            background-color: #fef3c7;
            border-left: 4px solid #f59e0b;
            padding: 16px;
            border-radius: 6px;
            margin: 20px 0;
        }
        .error-box {
            background-color: #fef2f2;
            border-left: 4px solid #ef4444;
            padding: 16px;
            border-radius: 6px;
            margin: 20px 0;
        }
        .success-box {
            background-color: #ecfdf5;
            border-left: 4px solid #10b981;
            padding: 16px;
            border-radius: 6px;
            margin: 20px 0;
        }
        .footer {
            text-align: center;
            margin-top: 32px;
            padding-top: 24px;
            border-top: 1px solid #e5e7eb;
            font-size: 14px;
            color: #6b7280;
        }
        .amount {
            font-size: 24px;
            font-weight: 700;
            color: #111827;
        }";

    private static string WrapInLayout(string content, string accentColor = "#6366f1") => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>{GetBaseStyles()}</style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 class='logo' style='color: {accentColor};'>CraftorAI</h1>
            <p style='color: #6b7280; font-size: 14px; margin-top: 4px;'>AI-Powered Content Creation</p>
        </div>
        {content}
        <div class='footer'>
            <p>&copy; {DateTime.UtcNow.Year} CraftorAI. All rights reserved.</p>
            <p>Questions? Contact us at support@craftorai.com</p>
        </div>
    </div>
</body>
</html>";

    #endregion

    #region Auth Email Templates

    private static string GetEmailVerificationHtml(string verificationUrl, int expirationHours)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Welcome to CraftorAI!</h2>
            <p>Thank you for creating an account. Please verify your email address to get started.</p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='{verificationUrl}' class='button'>Verify Email Address</a>
            </div>

            <div class='info-box'>
                <p style='margin: 0;'><strong>Can't click the button?</strong> Copy and paste this link:</p>
                <p style='margin: 8px 0 0 0; word-break: break-all; color: #6366f1;'>{verificationUrl}</p>
            </div>

            <div class='warning-box'>
                <p style='margin: 0;'><strong>Security Notice:</strong> This link expires in {expirationHours} hour{(expirationHours == 1 ? "" : "s")}.</p>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>If you didn't create this account, you can safely ignore this email.</p>";

        return WrapInLayout(content);
    }

    private static string GetEmailVerificationText(string verificationUrl, int expirationHours) => $@"
Welcome to CraftorAI!

Thank you for creating an account. Please verify your email address by visiting:

{verificationUrl}

This link expires in {expirationHours} hour{(expirationHours == 1 ? "" : "s")}.

If you didn't create this account, you can safely ignore this email.

- CraftorAI Team";

    private static string GetPasswordResetHtml(string resetUrl, int expirationHours)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Reset Your Password</h2>
            <p>We received a request to reset your password. Click the button below to set a new password:</p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='{resetUrl}' class='button button-danger'>Reset Password</a>
            </div>

            <div class='info-box'>
                <p style='margin: 0;'><strong>Can't click the button?</strong> Copy and paste this link:</p>
                <p style='margin: 8px 0 0 0; word-break: break-all; color: #6366f1;'>{resetUrl}</p>
            </div>

            <div class='error-box'>
                <p style='margin: 0;'><strong>Security Notice:</strong></p>
                <ul style='margin: 8px 0 0 16px; color: #7f1d1d;'>
                    <li>This link expires in {expirationHours} hour{(expirationHours == 1 ? "" : "s")}</li>
                    <li>This link can only be used once</li>
                    <li>If you didn't request this, ignore this email</li>
                </ul>
            </div>";

        return WrapInLayout(content, "#dc2626");
    }

    private static string GetPasswordResetText(string resetUrl, int expirationHours) => $@"
Reset Your Password - CraftorAI

We received a request to reset your password. Visit this link to set a new password:

{resetUrl}

SECURITY NOTICE:
- This link expires in {expirationHours} hour{(expirationHours == 1 ? "" : "s")}
- This link can only be used once
- If you didn't request this, ignore this email

- CraftorAI Team";

    private static string GetTeamInvitationHtml(string firstName, string organizationName, string inviterName, string role, string acceptUrl)
    {
        var roleDescription = role.ToLower() switch
        {
            "admin" => "Full access to all features and settings",
            "manager" => "Can manage content and team members",
            "editor" => "Can create and edit content",
            "viewer" => "Can view content and basic analytics",
            _ => "Team member"
        };

        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>You're Invited!</h2>
            <p>Hi {firstName}, <strong>{inviterName}</strong> has invited you to join their team on CraftorAI.</p>

            <div class='info-box'>
                <p style='margin: 0 0 8px 0; color: #6b7280; font-size: 14px;'>Organization:</p>
                <p style='margin: 0; font-size: 20px; font-weight: 600; color: #111827;'>{organizationName}</p>
                <p style='margin: 12px 0 4px 0;'>
                    <span style='background: #6366f1; color: white; padding: 4px 12px; border-radius: 12px; font-size: 14px;'>{char.ToUpper(role[0])}{role[1..].ToLower()}</span>
                </p>
                <p style='margin: 8px 0 0 0; color: #6b7280; font-size: 14px;'>{roleDescription}</p>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='{acceptUrl}' class='button'>Accept Invitation</a>
            </div>

            <div class='warning-box'>
                <p style='margin: 0;'><strong>This invitation expires in 7 days.</strong></p>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>If you didn't expect this invitation, you can safely ignore it.</p>";

        return WrapInLayout(content);
    }

    private static string GetTeamInvitationText(string firstName, string organizationName, string inviterName, string role, string acceptUrl)
    {
        var roleDescription = role.ToLower() switch
        {
            "admin" => "Full access to all features and settings",
            "manager" => "Can manage content and team members",
            "editor" => "Can create and edit content",
            "viewer" => "Can view content and basic analytics",
            _ => "Team member"
        };

        return $@"
You're Invited to CraftorAI!

Hi {firstName},

{inviterName} has invited you to join their team on CraftorAI.

ORGANIZATION: {organizationName}
YOUR ROLE: {char.ToUpper(role[0])}{role[1..].ToLower()}
{roleDescription}

Accept Invitation: {acceptUrl}

This invitation expires in 7 days.

If you didn't expect this invitation, you can safely ignore it.

- CraftorAI Team";
    }

    #endregion

    #region Subscription Lifecycle Templates

    private static string GetWelcomeHtml(string toName)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Welcome to CraftorAI, {toName}!</h2>
            <p>We're thrilled to have you on board. Get ready to create amazing content with the power of AI.</p>

            <div class='success-box'>
                <h3 style='margin: 0 0 12px 0; color: #065f46;'>Get Started</h3>
                <ul style='margin: 0; padding-left: 20px; color: #047857;'>
                    <li>Connect your social media accounts</li>
                    <li>Create your first AI-powered post</li>
                    <li>Schedule content across platforms</li>
                    <li>Invite team members to collaborate</li>
                </ul>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/dashboard' class='button button-success'>Go to Dashboard</a>
            </div>";

        return WrapInLayout(content, "#059669");
    }

    private static string GetWelcomeText(string toName) => $@"
Welcome to CraftorAI, {toName}!

We're thrilled to have you on board. Get ready to create amazing content with AI.

GET STARTED:
- Connect your social media accounts
- Create your first AI-powered post
- Schedule content across platforms
- Invite team members to collaborate

Visit your dashboard: https://app.craftorai.com/dashboard

- CraftorAI Team";

    private static string GetTrialEndingHtml(string toName, int daysRemaining, string planName)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>{daysRemaining} Day{(daysRemaining > 1 ? "s" : "")} Left in Your Trial</h2>
            <p>Hi {toName},</p>
            <p>Your <strong>{planName}</strong> trial ends in <strong>{daysRemaining} day{(daysRemaining > 1 ? "s" : "")}</strong>.</p>

            <div class='warning-box'>
                <p style='margin: 0;'>To continue enjoying all features without interruption, please update your payment method.</p>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/billing' class='button'>Manage Billing</a>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>Need help? Reply to this email or contact our support team.</p>";

        return WrapInLayout(content, "#f59e0b");
    }

    private static string GetTrialEndingText(string toName, int daysRemaining, string planName) => $@"
{daysRemaining} Day{(daysRemaining > 1 ? "s" : "")} Left in Your Trial

Hi {toName},

Your {planName} trial ends in {daysRemaining} day{(daysRemaining > 1 ? "s" : "")}.

To continue enjoying all features, update your payment method:
https://app.craftorai.com/billing

Need help? Reply to this email.

- CraftorAI Team";

    private static string GetTrialEndedHtml(string toName, string planName)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Welcome to {planName}!</h2>
            <p>Hi {toName},</p>
            <p>Your trial has ended and your <strong>{planName}</strong> subscription is now active.</p>

            <div class='success-box'>
                <p style='margin: 0;'>Thank you for choosing CraftorAI! We're excited to help you create amazing content.</p>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/dashboard' class='button button-success'>Go to Dashboard</a>
            </div>";

        return WrapInLayout(content, "#059669");
    }

    private static string GetTrialEndedText(string toName, string planName) => $@"
Welcome to {planName}!

Hi {toName},

Your trial has ended and your {planName} subscription is now active.

Thank you for choosing CraftorAI!

Visit your dashboard: https://app.craftorai.com/dashboard

- CraftorAI Team";

    private static string GetPaymentFailedHtml(string toName, string planName, decimal amount)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Payment Failed</h2>
            <p>Hi {toName},</p>
            <p>We couldn't process your payment of <strong>${amount:F2}</strong> for your <strong>{planName}</strong> subscription.</p>

            <div class='error-box'>
                <p style='margin: 0 0 12px 0;'><strong>Common reasons:</strong></p>
                <ul style='margin: 0; padding-left: 20px;'>
                    <li>Insufficient funds</li>
                    <li>Expired card</li>
                    <li>Card blocked by bank</li>
                    <li>Incorrect billing info</li>
                </ul>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/billing' class='button button-danger'>Update Payment Method</a>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>We'll retry in a few days. If payment continues to fail, your subscription may be suspended.</p>";

        return WrapInLayout(content, "#dc2626");
    }

    private static string GetPaymentFailedText(string toName, string planName, decimal amount) => $@"
Payment Failed

Hi {toName},

We couldn't process your payment of ${amount:F2} for your {planName} subscription.

Common reasons:
- Insufficient funds
- Expired card
- Card blocked by bank
- Incorrect billing info

Update your payment method: https://app.craftorai.com/billing

We'll retry in a few days. If payment continues to fail, your subscription may be suspended.

- CraftorAI Team";

    private static string GetSubscriptionCancelledHtml(string toName, string planName, DateTime endDate)
    {
        var formattedDate = endDate.ToString("MMMM dd, yyyy");
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Subscription Cancelled</h2>
            <p>Hi {toName},</p>
            <p>Your <strong>{planName}</strong> subscription has been cancelled as requested.</p>

            <div class='warning-box'>
                <p style='margin: 0;'><strong>Access Until:</strong> {formattedDate}</p>
                <p style='margin: 8px 0 0 0;'>You'll have full access to all features until this date.</p>
            </div>

            <p>We'd love to know why you cancelled:</p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/feedback?type=cancellation' style='color: #6b7280; margin-right: 16px;'>Share Feedback</a>
                <a href='https://app.craftorai.com/pricing' class='button'>Reactivate</a>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>Your data will be stored for 30 days. You can reactivate anytime.</p>";

        return WrapInLayout(content, "#6b7280");
    }

    private static string GetSubscriptionCancelledText(string toName, string planName, DateTime endDate)
    {
        var formattedDate = endDate.ToString("MMMM dd, yyyy");
        return $@"
Subscription Cancelled

Hi {toName},

Your {planName} subscription has been cancelled.

ACCESS UNTIL: {formattedDate}
You'll have full access until this date.

Reactivate: https://app.craftorai.com/pricing
Share Feedback: https://app.craftorai.com/feedback?type=cancellation

Your data will be stored for 30 days.

- CraftorAI Team";
    }

    #endregion

    #region Stripe Notification Templates

    private static string GetCheckoutCompletedHtml(string toName, string planName, decimal amount, string billingPeriod, DateTime nextBillingDate)
    {
        var formattedDate = nextBillingDate.ToString("MMMM dd, yyyy");
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Welcome to {planName}!</h2>
            <p>Hi {toName},</p>
            <p>Your subscription has been confirmed. You now have access to all {planName} features!</p>

            <div class='success-box'>
                <div style='text-align: center;'>
                    <p class='amount'>${amount:F2}/{billingPeriod.ToLower()}</p>
                    <p style='margin: 8px 0 0 0; color: #6b7280;'>Next billing date: {formattedDate}</p>
                </div>
            </div>

            <div class='info-box'>
                <h3 style='margin: 0 0 12px 0; color: #1e40af;'>What's Next?</h3>
                <ul style='margin: 0; padding-left: 20px;'>
                    <li>Connect your social media accounts</li>
                    <li>Create AI-powered content</li>
                    <li>Schedule posts across platforms</li>
                    <li>Invite your team to collaborate</li>
                </ul>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/dashboard' class='button button-success'>Get Started</a>
            </div>";

        return WrapInLayout(content, "#059669");
    }

    private static string GetCheckoutCompletedText(string toName, string planName, decimal amount, string billingPeriod, DateTime nextBillingDate)
    {
        var formattedDate = nextBillingDate.ToString("MMMM dd, yyyy");
        return $@"
Welcome to {planName}!

Hi {toName},

Your subscription has been confirmed!

PLAN: {planName}
AMOUNT: ${amount:F2}/{billingPeriod.ToLower()}
NEXT BILLING: {formattedDate}

Get started: https://app.craftorai.com/dashboard

- CraftorAI Team";
    }

    private static string GetInvoicePaidHtml(string toName, string invoiceNumber, decimal amount, string planName, string? invoiceUrl)
    {
        var invoiceLink = !string.IsNullOrEmpty(invoiceUrl)
            ? $"<p style='margin-top: 16px;'><a href='{invoiceUrl}' style='color: #6366f1;'>View/Download Invoice</a></p>"
            : "";

        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Payment Received</h2>
            <p>Hi {toName},</p>
            <p>Thank you for your payment. Here's your receipt:</p>

            <div class='success-box'>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; color: #6b7280;'>Invoice Number</td>
                        <td style='padding: 8px 0; text-align: right; font-weight: 600;'>#{invoiceNumber}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; color: #6b7280;'>Plan</td>
                        <td style='padding: 8px 0; text-align: right; font-weight: 600;'>{planName}</td>
                    </tr>
                    <tr style='border-top: 1px solid #d1fae5;'>
                        <td style='padding: 12px 0; color: #065f46; font-weight: 600;'>Amount Paid</td>
                        <td style='padding: 12px 0; text-align: right; font-size: 20px; font-weight: 700; color: #065f46;'>${amount:F2}</td>
                    </tr>
                </table>
                {invoiceLink}
            </div>

            <p style='color: #6b7280; font-size: 14px;'>This receipt was sent to confirm your payment. No action is required.</p>";

        return WrapInLayout(content, "#059669");
    }

    private static string GetInvoicePaidText(string toName, string invoiceNumber, decimal amount, string planName, string? invoiceUrl)
    {
        var invoiceLink = !string.IsNullOrEmpty(invoiceUrl)
            ? $"\nView Invoice: {invoiceUrl}"
            : "";

        return $@"
Payment Received

Hi {toName},

Thank you for your payment.

INVOICE: #{invoiceNumber}
PLAN: {planName}
AMOUNT PAID: ${amount:F2}
{invoiceLink}

- CraftorAI Team";
    }

    private static string GetInvoicePaymentFailedHtml(string toName, string invoiceNumber, decimal amount, string planName)
    {
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Payment Failed</h2>
            <p>Hi {toName},</p>
            <p>We were unable to process your payment for invoice <strong>#{invoiceNumber}</strong>.</p>

            <div class='error-box'>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; color: #7f1d1d;'>Plan</td>
                        <td style='padding: 8px 0; text-align: right; font-weight: 600;'>{planName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; color: #7f1d1d;'>Amount Due</td>
                        <td style='padding: 8px 0; text-align: right; font-size: 20px; font-weight: 700;'>${amount:F2}</td>
                    </tr>
                </table>
            </div>

            <p>Please update your payment method to avoid service interruption.</p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/billing' class='button button-danger'>Update Payment Method</a>
            </div>

            <div class='warning-box'>
                <p style='margin: 0;'><strong>Note:</strong> We'll automatically retry in a few days. If payment continues to fail, your subscription may be suspended.</p>
            </div>";

        return WrapInLayout(content, "#dc2626");
    }

    private static string GetInvoicePaymentFailedText(string toName, string invoiceNumber, decimal amount, string planName) => $@"
Payment Failed

Hi {toName},

We couldn't process your payment for invoice #{invoiceNumber}.

PLAN: {planName}
AMOUNT DUE: ${amount:F2}

Update your payment method: https://app.craftorai.com/billing

We'll retry in a few days. If payment continues to fail, your subscription may be suspended.

- CraftorAI Team";

    private static string GetSubscriptionCreatedHtml(string toName, string planName, decimal amount, string billingPeriod, DateTime? trialEndDate)
    {
        var trialInfo = trialEndDate.HasValue
            ? $@"<div class='info-box'>
                    <p style='margin: 0;'><strong>Trial Period:</strong> Your trial ends on {trialEndDate.Value:MMMM dd, yyyy}</p>
                    <p style='margin: 8px 0 0 0;'>You won't be charged until your trial ends.</p>
                </div>"
            : "";

        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>{(trialEndDate.HasValue ? "Your Trial Has Started!" : "Subscription Activated!")}</h2>
            <p>Hi {toName},</p>
            <p>Welcome to <strong>{planName}</strong>! You now have access to all the features included in your plan.</p>

            <div class='success-box'>
                <div style='text-align: center;'>
                    <p class='amount'>${amount:F2}/{billingPeriod.ToLower()}</p>
                </div>
            </div>

            {trialInfo}

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/dashboard' class='button button-success'>Start Creating</a>
            </div>";

        return WrapInLayout(content, "#059669");
    }

    private static string GetSubscriptionCreatedText(string toName, string planName, decimal amount, string billingPeriod, DateTime? trialEndDate)
    {
        var trialInfo = trialEndDate.HasValue
            ? $"\nTRIAL ENDS: {trialEndDate.Value:MMMM dd, yyyy}\nYou won't be charged until your trial ends.\n"
            : "";

        return $@"
{(trialEndDate.HasValue ? "Your Trial Has Started!" : "Subscription Activated!")}

Hi {toName},

Welcome to {planName}!

AMOUNT: ${amount:F2}/{billingPeriod.ToLower()}
{trialInfo}
Get started: https://app.craftorai.com/dashboard

- CraftorAI Team";
    }

    private static string GetSubscriptionUpdatedHtml(string toName, string oldPlanName, string newPlanName, decimal newAmount, string billingPeriod)
    {
        var isUpgrade = newAmount > 0; // Simplified check
        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Subscription Updated</h2>
            <p>Hi {toName},</p>
            <p>Your subscription has been updated successfully.</p>

            <div class='info-box'>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; color: #6b7280;'>Previous Plan</td>
                        <td style='padding: 8px 0; text-align: right;'>{oldPlanName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; color: #6b7280;'>New Plan</td>
                        <td style='padding: 8px 0; text-align: right; font-weight: 600; color: #6366f1;'>{newPlanName}</td>
                    </tr>
                    <tr style='border-top: 1px solid #bfdbfe;'>
                        <td style='padding: 12px 0; font-weight: 600;'>New Amount</td>
                        <td style='padding: 12px 0; text-align: right; font-size: 20px; font-weight: 700;'>${newAmount:F2}/{billingPeriod.ToLower()}</td>
                    </tr>
                </table>
            </div>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/billing' class='button'>View Billing Details</a>
            </div>";

        return WrapInLayout(content);
    }

    private static string GetSubscriptionUpdatedText(string toName, string oldPlanName, string newPlanName, decimal newAmount, string billingPeriod) => $@"
Subscription Updated

Hi {toName},

Your subscription has been updated:

PREVIOUS PLAN: {oldPlanName}
NEW PLAN: {newPlanName}
NEW AMOUNT: ${newAmount:F2}/{billingPeriod.ToLower()}

View billing: https://app.craftorai.com/billing

- CraftorAI Team";

    private static string GetSubscriptionCancelledByStripeHtml(string toName, string planName, DateTime accessEndDate, string? reason)
    {
        var formattedDate = accessEndDate.ToString("MMMM dd, yyyy");
        var reasonInfo = !string.IsNullOrEmpty(reason)
            ? $"<p style='margin: 8px 0 0 0;'><strong>Reason:</strong> {reason}</p>"
            : "";

        var content = $@"
            <h2 style='color: #111827; margin-bottom: 16px;'>Subscription Cancelled</h2>
            <p>Hi {toName},</p>
            <p>Your <strong>{planName}</strong> subscription has been cancelled.</p>

            <div class='warning-box'>
                <p style='margin: 0;'><strong>Access Until:</strong> {formattedDate}</p>
                <p style='margin: 8px 0 0 0;'>You'll continue to have access until this date.</p>
                {reasonInfo}
            </div>

            <p>We're sorry to see you go. If this was a mistake or you'd like to resubscribe:</p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='https://app.craftorai.com/pricing' class='button'>Reactivate Subscription</a>
            </div>

            <p style='color: #6b7280; font-size: 14px;'>Your data will be securely stored for 30 days after your access ends.</p>";

        return WrapInLayout(content, "#6b7280");
    }

    private static string GetSubscriptionCancelledByStripeText(string toName, string planName, DateTime accessEndDate, string? reason)
    {
        var formattedDate = accessEndDate.ToString("MMMM dd, yyyy");
        var reasonInfo = !string.IsNullOrEmpty(reason)
            ? $"\nReason: {reason}"
            : "";

        return $@"
Subscription Cancelled

Hi {toName},

Your {planName} subscription has been cancelled.

ACCESS UNTIL: {formattedDate}
{reasonInfo}

Reactivate: https://app.craftorai.com/pricing

Your data will be stored for 30 days.

- CraftorAI Team";
    }

    #endregion
}
