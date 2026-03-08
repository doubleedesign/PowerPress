function Get-Calling-Function-Names {
	param (
		[int]$TraceLevels = 1
	)
	
	$exclude = @(
		"Run-Wp-Cli-Command-With-Custom-Output",
		"Run-Composer-Command-With-Custom-Output-Handling",
		"Remove-With-Wait",
		"Message-With-Caller",
		"SuccessMessage",
		"WarningMessage",
		"ErrorMessage",
		"InfoMessage",
		"DebugMessage",
		"Prompt-For-Text",
		"Prompt-For-YesOrNo"
	)

	# Loop through the call stack and find the callers not in the exclude list up to the requested trace level
	$stack = Get-PSCallStack | Select-Object -Skip 1
	$result = @()
	$counter = 0
	foreach ($frame in $stack) {
		# $frame | Format-List | Out-String | Write-Host -ForegroundColor DarkGray # debug the contents of $frame
		if ($frame.Command -notin $exclude) {
			# If the caller is main.ps1, also include the line number
			if ($frame.Command -eq "main.ps1") {
				$result += "$($frame.Command):$($frame.ScriptLineNumber)"
			}
			else {
				$result += $frame.Command
			}
			
			$counter++
			if ($counter -ge $TraceLevels) {
				break
			}
		}
	}
	
	return $result
}

function Message-With-Caller {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)

	$LineWidth = $Host.UI.RawUI.WindowSize.Width
	$Callers = Get-Calling-Function-Names -TraceLevels $TraceLevels
	$Caller = ($Callers | ForEach-Object { "[$_]" }) -join ""

	# If the message is longer than the window, move the caller info to the next line to avoid wrapping issues
	if (($Message.Length + $Caller.Length + 6) -gt $LineWidth) {
		$RightAlignedCaller = $Caller.PadLeft([Math]::Max($Caller.Length, $LineWidth - 6))
		return @{
			Message = $Message.Trim();
			Caller = "`n$RightAlignedCaller"
		}
	}

	$Output = $Message.Trim().PadRight($LineWidth - $Caller.Length - 6)
	return @{
		Message = $Output;
		Caller = "$Caller"
	}
}

function SuccessMessage {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)

	$Output = Message-With-Caller -Message $Message -TraceLevels $TraceLevels
	Write-Host "✔  $($Output.Message) " -ForegroundColor Green -NoNewline
	Write-Host $Output.Caller -ForegroundColor DarkGray
}

function WarningMessage {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)

	$Output = Message-With-Caller -Message $Message -TraceLevels $TraceLevels
	Write-Host "⚠️ $($Output.Message) " -ForegroundColor Yellow -NoNewline
	Write-Host $Output.Caller -ForegroundColor DarkGray
}

function ErrorMessage {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)

	$Output = Message-With-Caller -Message $Message -TraceLevels $TraceLevels
	Write-Host "✖  $($Output.Message) " -ForegroundColor Red -NoNewline
	Write-Host $Output.Caller -ForegroundColor DarkGray
}

function InfoMessage {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)

	$Output = Message-With-Caller -Message $Message -TraceLevels $TraceLevels
	Write-Host "📝 $($Output.Message) " -ForegroundColor Blue -NoNewline
	Write-Host $Output.Caller -ForegroundColor DarkGray
}

function DebugMessage {
	param (
		[string]$Message,
		[int]$TraceLevels = 1
	)
	
	# Check for POWERPRESS_DEBUG environment variable - if not set, don't output anything
	if (-not $env:POWERPRESS_DEBUG) {
		return
	}
	
	$Output = Message-With-Caller -Message "[DEBUG] $Message" -TraceLevels $TraceLevels
	Write-Host "🐞 $($Output.Message) " -ForegroundColor White -NoNewline
	Write-Host $Output.Caller -ForegroundColor DarkGray
}

function Prompt-For-Text {
	param (
		[string]$Message,
		[string]$DefaultValue = ""
	)
	
	$FormattedMessage = $Message.Trim()
	if ($PSBoundParameters.ContainsKey('DefaultValue')) {
		if([string]::IsNullOrWhiteSpace($DefaultValue)) {
			$FormattedMessage += " (default is empty)"
		}
		else {
			$FormattedMessage += " (default is '$DefaultValue')"
		}
	}
	$MessageWithDebug = Message-With-Caller -Message $FormattedMessage
	
	Write-Host ""
	Write-Host "❓ $($MessageWithDebug.Message)" -ForegroundColor Cyan -NoNewline
	Write-Host " $($MessageWithDebug.Caller)" -ForegroundColor DarkGray

	$UserInput = Read-Host
	if ([string]::IsNullOrWhiteSpace($UserInput)) {
		# Move the cursor up one line and clear it, then output the default value so it is seen in the same way as an input one
		[Console]::SetCursorPosition(0, [Console]::CursorTop - 1)
		Write-Host (" " * [Console]::BufferWidth) -NoNewline
		[Console]::SetCursorPosition(0, [Console]::CursorTop)
		Write-Host $DefaultValue -ForegroundColor Gray
		
		return $DefaultValue
	}
	
	return $UserInput.Trim()
}

function Prompt-For-YesOrNo {
	param (
		[string]$Message,
		[string]$YesOption,
		[string]$NoOption,
		[bool]$DefaultYes = $false
	)

	$options = @($YesOption, $NoOption)
	$selected = if ($DefaultYes) { 0 } else { 1 }
	
	$Output = Message-With-Caller -Message $Message

	Write-Host ""
	Write-Host "❔ $($Output.Message)" -ForegroundColor Cyan -NoNewline
	Write-Host " $($MessageWithDebug.Caller)" -ForegroundColor DarkGray

	while ($true) {
		for ($i = 0; $i -lt $options.Count; $i++) {
			if ($i -eq $selected) {
				Write-Host "   ● $($options[$i])" -ForegroundColor Yellow
			} else {
				Write-Host "   ○ $($options[$i])" -ForegroundColor Gray
			}
		}

		$key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
		switch ($key.VirtualKeyCode) {
			# Up arrow
			38 { $selected = ($selected - 1 + $options.Count) % $options.Count }
			# Down arrow
			40 { $selected = ($selected + 1) % $options.Count }
			# Enter
			13 {                                                                  
				return $selected -eq 0
			}
		}
		
		[Console]::SetCursorPosition(0, [Console]::CursorTop - $options.Count)
	}
}

function Display-Json-Table {
	param (
		[string]$JsonString
	)

	try {
		$JsonObject = $JsonString | ConvertFrom-Json
		$JsonObject | Format-List | Out-String | Write-Host -ForegroundColor Gray
	}
	catch {
		ErrorMessage "Failed to parse JSON" -TraceLevels 2
		ErrorMessage $_
	}
}

$DividerWidth = 100
function Display-Section-Header {
	param (
		[string]$Title
	)

	$Output = $Title.Trim()
	$TotalPadding = $DividerWidth - $Output.Length - 2 
	$LeftPadding = [math]::Floor($TotalPadding / 2)
	$RightPadding = [math]::Ceiling($TotalPadding / 2)

	$Output = ("=" * $LeftPadding) + " $Output " + ("=" * $RightPadding)
	Write-Host "`n$Output" -ForegroundColor Magenta
}

function Display-Section-Footer {
	$Output = "=" * $DividerWidth
	Write-Host "$Output`n" -ForegroundColor Magenta
}

Export-ModuleMember -Function SuccessMessage, WarningMessage, ErrorMessage, InfoMessage, DebugMessage
Export-ModuleMember -Function Prompt-For-Text, Prompt-For-YesOrNo, Display-Json-Table
Export-ModuleMember -Function Display-Section-Header, Display-Section-Footer