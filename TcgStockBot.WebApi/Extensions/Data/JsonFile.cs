using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcgStockBot.WebApi.Extensions.Data;

/// <summary>
/// Reads and atomically writes the bot's JSON data files.
/// </summary>
internal static class JsonFile
{
    /// <summary>Serializer settings for every data file (camelCase, enums as text, indented).</summary>
    public static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    /// <summary>
    /// Reads a file, or returns the default value when it does not exist.
    /// </summary>
    /// <typeparam name="T">The file content type.</typeparam>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The deserialized content, or default.</returns>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Synchronous read, for start-up.
    /// </summary>
    /// <typeparam name="T">The file content type.</typeparam>
    /// <param name="path">The file path.</param>
    /// <returns>The deserialized content, or default.</returns>
    public static T? Read<T>(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), SerializerOptions) : default;

    /// <summary>
    /// Writes to a temporary file and renames it, so a crash never leaves a half-written file.
    /// </summary>
    /// <typeparam name="T">The content type.</typeparam>
    /// <param name="path">The file path.</param>
    /// <param name="value">The content.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is replaced.</returns>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    /// <summary>
    /// Deep copy through JSON, so callers can never mutate shared state.
    /// </summary>
    /// <typeparam name="T">The type to copy.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>An independent copy.</returns>
    public static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, SerializerOptions), SerializerOptions)!;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
