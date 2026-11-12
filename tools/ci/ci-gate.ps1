<#
.SYNOPSIS
    Decides what each platform job of ci.yml does, and computes the native payload's cache key.

.DESCRIPTION
    Outputs (to $GITHUB_OUTPUT on a runner, to the console otherwise):

      key                  hash of every input of the native payload: git object ids, which are
                           the same on every OS whatever the checkout's line endings
      win, linux, osx      full | skip
                             full  native payload (from the cache when the key hits), managed build, tests
                             skip  the job does not run (counts as passed for the required checks)

    ci.yml runs by itself only for pushes to master (and the weekly schedule); on any other branch it
    runs when started by hand (workflow_dispatch), and its checks on the branch's head commit are
    the ones a pull request's required checks read (owner, 2026-11-06).

    workflow_dispatch: every job is full, macOS only with the macos input. A run on a branch other
    than master whose changes against master are all in the list below (what cannot change a result:
    Markdown, docs/, the other workflows, tools/consume, the generator, the coverage tool; the same
    list as ci.yml's push paths-ignore) skips every platform job, which counts as passed.

    schedule: macOS alone, on master (Windows and Linux run on every change).

    push to master: when the pushed commit is the merge of a pull request whose head has the same
    tree and passed ci in a run of that head (a workflow_dispatch run on the branch), that tree has
    been tested and every platform skips. The ruleset requires a branch to be up to date with master
    before it merges, so a merged tree is the tested one. Master then builds no payload for its cache
    either (owner, 2026-11-06): a cache made on a branch is visible to that branch only, so the next
    branch run after a payload change builds the payload in its own run, which is cheaper than a
    separate warming job on master. Anything else on master (a direct push, an API error) is full,
    which caches the payload there. macOS skips on master pushes.

    Run locally against the repository's history:
      pwsh tools/ci/ci-gate.ps1 -EventName push -Sha <merge commit> -Repository owner/name -Token <token>
#>
param(
    [string]$EventName = $env:GITHUB_EVENT_NAME,
    [string]$Sha = $env:GITHUB_SHA,
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$Token = $env:GH_TOKEN,
    [string]$MacOSInput = $env:GATE_MACOS,        # workflow_dispatch input "macos"
    [string]$Ref = $env:GATE_REF                  # github.ref
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
    $reason = "push to master: no branch run covers this tree, every job runs."
    try {
        $tree = (Get-Api "git/commits/$Sha").tree.sha
        $tested = $null
        foreach ($pr in @(Get-Api "commits/$Sha/pulls") | Where-Object { $_.merged_at }) {
            $head = $pr.head.sha
            if ((Get-Api "git/commits/$head").tree.sha -ne $tree) { continue }
            $runs = Get-Api "actions/workflows/ci.yml/runs?head_sha=$head&status=success&per_page=20"
            $run = @($runs.workflow_runs | Where-Object { $_.event -in 'workflow_dispatch', 'pull_request' }) | Select-Object -First 1
            if ($run) {
                $tested = "pull request #$($pr.number): ci run $($run.id) ($($run.event)) passed on $($head.Substring(0, 7)), whose tree is this commit's"
                break
            }
        }
        if ($tested) {
            foreach ($p in $platforms.Keys) { $modes[$p] = 'skip' }
            $reason = "push to master, tested as $tested. Nothing is repeated; the next branch run whose payload key master has not cached builds the payload in its own run."
        }
    }
    catch {
        Write-Warning "could not establish that this tree was tested ($($_.Exception.Message)); every job runs"
        foreach ($p in $platforms.Keys) { $modes[$p] = 'full' }
        $reason = "push to master: the GitHub API did not answer, every job runs."
    }
}

# ------------------------------------------------------------------------------ macOS, opt-in
switch ($EventName) {
    'workflow_dispatch' {
        if ($MacOSInput -ne 'true') {
            $modes.osx = 'skip'
            $reason = "$EventName event: Windows and Linux run; macOS only with the macos input."
        }
    }
    'schedule' {
        $modes.win = 'skip'; $modes.linux = 'skip'; $modes.osx = 'full'
        $reason = "weekly schedule: macOS runs on master; Windows and Linux run on every change."
    }
    'push' {
        $modes.osx = 'skip'
        $reason += " macOS is opt-in and skips on master pushes."
    }
}

# ------------------------------------------------------------------------------ docs-only branches
# Paths that cannot change a result; ci.yml's push trigger ignores the same list (checked below).
# Besides docs and the other workflows: every tool no ci.yml job runs, i.e. the local ones
# (check.ps1, check-docs.ps1, upscalers.ps1, compile-test-shaders.ps1, whose output is committed),
# the release's (release.yml: api-changes, check-packages, release-body, upscaler-runtimes, apidiff),
# the generator (regen.yml) and the coverage tool (coverage.yml).
$noResult = '*.md', 'docs/*', '.idea/*', '.github/workflows/release.yml', '.github/workflows/regen.yml',
            '.github/workflows/coverage.yml', '.github/workflows/checks.yml', 'tools/consume/*', 'tools/generate-native.ps1',
            'tools/generate-native-docs.ps1', 'tools/coverage.ps1', 'tools/coverage/*', 'tools/check.ps1', 'tools/check-docs.ps1',
            'tools/upscalers.ps1', 'tools/compile-test-shaders.ps1', 'tools/apidiff/*', 'tools/ci/api-changes.ps1',
            'tools/ci/check-packages.ps1', 'tools/ci/release-body.ps1', 'tools/ci/upscaler-runtimes.ps1', 'tools/ci/verify-linux.sh'
# The tools ci.yml's jobs do run (directly, or build-native.ps1's CMake include): a change runs the jobs
$ciTools = 'tools/build-native.ps1', 'tools/cmake/*', 'tools/ci/ci-gate.ps1', 'tools/ci/apply-patches.ps1',
           'tools/ci/descs-version.ps1', 'tools/ci/fetch-vulkan-layer.sh', 'tools/ci/fetch-vulkan-windows.ps1'

# Keeping the two lists true: a tool in neither runs every job (the safe side) and is named here, and
# ci.yml's paths-ignore must be this list
$notes = @()
$unclassified = @(git ls-tree -r --name-only $Sha -- tools | Where-Object { $f = $_; -not (@($noResult) + @($ciTools) | Where-Object { $f -like $_ }) })
if ($unclassified) { $notes += "tools in neither ci-gate.ps1 list (treated as changing a result; add each to `$noResult or `$ciTools): $($unclassified -join ', ')" }
$ciYml = git show "${Sha}:.github/workflows/ci.yml"
$ignored = @(($ciYml -join "`n") -split "`n" | Select-String -Pattern '^\s+-\s+"([^"]+)"\s*$' | ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\*\*', '*' })
if (Compare-Object ($ignored | Sort-Object -Unique) ($noResult | Sort-Object -Unique)) {
    $notes += "ci.yml's push paths-ignore differs from ci-gate.ps1's `$noResult: $((Compare-Object ($ignored | Sort-Object -Unique) ($noResult | Sort-Object -Unique) | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join '; ')"
}
foreach ($n in $notes) { Write-Warning $n; if ($env:GITHUB_ACTIONS) { Write-Host "::warning title=ci gate::$n" } }

if ($EventName -eq 'workflow_dispatch' -and $Ref -and $Ref -ne 'refs/heads/master') {
    try {
        $compare = Get-Api "compare/master...$Sha"
        $files = @($compare.files | ForEach-Object { $_.filename })
        $result = @($files | Where-Object { $f = $_; -not ($noResult | Where-Object { $f -like $_ }) })
        if ($files.Count -gt 0 -and $files.Count -lt 300 -and $result.Count -eq 0) {
            foreach ($p in $platforms.Keys) { $modes[$p] = 'skip' }
            $reason = "$Ref changes only docs and other paths that cannot change a result against master ($($files.Count) files): every platform job skips."
        }
    }
    catch {
        Write-Warning "could not compare the branch with master ($($_.Exception.Message)); the jobs run"
    }
}

# ------------------------------------------------------------------------------ outputs
$lines = @("key=$key") + @($modes.Keys | ForEach-Object { "$_=$($modes[$_])" })
$summary = @("### ci gate", "", $reason, "") + @($notes | ForEach-Object { "> $_" }) + @("", "| Job | Mode | Payload cache key |", "|---|---|---|") +
    @($platforms.Keys | ForEach-Object { "| $($platforms[$_]) | $($modes[$_]) | native-$($platforms[$_])-$key |" }) +
    @("", "Key inputs:", "", '```') + $ids + '```'
if ($env:GITHUB_OUTPUT) { $lines | Add-Content -Path $env:GITHUB_OUTPUT -Encoding utf8 } else { $lines | Write-Output }
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8 } else { $reason | Write-Host }
