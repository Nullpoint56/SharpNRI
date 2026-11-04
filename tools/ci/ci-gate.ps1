<#
.SYNOPSIS
    Decides what each platform job of ci.yml does, and computes the native payload's cache key.

.DESCRIPTION
    Outputs (to $GITHUB_OUTPUT on a runner, to the console otherwise):

      key                  hash of every input of the native payload: git object ids, which are
                           the same on every OS whatever the checkout's line endings
      win, linux, osx      full | warm | skip
                             full  native payload (from the cache when the key hits), managed build, tests
                             warm  native payload built and saved to the cache, nothing else
                             skip  the job does not run

    pull_request and workflow_dispatch: every job is full, except macOS (below).

    macOS is opt-in: hosted macOS arm64 runners are often unavailable for long stretches, and a
    macOS minute costs about ten Linux minutes. osx is full for a pull request labelled "macos",
    for the "macos" label being added (then win and linux skip: that head ran already), for a
    manual run with the macos input, and for the weekly schedule (then win and linux skip: every
    pull request runs them). Pushes to master skip it. The release builds and tests macOS itself.

    push to master: when the pushed commit is the merge of a pull request whose head has the
    same tree and passed ci as a pull_request run, that tree has been tested; running the suite
    again finds nothing and costs minutes. Each platform is then warm when master has no payload
    cache for the key, else skip. Pull requests restore caches made on master; a cache made in a
    pull request is visible to that pull request only, so master is where a payload must be
    cached for the next pull request to find it. Anything else on master (a direct push, a merge
    of a branch that was behind master, an API error) is full.

    Run locally against the repository's history:
      pwsh tools/ci/ci-gate.ps1 -EventName push -Sha <merge commit> -Repository owner/name -Token <token>
#>
param(
    [string]$EventName = $env:GITHUB_EVENT_NAME,
    [string]$Sha = $env:GITHUB_SHA,
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$Token = $env:GH_TOKEN,
    [string]$Action = $env:GATE_ACTION,           # github.event.action
    [string]$Label = $env:GATE_LABEL,             # github.event.label.name, for "labeled"
    [string]$PullRequestLabels = $env:GATE_PR_LABELS, # JSON array of the pull request's label names
    [string]$MacOSInput = $env:GATE_MACOS         # workflow_dispatch input "macos"
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------------------ native payload key
# Everything tools/build-native.ps1 builds or stages from: the NRI commit, the patches, the script,
# its CMake include, the NRI pin, the notices it copies into licenses/, and ci.yml (packages
# installed before the build, the build invocation). What NRI fetches is pinned by its commit.
$inputs = 'nri', 'patches', 'tools/build-native.ps1', 'tools/cmake', 'Directory.Build.props', 'THIRD-PARTY-NOTICES.md',
          '.github/workflows/ci.yml'
$ids = foreach ($path in $inputs) {
    $id = git rev-parse --verify --quiet "${Sha}:$path"
    if ($LASTEXITCODE -ne 0 -or -not $id) { throw "native payload input '$path' not found in $Sha" }
    "$path $id"
}
$key = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData(
    [System.Text.Encoding]::UTF8.GetBytes($ids -join "`n"))).Substring(0, 20).ToLowerInvariant()

# ------------------------------------------------------------------------------ what each job does
$platforms = [ordered]@{ win = 'win-x64'; linux = 'linux-x64'; osx = 'osx-arm64' }
$modes = [ordered]@{}
foreach ($p in $platforms.Keys) { $modes[$p] = 'full' }
$reason = "$EventName event: every job runs."

function Get-Api([string]$Path) {
    Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/$Path" -Headers @{
        Authorization          = "Bearer $Token"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
}

if ($EventName -eq 'push') {
    $reason = "push to master: no pull request run covers this tree, every job runs."
    try {
        $tree = (Get-Api "git/commits/$Sha").tree.sha
        $tested = $null
        foreach ($pr in @(Get-Api "commits/$Sha/pulls") | Where-Object { $_.merged_at }) {
            $head = $pr.head.sha
            if ((Get-Api "git/commits/$head").tree.sha -ne $tree) { continue }
            $runs = Get-Api "actions/workflows/ci.yml/runs?head_sha=$head&event=pull_request&status=success&per_page=1"
            if ($runs.total_count -gt 0) {
                $tested = "pull request #$($pr.number): ci run $($runs.workflow_runs[0].id) passed on $($head.Substring(0, 7)), whose tree is this commit's"
                break
            }
        }
        if ($tested) {
            foreach ($p in $platforms.Keys) {
                $caches = Get-Api "actions/caches?ref=refs/heads/master&key=native-$($platforms[$p])-$key"
                $modes[$p] = if ($caches.total_count -gt 0) { 'skip' } else { 'warm' }
            }
            $reason = "push to master, tested as $tested. Tests are not repeated; a platform without a payload cache on master builds one."
        }
    }
    catch {
        Write-Warning "could not establish that this tree was tested ($($_.Exception.Message)); every job runs"
        foreach ($p in $platforms.Keys) { $modes[$p] = 'full' }
        $reason = "push to master: the GitHub API did not answer, every job runs."
    }
}

# ------------------------------------------------------------------------------ macOS, opt-in
$labels = @()
if ($PullRequestLabels) { try { $labels = @($PullRequestLabels | ConvertFrom-Json) } catch { $labels = @() } }
switch ($EventName) {
    'pull_request' {
        if ($Action -eq 'labeled') {
            $modes.win = 'skip'; $modes.linux = 'skip'; $modes.osx = 'full'
            $reason = "the macos label was added: macOS runs; Windows and Linux ran on this head already."
        }
        elseif ($labels -contains 'macos') {
            $reason += " The pull request is labelled macos: macOS runs too."
        }
        else {
            $modes.osx = 'skip'
            $reason = "$EventName event: Windows and Linux run; macOS is opt-in (label the pull request macos)."
        }
    }
    'workflow_dispatch' {
        if ($MacOSInput -ne 'true') {
            $modes.osx = 'skip'
            $reason = "$EventName event: Windows and Linux run; macOS only with the macos input."
        }
    }
    'schedule' {
        $modes.win = 'skip'; $modes.linux = 'skip'; $modes.osx = 'full'
        $reason = "weekly schedule: macOS runs on master; Windows and Linux run on every pull request."
    }
    'push' {
        $modes.osx = 'skip'
        $reason += " macOS is opt-in and skips on master pushes."
    }
}

# ------------------------------------------------------------------------------ outputs
$lines = @("key=$key") + @($modes.Keys | ForEach-Object { "$_=$($modes[$_])" })
$summary = @("### ci gate", "", $reason, "", "| Job | Mode | Payload cache key |", "|---|---|---|") +
    @($platforms.Keys | ForEach-Object { "| $($platforms[$_]) | $($modes[$_]) | native-$($platforms[$_])-$key |" }) +
    @("", "Key inputs:", "", '```') + $ids + '```'
if ($env:GITHUB_OUTPUT) { $lines | Add-Content -Path $env:GITHUB_OUTPUT -Encoding utf8 } else { $lines | Write-Output }
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8 } else { $reason | Write-Host }
