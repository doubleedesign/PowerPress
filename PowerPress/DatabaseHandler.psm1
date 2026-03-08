function Execute-MySql-Command {
	param (
		[string]$command,
		[Boolean]$exitOnFail = $true
	)
	
	try {
		$result = mysql -h $global:SiteConfig.DbHost -P $global:SiteConfig.DbPort -u $global:SiteConfig.DbUser -e $command
		return $result
	}
	catch {
		ErrorMessage "Error executing MySQL command: $_"
		if ($exitOnFail) {
			exit 1
		}
	}
}

function Db-Exists {
	$dbName = $global:SiteConfig.DbName
	try {
		$result = Execute-MySql-Command -command "SELECT 1 FROM information_schema.schemata WHERE schema_name = '$dbName';" -exitOnFail $false
		return $result -ne $null
	}
	catch {
		if ($errorOutput -match "Unknown database" -or $errorOutput -match "doesn't exist") {
			return $false
		}
		else {
			ErrorMessage $errorOutput
			return $false
		}
	}
	
	return $false
}

function Is-Database-Empty {
	$dbName = $global:SiteConfig.DbName
	try {
		$result = Execute-MySql-Command -command "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$dbName';" -exitOnFail $false
		if ($result -ne $null) {
			$tableCount = $result.Replace("COUNT(*)", "").Trim()
			return $tableCount -eq 0
		}
		else {
			ErrorMessage "Failed to check if database is empty: No result returned"
			return $false
		}
	}
	catch {
		ErrorMessage "Failed to check if database is empty: $_"
		return $false
	}
}

function Maybe-Drop-Existing-Database {
	$dbName = $global:SiteConfig.DbName
	$dbExists = Db-Exists
	$dbEmpty = Is-Database-Empty

	if($dbExists -and $dbEmpty) {
		SuccessMessage "Database $dbName already exists but is empty, skipping drop"
	}
	elseif($dbExists) {
		WarningMessage("Database $dbName already exists and is not empty")

		$choice = `Prompt-For-YesOrNo `
			-Message "Do you want to drop the existing database and create a new one?" `
			-YesOption "Yes, drop the existing database" `
			-NoOption "No, leave the existing database as-is" `
			-DefaultYes $false
		
		if ($choice -eq $true) {
			Execute-MySql-Command "DROP DATABASE $dbName"
			$existsNow = Db-Exists
			if(-not $existsNow) {
				SuccessMessage "Dropped existing database: $dbName"
			}
			else {
				ErrorMessage "Failed to drop database: $dbName"
				exit 1
			}
		}
	}
}

function Create-Database-If-Not-Exists {
	$dbName = $global:SiteConfig.DbName
	$dbExists = Db-Exists
	if ($dbExists) {
		return
	}
	
	$dbName = $global:SiteConfig.DbName
	$result = Execute-MySql-Command "CREATE DATABASE IF NOT EXISTS $dbName;"
	if(Db-Exists) {
		SuccessMessage("Database created: $dbName") 
	}
	else {
		ErrorMessage("Failed to create database: $dbName")
		ErrorMessage($_)
		exit 1
	}
}

function Maybe-Import-Database {
	$pathToSql = Prompt-For-Text "Enter the full path to the .sql file you want to import"
	$pathToSql = $pathToSql.Trim('"')
	if (-not (Test-Path $sqlFilePath)) {
		Throw "SQL file not found: $sqlFilePath"
	}
	
	Execute-MySql-Command "$($global:SiteConfig.DbName) < $pathToSql"
	
	# Get the site URL from the WP options table and update it to the local one
	$oldSiteUrl = Execute-MySql-Command "SELECT option_value FROM $($global:SiteConfig.DbName).wp_options WHERE option_name='siteurl';"
	if ($LASTEXITCODE -ne 0) {
		ErrorMessage "Failed to get site URL from database, cannot update automatically"
	}
	else {
		wp search-replace $oldSiteUrl $global:SiteConfig.SiteUrl --skip-columns=guid
	}
}

Export-ModuleMember -Function Maybe-Drop-Existing-Database, Create-Database-If-Not-Exists, Maybe-Import-Database