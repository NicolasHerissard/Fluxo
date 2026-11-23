using System.Text;
using System.Text.RegularExpressions;

namespace Finance.Android17.Maui;

/// <summary>
/// Icônes de l'application : des tracés sur une grille de 24 × 24 (jeu Lucide, comme la maquette), dessinés par
/// <see cref="IconView"/>. Une icône est un simple nom, ce qui permet de la choisir depuis un ViewModel et de la
/// teinter à volonté, dans les deux thèmes.
/// </summary>
internal static partial class AppIcons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["bell"] = "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9 M10.3 21a1.94 1.94 0 0 0 3.4 0",
        ["plus"] = "M5 12h14 M12 5v14",
        ["x"] = "M18 6 6 18 M6 6l12 12",
        ["check"] = "M20 6 9 17l-5-5",
        ["lock"] = "M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M7 11V7a5 5 0 0 1 10 0v4",
        ["eye"] = "M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0 M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0",
        ["eye-off"] = "M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49 M14.084 14.158a3 3 0 0 1-4.242-4.242 M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143 M2 2l20 20",
        ["pencil"] = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z M15 5l4 4",
        ["trash"] = "M3 6h18 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M10 11v6 M14 11v6",
        ["home"] = "M4 3h5a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M15 3h5a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1h-5a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M15 12h5a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-5a1 1 0 0 1-1-1v-7a1 1 0 0 1 1-1z M4 16h5a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-3a1 1 0 0 1 1-1z",
        ["receipt"] = "M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1z M14 8H8 M16 12H8 M13 16H8",
        ["repeat"] = "M2 9l3-3 3 3 M13 18H7a2 2 0 0 1-2-2V6 M22 15l-3 3-3-3 M11 6h6a2 2 0 0 1 2 2v10",
        ["wallet"] = "M19 7V4a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v4h-3a2 2 0 0 0 0 4h3a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1 M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1v-4",
        ["settings"] = "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0",
        ["sun"] = "M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0 M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41",
        ["moon"] = "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z",
        ["monitor"] = "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M8 21h8 M12 17v4",
        ["arrow-up-right"] = "M7 7h10v10 M7 17L17 7",
        ["arrow-down-left"] = "M17 7L7 17 M17 17H7V7",
        ["chevron-right"] = "M9 18l6-6-6-6",
        ["chevron-down"] = "M6 9l6 6 6-6",
        ["chevron-left"] = "M15 18l-6-6 6-6",
        ["sliders"] = "M21 4h-7 M10 4H3 M21 12h-9 M8 12H3 M21 20h-5 M12 20H3 M14 2v4 M8 10v4 M16 18v4",
        ["search"] = "M21 21l-4.34-4.34 M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0",
        ["calendar"] = "M8 2v4 M16 2v4 M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M3 10h18",
        ["trending-down"] = "M16 17h6v-6 M22 17l-8.5-8.5-5 5L2 7",
        ["trending-up"] = "M16 7h6v6 M22 7l-8.5 8.5-5-5L2 17",
        ["alert"] = "M21.73 18l-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3 M12 9v4 M12 17h.01",
        ["circle-check"] = "M21.801 10A10 10 0 1 1 17 3.335 M9 11l3 3L22 4",
        ["tag"] = "M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z M7.5 7.5h.01",
        ["database"] = "M21 5a9 3 0 1 1-18 0 9 3 0 0 1 18 0 M3 5v14a9 3 0 0 0 18 0V5 M3 12a9 3 0 0 0 18 0",
        ["shield"] = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M9 12l2 2 4-4",
        ["camera"] = "M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z M16 13a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
        ["target"] = "M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0 M18 12a6 6 0 1 1-12 0 6 6 0 0 1 12 0 M14 12a2 2 0 1 1-4 0 2 2 0 0 1 4 0",
    };

    private static readonly Dictionary<string, PathF> Cache = new(StringComparer.Ordinal);

    /// <summary>Le tracé de l'icône <paramref name="name"/> (grille 24 × 24), ou <c>null</c> si elle n'existe pas.</summary>
    public static PathF? Get(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Cache.TryGetValue(name, out var cached)) return cached;
        if (!Paths.TryGetValue(name, out var data)) return null;
        return Cache[name] = PathBuilder.Build(Normalize(data));
    }

    /// <summary>
    /// Écrit chaque commande avec tous ses nombres séparés par des espaces et répète la commande pour chaque groupe,
    /// ce qui permet de coller telles quelles les données compactes d'un fichier SVG (« 0-.696 », « .5.5 »).
    /// </summary>
    internal static string Normalize(string data)
    {
        var tokens = Tokens().Matches(data);
        var result = new StringBuilder();
        var command = ' ';
        var index = 0;
        while (index < tokens.Count)
        {
            var token = tokens[index].Value;
            if (char.IsLetter(token[0]))
            {
                command = token[0];
                index++;
                if (command is 'Z' or 'z') result.Append("Z ");
                continue;
            }

            var count = ArgumentCount(command);
            if (count == 0 || index + count > tokens.Count) break;
            result.Append(command);
            for (var i = 0; i < count; i++) result.Append(' ').Append(tokens[index + i].Value);
            result.Append(' ');
            index += count;
            if (command == 'M') command = 'L';
            else if (command == 'm') command = 'l';
        }
        return result.ToString().TrimEnd();
    }

    private static int ArgumentCount(char command) => char.ToUpperInvariant(command) switch
    {
        'M' or 'L' or 'T' => 2,
        'H' or 'V' => 1,
        'C' => 6,
        'S' or 'Q' => 4,
        'A' => 7,
        _ => 0
    };

    [GeneratedRegex(@"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex Tokens();
}
