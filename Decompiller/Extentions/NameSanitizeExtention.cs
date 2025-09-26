namespace Decompiller.Extentions
{
    public static class NameSanitizeExtention
    {
        public static string SanitizeName(this string name)
        {
            if (string.IsNullOrWhiteSpace(name)
                || name.Contains("'"))
                return name;

            if(name.Contains("<")
                || name.Contains(">")
                || name.Contains("$"))
            {
                //return $"'{name}'";
                return name.Replace("<", "_").Normalize().Replace(">", "_").Replace("$", "_");
            }

            return name;
        }
    }
}
