#!/usr/bin/env bash
# PreToolUse hook: in the main checkout, agents may edit files but must not
# commit, push or open/merge pull requests. Worktree sessions are allowed.
# Only Claude Code runs this; git on the command line is unaffected.

input=$(cat)
tool=$(jq -r '.tool_name // ""' <<<"$input")
dir=${CLAUDE_PROJECT_DIR:-$(jq -r '.cwd // ""' <<<"$input")}

paths=$(git -C "$dir" rev-parse --path-format=absolute --git-dir --git-common-dir 2>/dev/null) || exit 0
git_dir=$(sed -n 1p <<<"$paths")
common_dir=$(sed -n 2p <<<"$paths")
[ "$git_dir" = "$common_dir" ] || exit 0   # worktree: allowed

deny() {
  jq -n --arg r "$1" '{hookSpecificOutput: {hookEventName: "PreToolUse",
    permissionDecision: "deny", permissionDecisionReason: $r}}'
  exit 0
}

reason="This session runs in the main checkout, where agents edit files only: no commits, pushes or pull requests (see Working mode in AI.md). Leave the changes for the user to commit, or do the work in a worktree thread."

case "$tool" in
  mcp__t3-code__pull_request_create|mcp__t3-code__pull_request_merge) deny "$reason" ;;
  Bash)
    cmd=$(jq -r '.tool_input.command // ""' <<<"$input")
    if grep -Eq '(^|[^[:alnum:]_-])git([[:space:]]+[^;&|[:space:]]+)*[[:space:]]+(commit|push)([[:space:]]|$|[;&|)])' <<<"$cmd" \
      || grep -Eq '(^|[^[:alnum:]_-])gh[[:space:]]+pr[[:space:]]+(create|merge)' <<<"$cmd"; then
      deny "$reason"
    fi
    ;;
esac
exit 0
