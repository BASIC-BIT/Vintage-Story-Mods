[CmdletBinding()]
param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Reflection.Metadata
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
try {
    $entries = @($zip.Entries | Where-Object FullName -eq 'thebasics.dll')
    if ($entries.Count -ne 1) { throw 'Package must contain exactly one root thebasics.dll.' }
    $stream = [IO.MemoryStream]::new()
    $inputStream = $entries[0].Open()
    try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() }
    $stream.Position = 0
    $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $stamps = @()
        foreach ($handle in $metadata.GetAssemblyDefinition().GetCustomAttributes()) {
            $attribute = $metadata.GetCustomAttribute($handle)
            if ($attribute.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
            $constructor = $metadata.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
            if ($constructor.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
            $type = $metadata.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$constructor.Parent)
            if ($metadata.GetString($type.Namespace) -ne 'System.Reflection' -or $metadata.GetString($type.Name) -ne 'AssemblyMetadataAttribute') { continue }
            $blob = $metadata.GetBlobReader($attribute.Value)
            if ($blob.ReadUInt16() -ne 1) { throw 'Invalid assembly metadata attribute.' }
            $key = $blob.ReadSerializedString()
            $value = $blob.ReadSerializedString()
            if ($key -eq 'GuiCaptureSourceTreeHash') { $stamps += $value }
        }
        if ($stamps.Count -ne 1 -or $stamps[0] -notmatch '^[0-9a-f]{64}$') { throw 'Compiled GUI source stamp is missing, duplicated, or invalid.' }
        $stamps[0]
    } finally { $pe.Dispose(); $stream.Dispose() }
} finally { $zip.Dispose() }
