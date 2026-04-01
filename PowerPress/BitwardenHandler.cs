using System.Management.Automation;
using System.Text;
using System.Text.Json;

namespace PowerPress;

public class BitwardenHandler {
	private readonly string clientId = Environment.GetEnvironmentVariable("BW_CLIENTID") ?? "";
	private readonly string clientSecret = Environment.GetEnvironmentVariable("BW_CLIENTSECRET") ?? "";
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();
	private readonly UserInput ui = new();

	public bool MaybeLogIn(bool confirmFirst = false) {
		if (!this.CanAccessBitwarden()) {
			return false;
		}

		if (confirmFirst) {
			bool proceed = this.ui.PromptForYesOrNo(
				"Do you want to log into Bitwarden to automatically store generated WordPress admin credentials?",
				"Yes, log me in",
				"No, I will be importing an existing database or will handle the generated credentials myself",
				true
			);

			if (!proceed) {
				return false;
			}
		}

		BitwardenVaultStatus status = this.GetStatus();
		this.logger.InfoMessage("Bitwarden login status: " + status);

		switch (status) {
			case BitwardenVaultStatus.Unauthenticated:
				return this.LogIn() && this.Unlock();
			case BitwardenVaultStatus.Locked:
				return this.Unlock();
			case BitwardenVaultStatus.Unlocked:
				this.logger.SuccessMessage("Bitwarden vault is unlocked");
				return true;
		}

		return false;
	}

	private bool LogIn() {
		CommandResult result = this.ps.RunCommand("bw", ["login", "--apikey"]);
		if (result.Success && result.Output.First().Equals("You are logged in!")) {
			this.logger.SuccessMessage("Logged into Bitwarden CLI");
			return true;
		}

		this.logger.ErrorMessage(result.Output.First());
		return false;
	}

	private bool CanAccessBitwarden() {
		if (string.IsNullOrEmpty(this.clientId) || string.IsNullOrEmpty(this.clientSecret)) {
			this.logger.ErrorMessage("Bitwarden API credentials are not set in environment variables");

			return false;
		}

		if (this.ps.GetCommand("bw").Any()) {
			return true;
		}

		this.logger.ErrorMessage("Bitwarden CLI is not available");

		return false;
	}

	private BitwardenVaultStatus GetStatus() {
		if (!this.CanAccessBitwarden()) {
			Environment.Exit(1);
		}

		CommandResult raw = this.ps.RunCommand("bw", ["status"]);

		if (raw.Output.Count == 0) {
			return BitwardenVaultStatus.Unauthenticated;
		}

		try {
			BitwardenStatus result = JsonSerializer.Deserialize<BitwardenStatus>(raw.Output.First())!;

			return result.Status;
		}
		catch (Exception e) {
			this.logger.ErrorMessage(e.Message);

			return BitwardenVaultStatus.Unauthenticated;
		}
	}

	private bool Unlock() {
		CommandResult result = this.ps.RunCommand("bw", ["unlock", "--passwordenv", "BW_PASSWORD"]);
		if (!result.Success) {
			this.logger.ErrorMessage(result.Output.First());
			return false;
		}

		if (!result.Output.First().Equals("Your vault is now unlocked!")) {
			this.logger.ErrorMessage($"Unexpected output from bw unlock: {result.Output.First()}");
		}

		try {
			// Find the line that contains the session token and extract it
			string sessionLine = result.Output.Single(line => line.Contains("> $env:BW_SESSION"));
			string token = sessionLine.Replace("> $env:BW_SESSION=", "").Trim('"');

			if (string.IsNullOrEmpty(token)) {
				this.logger.ErrorMessage("Could not parse session token to unlock Bitwarden vault");
				return false;
			}

			// Put it in an environment variable for the current process
			Environment.SetEnvironmentVariable("BW_SESSION", token, EnvironmentVariableTarget.Process);

			// Check if we are now unlocked
			BitwardenVaultStatus status = this.GetStatus();
			if (status == BitwardenVaultStatus.Unlocked) {
				this.logger.SuccessMessage("Unlocked Bitwarden vault");
				return true;
			}

			this.logger.ErrorMessage("Failed to unlock Bitwarden vault.");
			return false;
		}
		catch (InvalidOperationException e) {
			this.logger.ErrorMessage(e.Message);
			return false;
		}
	}

	public bool MaybeLogOut() {
		if (Environment.GetEnvironmentVariable("BW_SESSION") == null) {
			this.logger.InfoMessage("No active Bitwarden session found, skipping logout");
			return true;
		}

		if (this.GetStatus() == BitwardenVaultStatus.Unauthenticated) {
			this.logger.InfoMessage("You are not logged into Bitwarden, skipping logout");
			return true;
		}

		return this.LogOut();
	}

	private bool LogOut() {
		CommandResult result = this.ps.RunCommand("bw", ["logout"]);

		if (!result.Success || result.Output.First() != "You have logged out.") {
			this.logger.ErrorMessage(result.Output.First());
			return false;
		}

		this.logger.SuccessMessage(result.Output.First());
		return true;
	}

	public bool MaybeSaveCredentials(string siteName, string url, string username, string password) {
		BitwardenVaultStatus status = this.GetStatus();
		if (status != BitwardenVaultStatus.Unlocked) {
			this.logger.WarningMessage("Bitwarden vault is not unlocked, cannot save credentials");
			this.CopyPasswordToClipboard(password);
			return false;
		}

		var item = new {
			type = 1,
			name = siteName,
			login = new {
				username,
				password,
				uris = new[] {
					// Match type 2 means Bitwarden will recognise this item if the URL starts with the provided URI
					// Important for it recognising all of: /wp-admin, /wp-login.php, WooCommerce login pages, etc.
					new { match = 2, uri = url }
				}
			}
		};

		try {
			string json = JsonSerializer.Serialize(item);
			//CommandResult encoded = this.ps.RunCommand("bw", ["encode", json]);
			string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
			CommandResult result = this.ps.RunCommand("bw", ["create", "item", encoded]);
			if (result.Success) {
				// TODO: The returned value, accessible from result.Output.First(), is a JSON string of the entry. Should probably check some of its values here to confirm.
				this.logger.SuccessMessage("Saved password to Bitwarden vault");
				return true;
			}

			throw new RuntimeException(result.Output.First());
		}
		catch (Exception e) {
			this.logger.ErrorMessage(e.Message);
			this.CopyPasswordToClipboard(password);
			return false;
		}
	}

	private void CopyPasswordToClipboard(string password) {
		this.ps.RunCommand("Set-Clipboard", [password]);

		CommandResult copied = this.ps.RunCommand("Get-Clipboard", []);
		if (copied.Output.First().Equals(password)) {
			this.logger.SuccessMessage("Password copied to clipboard");
			return;
		}

		this.logger.WarningMessage("Password not copied to clipboard");
	}
}