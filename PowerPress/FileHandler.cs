using Microsoft.VisualBasic.FileIO;

namespace PowerPress;

public class FileHandler {
	private readonly LocalSiteConfig config;
	private readonly Logger logger = new();
	private readonly UserInput ui = new();

	public FileHandler(LocalSiteConfig config) {
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

	public void MaybeDeleteFolder(string path, string? prompt) {
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
		}

		string readmeFile = Path.Combine(this.config.SiteDir, "README.md");
		string projectTemplate = Path.Combine(this.config.SiteDir, "README-project.md");

		// Delete the original README.md
		if (File.Exists(readmeFile)) {
			File.Delete(readmeFile);
		}

		if (File.Exists(projectTemplate)) {
			try {
				string siteName = this.config.SiteName ?? "";
				string fileContent = File.ReadAllText(projectTemplate);
				fileContent = fileContent.Replace("My Project Name", siteName);
				fileContent = fileContent.Replace("[Client Name]", siteName);
				File.WriteAllText(projectTemplate, fileContent);
				// TODO: Add a check to verify the content was actually updated
				this.logger.SuccessMessage("Updated README-project.md with project name");
			}
			catch (Exception e) {
				this.logger.ErrorMessage("Failed to update README-project.md");
				this.logger.ErrorMessage(e.Message);
			}

			try {
				File.Move(projectTemplate, readmeFile);
				// TODO: Add a check to verify
				this.logger.SuccessMessage("Updated README.md with project template");
			}
			catch (Exception e) {
				this.logger.ErrorMessage("Failed to save new README.md from template");
				this.logger.ErrorMessage(e.Message);
			}
		}
	}

	private bool FolderExistsNonEmpty(string path) {
		return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
	}

	public void MoveToRecycleBin(string path) {
		bool isFile = File.Exists(path);

		if (isFile) {
			FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
			return;
		}

		FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
	}
}