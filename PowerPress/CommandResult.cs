namespace PowerPress;

public record CommandResult(bool Success, IReadOnlyList<string> Output);