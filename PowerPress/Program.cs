using System.Diagnostics;
using System.Reflection;
using PowerPress;

UserInput ui = new();
string action = ui.PromptForSelection(
	"Select an action:",
	new Dictionary<string, string> {
		["Run the script"] = "run",
		["Manually run a single module"] = "test"
	}
);

if (action == "test") {
	string module = ui.PromptForSelection(
		"Select a module to run:",
		new Dictionary<string, string> {
			["Check dependencies"] = "check-deps",
			["Check Bitwarden status"] = "bitwarden"
		}
	);

	switch (module) {
		case "check-deps":
			Dependencies deps = new();
			deps.CheckPermissions();
			deps.CheckDependencies();
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