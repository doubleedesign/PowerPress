using System.Text.Json;
using System.Text.Json.Nodes;

namespace PowerPress;

public class ComposerHandler {
	private readonly string composerJsonDevPath;
	private readonly string composerJsonPath;
	private readonly LocalSiteConfig config;
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();

	public ComposerHandler(LocalSiteConfig config) {
		this.config = config;
		this.composerJsonPath = Path.Combine(this.config.WpDir, "composer.json");
		this.composerJsonDevPath = Path.Combine(this.config.WpDir, "composer.dev.json");
	}

	public void Init() {
		this.UpdateProjectInfo(this.composerJsonPath);
		this.UpdateProjectInfo(this.composerJsonDevPath);
	}

	private void UpdateComposerJson(string path, string key, string value) {
		if (!File.Exists(path)) {
			this.logger.ErrorMessage("File not found: " + path);
			Environment.Exit(1);
		}

		// Read and parse JSON
		string jsonContent = File.ReadAllText(path);
		JsonNode json = JsonNode.Parse(jsonContent)!;

		// Set the key/value
		json[key] = JsonValue.Create(value);

		// Convert back to JSON and save
		JsonSerializerOptions options = new() { WriteIndented = true };
		File.WriteAllText(path, json.ToJsonString(options));
	}

	private void UpdateProjectInfo(string path) {
		if (!File.Exists(path)) {
			this.logger.ErrorMessage("File not found: " + path);
			Environment.Exit(1);
		}

		// Save original content in case we need to revert
		string originalContent = File.ReadAllText(path);

		this.logger.InfoMessage($"Updating composer.json: {path}");
		this.UpdateComposerJson(path, "name", $"doubleedesign/{this.config.SiteSlug}");
		this.UpdateComposerJson(path, "version", "1.0.0");
		this.UpdateComposerJson(path, "homepage", this.config.ProductionUrl ?? "");

		// Confirm none of the updated keys have empty values
		JsonNode json = JsonNode.Parse(File.ReadAllText(path))!;
		string? name = json["name"]?.GetValue<string>();
		string? version = json["version"]?.GetValue<string>();

		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version) || name.EndsWith('/')) {
			this.logger.ErrorMessage("One or more of the required composer.json keys have empty or invalid values");
			this.logger.WarningMessage("Reverting composer.json to original content. Please troubleshoot the UpdateComposerJson step before running again.");
			File.WriteAllText(path, originalContent);
		}
	}

	public void UpdateDevRepositories(string localPackagesPath) {
		if (!File.Exists(this.composerJsonDevPath)) {
			this.logger.ErrorMessage($"File not found: {this.composerJsonDevPath}, skipping repo path updates");
			return;
		}

		if (!Directory.Exists(localPackagesPath)) {
			this.logger.ErrorMessage($"Directory not found: {localPackagesPath}, skipping repo path updates");
			return;
		}

		this.logger.InfoMessage($"Using local packages directory: {localPackagesPath}");

		// Read and parse JSON
		string jsonContent = File.ReadAllText(this.composerJsonDevPath);
		JsonNode json = JsonNode.Parse(jsonContent)!;

		JsonArray? repositories = json["repositories"]?.AsArray();
		if (repositories is null) {
			this.logger.ErrorMessage($"No repositories found in {this.composerJsonDevPath}, skipping updates");
			return;
		}

		// Loop through the repos and update any that have local copies
		foreach (JsonNode? repo in repositories) {
			if (repo is null) continue;

			bool isSymlink = repo["options"]?["symlink"]?.GetValue<bool>() == true;
			if (!isSymlink) continue;

			string? url = repo["url"]?.GetValue<string>();
			if (string.IsNullOrEmpty(url)) continue;

			string packageName = url.Split('/').Last();
			string expectedPath = Path.Combine(localPackagesPath, packageName);

			if (Directory.Exists(expectedPath)) {
				repo["url"] = expectedPath;
			}
			else {
				this.logger.WarningMessage($"Local package not found for {packageName} at expected path: {expectedPath}");
				this.logger.WarningMessage($"Skipping composer.json update for {packageName}");
			}
		}

		// Save updated JSON
		JsonSerializerOptions options = new() { WriteIndented = true };
		File.WriteAllText(this.composerJsonDevPath, json.ToJsonString(options));
	}

	public void RemoveDependency(string packageName) {
		string[] files = [this.composerJsonPath, this.composerJsonDevPath];
		foreach (string file in files) {
			if (!File.Exists(file)) {
				this.logger.ErrorMessage($"File not found: {file}, skipping dependency removal");
				return;
			}

			string jsonContent = File.ReadAllText(file);
			JsonNode json = JsonNode.Parse(jsonContent)!;

			JsonNode? deps = json["require"];
			if (deps is null) {
				this.logger.InfoMessage($"No require section found in {file}, skipping dependency removal");
				return;
			}

			if (deps[packageName] is null) {
				this.logger.InfoMessage($"Package {packageName} not found in require, skipping dependency removal");
				return;
			}

			deps.AsObject().Remove(packageName);

			// Save updated JSON
			JsonSerializerOptions options = new() { WriteIndented = true };
			File.WriteAllText(file, json.ToJsonString(options));
		}
	}

	private void RunCommand(string command) {
		if (this.config.SiteDir is null) {
			this.logger.ErrorMessage("Cannot run Composer command because the site directory is not set in the config");
			return;
		}

		string[] args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		Directory.SetCurrentDirectory(this.config.SiteDir);
		CommandResult result = this.ps.RunCommand("composer", args);

		if (!result.Success) {
			this.logger.ErrorMessage(result.Output.First());
			Environment.Exit(1);
		}

		this.logger.SuccessMessage(result.Output.First());
	}

	public void RunInstall() {
		// TODO: ignore warnings coming from the installed packages
		this.RunCommand("install");
	}
}