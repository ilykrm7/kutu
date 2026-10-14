using System.Security.Cryptography;
using System.Text;

namespace Kutu.Core.Security;

public sealed record PasswordRecipe(
    int Length,
    bool Upper,
    bool Lower,
    bool Digits,
    bool Symbols,
    bool ExcludeAmbiguous)
{
    public static PasswordRecipe Default { get; } = new(20, true, true, true, true, true);
}

public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";
    private const string Ambiguous = "0Oo1lI|";

    public static string Create(PasswordRecipe recipe)
    {
        var length = recipe.Length < 4 ? 4 : recipe.Length > 128 ? 128 : recipe.Length;
        var sets = new List<string>();
        if (recipe.Upper)
            sets.Add(Filter(Upper, recipe.ExcludeAmbiguous));
        if (recipe.Lower)
            sets.Add(Filter(Lower, recipe.ExcludeAmbiguous));
        if (recipe.Digits)
            sets.Add(Filter(Digits, recipe.ExcludeAmbiguous));
        if (recipe.Symbols)
            sets.Add(Filter(Symbols, recipe.ExcludeAmbiguous));
        sets.RemoveAll(set => set.Length == 0);
        if (sets.Count == 0)
            sets.Add(Filter(Lower + Digits, recipe.ExcludeAmbiguous));

        var all = string.Concat(sets);
        var chars = new char[length];
        using var rng = RandomNumberGenerator.Create();
        for (var i = 0; i < chars.Length; i++)
        {
            var alphabet = i < sets.Count ? sets[i] : all;
            chars[i] = alphabet[Next(rng, alphabet.Length)];
        }

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var swap = Next(rng, i + 1);
            (chars[i], chars[swap]) = (chars[swap], chars[i]);
        }

        return new string(chars);
    }

    private static int Next(RandomNumberGenerator rng, int max)
    {
        if (max <= 1)
            return 0;

        var buffer = new byte[4];
        var limit = uint.MaxValue - (uint.MaxValue % (uint)max);
        uint value;
        do
        {
            rng.GetBytes(buffer);
            value = BitConverter.ToUInt32(buffer, 0);
        }
        while (value >= limit);

        return (int)(value % (uint)max);
    }

    private static string Filter(string alphabet, bool excludeAmbiguous)
    {
        if (!excludeAmbiguous)
            return alphabet;

        var builder = new StringBuilder(alphabet.Length);
        foreach (var ch in alphabet)
        {
            if (Ambiguous.IndexOf(ch) < 0)
                builder.Append(ch);
        }

        return builder.ToString();
    }
}
