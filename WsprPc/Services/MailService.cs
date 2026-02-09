using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WsprPc.Services;

/// <summary>
/// Result of parsing an AI response for email content.
/// </summary>
public record MailResult(string? EmailTo, string? Subject, string? Body, string RawResponse);

/// <summary>
/// Service for handling mail prompt functionality - parsing AI responses and opening the mail client.
/// </summary>
public class MailService
{
    /// <summary>
    /// Parses an AI response to extract email components (EMAIL_TO, SUBJECT, BODY).
    /// </summary>
    public MailResult ParseMailResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new MailResult(null, null, null, response);

        try
        {
            // Try to find structured format
            var emailToMatch = Regex.Match(response, @"EMAIL_TO:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            var subjectMatch = Regex.Match(response, @"SUBJECT:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            var bodyMatch = Regex.Match(response, @"BODY:\s*([\s\S]*)", RegexOptions.IgnoreCase);

            var emailTo = emailToMatch.Success ? emailToMatch.Groups[1].Value.Trim() : null;
            var subject = subjectMatch.Success ? subjectMatch.Groups[1].Value.Trim() : null;
            var body = bodyMatch.Success ? bodyMatch.Groups[1].Value.Trim() : null;

            // If we got all fields, return them
            if (!string.IsNullOrWhiteSpace(emailTo) && 
                !string.IsNullOrWhiteSpace(subject) && 
                !string.IsNullOrWhiteSpace(body))
            {
                return new MailResult(emailTo, subject, body, response);
            }

            // Fallback: try to find email with regex
            var emailPattern = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
            var emailMatch = emailPattern.Match(response);
            
            if (emailMatch.Success)
            {
                // Use found email with entire response as body
                return new MailResult(emailMatch.Value, "AI Generated Email", response, response);
            }

            // Last fallback: return empty fields
            return new MailResult(null, null, null, response);
        }
        catch (Exception)
        {
            return new MailResult(null, null, null, response);
        }
    }

    /// <summary>
    /// Opens the default mail client with a pre-filled draft email.
    /// The email is NOT sent automatically - it opens for the user to review and edit.
    /// </summary>
    public bool OpenMailClient(string to, string subject, string body)
    {
        try
        {
            var mailto = $"mailto:{Uri.EscapeDataString(to)}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
            
            Process.Start(new ProcessStartInfo
            {
                FileName = mailto,
                UseShellExecute = true
            });
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the system prompt for mail mode, instructing the AI to search for email addresses
    /// and compose emails in a structured format.
    /// </summary>
    public string BuildMailSystemPrompt(string userInstruction)
    {
        return $@"Användaren kommer att be om att skicka ett mail till en person eller be om en mailadress.

1. Sök upp personens e-postadress via Google Search
2. Komponera ett mail baserat på vad användaren säger

Svara ALLTID i följande format:
EMAIL_TO: [e-postadress]
SUBJECT: [ämnesrad]
BODY: [meddelandetext]

Följ användarens instruktioner nedan för tonalitet och stil:

{userInstruction}";
    }
}
