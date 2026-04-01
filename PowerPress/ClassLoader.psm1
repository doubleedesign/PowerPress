function Import-Classes {
	# Get all files in the current directory that match the class file pattern
	$classFiles = Get-ChildItem -Path $PSScriptRoot -Filter "*.cs" -File | Select-Object -ExpandProperty FullName

	# Filter out certain files
	$exclude = @("Program.cs")
	$classFiles = $classFiles | Where-Object { $exclude -notcontains (Split-Path $_ -Leaf) }

	try {
		# Load them all at once so inheritance works - otherwise classes can't "see" their parent
		Add-Type -Path $classFiles
		Write-Host "✔  Loaded all classes" -ForegroundColor Green
	}
	catch {
		Write-Host "✖  $( $_.Exception.Message )" -ForegroundColor Red
		if ($_.Exception.InnerException) {
			Write-Host "   $( $_.Exception.InnerException.Message )" -ForegroundColor Red
		}
		exit 1
	}
}

Export-ModuleMember -Function Import-Classes