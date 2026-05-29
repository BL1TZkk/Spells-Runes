using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Common;

namespace SpellsAndRunes.Lore;

public enum SpellbookLoreCategory
{
    Lore,
    Journal
}

public sealed class SpellbookLoreEntry
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "lore";
    public string Title { get; set; } = "";
    public string Preview { get; set; } = "";
    public string[] Body { get; set; } = Array.Empty<string>();
    public bool UnlockedByDefault { get; set; }

    public SpellbookLoreCategory CategoryKind
        => string.Equals(Category, "journal", StringComparison.OrdinalIgnoreCase)
            ? SpellbookLoreCategory.Journal
            : SpellbookLoreCategory.Lore;
}

public static class SpellbookLoreRegistry
{
    private const string LoreAssetPath = "config/spellbook-lore.json";

    private static readonly List<SpellbookLoreEntry> entries = new();
    private static readonly Dictionary<string, SpellbookLoreEntry> byId = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SpellbookLoreEntry> All => entries;

    public static IEnumerable<SpellbookLoreEntry> ByCategory(SpellbookLoreCategory category)
        => entries.Where(entry => entry.CategoryKind == category);

    public static SpellbookLoreEntry? Get(string id)
        => byId.TryGetValue(id, out var entry) ? entry : null;

    public static void Load(ICoreAPI api)
    {
        entries.Clear();
        byId.Clear();

        try
        {
            var asset = FindLoreAsset(api);
            if (asset == null)
            {
                api.Logger.Warning("[Spells & Runes] Missing spellbook lore asset: spellsandrunes:{0}", LoreAssetPath);
                return;
            }

            var loaded = ReadEntries(asset.ToText());

            if (loaded == null)
            {
                api.Logger.Warning("[Spells & Runes] Spellbook lore asset did not contain a valid entry list.");
                return;
            }

            foreach (var entry in loaded)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                entry.Id = entry.Id.Trim();
                entry.Category = NormalizeCategory(entry.Category);
                entry.Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Id : entry.Title.Trim();
                entry.Body ??= Array.Empty<string>();

                if (string.IsNullOrWhiteSpace(entry.Preview))
                    entry.Preview = BuildPreview(entry.Body.FirstOrDefault() ?? "");

                if (byId.ContainsKey(entry.Id))
                {
                    api.Logger.Warning("[Spells & Runes] Duplicate spellbook lore id '{0}' ignored.", entry.Id);
                    continue;
                }

                entries.Add(entry);
                byId[entry.Id] = entry;
            }

#if DEBUG
            api.Logger.Notification("[Spells & Runes] Loaded {0} spellbook lore entries.", entries.Count);
#endif
        }
        catch (Exception e)
        {
            entries.Clear();
            byId.Clear();
            api.Logger.Warning("[Spells & Runes] Could not load spellbook lore entries: {0}", e.Message);
        }
    }

    private static IAsset? FindLoreAsset(ICoreAPI api)
    {
        foreach (var path in new[]
        {
            "spellsandrunes:config/spellbook-lore.json",
            "spellsandrunes:config/spellbook-lore",
        })
        {
            var asset = api.Assets.TryGet(new AssetLocation(path));
            if (asset != null) return asset;
        }

        return api.Assets.GetMany("config", "spellbook-lore", true)
            .FirstOrDefault(asset => asset.Location.Domain == "spellsandrunes");
    }

    private static List<SpellbookLoreEntry>? ReadEntries(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
            return JsonSerializer.Deserialize<List<SpellbookLoreEntry>>(json, options);

        var wrapped = JsonSerializer.Deserialize<SpellbookLoreFile>(json, options);
        return wrapped?.Entries;
    }

    private static string NormalizeCategory(string? category)
        => string.Equals(category, "journal", StringComparison.OrdinalIgnoreCase) ? "journal" : "lore";

    private static string BuildPreview(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length <= 78) return text;
        return text[..75].TrimEnd() + "...";
    }

    private sealed class SpellbookLoreFile
    {
        public List<SpellbookLoreEntry>? Entries { get; set; }
    }
}
