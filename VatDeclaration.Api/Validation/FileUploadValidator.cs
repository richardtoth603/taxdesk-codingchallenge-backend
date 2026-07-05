using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VatDeclaration.Api.Models;

namespace VatDeclaration.Api.Validation;

/// <summary>
/// Validates uploaded files before they are ever parsed: extension, declared content type,
/// size, and a light "magic bytes" sniff to reject obviously non-text/binary payloads
/// (e.g. someone renaming an .exe to .csv).
/// </summary>
public class FileUploadValidator
{
    private readonly FileUploadOptions _options;

    public FileUploadValidator(IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
    }

    public (bool IsValid, string? Error) Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return (false, "No file was uploaded.");
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            return (false, $"File exceeds the maximum allowed size of {_options.MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension))
        {
            return (false, $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        var allowedContentTypes = new[]
        {
            "text/csv", "application/vnd.ms-excel", "text/plain", "application/csv", "application/octet-stream"
        };
        if (!string.IsNullOrEmpty(file.ContentType) && !allowedContentTypes.Contains(file.ContentType))
        {
            return (false, $"Unsupported content type '{file.ContentType}'.");
        }

        if (!LooksLikeTextFile(file))
        {
            return (false, "The file content does not look like a valid text/CSV file.");
        }

        return (true, null);
    }

    /// <summary>
    /// Reads a small sample of the file and rejects it if it contains a high proportion
    /// of non-printable bytes or a known executable/archive file signature.
    /// </summary>
    private static bool LooksLikeTextFile(IFormFile file)
    {
        const int sampleSize = 512;
        byte[] buffer = new byte[Math.Min(sampleSize, file.Length)];

        using (var stream = file.OpenReadStream())
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            stream.Position = 0;
            if (read <= 0)
            {
                return false;
            }
        }

        // Reject known binary/executable magic numbers (MZ, ELF, ZIP/Office, PDF, PNG, JPEG...).
        byte[][] blockedSignatures =
        {
            new byte[] { 0x4D, 0x5A },                   // MZ (EXE/DLL)
            new byte[] { 0x7F, 0x45, 0x4C, 0x46 },        // ELF
            new byte[] { 0x50, 0x4B, 0x03, 0x04 },        // ZIP / docx / xlsx
            new byte[] { 0x25, 0x50, 0x44, 0x46 },        // %PDF
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },        // PNG
            new byte[] { 0xFF, 0xD8, 0xFF },              // JPEG
        };

        foreach (var signature in blockedSignatures)
        {
            if (buffer.Length >= signature.Length && buffer.Take(signature.Length).SequenceEqual(signature))
            {
                return false;
            }
        }

        int nonPrintable = buffer.Count(b => b < 8 || (b > 13 && b < 32));
        double ratio = buffer.Length == 0 ? 1 : (double)nonPrintable / buffer.Length;
        return ratio < 0.05;
    }
}
