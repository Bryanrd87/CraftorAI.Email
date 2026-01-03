namespace CraftorAI.Email.Contracts;

/// <summary>
/// Unified email service interface for all CraftorAI projects.
/// Combines auth emails, subscription emails, and Stripe notification emails.
/// </summary>
public interface IEmailService
{
    // =====================================================
    // Core send methods
    // =====================================================

    /// <summary>
    /// Send a generic email with HTML and optional plain text body.
    /// </summary>
    Task<bool> SendEmailAsync(string to, string subject, string htmlBody, string? plainTextBody = null);

    /// <summary>
    /// Send a generic email with detailed result response.
    /// </summary>
    Task<EmailSendResponse> SendEmailWithResultAsync(string to, string subject, string htmlBody, string? plainTextBody = null);

    // =====================================================
    // Auth emails (from CraftorAI.AUTH)
    // =====================================================

    /// <summary>
    /// Send email verification link to new user.
    /// </summary>
    Task<bool> SendEmailVerificationAsync(string email, string verificationUrl);

    /// <summary>
    /// Send email verification link with custom expiration.
    /// </summary>
    Task<bool> SendEmailVerificationAsync(string email, string verificationUrl, int expirationHours);

    /// <summary>
    /// Send email verification with detailed result.
    /// </summary>
    Task<EmailSendResponse> SendEmailVerificationWithResultAsync(string email, string verificationUrl);

    /// <summary>
    /// Send email verification with detailed result and custom expiration.
    /// </summary>
    Task<EmailSendResponse> SendEmailVerificationWithResultAsync(string email, string verificationUrl, int expirationHours);

    /// <summary>
    /// Send password reset link.
    /// </summary>
    Task<bool> SendPasswordResetAsync(string email, string resetUrl);

    /// <summary>
    /// Send password reset link with custom expiration.
    /// </summary>
    Task<bool> SendPasswordResetAsync(string email, string resetUrl, int expirationHours);

    /// <summary>
    /// Send team invitation email.
    /// </summary>
    Task<bool> SendTeamInvitationAsync(string email, string firstName, string organizationName, string inviterName, string role, string acceptInvitationUrl);

    // =====================================================
    // Subscription lifecycle emails (from CraftorAI.Infrastructure)
    // =====================================================

    /// <summary>
    /// Send welcome email to new subscriber.
    /// </summary>
    Task SendWelcomeEmailAsync(string toEmail, string toName);

    /// <summary>
    /// Send trial ending reminder email.
    /// </summary>
    Task SendTrialEndingEmailAsync(string toEmail, string toName, int daysRemaining, string planName);

    /// <summary>
    /// Send trial ended notification.
    /// </summary>
    Task SendTrialEndedEmailAsync(string toEmail, string toName, string planName);

    /// <summary>
    /// Send payment failed notification (general).
    /// </summary>
    Task SendPaymentFailedEmailAsync(string toEmail, string toName, string planName, decimal amount);

    /// <summary>
    /// Send subscription cancelled notification (user-initiated).
    /// </summary>
    Task SendSubscriptionCancelledEmailAsync(string toEmail, string toName, string planName, DateTime endDate);

    // =====================================================
    // Stripe webhook notification emails (NEW)
    // =====================================================

    /// <summary>
    /// Send checkout completed / subscription confirmed email.
    /// Called when checkout.session.completed event is received.
    /// </summary>
    Task SendCheckoutCompletedEmailAsync(
        string toEmail,
        string toName,
        string planName,
        decimal amount,
        string billingPeriod,
        DateTime nextBillingDate);

    /// <summary>
    /// Send invoice paid / payment receipt email.
    /// Called when invoice.paid event is received.
    /// </summary>
    Task SendInvoicePaidEmailAsync(
        string toEmail,
        string toName,
        string invoiceNumber,
        decimal amount,
        string planName,
        string? invoiceUrl);

    /// <summary>
    /// Send invoice payment failed email.
    /// Called when invoice.payment_failed event is received.
    /// </summary>
    Task SendInvoicePaymentFailedEmailAsync(
        string toEmail,
        string toName,
        string invoiceNumber,
        decimal amount,
        string planName);

    /// <summary>
    /// Send subscription created email (including trial start).
    /// Called when customer.subscription.created event is received.
    /// </summary>
    Task SendSubscriptionCreatedEmailAsync(
        string toEmail,
        string toName,
        string planName,
        decimal amount,
        string billingPeriod,
        DateTime? trialEndDate);

    /// <summary>
    /// Send subscription updated email (plan change).
    /// Called when customer.subscription.updated event is received with plan change.
    /// </summary>
    Task SendSubscriptionUpdatedEmailAsync(
        string toEmail,
        string toName,
        string oldPlanName,
        string newPlanName,
        decimal newAmount,
        string billingPeriod);

    /// <summary>
    /// Send subscription cancelled by Stripe email.
    /// Called when customer.subscription.deleted event is received.
    /// </summary>
    Task SendSubscriptionCancelledByStripeEmailAsync(
        string toEmail,
        string toName,
        string planName,
        DateTime accessEndDate,
        string? reason);
}
