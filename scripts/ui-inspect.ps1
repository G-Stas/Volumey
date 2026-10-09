<#
.SYNOPSIS
    Inspects and drives the running dev build of Volumey through UI Automation, without a mouse.

.EXAMPLE
    ./scripts/ui-inspect.ps1 -Show                          # bring the window up (like a second app launch would)
    ./scripts/ui-inspect.ps1 -Screenshot $env:TEMP\v.png    # capture the app's windows into one image
    ./scripts/ui-inspect.ps1 -Tree -Depth 8                 # dump controls: type, name, automation id, state
    ./scripts/ui-inspect.ps1 -Click 'Settings'              # invoke/toggle/select the first control with that name or id
    ./scripts/ui-inspect.ps1 -Click SettingsItem -Mouse     # real mouse click, for controls that only react to clicks
    ./scripts/ui-inspect.ps1 -At 290,268                    # real click at a window-relative position (unnamed controls)
#>
[CmdletBinding()]
param(
    # defaults to the running Debug build of this repo
    [int]$ProcessId,
    [switch]$Show,
    [string]$Screenshot,
    [switch]$Tree,
    [int]$Depth = 6,
    [string]$Click,
    # click with the real mouse at the control center (moves the cursor) instead of UI Automation patterns;
    # needed where the app reacts to clicks, not to selection (e.g. NavigationView items)
    [switch]$Mouse,
    # real mouse click at "x,y" relative to the app window's top-left corner (for controls without a name,
    # e.g. combo boxes; take a -Screenshot first to find the position). Refuses to click if the window isn't visible.
    [int[]]$At
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type -Namespace Native -Name Win32 -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string lpString);
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr hWnd, uint msg, System.IntPtr wParam, System.IntPtr lParam);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extra);
[DllImport("user32.dll")] public static extern System.IntPtr WindowFromPoint(System.Drawing.Point point);
[DllImport("user32.dll")] public static extern System.IntPtr GetAncestor(System.IntPtr hWnd, uint flags);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint processId);
'@ -ReferencedAssemblies System.Drawing.Primitives -ErrorAction SilentlyContinue

# A real click at a screen point, only if the window there belongs to the dev build: another app's window can cover Volumey,
# and the click would land in that app instead (it happened once with a chat window on top)
function Invoke-MouseClick([int]$sx, [int]$sy) {
    $hwnd = [Native.Win32]::WindowFromPoint([System.Drawing.Point]::new($sx, $sy))
    $root = [Native.Win32]::GetAncestor($hwnd, 2) # GA_ROOT
    [uint32]$owner = 0
    [void][Native.Win32]::GetWindowThreadProcessId($root, [ref]$owner)
    if ($owner -ne $ProcessId) {
        $other = (Get-Process -Id $owner -ErrorAction SilentlyContinue).ProcessName
        throw "Not clicking: the window at $sx,$sy belongs to '$other' (pid $owner), not the dev build. Bring Volumey to the front first."
    }
    [void][Native.Win32]::SetCursorPos($sx, $sy)
    [Native.Win32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero) # left down
    [Native.Win32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero) # left up
}

$repo = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repo 'Volumey\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Volumey.exe'
if (-not $ProcessId) {
    $process = Get-Process Volumey -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Select-Object -First 1
    if (-not $process) { throw 'The dev build is not running. Start it with scripts/run-dev.ps1.' }
    $ProcessId = $process.Id
}

if ($Show) {
    # same message a second Debug instance broadcasts (Startup.WM_SHOWME with the "-dev" suffix)
    $msg = [Native.Win32]::RegisterWindowMessage('WM_SHOWME-dev')
    [void][Native.Win32]::PostMessage([IntPtr]0xffff, $msg, [IntPtr]::Zero, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 700
}

$ae = [System.Windows.Automation.AutomationElement]
$condition = New-Object System.Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $ProcessId)
$windows = @($ae::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition))
foreach ($w in $windows) {
    $r = $w.Current.BoundingRectangle
    "window '{0}' at {1},{2} size {3}x{4} offscreen={5}" -f $w.Current.Name, $r.X, $r.Y, $r.Width, $r.Height, $w.Current.IsOffscreen
}

function Get-State($element) {
    $states = @()
    foreach ($p in $element.GetSupportedPatterns()) {
        $pattern = $element.GetCurrentPattern($p)
        switch ($p.ProgrammaticName) {
            'TogglePatternIdentifiers.Pattern'        { $states += "toggle=$($pattern.Current.ToggleState)" }
            'ValuePatternIdentifiers.Pattern'         { $states += "value='$($pattern.Current.Value)'" }
            'RangeValuePatternIdentifiers.Pattern'    { $states += "range=$($pattern.Current.Value)" }
            'SelectionItemPatternIdentifiers.Pattern' { $states += "selected=$($pattern.Current.IsSelected)" }
        }
    }
    if (-not $element.Current.IsEnabled) { $states += 'disabled' }
    $states -join ' '
}

function Write-Tree($element, $level) {
    if ($level -gt $Depth) { return }
    $c = $element.Current
    $type = $c.ControlType.ProgrammaticName -replace '^ControlType\.', ''
    if ($c.Name -or $c.AutomationId -or $level -le 1) {
        '{0}{1} "{2}" id={3} {4}' -f ('  ' * $level), $type, $c.Name, $c.AutomationId, (Get-State $element)
    }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $child = $walker.GetFirstChild($element)
    while ($child) { Write-Tree $child ($level + 1); $child = $walker.GetNextSibling($child) }
}

if ($Tree) { foreach ($w in $windows) { Write-Tree $w 0 } }

if ($Click) {
    $nameCondition = New-Object System.Windows.Automation.OrCondition(
        (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $Click)),
        (New-Object System.Windows.Automation.PropertyCondition($ae::AutomationIdProperty, $Click)))
    $target = $null
    foreach ($w in $windows) { $target = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition); if ($target) { break } }
    if (-not $target) { throw "No control named '$Click'" }
    $pattern = $null
    if ($Mouse) {
        $r = $target.Current.BoundingRectangle
        Invoke-MouseClick ([int]($r.X + $r.Width / 2)) ([int]($r.Y + $r.Height / 2))
    }
    elseif ($target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
    elseif ($target.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) { $pattern.Toggle() }
    elseif ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select() }
    elseif ($target.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { $pattern.Expand() }
    else { throw "'$Click' supports no invoke/toggle/select/expand pattern" }
    "clicked '$Click' ($($target.Current.ControlType.ProgrammaticName))"
    Start-Sleep -Milliseconds 500
}

if ($At) {
    # the main window is the largest one; popups (drop-downs, tooltips) are separate windows of the process
    $window = $windows | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 } |
        Sort-Object { $_.Current.BoundingRectangle.Width * $_.Current.BoundingRectangle.Height } -Descending | Select-Object -First 1
    if (-not $window) { throw 'No visible window to click into (use -Show)' }
    if ($At.Count -ne 2) { throw "-At takes two numbers: x,y" }
    $x, $y = $At
    $r = $window.Current.BoundingRectangle
    if ($x -lt 0 -or $y -lt 0 -or $x -ge $r.Width -or $y -ge $r.Height) { throw "$x,$y is outside the window ($($r.Width)x$($r.Height))" }
    Invoke-MouseClick ([int]($r.X + $x)) ([int]($r.Y + $y))
    "clicked at $x,$y"
    Start-Sleep -Milliseconds 700
}

if ($Screenshot) {
    $visible = $windows | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 }
    if (-not $visible) { throw 'No visible window to capture (use -Show)' }
    $rects = $visible | ForEach-Object { $_.Current.BoundingRectangle }
    $left = [int]($rects | Measure-Object X -Minimum).Minimum
    $top = [int]($rects | Measure-Object Y -Minimum).Minimum
    $right = [int]($rects | ForEach-Object { $_.X + $_.Width } | Measure-Object -Maximum).Maximum
    $bottom = [int]($rects | ForEach-Object { $_.Y + $_.Height } | Measure-Object -Maximum).Maximum
    $bmp = New-Object System.Drawing.Bitmap ($right - $left), ($bottom - $top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($left, $top, 0, 0, $bmp.Size)
    $bmp.Save($Screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
    "screenshot: $Screenshot"
}
