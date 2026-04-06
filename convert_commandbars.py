#!/usr/bin/env python
"""Convert COMMAND_BAR and STANCE_BAR in ingame-player.yaml files to flex layout."""
import re

files_config = {
    'mods/d2k/chrome/ingame-player.yaml': {
        'command_bar': {'buttons': ['ATTACK_MOVE', 'FORCE_MOVE', 'FORCE_ATTACK', 'GUARD', 'DEPLOY', 'SCATTER', 'STOP', 'QUEUE_ORDERS'], 'gap': 1},  # stride 35, width 34
        'stance_bar': {'buttons': ['STANCE_ATTACKANYTHING', 'STANCE_DEFEND', 'STANCE_RETURNFIRE', 'STANCE_HOLDFIRE'], 'gap': 0},  # stride 29, width 29
    },
    'mods/ra/chrome/ingame-player.yaml': {
        'command_bar': {'buttons': ['ATTACK_MOVE', 'FORCE_MOVE', 'FORCE_ATTACK', 'GUARD', 'DEPLOY', 'SCATTER', 'STOP', 'QUEUE_ORDERS'], 'gap': 0},  # stride 34, width 34
        'stance_bar': {'buttons': ['STANCE_ATTACKANYTHING', 'STANCE_DEFEND', 'STANCE_RETURNFIRE', 'STANCE_HOLDFIRE'], 'gap': 0},  # stride 34, width 34
    },
    'mods/ts/chrome/ingame-player.yaml': {
        'command_bar': {'buttons': ['ATTACK_MOVE', 'FORCE_MOVE', 'FORCE_ATTACK', 'GUARD', 'DEPLOY', 'SCATTER', 'STOP', 'QUEUE_ORDERS'], 'gap': 0},  # stride 35, width 35
        'stance_bar': {'buttons': ['STANCE_ATTACKANYTHING', 'STANCE_DEFEND', 'STANCE_RETURNFIRE', 'STANCE_HOLDFIRE'], 'gap': 0},  # stride 28, width 28
    },
}

for filepath, config in files_config.items():
    print(f"\n=== {filepath} ===")
    with open(filepath, 'r') as f:
        lines = f.readlines()

    modified = False

    for bar_type, bar_config in config.items():
        bar_name = 'COMMAND_BAR' if bar_type == 'command_bar' else 'STANCE_BAR'
        gap = bar_config['gap']
        buttons = bar_config['buttons']

        # Find the container line
        container_idx = None
        children_idx = None
        indent_level = None

        for i, line in enumerate(lines):
            stripped = line.rstrip('\n')
            if f'Container@{bar_name}:' in stripped or f'@{bar_name}:' in stripped:
                container_idx = i
                indent_level = len(line) - len(line.lstrip('\t'))
                continue

            if container_idx is not None and children_idx is None:
                if stripped.strip() == 'Children:':
                    line_indent = len(line) - len(line.lstrip('\t'))
                    if line_indent == indent_level + 1:
                        children_idx = i
                        break

        if container_idx is None or children_idx is None:
            print(f"  SKIP: {bar_name} not found")
            continue

        # Insert FlexDirection and Gap before Children:
        child_indent = '\t' * (indent_level + 1)
        insert_lines = [child_indent + 'FlexDirection: Row\n']
        if gap > 0:
            insert_lines.append(child_indent + f'Gap: {gap}\n')

        lines[children_idx:children_idx] = insert_lines
        children_idx += len(insert_lines)
        print(f"  OK: {bar_name} flex props added")
        modified = True

        # Now find and modify each button
        button_indent = indent_level + 2  # two levels deeper than container
        for btn_name in buttons:
            for i, line in enumerate(lines):
                if f'Button@{btn_name}:' in line or f'@{btn_name}:' in line:
                    btn_line_indent = len(line) - len(line.lstrip('\t'))
                    if btn_line_indent != button_indent:
                        continue

                    prop_indent = '\t' * (button_indent + 1)

                    # Look for X: line at DIRECT property level (button_indent + 1)
                    # Only remove X: <number> at exactly button's property indent level
                    target_prop_indent = button_indent + 1
                    j = i + 1
                    while j < len(lines):
                        prop_line = lines[j].rstrip('\n')
                        prop_stripped = prop_line.strip()

                        if prop_stripped and not prop_stripped.startswith('#'):
                            prop_line_indent = len(lines[j]) - len(lines[j].lstrip('\t'))
                            if prop_line_indent <= button_indent:
                                break  # Next sibling or parent
                            # Only remove X: at direct property level, not deeper children
                            if prop_line_indent == target_prop_indent and re.match(r'\s+X:\s+\d+\s*$', lines[j]):
                                lines.pop(j)
                                continue

                        j += 1

                    # Find where to insert Positioning: Flex
                    # Insert after the last property at button_indent+1 level, before Children: or next button
                    j = i + 1
                    last_prop_idx = i
                    while j < len(lines):
                        prop_line = lines[j].rstrip('\n')
                        prop_stripped = prop_line.strip()

                        if prop_stripped:
                            prop_line_indent = len(lines[j]) - len(lines[j].lstrip('\t'))
                            if prop_line_indent == button_indent + 1:
                                if prop_stripped == 'Children:':
                                    break
                                last_prop_idx = j
                            elif prop_line_indent <= button_indent:
                                break

                        j += 1

                    # Insert Positioning: Flex after last direct property
                    lines.insert(last_prop_idx + 1, prop_indent + 'Positioning: Flex\n')
                    print(f"    OK: {btn_name}")
                    break

    if modified:
        with open(filepath, 'w', newline='\n') as f:
            f.writelines(lines)

print("\nDone!")
