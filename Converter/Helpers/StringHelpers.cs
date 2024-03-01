namespace Converter.Helpers
{
    public static class StringHelpers
    {
        public static string Capitalize(this string word)
        {
            return word[..1].ToUpper() + word[1..].ToLower();
        }
    }
}
