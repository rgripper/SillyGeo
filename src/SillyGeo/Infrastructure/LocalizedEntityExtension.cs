namespace SillyGeo.Infrastructure
{
    public static class LocalizedEntityExtensions
    {
        public static string GetLocalizedName(this ILocalizedEntity localized, string cultureName)
        {
            string value;
            if (localized.NamesByCultures.TryGetValue(cultureName, out value))
            {
                return value;
            }

            return localized.Name;
        }
    }
}
