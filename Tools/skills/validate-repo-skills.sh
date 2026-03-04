#!/bin/sh

set -eu

root_dir="$(CDPATH= cd -- "$(dirname "$0")" && pwd)"
status=0

for dir in "$root_dir"/*; do
  [ -d "$dir" ] || continue
  [ -f "$dir/SKILL.md" ] || continue

  skill_name="$(basename "$dir")"
  echo "== validating $skill_name =="

  if ! grep -q '^---$' "$dir/SKILL.md"; then
    echo "  missing YAML frontmatter markers in SKILL.md"
    status=1
  fi

  if ! grep -q "^name: $skill_name$" "$dir/SKILL.md"; then
    echo "  frontmatter name mismatch in SKILL.md"
    status=1
  fi

  if ! grep -q '^description: ' "$dir/SKILL.md"; then
    echo "  missing description in SKILL.md"
    status=1
  fi

  if [ ! -f "$dir/agents/openai.yaml" ]; then
    echo "  missing agents/openai.yaml"
    status=1
  elif ! grep -Fq "\$$skill_name" "$dir/agents/openai.yaml"; then
    echo "  default_prompt does not reference \$$skill_name"
    status=1
  fi

  ref_count="$(find "$dir/references" -maxdepth 1 -type f 2>/dev/null | wc -l | tr -d ' ')"
  if [ "$ref_count" -lt 1 ]; then
    echo "  missing reference documents"
    status=1
  fi
done

if [ "$status" -eq 0 ]; then
  echo "repo-local skill pack validation passed"
fi

exit "$status"
