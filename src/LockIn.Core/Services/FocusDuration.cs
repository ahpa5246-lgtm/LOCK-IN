namespace LockIn.Core.Services;

public static class FocusDuration
{
    public const int MinimumMinutes = 3;
    public const int MaximumMinutes = 720;
    public const string ValidationMessage =
        "Enter a duration between 3 and 720 minutes.";

    public static int ParseMinutes(string input)
    {
        if (!int.TryParse(input.Trim(), out var minutes) ||
            minutes < MinimumMinutes ||
            minutes > MaximumMinutes)
        {
            throw new InvalidOperationException(ValidationMessage);
        }

        return minutes;
    }
}
