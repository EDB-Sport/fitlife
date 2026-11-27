#!/usr/bin/env bash
# Which lines of a C# file hold code. Source it; tdd-check.sh (#361, #484) and mutation.sh (#382) share it.

# The lines of a C# file (stdin) that hold code: every line except blank ones and ones made only of comments.
# Tracks /* */ across lines and skips strings and char literals, so "http://x" is code and "/* x */ y;" is too.
code_lines() {
  awk '
    { code = 0; n = length($0); q = ""
      for (i = 1; i <= n; i++) {
        c = substr($0, i, 1); d = substr($0, i + 1, 1)
        if (blk) { if (c == "*" && d == "/") { blk = 0; i++ } }
        else if (q != "") { if (c == "\\") i++; else if (c == q) q = "" }
        else if (c == "/" && d == "/") break
        else if (c == "/" && d == "*") { blk = 1; i++ }
        else if (c == "\"" || c == "\047") { q = c; code = 1 }
        else if (c !~ /[ \t\r]/) code = 1
      }
      if (code) print }'
}

# Does the file at a commit contain a raw string (""") or a verbatim string (@", $@" or @$")? has_multiline_string <commit> <file>
has_multiline_string() { git show "$1:$2" 2>/dev/null | grep -qF -e '"""' -e '@"' -e '@$"'; }
