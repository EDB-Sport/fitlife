#!/usr/bin/env bash
# Plan of the .NET jobs in ci.yml (#568): "skip" when every changed path is Markdown that neither the build nor a test
# reads, "run" otherwise. Reads the changed paths, one per line, on stdin.
# Fails open: no input, or only empty lines, prints "run", so a broken diff never skips the build.
# Markdown that runs the jobs, matched by file name in any folder:
#   GLOSSARY.md               the build passes it to the TEAM analyzers (Directory.Build.props, TEAM008)
#   AnalyzerReleases.*.md     AdditionalFiles of tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj
#   ACCESS.md                 docs/domain/ACCESS.md, read by a test (#489)
# tools/ci/dotnet-plan.test.sh fails when an AdditionalFiles entry or a string in tests/ names a .md not on this list.
# Usage: git diff --name-only "$BASE_SHA...$HEAD_SHA" | tools/ci/dotnet-plan.sh
set -uo pipefail

seen=0
while IFS= read -r path || [ -n "$path" ]; do
  [ -n "$path" ] || continue
  seen=1
  case ${path##*/} in
    GLOSSARY.md | AnalyzerReleases.*.md | ACCESS.md) echo run && exit 0 ;;
    *.md) ;;
    *) echo run && exit 0 ;;
  esac
done
if [ "$seen" = 1 ]; then echo skip; else echo run; fi
