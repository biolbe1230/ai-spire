$file = "d:\GGames\ai-spire\decompiled\sts2.decompiled.cs"
$targets = @(222988, 230818, 239659, 144813, 194651, 102834, 39192, 7259)
$sr = [System.IO.StreamReader]::new($file, [System.Text.Encoding]::UTF8)
$lineNum = 0
$currentNs = ""
$results = @{}
$remaining = [System.Collections.Generic.HashSet[int]]::new([int[]]$targets)
while (($line = $sr.ReadLine()) -ne $null) {
    $lineNum++
    if ($line -match '^\s*namespace\s+(\S+)') { $currentNs = $Matches[1] }
    if ($remaining.Contains($lineNum)) {
        $results[$lineNum] = $currentNs
        $remaining.Remove($lineNum) | Out-Null
        if ($remaining.Count -eq 0) { break }
    }
}
$sr.Close()
foreach ($t in $targets) { Write-Output "${t}: $($results[$t])" }
