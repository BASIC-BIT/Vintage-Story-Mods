# Test-only: writes a generic Windows credential whose blob is the UTF-8 bytes
# read from stdin, the same layout set-moddb-session.ps1 used for the real
# session. Never used by the production adapter.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Target)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TheBasics.WinCredFixture
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    public static class NativeMethods
    {
        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredWrite(ref Credential credential, uint flags);
    }
}
'@

$stream = New-Object IO.MemoryStream
[Console]::OpenStandardInput().CopyTo($stream)
$bytes = $stream.ToArray()
$handle = [Runtime.InteropServices.GCHandle]::Alloc($bytes, [Runtime.InteropServices.GCHandleType]::Pinned)
try {
    $credential = [TheBasics.WinCredFixture.Credential]@{
        Type = 1
        TargetName = $Target
        CredentialBlobSize = $bytes.Length
        CredentialBlob = $handle.AddrOfPinnedObject()
        Persist = 2
        UserName = "wincred-test-fixture"
    }
    if (-not [TheBasics.WinCredFixture.NativeMethods]::CredWrite([ref]$credential, 0)) {
        [Console]::Error.WriteLine("fixture write failed")
        exit 1
    }
} finally {
    $handle.Free()
    [Array]::Clear($bytes, 0, $bytes.Length)
}
