using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace BancontactPro.Net.Sample.Web.Debugging;

/// <summary>
/// Describes what a callback's Signature header looks like, so a rejected callback can be understood without a
/// debugger: how many dots it has, what the JOSE header says, how big the body is. It verifies nothing.
/// </summary>
public static class CallbackDiagnostics
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Analyse(string signatureHeader, byte[] body)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            lines.Add("No Signature header was sent.");
            return string.Join('\n', lines);
        }

        var parts = signatureHeader.Split('.');
        var form = parts.Length switch
        {
            2 => "header.signature (one dot)",
            3 when parts[1].Length == 0 => "header..signature (two dots, empty payload segment)",
            3 => "header.payload.signature (a non-empty middle segment)",
            _ => $"{parts.Length - 1} dots",
        };
        lines.Add($"Signature header: {signatureHeader.Length} characters, form {form}.");
        lines.Add($"Body: {body.Length} bytes.");

        try
        {
            var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0]));
            lines.Add("JOSE header:");
            lines.Add(JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Indented));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            lines.Add($"The first segment is not a base64url JSON header ({ex.GetType().Name}).");
        }

        var signature = parts[^1];
        try
        {
            lines.Add($"Signature segment: {Base64Url.DecodeFromChars(signature).Length} bytes (ES256 expects 64).");
        }
        catch (FormatException)
        {
            lines.Add("The last segment is not valid base64url.");
        }

        return string.Join('\n', lines);
    }
}