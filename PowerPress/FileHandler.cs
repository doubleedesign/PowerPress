using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace PowerPress;

public class FileHandler {
	private readonly Logger logger = new();
	private readonly UserInput ui = new();
	private LocalSiteConfig? config;

	// The methods that take a path are used before the config is available in main.ps1,
	// so we need the ability to set it later 
	public void SetConfig(LocalSiteConfig configObj) {
		this.config = configObj;
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

	/// <summary>
	/// </summary>
	/// <param name="path"></param>
	/// <param name="prompt"></param>
	/// <returns>Whether the folder does NOT exist.</returns>
	public bool MaybeDeleteFolder(string path, string? prompt = null) {
		if (!Directory.Exists(path)) {
			this.logger.InfoMessage($"{path} does not exist, skipping deletion");
			return true;
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
			return true;
		}

		return false;
	}

	public void UpdateProjectReadme() {
		if (this.config is null) {
			this.logger.ErrorMessage("Cannot update README because site config is not available");
			return;
		}

		// Delete the original README.md
		string readmeFile = Path.Combine(this.config.SiteDir, "README.md");
		if (File.Exists(readmeFile)) {
			File.Delete(readmeFile);
		}

		try {
			string projectTemplate = Path.Combine(this.config.SiteDir, "README-project.md");
			this.FindAndReplaceText(projectTemplate, "My Project Name", this.config.SiteName);
			this.FindAndReplaceText(projectTemplate, "[Client Name]", this.config.SiteName);
			string firstLine = this.GetFirstLineText(projectTemplate);
			if (firstLine == $"# {this.config.SiteName}") {
				this.logger.SuccessMessage($"First line of README is now {firstLine}");
			}
			else {
				this.logger.WarningMessage($"First line of README is now {firstLine}. If this is not correct, edit README.md manually.");
			}
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Failed to update README-project.md");
			this.logger.ErrorMessage(e.Message);
		}

		try {
			string projectTemplate = Path.Combine(this.config.SiteDir, "README-project.md");
			File.Move(projectTemplate, readmeFile);
			if (this.GetFirstLineText(readmeFile) == $"# {this.config.SiteName}") {
				this.logger.SuccessMessage("Updated README with project template");
			}
			else {
				this.logger.WarningMessage("README update does not seem to have completed correctly and may need manual fixing.");
			}
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Failed to save new README.md from template");
			this.logger.ErrorMessage(e.Message);
		}
	}

	private string GetFirstLineText(string filePath) {
		string[] lines = File.ReadAllLines(filePath);

		return lines[0];
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
		string content = File.ReadAllText(path);
		content = content.Replace(search, replace);

		File.WriteAllText(path, content);
	}

	public void FindAndReplaceRegex(string path, string regex, string replace) {
		string content = File.ReadAllText(path);
		content = Regex.Replace(content, regex, replace);

		File.WriteAllText(path, content);
	}

	private bool FolderExistsNonEmpty(string path) {
		return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
	}

	public void CopyDirectory(string source, string dest) {
		if (!Directory.Exists(source)) {
			this.logger.WarningMessage($"Directory {source} does not exist, skipping copy");
			return;
		}

		// Create the destination directory if it doesn't already exist
		if (!Directory.Exists(dest)) {
			Directory.CreateDirectory(dest);
		}

		// Ref: https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
		// Get information about the source directory
		DirectoryInfo dir = new(source);
		// Cache directories before we start copying
		DirectoryInfo[] dirs = dir.GetDirectories();

		// Get the files in the source directory root and copy to the destination directory
		this.CopyFiles(source, dest);

		// Recursively call this method to copy subdirectories
		foreach (DirectoryInfo subDir in dirs) {
			string newDestinationDir = Path.Combine(dest, subDir.Name);
			this.CopyDirectory(subDir.FullName, newDestinationDir);
		}

		this.logger.SuccessMessage($"Copied directory from {source} \n \t \tto {dest}");
	}

	private void CopyFiles(string source, string dest) {
		DirectoryInfo dir = new(source);
		foreach (FileInfo file in dir.GetFiles()) {
			string targetFilePath = Path.Combine(dest, file.Name);
			file.CopyTo(targetFilePath);
		}
	}
}