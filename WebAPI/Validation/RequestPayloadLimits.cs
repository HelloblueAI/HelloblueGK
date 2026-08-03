using System.Text.Json;
using System.Text.RegularExpressions;

namespace HB_NLP_Research_Lab.WebAPI.Validation;

public static class RequestPayloadLimits
{
    public const int MaxDictionaryEntries = 50;
    public const int MaxDictionaryKeyLength = 100;
    public const int MaxSerializedJsonBytes = 16 * 1024;
    public const int MaxShortTextLength = 200;
    public const int MaxLongTextLength = 1000;

    // Reject keys that look like secrets so they are not persisted/echoed in API responses.
    private static readonly Regex SensitiveKeyPattern = new(
        @"(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|authorization|auth[_-]?header|bearer|credential|connectionstring)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryValidateDictionary<TValue>(
        IReadOnlyDictionary<string, TValue>? values,
        string fieldName,
        out string? validationMessage)
    {
        validationMessage = null;
        if (values == null)
        {
            return true;
        }

        if (values.Count > MaxDictionaryEntries)
        {
            validationMessage = $"{fieldName} cannot contain more than {MaxDictionaryEntries} entries.";
            return false;
        }

        if (values.Keys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > MaxDictionaryKeyLength))
        {
            validationMessage = $"{fieldName} keys must be non-empty and cannot exceed {MaxDictionaryKeyLength} characters.";
            return false;
        }

        var sensitiveKey = values.Keys.FirstOrDefault(IsSensitiveKey);
        if (sensitiveKey != null)
        {
            validationMessage = $"{fieldName} cannot include sensitive key '{sensitiveKey}'.";
            return false;
        }

        try
        {
            var serializedSize = JsonSerializer.SerializeToUtf8Bytes(values).Length;
            if (serializedSize > MaxSerializedJsonBytes)
            {
                validationMessage = $"{fieldName} cannot exceed {MaxSerializedJsonBytes} serialized bytes.";
                return false;
            }
        }
        catch (NotSupportedException)
        {
            validationMessage = $"{fieldName} contains unsupported values.";
            return false;
        }

        return true;
    }

    public static bool IsSensitiveKey(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && SensitiveKeyPattern.IsMatch(key);
    }

    public static bool TryValidateOptionalText(
        string? value,
        string fieldName,
        int maxLength,
        out string? validationMessage)
    {
        validationMessage = null;
        if (value == null || value.Length <= maxLength)
        {
            return true;
        }

        validationMessage = $"{fieldName} cannot exceed {maxLength} characters.";
        return false;
    }
}
