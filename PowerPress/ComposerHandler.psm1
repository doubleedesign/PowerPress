# TODO: This is here as a reference for possible updates to the C# version 
# Delete when that is finalised
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
