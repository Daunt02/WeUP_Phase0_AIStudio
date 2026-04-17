using System.Text;
using System.Text.RegularExpressions;

namespace WeUP.Domain.Dedupe.FuzzyMatching;

// ============================================================================
// M2-P07: FuzzyNormalizationHelpers — Text Normalization Utilities v1.0
//
// NORMALIZATION PIPELINE (applied in order):
//   1. Null / whitespace guard → return string.Empty
//   2. Unicode NFC normalization (canonical composition)
//   3. Lowercase (invariant culture)
//   4. Abbreviation expansion (domain-specific tables)
//   5. Punctuation → single space
//   6. Whitespace collapse → single space between tokens
//   7. Trim
//
// DETERMINISM CONTRACT:
//   For any fixed input string, NormalizeTitle / NormalizeVenue always produce
//   the same output. No locale-sensitive operations are used after step 3.
//
// UNIT TEST HELPER CONTRACT:
//   All public methods in this class are pure and can be called directly
//   from test code without any DI or test doubles.
// ============================================================================

/// <summary>
/// Static, pure text normalization utilities for fuzzy matching.
///
/// Two normalization profiles are provided:
///   <see cref="NormalizeTitle"/> — For event title fields. Expands common
///     entertainment and event abbreviations (feat., ft., vs., &amp;, @, w/).
///   <see cref="NormalizeVenue"/> — For venue name and address fields. Expands
///     street-type abbreviations (St, Ave, Blvd) and cardinal directions.
///
/// Both profiles apply the full normalization pipeline; they differ only in
/// which abbreviation expansion table is applied.
///
/// Helper methods for individual pipeline steps are exposed as internal
/// (visible to test assembly via InternalsVisibleTo) for fine-grained testing.
/// </summary>
public static partial class FuzzyNormalizationHelpers
{
    // -------------------------------------------------------------------------
    // Title abbreviation table
    //
    // Keys are matched as whole tokens (word-boundary check applied).
    // Values are their canonical expanded forms.
    // Listed in typical order of ambiguity risk (most context-sensitive first).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Abbreviation expansions applied to <b>title</b> fields only.
    ///
    /// Design notes:
    /// - "w/" is listed as-is; the slash is removed in the punctuation step
    ///   after expansion, so "w/ special guest" → "with special guest".
    /// - "&amp;" is a punctuation character but listed here so the expansion
    ///   happens before punctuation stripping.
    /// - Short tokens ("ft", "vs") carry ambiguity risk in address context,
    ///   so they belong to TitleAbbreviations only, not VenueAbbreviations.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> TitleAbbreviations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Featuring / collaboration markers
            ["feat"]   = "featuring",
            ["feat."]  = "featuring",
            ["ft"]     = "featuring",
            ["ft."]    = "featuring",
            // Versus
            ["vs"]     = "versus",
            ["vs."]    = "versus",
            // With
            ["w/"]     = "with",
            // Ampersand — expand before punctuation stripping so "A & B" → "a and b"
            ["&"]      = "and",
            // At sign — common in event titles "DJ Kool @ The Venue"
            ["@"]      = "at",
            // Presents abbreviations
            ["pres"]   = "presents",
            ["pres."]  = "presents",
        };

    // -------------------------------------------------------------------------
    // Venue / address abbreviation table
    //
    // Keys are whole-token matched. Values are canonical expanded street types
    // and cardinal directions per USPS Publication 28 conventions.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Abbreviation expansions applied to <b>venue</b> and <b>address</b> fields.
    ///
    /// Cardinal directions (N, S, E, W) are included here but NOT in the title
    /// table because they would wrongly expand standalone musical notes / grades
    /// in event titles ("Key of E", "Grade A").
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> VenueAbbreviations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Street type expansions (USPS Publication 28)
            ["st"]    = "street",
            ["st."]   = "street",
            ["ave"]   = "avenue",
            ["ave."]  = "avenue",
            ["blvd"]  = "boulevard",
            ["blvd."] = "boulevard",
            ["dr"]    = "drive",
            ["dr."]   = "drive",
            ["rd"]    = "road",
            ["rd."]   = "road",
            ["ln"]    = "lane",
            ["ln."]   = "lane",
            ["ct"]    = "court",
            ["ct."]   = "court",
            ["pl"]    = "place",
            ["pl."]   = "place",
            ["sq"]    = "square",
            ["sq."]   = "square",
            ["hwy"]   = "highway",
            ["hwy."]  = "highway",
            ["pkwy"]  = "parkway",
            ["pkwy."] = "parkway",
            ["fwy"]   = "freeway",
            ["fwy."]  = "freeway",
            ["cir"]   = "circle",
            ["cir."]  = "circle",
            ["aly"]   = "alley",
            ["aly."]  = "alley",
            ["ter"]   = "terrace",
            ["ter."]  = "terrace",
            ["trl"]   = "trail",
            ["trl."]  = "trail",
            ["xing"]  = "crossing",
            ["xing."] = "crossing",
            // Cardinal directions
            ["n"]     = "north",
            ["n."]    = "north",
            ["s"]     = "south",
            ["s."]    = "south",
            ["e"]     = "east",
            ["e."]    = "east",
            ["w"]     = "west",
            ["w."]    = "west",
            ["ne"]    = "northeast",
            ["ne."]   = "northeast",
            ["nw"]    = "northwest",
            ["nw."]   = "northwest",
            ["se"]    = "southeast",
            ["se."]   = "southeast",
            ["sw"]    = "southwest",
            ["sw."]   = "southwest",
            // Suite / unit designators
            ["ste"]   = "suite",
            ["ste."]  = "suite",
            ["apt"]   = "apartment",
            ["apt."]  = "apartment",
            ["bldg"]  = "building",
            ["bldg."] = "building",
            ["flr"]   = "floor",
            ["flr."]  = "floor",
        };

    // -------------------------------------------------------------------------
    // Pre-compiled regex for whitespace normalization.
    // GeneratedRegex is used for .NET 7+ source-generated, zero-overhead regex.
    // -------------------------------------------------------------------------

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceRegex();

    // =========================================================================
    // Public normalization methods
    // =========================================================================

    /// <summary>
    /// Normalize an event title string for fuzzy comparison.
    ///
    /// Pipeline:
    ///   null/whitespace → string.Empty
    ///   → NFC normalization
    ///   → lowercase
    ///   → expand TitleAbbreviations
    ///   → punctuation → space
    ///   → whitespace collapse + trim
    ///
    /// Examples:
    ///   "DJ Kool feat. Lil Mo"   → "dj kool featuring lil mo"
    ///   "Salsa Night w/ live band" → "salsa night with live band"
    ///   "Rock &amp; Roll Revival"     → "rock and roll revival"
    ///   null                     → ""
    /// </summary>
    public static string NormalizeTitle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = NormalizeUnicodeNfc(input);
        normalized = normalized.ToLowerInvariant();
        normalized = ExpandAbbreviations(normalized, TitleAbbreviations);
        normalized = ReplacePunctuationWithSpace(normalized);
        normalized = CollapseWhitespace(normalized);
        return normalized;
    }

    /// <summary>
    /// Normalize a venue name or address string for fuzzy comparison.
    ///
    /// Pipeline:
    ///   null/whitespace → string.Empty
    ///   → NFC normalization
    ///   → lowercase
    ///   → expand VenueAbbreviations
    ///   → punctuation → space
    ///   → whitespace collapse + trim
    ///
    /// Examples:
    ///   "Skyline Club, 123 Main St"  → "skyline club 123 main street"
    ///   "Warehouse 22 N. 5th Ave"    → "warehouse 22 north 5th avenue"
    ///   "The Grand Ballroom - Suite 100 Ste." → "the grand ballroom suite 100 suite"
    ///   null                         → ""
    /// </summary>
    public static string NormalizeVenue(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = NormalizeUnicodeNfc(input);
        normalized = normalized.ToLowerInvariant();
        normalized = ExpandAbbreviations(normalized, VenueAbbreviations);
        normalized = ReplacePunctuationWithSpace(normalized);
        normalized = CollapseWhitespace(normalized);
        return normalized;
    }

    // =========================================================================
    // Internal pipeline steps — exposed for fine-grained unit testing
    // =========================================================================

    /// <summary>
    /// Apply Unicode NFC (canonical composition) normalization.
    /// Ensures combining characters are represented consistently before
    /// any ASCII-level comparison.
    /// </summary>
    internal static string NormalizeUnicodeNfc(string input) =>
        input.Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Replace every non-alphanumeric, non-whitespace character with a space.
    ///
    /// Rationale: punctuation differences between sources (hyphens, commas,
    /// colons, slashes) should not penalize similarity scoring when the
    /// underlying tokens are identical.
    ///
    /// Characters preserved: letters (A–Z, a–z), digits (0–9), whitespace.
    /// All other characters → single space (collapsed downstream).
    /// </summary>
    internal static string ReplacePunctuationWithSpace(string input)
    {
        if (input.Length == 0)
        {
            return input;
        }

        var buffer = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            buffer[i] = char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ';
        }

        return new string(buffer);
    }

    /// <summary>
    /// Replace any run of whitespace characters with a single space and trim
    /// leading/trailing whitespace.
    /// </summary>
    internal static string CollapseWhitespace(string input) =>
        MultipleWhitespaceRegex().Replace(input, " ").Trim();

    /// <summary>
    /// Expand abbreviations in <paramref name="input"/> using the provided
    /// <paramref name="table"/>. Matching is token-based (whole-word only)
    /// and case-insensitive relative to the already-lowercased input.
    ///
    /// Algorithm:
    ///   Split on whitespace → check each token against the table →
    ///   replace matched tokens with the canonical expanded form →
    ///   rejoin with single spaces.
    ///
    /// Note: the input is expected to be already lowercased. The table keys
    ///   are compared case-insensitively as a defensive measure.
    ///
    /// Example (title table):
    ///   "lil mo feat. someone"  →  "lil mo featuring someone"
    ///
    /// Example (venue table):
    ///   "123 main st"  →  "123 main street"
    /// </summary>
    internal static string ExpandAbbreviations(string input, IReadOnlyDictionary<string, string> table)
    {
        if (input.Length == 0 || table.Count == 0)
        {
            return input;
        }

        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(input.Length + 32);
        var changed = false;

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (i > 0)
            {
                sb.Append(' ');
            }

            if (table.TryGetValue(token, out var expansion))
            {
                sb.Append(expansion);
                changed = true;
            }
            else
            {
                sb.Append(token);
            }
        }

        return changed ? sb.ToString() : input;
    }

    // =========================================================================
    // Shared similarity primitives — used by matchers
    // =========================================================================

    /// <summary>
    /// Compute blended text similarity: 60% normalized Levenshtein + 40% token Jaccard.
    ///
    /// Both inputs should already be normalized by the appropriate profile
    /// (NormalizeTitle or NormalizeVenue) before calling this method.
    ///
    /// Returns 0.0 when either input is empty (explicit null-penalty contract).
    /// Returns 1.0 on exact string equality after normalization (short-circuit).
    ///
    /// Score interpretation:
    ///   1.00 = Identical after normalization
    ///   0.80+ = Very likely same entity (e.g., "skyline club" vs "skyline club downtown")
    ///   0.50–0.79 = Partial match; may share significant tokens
    ///   0.00–0.49 = Weak or no textual similarity
    /// </summary>
    /// <param name="normalizedLeft">Pre-normalized left-side string.</param>
    /// <param name="normalizedRight">Pre-normalized right-side string.</param>
    public static double BlendedSimilarity(string normalizedLeft, string normalizedRight)
    {
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0.0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var levenshtein = NormalizedLevenshteinSimilarity(normalizedLeft, normalizedRight);
        var jaccard = TokenJaccardSimilarity(normalizedLeft, normalizedRight);

        return Clamp01((0.6 * levenshtein) + (0.4 * jaccard));
    }

    /// <summary>
    /// Normalized Levenshtein similarity: 1 - (editDistance / maxLength).
    /// Returns values in [0.0, 1.0].
    ///
    /// Time complexity: O(m×n) where m and n are string lengths.
    /// For typical event title/venue strings (≤200 chars) this is negligible.
    /// </summary>
    internal static double NormalizedLevenshteinSimilarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0.0;
        }

        var distance = LevenshteinDistance(left, right);
        var maxLen = Math.Max(left.Length, right.Length);
        return Clamp01(1.0 - ((double)distance / maxLen));
    }

    /// <summary>
    /// Standard Levenshtein (edit) distance between two strings.
    /// Uses the standard DP matrix approach.
    /// </summary>
    internal static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;

        var matrix = new int[left.Length + 1, right.Length + 1];

        for (var i = 0; i <= left.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    /// <summary>
    /// Token Jaccard similarity: |intersection(A,B)| / |union(A,B)|.
    ///
    /// Tokens are defined as whitespace-separated words in the already-normalized
    /// input. Duplicate tokens are deduplicated (set semantics).
    ///
    /// Returns 0.0 when either side has no tokens.
    ///
    /// Score interpretation:
    ///   1.00 = Exact same vocabulary
    ///   0.50 = Half the tokens are shared
    ///   0.00 = No shared tokens
    /// </summary>
    internal static double TokenJaccardSimilarity(string normalizedLeft, string normalizedRight)
    {
        var leftTokens = SplitToTokenSet(normalizedLeft);
        var rightTokens = SplitToTokenSet(normalizedRight);

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0.0;
        }

        var intersection = 0;
        foreach (var token in leftTokens)
        {
            if (rightTokens.Contains(token))
            {
                intersection++;
            }
        }

        if (intersection == 0) return 0.0;

        var union = leftTokens.Count + rightTokens.Count - intersection;
        return union <= 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// Split a normalized string into a deduplicated token set.
    /// Uses Ordinal comparison (input is already lowercased).
    /// </summary>
    internal static HashSet<string> SplitToTokenSet(string normalizedValue)
    {
        var parts = normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new HashSet<string>(parts, StringComparer.Ordinal);
    }

    /// <summary>Clamp a value to the [0.0, 1.0] range.</summary>
    internal static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    /// <summary>Round a score to 4 decimal places for stable display.</summary>
    internal static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
