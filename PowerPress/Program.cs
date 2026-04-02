using System.Diagnostics;
using System.Reflection;
using PowerPress;

UserInput ui = new();
Logger logger = new();

string action = ui.PromptForSelection(
	"Select an action:",
	new Dictionary<string, string> {
		["Run the script"] = "run",
		["Manually run a selected action"] = "test"
	}
);

if (action == "test") {
	string module = ui.PromptForSelection(
		"Select a module to run:",
		new Dictionary<string, string> {
			["Check dependencies"] = "deps",
			["Check Bitwarden access"] = "bitwarden-access",
			["Test saving credentials in Bitwarden"] = "bitwarden-save",
			["Create an empty database"] = "database",
			["Import a database"] = "import",
			["Test folder creation and deletion"] = "folders",
			["Test WP-CLI"] = "wp"
		}
	);

	Dependencies deps = new();
	BitwardenHandler bw = new();
	LocalSiteConfig testSiteConfig = new("test-site", "C:/temp/test-site", "https://example.com");
	DatabaseHandler db = new(testSiteConfig);
	FileHandler fh = new();
	fh.SetConfig(testSiteConfig);

	switch (module) {
		case "deps":
			deps.CheckPermissions();
			deps.CheckDependencies();
			Environment.Exit(0);
			break;
		case "bitwarden-access":
			logger.InfoMessage("Testing Bitwarden login flow");
			bw.MaybeLogIn();
			logger.InfoMessage("Testing Bitwarden logout flow");
			bw.MaybeLogOut();
			Environment.Exit(0);
			break;
		case "bitwarden-save":
			bw.MaybeLogIn();
			bw.MaybeSaveCredentials("example.com", "https://example.com", "admin", "password123!");
			bw.MaybeLogOut();
			Environment.Exit(0);
			break;
		case "database":
			db.MaybeDropDb();
			db.MaybeCreateDb();
			break;
		case "import":
			db.MaybeImportData();
			break;
		case "folders":
			fh.MaybeCreateFolder("C:/temp/powerpress-temp");
			// Test creation again to see what happens when it exists but is empty
			fh.MaybeCreateFolder("C:/temp/powerpress-temp");
			// Put an empty text file in it
			File.WriteAllText("C:/temp/powerpress-temp/test.txt", "This is a test file.");
			// Test the response to creation request 
			fh.MaybeCreateFolder("C:/temp/powerpress-temp");
			// Then test deletion with prompt
			fh.MaybeDeleteFolder("C:/temp/powerpress-temp", "Do you want to delete the test folder?");
			// Check if it worked
			if (!Directory.Exists("C:/temp/powerpress-temp")) {
				logger.SuccessMessage("Test folder was successfully deleted.");
			}

			break;
		case "wp":
			WordPressHandler wp = new(testSiteConfig);
			fh.MaybeCreateFolder("C:/temp/test-site");
			fh.MaybeCreateFolder("C:/temp/test-site/app");
			wp.RunCliCommand("--version"); // Something we expect to work
			wp.RunCliCommand("plugin activate doublee-breadcrumbs"); // Something we expect to fail (because WP is not installed)
			Environment.Exit(0);
			break;
		default:
			Console.WriteLine("Unknown module selected.");
			break;
	}
}

if (action == "run") {
	string currentFilePath = Assembly.GetExecutingAssembly().Location;
	string[] split = currentFilePath.Split('\\');
	string powerpressPath = string.Join("\\", split.Take(split.Length - 4));
	string mainScriptPath = powerpressPath + "\\main.ps1";

	Console.Write("Enter the site name (kebab-case): ");
	string? siteName = Console.ReadLine();

	Console.Write("Run in debug mode? (y/N): ");
	string? debugInput = Console.ReadLine();
	bool isDebugMode = debugInput != null && debugInput.ToLower() == "y";

	string scriptArgs = $"-NoExit -NoProfile -ExecutionPolicy Bypass -Command \"& '{mainScriptPath}' '{siteName}'";
	if (isDebugMode) {
		scriptArgs += " -Debug'";
	}

	Process process = new() {
		StartInfo = new ProcessStartInfo {
			FileName = "pwsh",
			Arguments = scriptArgs + "; Read-Host 'Press Enter to exit'\"",
			UseShellExecute = false,
			CreateNoWindow = false
		}
	};

	process.StartInfo.EnvironmentVariables["PATH"] =
		Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + ";" +
		Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

	process.Start();
	process.WaitForExit();
}