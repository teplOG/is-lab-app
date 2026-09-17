public static class NoteRules
{
    public const int MaxTitleLength = 200;

    public static string? Validate(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required";
        }

        if (title.Length > MaxTitleLength)
        {
            return $"Title must be {MaxTitleLength} characters or less";
        }

        return null;
    }
}
