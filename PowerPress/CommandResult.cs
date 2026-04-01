namespace PowerPress;

// ReSharper disable RedundantUsingDirective
using System.Collections.Generic;

public record CommandResult(bool Success, List<string> Output);