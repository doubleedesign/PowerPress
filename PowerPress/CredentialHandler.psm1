function Can-Access-Bitwarden {
	$clientId = [Environment]::GetEnvironmentVariable("BW_CLIENTID")
	$clientSecret = [Environment]::GetEnvironmentVariable("BW_CLIENTSECRET")

	if (-not $clientId -or -not $clientSecret) {
		Write-Host ""
		ErrorMessage "Bitwarden API credentials are not set in environment variables"

		return $False
	}

	if (-not (Get-Command "bw" -ErrorAction SilentlyContinue)) {
		WarningMessage "Bitwarden CLI is not available"
		$global:SiteConfig.AdminPassword | Set-Clipboard
		InfoMessage "Your admin password has been copied to the clipboard"

		return $False
	}

	return $True
}

function Maybe-Log-Into-Bitwarden {
	$proceed = Prompt-For-YesOrNo `
		-Message "Do you want to log into Bitwarden to automatically store generated WordPress admin credentials?" `
		-YesOption "Yes, log me in" `
		-NoOption "No, I will be importing an existing database or will handle the generated credentials myself" `
		-DefaultYes $True
	
	if (-not (Can-Access-Bitwarden)) {
		return $False
	}
	
	InfoMessage "Checking Bitwarden login status"

	$status = bw status 2>&1 | ConvertFrom-Json
	$status = $status.status
	InfoMessage "Status: $status"
	
	if ($status -eq "unauthenticated") {
		try {
			bw login --apikey
			
			if ($LASTEXITCODE -ne 0) {
				Throw $_
			}
			
			SuccessMessage "Logged into Bitwarden CLI"
			
			return $True
		}
		catch {
			Write-Host ""
			ErrorMessage "Error accessing Bitwarden CLI"
			ErrorMessage $_

			return $False
		}
	}
	
	if($status -eq "locked") {
		try { 
			$output = bw unlock --passwordenv BW_PASSWORD 2>&1
			
			if ($LASTEXITCODE -ne 0) {
				Throw $_
			}
			
			# The output is an array of strings; check the first one to confirm unlock success
			$result = $output[0]
			if($result -ne "Your vault is now unlocked!") {
				Throw "Unexpected output from bw unlock: $result"
			}
			
			# Find the element that contains "> $env:BW_SESSION
			$sessionLine = $output | Where-Object { $_ -match "> $env:BW_SESSION" }
			if (-not $sessionLine) {
				Throw "Failed to find session token in bw unlock output"
			}
			
			# Find the token by finding the string between the double quotes
			$tokenMatch = [regex]::Match($sessionLine, '"([^"]+)"')
			if (-not $tokenMatch.Success) {
				Throw "Failed to parse session token from bw unlock output"
			}
			
			# Set it in the environment variable for the current process
			$token = $tokenMatch.Groups[1].Value
			[Environment]::SetEnvironmentVariable("BW_SESSION", $token, "Process")

			# Check if we are now unlocked
			$newStatus = bw status 2>&1 | ConvertFrom-Json
			if($newStatus.status -eq "unlocked") {
				SuccessMessage "Unlocked Bitwarden vault"
				return $True
			}
			else {
				Throw "Problem unlocking Bitwarden vault, status is still: $($newStatus.status)"
			}
		}
		catch {
			Write-Host ""
			ErrorMessage "Failed to unlock Bitwarden vault"
			ErrorMessage $_

			return $False
		}
	}
	
	if($status -eq "unlocked") {
		SuccessMessage "Bitwarden vault is unlocked"
		return $True
	}
}

function Maybe-Log-Out-Of-Bitwarden {
	if($env:BW_SESSION -eq $null) {
		InfoMessage "No active Bitwarden session found, skipping logout"
		return
	}

	try {
		bw logout
		
		if ($LASTEXITCODE -ne 0) {
			Throw $_
		}
		
		SuccessMessage "Logged out of Bitwarden CLI"
	}
	catch {
		Write-Host ""
		ErrorMessage "Error logging out of Bitwarden CLI"
		ErrorMessage $_
	}
}

function Maybe-Save-Credentials {
	$status = bw status 2>&1 | ConvertFrom-Json
	if ($status.status -ne "unlocked") {
		ErrorMessage "Bitwarden vault is not unlocked, cannot add item"

		$password | Set-Clipboard
		InfoMessage "Your admin password has been copied to the clipboard instead"

		return $False
	}
	
	try {
		$name = $global:SiteConfig.SiteName
		$url = $global:SiteConfig.SiteUrl
		$username = $global:SiteConfig.AdminUser
		$password = $global:SiteConfig.AdminPassword

		$item = @{
			type = 1 # Login item type
			name = "test.com test"
			login = @{
				username = "testuser"
				password = "testpass"
				uris = @(
					# Match type 2 means Bitwarden will recognise this item if the URL starts with the provided URI
					# Important for it recognising all of: /wp-admin, /wp-login.php, WooCommerce login pages, etc.
					@{ match = 2; uri = "https://test.com" }
				)
			}
		}
		
		$item = $item | ConvertTo-Json -Depth 10 | bw encode
		if ($LASTEXITCODE -ne 0) {
			Throw $_
		}
		
		SuccessMessage "Encoded credentials successfully"
		
		bw create item $item
		if ($LASTEXITCODE -ne 0) {
			Throw $_
		}
		
		SuccessMessage "Added credentials to Bitwarden vault"
		
		return $True
	}
	catch {
		Write-Host ""
		ErrorMessage "Failed to add credentials to Bitwarden vault"
		ErrorMessage $_
		
		return $False
	}
}

Export-ModuleMember -Function Maybe-Log-Into-Bitwarden, Maybe-Log-Out-Of-Bitwarden, Maybe-Save-Credentials