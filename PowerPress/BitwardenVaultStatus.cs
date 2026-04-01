using System.Text.Json.Serialization;

namespace PowerPress;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BitwardenVaultStatus {
	Unauthenticated,
	Locked,
	Unlocked
}