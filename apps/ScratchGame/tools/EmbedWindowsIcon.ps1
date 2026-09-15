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
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);
    }
}
'@
}

function Read-U16([byte[]]$Bytes, [int]$Offset) {
    return [System.BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-U32([byte[]]$Bytes, [int]$Offset) {
    return [System.BitConverter]::ToUInt32($Bytes, $Offset)
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

$entries = @()
for ($i = 0; $i -lt $count; $i++) {
    $offset = 6 + (16 * $i)
    $size = Read-U32 $bytes ($offset + 8)
    $imageOffset = Read-U32 $bytes ($offset + 12)

    if ($size -lt 1 -or ([uint64]$imageOffset + [uint64]$size) -gt [uint64]$bytes.Length) {
        throw "Invalid ICO image entry #$($i + 1)."
    }

    $image = New-Object byte[] $size
    [Array]::Copy($bytes, [int]$imageOffset, $image, 0, [int]$size)

    $entries += [pscustomobject]@{
        Id         = [uint16]($i + 1)
        Width      = $bytes[$offset]
        Height     = $bytes[$offset + 1]
        ColorCount = $bytes[$offset + 2]
        Reserved   = $bytes[$offset + 3]
        Planes     = Read-U16 $bytes ($offset + 4)
        BitCount   = Read-U16 $bytes ($offset + 6)
        Size       = [uint32]$size
        Image      = $image
    }
}

# Windows PE icon resources need both the RT_ICON payloads and one RT_GROUP_ICON
# directory that points to those payload IDs. dotnet single-file publish can retain
# individual RT_ICON resources while losing the group directory, which leaves the
# shell/taskbar unable to resolve the application icon reliably.
$groupStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($groupStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$count)
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

$RT_ICON = [IntPtr]3
$RT_GROUP_ICON = [IntPtr]14
$GROUP_ID = [IntPtr]1
$LANG_NEUTRAL = [uint16]0

$handle = [ScratchGame.NativeResources]::BeginUpdateResource($exe, $false)
if ($handle -eq [IntPtr]::Zero) {
    throw "BeginUpdateResource failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}

$success = $false
try {
    foreach ($entry in $entries) {
        if (-not [ScratchGame.NativeResources]::UpdateResource(
                $handle,
                $RT_ICON,
                [IntPtr][int]$entry.Id,
                $LANG_NEUTRAL,
                $entry.Image,
                [uint32]$entry.Image.Length)) {
            throw "UpdateResource RT_ICON #$($entry.Id) failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
        }
    }

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

Write-Host "Embedded and verified Windows icon group ($count image entries): $exe"
