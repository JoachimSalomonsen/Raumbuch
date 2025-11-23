$lines = Get-Content -Path "RaumbuchService\Controllers\RaumbuchController.cs"
$output = @()

# Keep lines 0-1512 (first part)
for($i=0; $i -le 1512; $i++) { 
    $output += $lines[$i] 
}

# Skip lines 1513-2358 (duplicated section)
# Keep lines 2359 to end
for($i=2359; $i -lt $lines.Count; $i++) { 
    $output += $lines[$i] 
}

$output | Set-Content -Path "RaumbuchService\Controllers\RaumbuchController.cs"
Write-Host "Fixed! Removed duplicate helper methods (lines 1513-2358)"
