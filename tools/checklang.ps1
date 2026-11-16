param([string]$Src = (Join-Path $PSScriptRoot "..\src"), [string]$Lang = "Fr", [switch]$ListAll)
# Finds every English text the mod translates and checks the table of one language (LangFr.cs, LangZh.cs...) has it.
# Also checks the setting descriptions (Desc<Lang>) and the viewer comment sections (Default<Lang>).
# Usage: checklang.ps1 -Lang Zh
$lit = '"(?:[^"\\]|\\.)*"'
$expr = "$lit(?:\s*\+\s*$lit)*"
function Join-Lits($s) {
    $parts = [regex]::Matches($s, $lit) | ForEach-Object { $_.Value.Substring(1, $_.Value.Length - 2) }
    ($parts -join '') -replace '\\"', '"' -replace '\\n', "`n"
}
function New-Set { New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal) }
$keys = New-Object System.Collections.Generic.List[string]
$patterns = @(
    "Lang\.(?:T|F|In\(\s*\w+)\s*\(\s*$expr\s*,\s*($expr)",
    "Lang\.In\(\s*[\w\.]+\s*,\s*$expr\s*,\s*($expr)",
    "(?:DevButton|DevHeader|Say)\(\s*$expr\s*,\s*($expr)",
    "(?:TitleEn|DescEn|SuccessEn|FailEn)\s*=>\s*($expr)",
    "NameEn\s*=\s*($expr)",
    "const string (?:ItemName|ProName|PolicyName)\s*=\s*($expr)"
)
foreach ($f in Get-ChildItem $Src -Filter *.cs) {
    if ($f.Name -match '^Lang..\.cs$') { continue } # the language tables themselves
    $t = [IO.File]::ReadAllText($f.FullName)
    foreach ($p in $patterns) {
        foreach ($m in [regex]::Matches($t, $p)) { $keys.Add((Join-Lits $m.Groups[1].Value)) }
    }
}
$extra = @("Auto (same as the game)", "Keyboard", "Mouse", "Easy", "Normal", "Hard", "the skater")
foreach ($e in $extra) { $keys.Add($e) }
$uniq = New-Set
foreach ($k in $keys) { if ($k -ne '') { [void]$uniq.Add($k) } }
$keys = @($uniq) | Sort-Object

$file = Join-Path $Src "Lang$Lang.cs"
$all = [IO.File]::ReadAllText($file)
# only the main table (between "<Lang> = new" and "Sections<Lang>")
$start = $all.IndexOf("$Lang = new"); $end = $all.IndexOf("Sections$Lang")
$tbl = if ($start -ge 0 -and $end -gt $start) { $all.Substring($start, $end - $start) } else { $all }
$have = New-Set
$dups = @()
foreach ($m in [regex]::Matches($tbl, "\{\s*($lit)\s*,\s*$lit\s*\}")) { $k = Join-Lits $m.Groups[1].Value; if (-not $have.Add($k)) { $dups += $k } }
$dups | ForEach-Object { "DUPLICATE: [$_]" }
$missing = $keys | Where-Object { -not $have.Contains($_) }
$unused = @($have) | Where-Object { -not $uniq.Contains($_) }
$unused | ForEach-Object { "UNUSED: [$_]" }
if ($ListAll) { $keys | ForEach-Object { "KEY: [$_]" } }
"texts: $($keys.Count)  in table: $($have.Count)  missing: $(@($missing).Count)"
$missing | ForEach-Object { "MISSING: [$_]" }

# setting descriptions: every key of DescEn (Lang.cs) needs one in Desc<Lang>
function Desc-Keys($text, $name) {
    $s = New-Set
    $i = $text.IndexOf("$name = new")
    if ($i -lt 0) { return ,$s }
    $j = $text.IndexOf("};", $i)
    foreach ($m in [regex]::Matches($text.Substring($i, $j - $i), "\{\s*""(\w+)""\s*,")) { [void]$s.Add($m.Groups[1].Value) }
    return ,$s
}
$en = Desc-Keys ([IO.File]::ReadAllText((Join-Path $Src "Lang.cs"))) "DescEn"
$dl = Desc-Keys $all "Desc$Lang"
"settings: $($en.Count)  described: $($dl.Count)"
@($en) | Where-Object { -not $dl.Contains($_) } | ForEach-Object { "MISSING SETTING: $_" }

# viewer comments: the same [sections] as the English defaults
$sc = [IO.File]::ReadAllText((Join-Path $Src "SkateContent.cs"))
function Comment-Sections($name) {
    $s = New-Set
    $i = $sc.IndexOf("const string $name =")
    if ($i -lt 0) { return ,$s }
    $j = $sc.IndexOf('";', $i)
    foreach ($m in [regex]::Matches($sc.Substring($i, $j - $i), "(?m)^\[(\w+)\]")) { [void]$s.Add($m.Groups[1].Value) }
    return ,$s
}
$ce = Comment-Sections "DefaultEn"; $cl = Comment-Sections "Default$Lang"
"comment sections: $($ce.Count)  in Default${Lang}: $($cl.Count)"
@($ce) | Where-Object { -not $cl.Contains($_) } | ForEach-Object { "MISSING COMMENTS: [$_]" }
