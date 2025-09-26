namespace Decompiller.Extentions
{
    public static class NameSanitizeExtention
    {
        public static string SanitizeName(this string name)
        {
            return string.IsNullOrWhiteSpace(name) || name.Contains("'") ? name : $"'{name}'";
            //return name.Replace("<", "").Normalize().Replace(">", "").Replace("$", "_");
        }
    }
}
