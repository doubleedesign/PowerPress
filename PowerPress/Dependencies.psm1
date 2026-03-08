function Check-Dependencies {
	# Check PowerShell version
	if ($PSVersionTable.PSVersion.Major -lt 7) {
		ErrorMessage "This script requires PowerShell 7 or higher. Please update your PowerShell version and try again."
		InfoMessage "If you are running via the terminal, install PowerShell 7 from the Microsoft Store, via Chocolatey, or another method and run this script using that instead of 'Windows PowerShell'."
		exit 1
	}
	else {
		SuccessMessage "PowerShell version is $($PSVersionTable.PSVersion)"
	}
	
	$commands = @("php", "mysql", "composer", "git", "herd", "robocopy", "wp")
	$missingCommands = @()
	foreach ($command in $commands) {
		if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
			ErrorMessage("$command is not available")
			$missingCommands += $command
		}
		else {
			SuccessMessage("$command is available")
		}
	}
	
	if ($missingCommands.Count -gt 0) {
		WarningMessage("Please install the missing dependencies and ensure they are in your PATH, and try again")
		exit 1
	}
	
	$requiredWpCommands = @("core", "scaffold", "option", "db", "search-replace", "plugin", "theme", "rewrite")
	$missingWpCommands = @()
	foreach ($command in $requiredWpCommands) {
		wp help $command 2>&1 | Out-Null
		if ($LASTEXITCODE -ne 0) {
			ErrorMessage "WP-CLI command '$command' is not available"
			$missingWpCommands += $command
		} else {
			SuccessMessage "WP-CLI command '$command' is available"
		}
	}
	if($missingWpCommands.Count -gt 0) {
		ErrorMessage "WP-CLI is missing some required commands: $($missingWpCommands -join ", "). `n If you are managing WP-CLI via composer, try globally installing the following packages:"
		if($missingWpCommands.length -gt 1 -and $missingWpCommands -contains "wp search-replace") {
			Write-Host "wp-cli/search-replace-command"
		}
		if($missingWpCommands.length -gt 0 -and ($missingCommands -notcontains "wp search-replace")) {
			Write-Host "wp-cli/wp-cli-bundle"
		}
		
		Write-Host ""
		exit 1
	}
}

function Check-Permissions {
	# Check if the user can execute scripts
	$currentUserPolicy = Get-ExecutionPolicy -Scope CurrentUser
	if (-not ($currentUserPolicy -eq "Bypass" -or $currentUserPolicy -eq "Unrestricted")) {
		ErrorMessage "Current user execution policy is set to '$currentUserPolicy'. You will not be able to complete the WordPress install properly."
		InfoMessage "To update the execution policy, open PowerShell as an administrator and run:"
		Write-Host "Set-ExecutionPolicy Bypass -Scope CurrentUser"
		exit 1
	}
	else {
		SuccessMessage "Your script execution policy is '$currentUserPolicy'"
	}

	# Check if the current user can create symlinks
	$output = whoami /priv | Select-String "SeCreateSymbolicLink" 2>&1
	if ($LASTEXITCODE -eq 0) {
		if ( [String]::IsNullOrEmpty($output)) {
			SuccessMessage "You can create symbolic links in the current context"
		}
		else {
			WarningMessage "Current user does not have permission to create symbolic links in the current context. `n   You may run into issues linking local Composer packages if you choose to install dependencies with that option."
			InfoMessage "To enable symbolic link creation, make sure Windows Developer Mode is enabled and run PowerShell with administrator privileges."
		}
	}
}

function Check-Php-Extensions {
	$requiredExtensions = @("curl", "openssl")
	$missingExtensions = @()

	foreach ($extension in $requiredExtensions) {
		if (-not (php -m | Select-String -Pattern $extension)) {
			ErrorMessage("PHP extension '$extension' is not enabled")
			$missingExtensions += $extension
		} else {
			SuccessMessage("PHP extension '$extension' is enabled")
		}
	}

	if ($missingExtensions.Count -gt 0) {
		WarningMessage("Please install the missing PHP extensions and try again.")
		exit 1
	}
}

Export-ModuleMember -Function Check-Dependencies, Check-Permissions, Check-Php-Extensions