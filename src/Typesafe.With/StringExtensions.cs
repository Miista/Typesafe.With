namespace Typesafe.With;

internal static class StringExtensions
{
    extension(string s)
    {
        public string ToParameterCase()
        {
            var firstLetterInLowercase = char.ToLowerInvariant(s[0]);
            var remainingString = s.Substring(1);

            return firstLetterInLowercase + remainingString;
        }

        public string ToPropertyCase()
        {
            var firstLetterInLowercase = char.ToUpperInvariant(s[0]);
            var remainingString = s.Substring(1);

            return firstLetterInLowercase + remainingString;
        }
    }
}