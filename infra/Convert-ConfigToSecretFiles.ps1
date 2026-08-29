#Requires -Version 7.0

<#
.SYNOPSIS
    Flattens the PodBridge JSON configuration into Key-Per-File secret entries for the Bicep deployment.

.DESCRIPTION
    Recursively walks the PodBridge configuration JSON - rooted at a top-level "PodBridge" property matching
    the shape of the "PodBridge" section in appsettings.json (e.g. Auth, Podcasts, RefreshIntervalMinutes) -
    and produces one entry per leaf value. Each entry contains:
    - Name: a Container Apps secret name (lower-case alphanumeric/hyphen, starts with a letter, max 63 chars)
    - Path: the file name to mount under /run/secrets, using the .NET "__" hierarchy delimiter
      (e.g. "PodBridge__Podcasts__0__PodcastId"), consumed by the existing, unmodified
      AddKeyPerFile("/run/secrets", optional: true) call in Program.cs
    - Value: the leaf configuration value as a string

    The result is written as { "items": [ ... ] } so it can be passed directly to the Bicep template's
    secure "secretFilesConfig" object parameter (az deployment group create --parameters secretFilesConfig=@file).

.PARAMETER ConfigJson
    The raw configuration as a JSON string, with a top-level "PodBridge" property (the content of the merged
    PODBRIDGE_CONFIG_JSON GitHub variable plus Auth secrets).

.PARAMETER OutputPath
    File path to write the flattened { "items": [...] } JSON to.

.EXAMPLE
    ./Convert-ConfigToSecretFiles.ps1 -ConfigJson $env:PODBRIDGE_CONFIG_JSON -OutputPath secret-files.json

.OUTPUTS
    None. Writes the flattened configuration to -OutputPath.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConfigJson,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function ConvertTo-SanitizedSecretName {
    <#
    .SYNOPSIS
        Converts a "__"-delimited config file path into a valid Azure Container Apps secret name.

    .DESCRIPTION
        Container Apps secret names must be lower case alphanumeric characters or '-', start with a
        letter, end with an alphanumeric character, and be at most 63 characters long.

    .PARAMETER FilePath
        The Key-Per-File path, e.g. "PodBridge__Podcasts__0__PodcastId".

    .OUTPUTS
        System.String. The sanitized secret name.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    $sanitized = $FilePath.ToLowerInvariant() -replace '[^a-z0-9]', '-'
    $sanitized = $sanitized -replace '-+', '-'
    $sanitized = $sanitized.Trim('-')

    if ($sanitized.Length -gt 63) {
        $sanitized = $sanitized.Substring(0, 63).TrimEnd('-')
    }

    if ($sanitized -notmatch '^[a-z]') {
        $sanitized = "cfg-$sanitized"
        if ($sanitized.Length -gt 63) {
            $sanitized = $sanitized.Substring(0, 63).TrimEnd('-')
        }
    }

    return $sanitized
}

function ConvertTo-SecretFileEntry {
    <#
    .SYNOPSIS
        Recursively flattens a parsed configuration value into Key-Per-File secret entries.

    .PARAMETER Value
        The current configuration node (object, array, or scalar).

    .PARAMETER PathSegments
        The "__"-delimited path segments accumulated so far.

    .OUTPUTS
        System.Management.Automation.PSCustomObject. One object per leaf value, with Name/Path/Value.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $Value,

        [string[]]$PathSegments = @()
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            ConvertTo-SecretFileEntry -Value $property.Value -PathSegments ($PathSegments + $property.Name)
        }

        return
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $index = 0
        foreach ($item in $Value) {
            ConvertTo-SecretFileEntry -Value $item -PathSegments ($PathSegments + [string]$index)
            $index++
        }

        return
    }

    # No hardcoded prefix here: the input config JSON already has "PodBridge" as its top-level property, so
    # that segment flows in naturally via recursion and $PathSegments already starts with "PodBridge".
    $filePath = $PathSegments -join '__'
    $stringValue = if ($Value -is [bool]) { $Value.ToString().ToLowerInvariant() } else { [string]$Value }

    [PSCustomObject]@{
        name  = ConvertTo-SanitizedSecretName -FilePath $filePath
        path  = $filePath
        value = $stringValue
    }
}

$parsedConfig = ConvertFrom-Json -InputObject $ConfigJson
$entries = ConvertTo-SecretFileEntry -Value $parsedConfig -PathSegments @()

$result = [PSCustomObject]@{
    items = @($entries)
}

$result | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding utf8NoBOM

Write-Output "Wrote $(@($entries).Count) secret file entries to $OutputPath"
