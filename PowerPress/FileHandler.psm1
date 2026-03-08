function Create-Folder {
	param (
		[string]$folderPath
	)
	
	try {
		New-Item -Path $folderPath -ItemType Directory -ErrorAction Stop
		if (Test-Path $folderPath) {
			SuccessMessage "Created directory: $folderPath" -TraceLevels 2
			return $true
		}
	}
	catch {
		ErrorMessage "Failed to create directory: $_"
		return $false
	}
}

function Move-Folder-To-Recycle-Bin {
	param (
		[string]$folderPath
	)
	
	if (Test-Path $folderPath) {
		try {
			# Use Shell.Application COM object to move to recycle bin
			$shell = New-Object -ComObject Shell.Application
			$folderItem = $shell.NameSpace((Split-Path $folderPath)).ParseName((Split-Path $folderPath -Leaf))
			$folderItem.InvokeVerb("delete") | Out-Null
			SuccessMessage "Moved $folderPath to Recycle Bin" -ForegroundColor Green -TraceLevels 3
			
			# Open Recycle Bin to show the user the folder was moved there
			#Start-Process "explorer.exe" -ArgumentList "shell:RecycleBinFolder"
			return $true
		}
		catch {
			ErrorMessage "Failed to move $folderPath to Recycle Bin" -TraceLevels 2
			ErrorMessage "$_"
			return $false
		}
	}
	else {
		ErrorMessage "Folder not found: $folderPath"
		return $true
	}
}

function Maybe-Delete-Folder {
	param (
		[string]$folderPath,
		[string]$exitIfNonExistent = $false,
		[string]$promptMessage = "Folder found at $folderPath. Do you want to delete it?"
	)
	
	if (-not (Test-Path $folderPath)) {
		SuccessMessage "Folder not found: $folderPath, no need to delete" -TraceLevels 2
		return $true
	}
	
	$confirmation = Prompt-For-YesOrNo -message $promptMessage -YesOption "Yes, delete the folder" -NoOption "No, keep the folder"
	if ($confirmation -eq $true) {
		return Move-Folder-To-Recycle-Bin -folderPath $folderPath
	}
}

function Definitely-Delete-Folder {
	param (
		[string]$folderPath
	)
	
	if (-not (Test-Path $folderPath)) {
		ErrorMessage "Folder not found: $folderPath" -TraceLevels 2
		return $false
	}
	
	# If we are currently in the folder or a subfolder, move to the parent directory before deleting
	$currentLocation = Get-Location
	if ($currentLocation.Path -like "$folderPath*") {
		Set-Location (Split-Path $folderPath)
		$newLocation = Get-Location
		InfoMessage "Moved to $newLocation"
	}

	return Move-Folder-To-Recycle-Bin -folderPath $folderPath
}

function Remove-With-Wait {
	param (
		[string]$path
	)
	
	if(-not (Test-Path $path)) {
		InfoMessage "Path not found: $path, skipping deletion" -TraceLevels 2
		return
	}

	# Do not proceed until deletion has finished and clear the output,
	# so we don't get the progress bar randomly popping up while the next commands are running
	try {
		$ProgressPreference = 'SilentlyContinue'
		Remove-Item -Path $path -Recurse -Force | Out-Null
		while (Test-Path $path) {
			Start-Sleep -Seconds 1
		}
		Write-Progress -Activity "Deleting" -Completed
		
		if (-not (Test-Path $path)) {
			SuccessMessage "Deleted $path" -TraceLevels 1
		}
	}
	catch {
		ErrorMessage "Failed to delete $path"
		ErrorMessage $_
	}
}

function Update-Project-Readme {
	$siteDir = $global:SiteConfig.SiteDir
	$readmeFile = Join-Path $siteDir "README.md"
	$projectTemplate = Join-Path $siteDir "README-project.md"
	
	# Delete the original README.md if it exists
	if (Test-Path $readmeFile) {
		Remove-Item $readmeFile -Force | Out-Null
	}
	
	# In README-project.md, replace the placeholder text with the actual project name
	if (Test-Path $projectTemplate) {
		try {
			$siteName = $global:SiteConfig.SiteName
			$fileContent = Get-Content $projectTemplate
			$fileContent = $fileContent.Replace("My Project Name", $siteName)
			$fileContent = $fileContent.Replace("[Client Name]", $siteName)
			SuccessMessage "Updated README-project.md with project name"
		}
		catch {
			ErrorMessage "Failed to update README-project.md"
			ErrorMessage "$_"
		}

		# Rename the README-project.md to README.md
		try {
			Rename-Item -Path $projectTemplate -NewName "README.md"
			SuccessMessage "Updated README.md with project template"
		}
		catch {
			ErrorMessage "Failed to save new README.md from template"
			ErrorMessage "$_"
		}
	}
}

Export-ModuleMember -Function Create-Folder, Maybe-Delete-Folder, Definitely-Delete-Folder, Remove-With-Wait, Update-Project-Readme