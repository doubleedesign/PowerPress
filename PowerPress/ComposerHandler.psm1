$Logger = [PowerPress.Logger]::new();

function Run-Composer-Command-With-Custom-Output-Handling {
	param (
		[string]$command
	)

	$Logger.DebugMessage("Running composer command: composer $command");

	$commandArgs = ($command -split '\s+')

	& composer @commandArgs 2>&1 | Where-Object {
		$_ -notmatch '^Warning:' -and
			$_ -notmatch '^Deprecated:' -and
			$_ -notmatch '^Notice:' -and
			$_ -notmatch 'PHP Warning' -and
			$_ -notmatch 'PHP Deprecated'
	} | ForEach-Object {
		if ($_ -match 'WordPress core moved successfully after composer install or update') {
			$Logger.SuccessMessage($_);
		}
		elseif ($_ -is [System.Management.Automation.RemoteException]) {
			Write-Host $_ -ForegroundColor Red
		}
		elseif ($_ -is [System.Management.Automation.ErrorRecord]) {
			Write-Host $_ -ForegroundColor Gray
		}
		else {
			Write-Host $_ -ForegroundColor Gray
		}
	}
}

function Run-Composer-Install-For-Plugin {
	param (
		[string]$pluginDir,
		[Boolean]$noDev = $true
	)

	$composerJsonPath = Join-Path $pluginDir "composer.json"
	if (-not (Test-Path $composerJsonPath)) {
		$Logger.ErrorMessage("Composer.json not found for plugin at expected path: $composerJsonPath");
	}

	Set-Location $pluginDir

	if ($noDev) {
		$Logger.InfoMessage("Running composer install --no-dev for plugin $pluginDir");
		Run-Composer-Command-With-Custom-Output-Handling -command "install --no-dev"
	}
	else {
		$Logger.InfoMessage("Running composer install for plugin $pluginDir");
		Run-Composer-Command-With-Custom-Output-Handling -command "install"
	}

	if ($LastExitCode -ne 0) {
		$Logger.ErrorMessage("Composer install failed for plugin $pluginDir");
	}
}

function Run-Composer-Install {
	# Move to project root
	Set-Location $global:SiteConfig.SiteDir
	$location = Get-Location
	$Logger.DebugMessage("Working from: $location");

	$Logger.InfoMessage("Installing project dependencies with Composer");

	# Run install but ignore warnings coming from the installed packages
	Run-Composer-Command-With-Custom-Output-Handling -command "install"

	if ($LastExitCode -ne 0) {
		$Logger.ErrorMessage("Composer install failed");
		$FileHandler.MaybeDeleteFolder($global:SiteConfig.SiteDir)
		exit 1
	}
}

Export-ModuleMember -Function Run-Composer-Command-With-Custom-Output-Handling, Run-Composer-Install, Run-Composer-Install-For-Plugin