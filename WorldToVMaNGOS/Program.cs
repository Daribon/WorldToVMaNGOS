// WorldToVMaNGOS – Convert WoWEmu .save → VMaNGOS SQL
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Text;
global using System.Threading.Tasks;

const int startingGuid = 4_000_000;

if (args.Length == 0 || !File.Exists(args[0]))
{
    Console.WriteLine("WorldToVMaNGOS – WoWEmu .save to VMaNGOS SQL Converter");
    Console.WriteLine("Usage: Drag a .save file onto the executable, or run:");
    Console.WriteLine($" {Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "WorldToVMaNGOS")} world.save");
    Console.WriteLine("Output: <input>_converted.sql");
    return;
}

await ConvertFileAsync(args[0]);

static async Task ConvertFileAsync(string inputPath)
{
    string outputPath = Path.Combine(
        Path.GetDirectoryName(inputPath)!,
                                     $"{Path.GetFileNameWithoutExtension(inputPath)}_converted.sql");

    var creatures = new List<string>();
    var gameobjects = new List<string>();
    var creatureGuid = new GuidCounter(startingGuid);
    var goGuid = new GuidCounter(startingGuid);
    var currentObject = new StringBuilder();

    try
    {
        await foreach (var rawLine in File.ReadLinesAsync(inputPath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("[OBJECT]", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessCurrentObjectAsync(currentObject.ToString(),
                                                creatures, gameobjects,
                                                creatureGuid, goGuid);
                currentObject.Clear();
            }
            else if (!string.IsNullOrEmpty(line))
            {
                currentObject.AppendLine(line);
            }
        }
        await ProcessCurrentObjectAsync(currentObject.ToString(),
                                        creatures, gameobjects,
                                        creatureGuid, goGuid);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading input file: {ex.Message}");
        return;
    }

    try
    {
        await using var fs = new FileStream(outputPath,
                                            FileMode.Create,
                                            FileAccess.Write,
                                            FileShare.None,
                                            bufferSize: 4096,
                                            FileOptions.Asynchronous);
        await using var writer = new StreamWriter(fs,
                                                  new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync($"-- VMaNGOS SQL generated from {Path.GetFileName(inputPath)}");
        await writer.WriteLineAsync($"-- Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await writer.WriteLineAsync();

        if (creatures.Count > 0)
        {
            await writer.WriteLineAsync($"-- CREATURES ({creatures.Count} total)");
            await WriteRecordsAsync(writer, "creature", creatures,
                                    "(`guid`, `id`, `map`, `position_x`, `position_y`, `position_z`, `orientation`, `spawntimesecsmin`, `spawntimesecsmax`, `wander_distance`, `health_percent`)");
        }

        if (gameobjects.Count > 0)
        {
            if (creatures.Count > 0) await writer.WriteLineAsync();
            await writer.WriteLineAsync($"-- GAMEOBJECTS ({gameobjects.Count} total)");
            await WriteRecordsAsync(writer, "gameobject", gameobjects,
                                    "(`guid`, `id`, `map`, `position_x`, `position_y`, `position_z`, `orientation`, `rotation0`, `rotation1`, `rotation2`, `rotation3`, `spawntimesecsmin`, `spawntimesecsmax`)");
        }

        Console.WriteLine($"Conversion complete: {outputPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error writing output file: {ex.Message}");
    }
}

static async Task WriteRecordsAsync(StreamWriter writer, string table,
                                    List<string> records, string columns)
{
    await writer.WriteLineAsync($"INSERT INTO `{table}` {columns} VALUES");
    for (int i = 0; i < records.Count; i++)
    {
        string suffix = i < records.Count - 1 ? "," : ";";
        await writer.WriteLineAsync(records[i] + suffix);
    }
}

static async Task ProcessCurrentObjectAsync(string objectData,
                                            List<string> creatures,
                                            List<string> gameobjects,
                                            GuidCounter creatureGuid,
                                            GuidCounter goGuid)
{
    if (string.IsNullOrWhiteSpace(objectData)) return;

    var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using var reader = new StringReader(objectData);
    while (await reader.ReadLineAsync() is { } line)
    {
        int eq = line.IndexOf('=');
        if (eq > 0)
        {
            string key = line[..eq].Trim().ToUpperInvariant();
            string value = line[(eq + 1)..].Trim();
            properties[key] = value;
        }
    }

    if (!properties.TryGetValue("TYPE", out string? typeStr) ||
        !properties.TryGetValue("XYZ", out string? xyz))
        return;

    string[] xyzParts = xyz.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (xyzParts.Length < 4) return;

    string x = xyzParts[0], y = xyzParts[1], z = xyzParts[2], o = xyzParts[3];
    string map = properties.GetValueOrDefault("MAP", "0");
    const string spawnTime = "1000";
    const string wanderDist = "0";

    switch (typeStr)
    {
        case "3" when properties.TryGetValue("SPAWN", out string? spawn):
        {
            string entry = spawn.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            creatures.Add($"({creatureGuid.Next()}, {entry}, {map}, {x}, {y}, {z}, {o}, {spawnTime}, {spawnTime}, {wanderDist}, 100)");
            break;
        }
        case "3" when properties.TryGetValue("SPAWN_GOBJ", out string? gobjEntry):
        {
            gameobjects.Add($"({goGuid.Next()}, {gobjEntry}, {map}, {x}, {y}, {z}, {o}, 0, 0, 0, 1, {spawnTime}, {spawnTime})");
            break;
        }
        case "5" when properties.TryGetValue("ENTRY", out string? gobjEntry):
        {
            string r0 = "0", r1 = "0", r2 = "0", r3 = "1";
            if (properties.TryGetValue("ROTATION", out string? rotation))
            {
                string[] rot = rotation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (rot.Length >= 4)
                    (r0, r1, r2, r3) = (rot[0], rot[1], rot[2], rot[3]);
            }
            gameobjects.Add($"({goGuid.Next()}, {gobjEntry}, {map}, {x}, {y}, {z}, {o}, {r0}, {r1}, {r2}, {r3}, {spawnTime}, {spawnTime})");
            break;
        }
    }
}

file class GuidCounter(int start)
{
    private int _value = start;
    public int Next() => _value++;
}
