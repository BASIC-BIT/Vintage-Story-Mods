# Narrow Windows Credential Manager adapter for the one-time session migration.
# Read: writes the credential blob (stored as UTF-8 by set-moddb-session.ps1) to
# the raw stdout stream, no trailing newline. Delete: removes the credential and
# treats ERROR_NOT_FOUND (1168) as success. Any failure exits nonzero with a
# fixed, value-free stderr line. No Write operation exists here on purpose.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet("Read", "Delete")][string]$Operation,
    [Parameter(Mandatory)][string]$Target
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$failed = $false

try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TheBasics.WinCredSession
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
        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("Advapi32.dll")]
        public static extern void CredFree(IntPtr credential);
    }
}
'@

    $GenericType = 1

    if ($Operation -eq "Delete") {
        if (-not [TheBasics.WinCredSession.NativeMethods]::CredDelete($Target, $GenericType, 0)) {
            $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            if ($code -ne 1168) { $failed = $true }
        }
    } else {
        $pointer = [IntPtr]::Zero
        $bytes = $null
        try {
            if (-not [TheBasics.WinCredSession.NativeMethods]::CredRead($Target, $GenericType, 0, [ref]$pointer)) {
                throw "read"
            }
            $credential = [Runtime.InteropServices.Marshal]::PtrToStructure(
                $pointer, [type][TheBasics.WinCredSession.Credential])
            $bytes = [byte[]]::new($credential.CredentialBlobSize)
            if ($bytes.Length -gt 0) {
                [Runtime.InteropServices.Marshal]::Copy($credential.CredentialBlob, $bytes, 0, $bytes.Length)
            }
            $stdout = [Console]::OpenStandardOutput()
            $stdout.Write($bytes, 0, $bytes.Length)
            $stdout.Flush()
        } finally {
            if ($null -ne $bytes) { [Array]::Clear($bytes, 0, $bytes.Length) }
            if ($pointer -ne [IntPtr]::Zero) { [TheBasics.WinCredSession.NativeMethods]::CredFree($pointer) }
        }
    }
} catch {
    $failed = $true
}

if ($failed) {
    [Console]::Error.WriteLine("wincred $($Operation.ToLowerInvariant()) failed")
    exit 1
}
exit 0
