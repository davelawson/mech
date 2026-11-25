#!/usr/bin/env bash
# PreToolUse hook enforcing the working modes in AI.md.
# Main checkout: agents may edit files but must not commit, push or open/merge
# pull requests. Worktree: full git control, including merging pull requests,
# except committing on or pushing to main/master.
# Only Claude Code runs this; git on the command line is unaffected.

input=$(cat)
tool=$(jq -r '.tool_name // ""' <<<"$input")
cmd=$(jq -r '.tool_input.command // ""' <<<"$input")
cwd=$(jq -r '.cwd // ""' <<<"$input")
dir=${cwd:-$CLAUDE_PROJECT_DIR}

# Judge a command by the directory its git runs in: the last `cd <dir>` or
# `git -C <dir>` in it, else the session's current directory. A session that
# moved into a worktree keeps CLAUDE_PROJECT_DIR pointing at the main checkout,
# so that is only a fallback.
if [ -n "$cmd" ]; then
  target=$(grep -oE "(^|[;&|(]|[[:space:]])(cd|git[[:space:]]+-C)[[:space:]]+(\"[^\"]+\"|'[^']+'|[^;&|[:space:]]+)" <<<"$cmd" \
    | tail -1 | sed -E "s/^.*(cd|-C)[[:space:]]+//; s/^[\"']//; s/[\"']\$//")
  if [ -n "$target" ]; then
    case $target in
      "~"*) target=$HOME${target#"~"} ;;
      /*) ;;
      *) target=$dir/$target ;;
    esac
    [ -d "$target" ] && dir=$target
  fi
fi

paths=$(git -C "$dir" rev-parse --path-format=absolute --git-dir --git-common-dir 2>/dev/null) || exit 0
git_dir=$(sed -n 1p <<<"$paths")
common_dir=$(sed -n 2p <<<"$paths")

deny() {
  jq -n --arg r "$1" '{hookSpecificOutput: {hookEventName: "PreToolUse",
    permissionDecision: "deny", permissionDecisionReason: $r}}'
  exit 0
}

# A git invocation running the given subcommand (options such as -C may come first).
git_sub() {
  grep -Eq "(^|[^[:alnum:]_-])git([[:space:]]+[^;&|[:space:]]+)*[[:space:]]+($1)([[:space:]]|\$|[;&|)])" <<<"$cmd"
}
gh_pr() {
  grep -Eq "(^|[^[:alnum:]_-])gh[[:space:]]+pr[[:space:]]+($1)" <<<"$cmd"
}

if [ "$git_dir" = "$common_dir" ]; then
  reason="This session runs in the main checkout, where agents edit files only: no commits, pushes or pull requests (see Working mode in AI.md). Leave the changes for the user to commit, or do the work in a worktree thread."
  case "$tool" in
    mcp__t3-code__pull_request_create|mcp__t3-code__pull_request_merge) deny "$reason" ;;
    Bash) { git_sub 'commit|push' || gh_pr 'create|merge'; } && deny "$reason" ;;
  esac
  exit 0
fi

# Worktree. Merging pull requests is allowed; AI.md says when.
main_reason="Agents never commit on or push to main or master (see Working mode in AI.md). Work on another branch and open a pull request."
case "$tool" in
  Bash)
    branch=$(git -C "$dir" branch --show-current 2>/dev/null)
    on_main=false; [[ $branch == main || $branch == master ]] && on_main=true
    $on_main && git_sub 'commit' && deny "$main_reason"
    if git_sub 'push'; then
      $on_main && deny "$main_reason"
      push=$(grep -oE 'git([[:space:]]+[^;&|[:space:]]+)*[[:space:]]+push[^;&|]*' <<<"$cmd")
      grep -Eq '(^|[[:space:]:+])(refs/heads/)?(main|master)([[:space:]]|$)|[[:space:]]--(all|mirror)([[:space:]]|$)' <<<"$push" \
        && deny "$main_reason"
    fi
    ;;
esac
exit 0
