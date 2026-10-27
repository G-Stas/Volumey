param(
	[ValidateSet('start', 'status', 'state', 'screenshot', 'request', 'stop')]
	[string]$Action = 'status',
	[string]$Request = '{}'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sessionRoot = Join-Path $env:LOCALAPPDATA 'Volumey\LocalDiagnostics'
$sessionFile = Join-Path $sessionRoot 'session.json'
$outputDir = Join-Path $root 'Volumey\bin\Debug\net10.0-windows10.0.19041.0\win-x64'
$executable = Join-Path $outputDir 'Volumey.exe'

function Read-Session {
	if(!(Test-Path -LiteralPath $sessionFile)) { return $null }
	try { return Get-Content -Raw -LiteralPath $sessionFile | ConvertFrom-Json }
	catch { return $null }
}

function Invoke-Request([string]$Json) {
	$session = Read-Session
	if(!$session) { throw 'No diagnostic session. Run start first.' }
	$client = [System.IO.Pipes.NamedPipeClientStream]::new('.', $session.pipe, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
	$client.Connect(3000)
	$reader = [System.IO.StreamReader]::new($client, [System.Text.Encoding]::UTF8, $false, 1024, $true)
	$writer = [System.IO.StreamWriter]::new($client, [System.Text.Encoding]::UTF8, 1024, $true)
	$writer.AutoFlush = $true
	$writer.WriteLine($Json)
	$result = $reader.ReadLine()
	$reader.Dispose()
	$writer.Dispose()
	$client.Dispose()
	return $result | ConvertFrom-Json
}

switch($Action) {
	start {
		if(!(Test-Path -LiteralPath $executable)) { & dotnet build (Join-Path $root 'Volumey\Volumey.csproj') -c Debug; if($LASTEXITCODE) { throw 'Build failed' } }
		New-Item -ItemType Directory -Force -Path $sessionRoot | Out-Null
		$existing = Read-Session
		if($existing) {
			try { if((Invoke-Request '{"op":"status"}').ready) { 'Reconnected to existing diagnostic process'; exit 0 } } catch {}
			$owner = Get-Process -Id $existing.pid -ErrorAction SilentlyContinue
			if($owner) {
				$ownerPath = $owner.Path
				if($ownerPath -eq $existing.executable) { throw "Diagnostic process $($existing.pid) is alive but its pipe is not responding; inspect $($existing.profile)." }
				throw "Session PID $($existing.pid) belongs to another executable; refusing to replace its descriptor."
			}
			Remove-Item -LiteralPath $sessionFile -Force
		}
		$pipe = 'Volumey.LocalDiagnostics.' + [guid]::NewGuid().ToString('N')
		$profile = Join-Path $sessionRoot ([guid]::NewGuid().ToString('N'))
		$sessionFile = Join-Path $sessionRoot 'session.json'
		$arguments = @('--local-diagnostics', '--session', $sessionFile, '--profile', $profile, '--pipe', $pipe)
		$process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
		$deadline = [DateTime]::UtcNow.AddSeconds(20)
		do {
			Start-Sleep -Milliseconds 200
			if($process.HasExited) { throw "Diagnostic process exited with code $($process.ExitCode)." }
			try { $result = Invoke-Request '{"op":"status"}'; if($result.ready) { $result | ConvertTo-Json -Depth 5; exit 0 } } catch {}
		} while([DateTime]::UtcNow -lt $deadline)
		throw "Diagnostic process did not become ready; see its profile at $profile."
	}
	status { $session = Read-Session; if(!$session) { 'stopped'; exit 1 }; Invoke-Request '{"op":"status"}' | ConvertTo-Json -Depth 5 }
	state { Invoke-Request '{"op":"state"}' | ConvertTo-Json -Depth 5 }
	screenshot { Invoke-Request '{"op":"screenshot"}' | ConvertTo-Json -Depth 5 }
	request { Invoke-Request $Request | ConvertTo-Json -Depth 5 }
	stop { Invoke-Request '{"op":"stop"}' | ConvertTo-Json -Depth 5 }
}
