<#
.SYNOPSIS
    Generates a PBKDF2 hash for PodBridge's hashed Basic Auth configuration.
.DESCRIPTION
    PodBridge never stores a plaintext Basic Auth username/password (see
    PodBridge.Logic.Security.CredentialHasher) - only a PBKDF2 (HMAC-SHA256) hash is configured via
    Auth.UsernameHash / Auth.PasswordHash. Run this script once per value during initial setup or whenever
    rotating credentials, so the plaintext value never needs to be pasted into a deployment platform's
    dashboard or a CI secret store - only the resulting hash is.
.PARAMETER Value
    The plaintext username or password to hash. Run the script once per value (it must be called separately
    for the username and the password).
.EXAMPLE
    ./New-CredentialHash.ps1 -Value 'myuser'
    Prints a hash to paste into Auth.UsernameHash / PodBridge__Auth__UsernameHash.
.EXAMPLE
    ./New-CredentialHash.ps1 -Value 'mypassword'
    Prints a hash to paste into Auth.PasswordHash / PodBridge__Auth__PasswordHash.
.OUTPUTS
    System.String. The formatted hash "{iterations}.{saltBase64}.{hashBase64}", matching the format
    produced and verified by PodBridge.Logic.Security.CredentialHasher.
.NOTES
    The iteration count, salt size, hash size and algorithm below must stay in sync with
    PodBridge.Logic.Security.CredentialHasher - a mismatch would make hashes generated here unverifiable
    by the running application. Each call uses a fresh random salt, so hashing the same value twice
    produces two different (but equally valid) hashes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Value
)

$ErrorActionPreference = 'Stop'

# Must match PodBridge.Logic.Security.CredentialHasher exactly.
$iterations = 210000
$saltSizeInBytes = 16
$hashSizeInBytes = 32

$saltBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes($saltSizeInBytes)
$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
$hashBytes = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
    $valueBytes,
    $saltBytes,
    $iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    $hashSizeInBytes)

$credentialHash = '{0}.{1}.{2}' -f $iterations, [System.Convert]::ToBase64String($saltBytes), [System.Convert]::ToBase64String($hashBytes)

Write-Output $credentialHash
