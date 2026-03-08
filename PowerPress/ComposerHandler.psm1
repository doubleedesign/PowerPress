function Update-Composer-Json {
	param (
		[string]$composerJsonPath,
		[string]$key,
		[string]$value
	)

	if (-not (Test-Path $composerJsonPath)) {
		ErrorMessage "File not found. Expected path: $composerJsonPath"
		exit 1
	}
	
	# Read and parse the JSON file
	$json = Get-Content $composerJsonPath | ConvertFrom-Json
	$json.$key = $value
	# Convert back to JSON and save
	$json | ConvertTo-Json -Depth 10 | Set-Content "$composerJsonPath"
}

function Composer-Json-Initial-Update {
	param (
		[string]$composerJsonPath
	)

	$composerJsonPath = Join-Path $global:SiteConfig.SiteDir "composer.json"
	if (-not (Test-Path $composerJsonPath)) {
		ErrorMessage "Composer.json not found. Expected path: $siteDir"
		exit 1
	}
	
	# Save original content in case we need to revert
	$originalContent = Get-Content $composerJsonPath -Raw

	InfoMessage "Updating composer.json: $composerJsonPath"
	Update-Composer-Json -composerJsonPath $composerJsonPath -key "name" -value "doubleedesign/$($global:SiteConfig.SiteSlug)"
	Update-Composer-Json -composerJsonPath $composerJsonPath -key "version" -value "1.0.0"
	Update-Composer-Json -composerJsonPath $composerJsonPath -key "homepage" -value "https://www.doubleedesign.com.au"
	
	# Confirm none of the updated keys have empty values
	$json = Get-Content $composerJsonPath | ConvertFrom-Json
	if ([string]::IsNullOrEmpty($json.name) -or [string]::IsNullOrEmpty($json.version) -or ($json.name[-1] -eq '/')) {
		ErrorMessage "One or more of the required composer.json keys have empty or invalid values"
		WarningMessage "Reverting composer.json to original content. Please troubleshoot the Update-Composer-Json step before running again."
		Set-Content -Path $composerJsonPath -Value $originalContent
		exit 1
	}
}

function Composer-Json-Repositories-Update {
	param (
		[string]$composerJsonPath,
		[string]$pathToLocalPackages
	)
	
	foreach ($repo in $json.repositories) {
		if($repo.options.symlink -eq $true) {
			$packageName = $repo.url.Split("/")[-1]
			if (Test-Path (Join-Path $pathToLocalPackages $packageName)) {
				$repo.url = Join-Path $pathToLocalPackages $packageName
			}
			else {
				WarningMessage "Local package not found for $packageName at expected path: $(Join-Path $pathToLocalPackages $packageName) `n Skipping composer.json update for $packageName"
			}
		}
	}

	$json | ConvertTo-Json -Depth 10 | Set-Content "$composerJsonPath"
}

function Remove-Dep-From-ComposerJson { 
	param (
		[string]$composerJsonPath,
		[string]$packageName
	)

	if (Test-Path $composerJsonPath) {
		InfoMessage "Updating composer.json to remove $folderToRemove"
		# Read and parse the JSON file
		$json = Get-Content $composerJsonPath | ConvertFrom-Json
		# Remove from require section
		$json.require.Remove("doubleedesign/$folderToRemove")
		# Remove from repositories section
		$json.repositories = $json.repositories | Where-Object { $_.url -notlike "*$folderToRemove*" }
		# Convert back to JSON and save
		$json | ConvertTo-Json -Depth 10 | Set-Content "$composerJsonPath"
		SuccessMessage "Removed $folderToRemove from composer.json" -ForegroundColor Green
	}
}

function Run-Composer-Command-With-Custom-Output-Handling {
	param (
		[string]$command
	)
	
	DebugMessage -Message "Running composer command: composer $command"

	$commandArgs = ($command -split '\s+')
	
	& composer @commandArgs 2>&1 | Where-Object {
		$_ -notmatch '^Warning:' -and
				$_ -notmatch '^Deprecated:' -and
				$_ -notmatch '^Notice:' -and
				$_ -notmatch 'PHP Warning' -and
				$_ -notmatch 'PHP Deprecated'
	} | ForEach-Object {
		if ($_ -match 'WordPress core moved successfully after composer install or update') {
			SuccessMessage $_
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
		ErrorMessage "Composer.json not found for plugin at expected path: $composerJsonPath"
	}

	Set-Location $pluginDir
	
	if($noDev) {
		InfoMessage "Running composer install --no-dev for plugin $pluginDir"
		Run-Composer-Command-With-Custom-Output-Handling -command "install --no-dev"
	}
	else {
		InfoMessage "Running composer install for plugin $pluginDir"
		Run-Composer-Command-With-Custom-Output-Handling -command "install"
	}
	
	if ($LastExitCode -ne 0) {
		ErrorMessage "Composer install failed for plugin $pluginDir"
	}
}

function Run-Composer-Install {
	# Move to project root
	Set-Location $global:SiteConfig.SiteDir
	DebugMessage "Working from $(Get-Location)"

	InfoMessage "Installing project dependencies with Composer"
	
	# Run install but ignore warnings coming from the installed packages
	Run-Composer-Command-With-Custom-Output-Handling -command "install"
	
	if ($LastExitCode -ne 0) {
		ErrorMessage "Composer install failed"
		Definitely-Delete-Folder -folderPath $global:SiteConfig.SiteDir
		exit 1
	}
}

Export-ModuleMember -Function Composer-Json-Initial-Update, Composer-Json-Repositories-Update, Remove-Dep-From-ComposerJson
Export-ModuleMember -Function Run-Composer-Command-With-Custom-Output-Handling, Run-Composer-Install, Run-Composer-Install-For-Plugin