using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace PowerPress;

public class FileHandler {
	private readonly Logger logger = new();
	private readonly UserInput ui = new();
	private LocalSiteConfig config;

	// The methods that take a path are used before the config is available in main.ps1,
	// so we need the ability to set it later 
	public void SetConfig(LocalSiteConfig config) {
		this.config = config;
	}

	public bool MaybeCreateFolder(string path) {
		if (Directory.Exists(path)) {
			if (!Directory.EnumerateFileSystemEntries(path).Any()) {
				this.logger.SuccessMessage($"{path} already exists and is empty, skipping creation");
				return true;
			}

			this.logger.WarningMessage($"{path} already exists and is not empty");
			return false;
		}

		try {
			Directory.CreateDirectory(path);

			if (!this.FolderExistsNonEmpty(path)) {
				this.logger.SuccessMessage($"Created folder: {path}");
				return true;
			}

			this.logger.ErrorMessage($"Failed to create folder: {path}");
			return false;
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Error creating folder: " + e.Message);
			return false;
		}
	}

	public void MaybeDeleteFolder(string path, string? prompt = null) {
		if (!Directory.Exists(path)) {
			this.logger.InfoMessage($"{path} does not exist, skipping deletion");
		}

		bool proceed = true; // Default if not prompt is provided

		if (prompt is not null) {
			proceed = this.ui.PromptForYesOrNo(
				prompt,
				"Yes, delete the folder",
				"No, keep the folder"
			);
		}

		if (proceed) {
			this.MoveToRecycleBin(path);
		}
	}

	public void UpdateProjectReadme() {
		if (this.config.SiteDir is null) {
			this.logger.WarningMessage("Could not update README because the site directory is not set");
			return;
		}

		// Delete the original README.md
		string readmeFile = Path.Combine(this.config.SiteDir, "README.md");
		if (File.Exists(readmeFile)) {
			File.Delete(readmeFile);
		}

		try {
			this.FindAndReplaceText("README-project.md", "My Project Name", this.config.SiteName ?? "");
			this.FindAndReplaceText("README-project.md", "[Client Name]", this.config.SiteName ?? "");
			// TODO: Add a check to verify the content was actually updated
			this.logger.SuccessMessage("Updated README-project.md with project name");
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Failed to update README-project.md");
			this.logger.ErrorMessage(e.Message);
		}

		try {
			string projectTemplate = Path.Combine(this.config.SiteDir, "README-project.md");
			File.Move(projectTemplate, readmeFile);
			// TODO: Add a check to verify
			this.logger.SuccessMessage("Updated README.md with project template");
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Failed to save new README.md from template");
			this.logger.ErrorMessage(e.Message);
		}
	}

	public void MoveToRecycleBin(string path) {
		bool isFile = File.Exists(path);

		if (isFile) {
			FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
			return;
		}

		FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
	}

	/// <summary>
	///     Find and replace text in a file.
	/// </summary>
	/// <param name="path">The path to the file from the site root</param>
	/// <param name="search">The value to search for</param>
	/// <param name="replace">The value to replace it with</param>
	public void FindAndReplaceText(string path, string search, string replace) {
		if (this.config.SiteDir is null) {
			this.logger.ErrorMessage("Cannot update file because SiteDir is not set in config");
		}

		string content = File.ReadAllText(path);
		content = content.Replace(search, replace);

		File.WriteAllText(path, content);
	}

	public void FindAndReplaceRegex(string path, string regex, string replace) {
		if (this.config.SiteDir is null) {
			this.logger.ErrorMessage("Cannot update file because SiteDir is not set in config");
		}

		string content = File.ReadAllText(path);
		content = Regex.Replace(content, regex, replace);

		File.WriteAllText(path, content);
	}

	private bool FolderExistsNonEmpty(string path) {
		return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
	}
}