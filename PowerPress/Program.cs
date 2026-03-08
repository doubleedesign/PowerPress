using System.Diagnostics;

var currentFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
var split = currentFilePath.Split('\\');
var powerpressPath = string.Join("\\", split.Take(split.Length - 4));
var mainScriptPath = powerpressPath + "\\main.ps1";

Console.Write("Enter the site name (kebab-case): ");
var siteName = Console.ReadLine();

Console.Write("Run in debug mode? (y/N): ");
var debugInput = Console.ReadLine();
var isDebugMode = debugInput != null && debugInput.ToLower() == "y";

var scriptArgs = $"-NoExit -NoProfile -ExecutionPolicy Bypass -Command \"& '{mainScriptPath}' '{siteName}'";
if (isDebugMode) {
	scriptArgs += " -Debug'";
}

var process = new Process {
	StartInfo = new ProcessStartInfo {
		FileName = "pwsh",       
		Arguments = scriptArgs + "; Read-Host 'Press Enter to exit'\"",
		UseShellExecute = false,
		CreateNoWindow = false
	}
};

process.StartInfo.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

process.Start();
process.WaitForExit();