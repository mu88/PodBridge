#Requires -Version 7.0
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

BeforeAll {
    $script:ScriptUnderTest = Join-Path $PSScriptRoot 'Convert-ConfigToSecretFiles.ps1'

    function Invoke-ConversionScript {
        <#
        .SYNOPSIS
            Runs Convert-ConfigToSecretFiles.ps1 against a config object and returns the parsed result.
        #>
        [CmdletBinding()]
        [OutputType([PSCustomObject])]
        param(
            [Parameter(Mandatory)]
            $Config
        )

        $outputPath = Join-Path $TestDrive ([System.Guid]::NewGuid().ToString() + '.json')
        $configJson = $Config | ConvertTo-Json -Depth 10
        & $script:ScriptUnderTest -ConfigJson $configJson -OutputPath $outputPath | Out-Null

        return Get-Content -Path $outputPath -Raw | ConvertFrom-Json
    }
}

Describe 'Convert-ConfigToSecretFiles' {
    Context 'Flattening nested objects and arrays' {
        It 'produces one entry per leaf value, joining path segments with "__" and prefixing "PodBridge__"' {
            $config = @{
                PodBridge = @{
                    RefreshIntervalMinutes = 60
                    Auth                    = @{
                        Enabled = $true
                    }
                }
            }

            $result = Invoke-ConversionScript -Config $config

            $result.items.Count | Should -Be 2
            ($result.items.path) | Should -Contain 'PodBridge__RefreshIntervalMinutes'
            ($result.items.path) | Should -Contain 'PodBridge__Auth__Enabled'
        }

        It 'indexes array elements numerically' {
            $config = @{
                PodBridge = @{
                    Podcasts = @(
                        @{ PodcastId = 'show-a' },
                        @{ PodcastId = 'show-b' }
                    )
                }
            }

            $result = Invoke-ConversionScript -Config $config

            ($result.items.path) | Should -Contain 'PodBridge__Podcasts__0__PodcastId'
            ($result.items.path) | Should -Contain 'PodBridge__Podcasts__1__PodcastId'
        }

        It 'skips null leaf values entirely' {
            $config = @{
                PodBridge = @{
                    Auth = @{
                        Username = $null
                    }
                }
            }

            $result = Invoke-ConversionScript -Config $config

            $result.items.Count | Should -Be 0
        }
    }

    Context 'Value conversion' {
        It 'lower-cases boolean values to match .NET configuration binding' {
            $config = @{ PodBridge = @{ Auth = @{ Enabled = $true } } }

            $result = Invoke-ConversionScript -Config $config

            $result.items[0].value | Should -Be 'true'
        }

        It 'converts numeric values to their string representation' {
            $config = @{ PodBridge = @{ RefreshIntervalMinutes = 60 } }

            $result = Invoke-ConversionScript -Config $config

            $result.items[0].value | Should -Be '60'
        }
    }

    Context 'Secret name sanitization' {
        It 'lower-cases the path and replaces non-alphanumeric characters with hyphens' {
            $config = @{ PodBridge = @{ Podcasts = @(@{ PodcastId = 'x' }) } }

            $result = Invoke-ConversionScript -Config $config

            $entry = $result.items | Where-Object { $_.path -eq 'PodBridge__Podcasts__0__PodcastId' }
            $entry.name | Should -Be 'podbridge-podcasts-0-podcastid'
        }

        It 'collapses consecutive hyphens and trims leading/trailing hyphens' {
            # "__" always maps to a run of underscores, which collapse to a single hyphen once sanitized.
            $config = @{ PodBridge = @{ Auth = @{ Enabled = $true } } }

            $result = Invoke-ConversionScript -Config $config

            $result.items[0].name | Should -Not -Match '--'
            $result.items[0].name | Should -Not -Match '^-|-$'
        }

        It 'truncates names longer than 63 characters and does not end on a hyphen' {
            $longKey = 'A' * 80
            $config = @{ PodBridge = @{ $longKey = 'value' } }

            $result = Invoke-ConversionScript -Config $config

            $result.items[0].name.Length | Should -BeLessOrEqual 63
            $result.items[0].name | Should -Not -Match '-$'
        }

        It 'prefixes with "cfg-" when the sanitized name would not start with a letter' {
            # Path segments are all-numeric or symbol-only after sanitization only in edge cases such as an
            # object key consisting solely of digits; simulate that directly via a top-level numeric key.
            $config = @{ '123' = 'value' }

            $result = Invoke-ConversionScript -Config $config

            $result.items[0].name | Should -Match '^cfg-'
        }
    }

    Context 'Empty configuration' {
        It 'returns an empty items array for an empty object' {
            $result = Invoke-ConversionScript -Config @{}

            $result.items | Should -BeNullOrEmpty
        }
    }
}
