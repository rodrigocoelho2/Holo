using System.Text;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

/// <summary>
/// Minimal streaming CSV reader (RFC 4180: quoted fields, doubled quotes, line breaks inside quotes).
/// Streaming matters: a retailer's feed can be hundreds of megabytes. Synchronous by design: callers run the whole
/// parse on a worker thread (<see cref="Task.Run(Action)"/>) instead of awaiting once per character.
/// </summary>
/// <param name="reader">The text to read.</param>
/// <param name="delimiter">The field delimiter.</param>
internal sealed class CsvRecordReader(TextReader reader, char delimiter = ',')
{
    private readonly StringBuilder _field = new();

    /// <summary>Gets or sets the field delimiter (can be changed after reading the header).</summary>
    public char Delimiter { get; set; } = delimiter;

    /// <summary>
    /// Reads the next record.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fields, or null at the end of the input.</returns>
    public List<string>? ReadRecord(CancellationToken cancellationToken)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var anyCharacter = false;
        _field.Clear();

        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                if (!anyCharacter)
                {
                    return null;
                }

                fields.Add(_field.ToString());
                return fields;
            }

            anyCharacter = true;
            var character = (char)next;

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        _field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    _field.Append(character);
                }

                continue;
            }

            if (character == '"' && _field.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == Delimiter)
            {
                fields.Add(_field.ToString());
                _field.Clear();
            }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && reader.Peek() == '\n')
                {
                    reader.Read();
                }

                if (fields.Count == 0 && _field.Length == 0)
                {
                    anyCharacter = false;   // blank line
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                fields.Add(_field.ToString());
                return fields;
            }
            else
            {
                _field.Append(character);
            }
        }
    }
}
