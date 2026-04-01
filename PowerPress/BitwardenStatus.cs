using System.Text.Json.Serialization;

namespace PowerPress;

public record BitwardenStatus(
	[property: JsonPropertyName("serverUrl")] string ServerUrl,
	[property: JsonPropertyName("status")] BitwardenVaultStatus Status,
	[property: JsonPropertyName("lastSync")] DateTime? LastSync,
	[property: JsonPropertyName("userId")] string? UserId,
	[property: JsonPropertyName("userEmail")] string? UserEmail
);