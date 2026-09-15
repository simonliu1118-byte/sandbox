param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$IconPath
)

$ErrorActionPreference = 'Stop'

$exe = [System.IO.Path]::GetFullPath($ExePath)
$ico = [System.IO.Path]::GetFullPath($IconPath)

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "EXE not found: $exe"
}
if (-not (Test-Path -LiteralPath $ico -PathType Leaf)) {
    throw "ICO not found: $ico"
}

if (-not ('ScratchGame.NativeResources' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace ScratchGame
{
    internal static class NativeResources
    {
        internal const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateResource(
            IntPtr hUpdate,
            IntPtr lpType,
            IntPtr lpName,
            ushort wLanguage,
            byte[] lpData,
            uint cbData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);
    }
}
'@
}

function Read-U16([byte[]]$Bytes, [int]$Offset) {
    return [System.BitConverter]::ToUInt16($Bytes, $Offset)
}

$bytes = [System.IO.File]::ReadAllBytes($ico)
if ($bytes.Length -lt 6) {
    throw 'Invalid ICO: file is too small.'
}

$reserved = Read-U16 $bytes 0
$type = Read-U16 $bytes 2
$count = Read-U16 $bytes 4
if ($reserved -ne 0 -or $type -ne 1 -or $count -lt 1) {
    throw 'Invalid ICO header.'
}

$directoryBytes = 6 + (16 * $count)
if ($bytes.Length -lt $directoryBytes) {
    throw 'Invalid ICO: directory is truncated.'
}

$RT_ICON = [IntPtr]3
$RT_GROUP_ICON = [IntPtr]14
$GROUP_ID = [IntPtr]1
$LANG_NEUTRAL = [uint16]0

# dotnet publish has already converted the ApplicationIcon into RT_ICON payloads.
# Read those final PE resources as the source of truth. The repository ICO is used
# only for the directory metadata (width/height/planes/bit-depth); we deliberately
# do not trust its payload offsets because older versions of this ICO contain a
# non-standard final entry that the Windows/.NET resource compiler tolerates.
$module = [ScratchGame.NativeResources]::LoadLibraryEx(
    $exe,
    [IntPtr]::Zero,
    [ScratchGame.NativeResources]::LOAD_LIBRARY_AS_DATAFILE)
if ($module -eq [IntPtr]::Zero) {
    throw "LoadLibraryEx failed before icon-group repair: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}

$entries = @()
try {
    for ($i = 0; $i -lt $count; $i++) {
        $id = [uint16]($i + 1)
        $offset = 6 + (16 * $i)
        $resource = [ScratchGame.NativeResources]::FindResource(
            $module,
            [IntPtr][int]$id,
            $RT_ICON)
        if ($resource -eq [IntPtr]::Zero) {
            throw "Published EXE is missing RT_ICON #$id."
        }

        $resourceSize = [ScratchGame.NativeResources]::SizeofResource($module, $resource)
        if ($resourceSize -lt 1) {
            throw "Published EXE has invalid RT_ICON #$id size."
        }

        $entries += [pscustomobject]@{
            Id         = $id
            Width      = $bytes[$offset]
            Height     = $bytes[$offset + 1]
            ColorCount = $bytes[$offset + 2]
            Reserved   = $bytes[$offset + 3]
            Planes     = Read-U16 $bytes ($offset + 4)
            BitCount   = Read-U16 $bytes ($offset + 6)
            Size       = [uint32]$resourceSize
        }
    }
}
finally {
    [void][ScratchGame.NativeResources]::FreeLibrary($module)
}

# Windows shell icon resolution needs an RT_GROUP_ICON directory that references
# the existing RT_ICON payload IDs. The single-file publish currently contains the
# payloads but omits this group directory.
$groupStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($groupStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$entries.Count)
    foreach ($entry in $entries) {
        $writer.Write([byte]$entry.Width)
        $writer.Write([byte]$entry.Height)
        $writer.Write([byte]$entry.ColorCount)
        $writer.Write([byte]$entry.Reserved)
        $writer.Write([uint16]$entry.Planes)
        $writer.Write([uint16]$entry.BitCount)
        $writer.Write([uint32]$entry.Size)
        $writer.Write([uint16]$entry.Id)
    }
    $writer.Flush()
    $groupData = $groupStream.ToArray()
}
finally {
    $writer.Dispose()
    $groupStream.Dispose()
}

$handle = [ScratchGame.NativeResources]::BeginUpdateResource($exe, $false)
if ($handle -eq [IntPtr]::Zero) {
    throw "BeginUpdateResource failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}

$success = $false
try {
    if (-not [ScratchGame.NativeResources]::UpdateResource(
            $handle,
            $RT_GROUP_ICON,
            $GROUP_ID,
            $LANG_NEUTRAL,
            $groupData,
            [uint32]$groupData.Length)) {
        throw "UpdateResource RT_GROUP_ICON failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }

    if (-not [ScratchGame.NativeResources]::EndUpdateResource($handle, $false)) {
        throw "EndUpdateResource failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    $success = $true
}
finally {
    if (-not $success -and $handle -ne [IntPtr]::Zero) {
        [void][ScratchGame.NativeResources]::EndUpdateResource($handle, $true)
    }
}

$module = [ScratchGame.NativeResources]::LoadLibraryEx(
    $exe,
    [IntPtr]::Zero,
    [ScratchGame.NativeResources]::LOAD_LIBRARY_AS_DATAFILE)
if ($module -eq [IntPtr]::Zero) {
    throw "LoadLibraryEx verification failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}
try {
    $group = [ScratchGame.NativeResources]::FindResource($module, $GROUP_ID, $RT_GROUP_ICON)
    if ($group -eq [IntPtr]::Zero) {
        throw 'Final EXE does not contain RT_GROUP_ICON #1.'
    }
}
finally {
    [void][ScratchGame.NativeResources]::FreeLibrary($module)
}

Write-Host "Embedded and verified RT_GROUP_ICON for $($entries.Count) published RT_ICON resources: $exe"
