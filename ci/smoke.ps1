# 冒烟测试：用 32 位 Windows PowerShell（与程序同为 32 位）加载编译产物，
# 确认 SQLite（开局库依赖）能真正打开数据库。失败则 CI 失败，不发布。
$ErrorActionPreference = 'Stop'
$global:BinDir = (Resolve-Path 'VinXiangQi/bin/Release').Path
"进程位数: " + [IntPtr]::Size * 8
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $global:BinDir ($n + '.dll')
    if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFrom($p) }
    return $null
})
$arch = if ([IntPtr]::Size -eq 8) { 'win-x64' } else { 'win-x86' }
$env:PATH = (Join-Path $global:BinDir "runtimes\$arch\native") + ';' + $global:BinDir + ';' + $env:PATH
Get-ChildItem $global:BinDir -Filter '*sqlite*' -Recurse | ForEach-Object { $_.FullName.Substring($global:BinDir.Length) }
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $global:BinDir 'SQLitePCLRaw.batteries_v2.dll'))
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $global:BinDir 'Microsoft.Data.Sqlite.dll'))
[SQLitePCL.Batteries_V2]::Init()
$c = New-Object Microsoft.Data.Sqlite.SqliteConnection('Data Source=:memory:')
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = 'select sqlite_version()'
$v = $cmd.ExecuteScalar()
$c.Close()
"SQLite 版本: $v"
if (-not $v) { throw 'SQLite 未返回版本号' }
# 配置文件里必须带上绑定重定向，否则程序运行时会找不到旧版 SQLitePCLRaw
$cfg = Get-Content (Join-Path $global:BinDir 'VinXiangQi.exe.config') -Raw
if ($cfg -notmatch 'SQLitePCLRaw.core') { throw 'VinXiangQi.exe.config 缺少 SQLitePCLRaw 绑定重定向' }
'冒烟测试通过'
