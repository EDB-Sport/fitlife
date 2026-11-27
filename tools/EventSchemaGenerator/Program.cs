using FitLife.Messaging;

// Usage: EventSchemaGenerator <directory>. A file is written only when it changes, so a build doesn't touch the rest.
string directory = args[0];
foreach ((string routingKey, string schema) in EventSchema.GenerateAll())
{
    string path = Path.Combine(directory, routingKey + ".schema.json");
    if (!File.Exists(path) || !string.Equals(await File.ReadAllTextAsync(path).ConfigureAwait(false), schema, StringComparison.Ordinal))
    {
        await File.WriteAllTextAsync(path, schema).ConfigureAwait(false);
    }
}
