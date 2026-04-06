#!/usr/bin/env python
"""Convert D2K mainmenu.yaml to flex layout."""

with open('mods/d2k/chrome/mainmenu.yaml', 'r') as f:
    content = f.read()

t7 = '\t' * 7
t6 = '\t' * 6
t5 = '\t' * 5

def replace_button(content, btn_name, y_val, text, extra_before='', extra_after=''):
    """Replace a button block removing X/Y and adding Positioning: Flex."""
    old = (t6 + 'Button@' + btn_name + ':\n'
           + (t7 + extra_before + '\n' if extra_before else '')
           + t7 + 'X: PARENT_WIDTH / 2 - WIDTH / 2\n'
           + (t7 + extra_after + '\n' if extra_after else '')
           + t7 + 'Y: ' + y_val + '\n'
           + t7 + 'Width: 140\n'
           + t7 + 'Height: 30\n'
           + t7 + 'Text: ' + text + '\n'
           + t7 + 'Font: Bold')
    new = (t6 + 'Button@' + btn_name + ':\n'
           + (t7 + extra_before + '\n' if extra_before else '')
           + (t7 + extra_after + '\n' if extra_after else '')
           + t7 + 'Width: 140\n'
           + t7 + 'Height: 30\n'
           + t7 + 'Text: ' + text + '\n'
           + t7 + 'Font: Bold\n'
           + t7 + 'Positioning: Flex')
    if old in content:
        content = content.replace(old, new, 1)
        print(f'  OK: {btn_name}')
    else:
        print(f'  FAIL: {btn_name}')
    return content

def add_flex_props(content, label_name, menu_name):
    """Add flex properties before Children: line of a menu."""
    old = (t5 + 'Children:\n'
           + t6 + 'Label@' + label_name + ':')
    new = (t5 + 'FlexDirection: Column\n'
           + t5 + 'AlignItems: Center\n'
           + t5 + 'Gap: 10\n'
           + t5 + 'Padding: 60, 0, 0, 0\n'
           + old)
    # Find the right occurrence by checking context
    idx = 0
    while True:
        pos = content.find(old, idx)
        if pos < 0:
            print(f'  FAIL: {menu_name} flex props not found')
            break
        ctx = content[max(0, pos-200):pos]
        if menu_name in ctx:
            content = content[:pos] + new[:-len(old)] + content[pos:]
            print(f'  OK: {menu_name} flex props')
            break
        idx = pos + 1
    return content

# === MAIN_MENU ===
print('MAIN_MENU:')
content = add_flex_props(content, 'MAINMENU_LABEL_TITLE', 'MAIN_MENU')

for btn, y, txt in [
    ('SINGLEPLAYER_BUTTON', '60', 'label-singleplayer-title'),
    ('MULTIPLAYER_BUTTON', '100', 'label-multiplayer-title'),
    ('SETTINGS_BUTTON', '140', 'button-settings-title'),
    ('EXTRAS_BUTTON', '180', 'button-extras-title'),
    ('CONTENT_BUTTON', '220', 'button-main-menu-content'),
    ('QUIT_BUTTON', '260', 'button-quit'),
]:
    content = replace_button(content, btn, y, txt)

# === SINGLEPLAYER_MENU ===
print('SINGLEPLAYER_MENU:')
content = add_flex_props(content, 'SINGLEPLAYER_MENU_TITLE', 'SINGLEPLAYER_MENU')

for btn, y, txt in [
    ('SKIRMISH_BUTTON', '60', 'button-singleplayer-menu-skirmish'),
    ('MISSIONS_BUTTON', '100', 'label-missions-title'),
    ('LOAD_BUTTON', '140', 'button-singleplayer-menu-load'),
    ('ENCYCLOPEDIA_BUTTON', '180', 'label-mentat-title'),
]:
    content = replace_button(content, btn, y, txt)

# === EXTRAS_MENU ===
print('EXTRAS_MENU:')
content = add_flex_props(content, 'EXTRAS_MENU_TITLE', 'EXTRAS_MENU')

for btn, y, txt in [
    ('REPLAYS_BUTTON', '60', 'button-extras-menu-replays'),
    ('MUSIC_BUTTON', '100', 'label-music-title'),
    ('MAP_EDITOR_BUTTON', '140', 'label-map-editor-title'),
    ('ASSETBROWSER_BUTTON', '180', 'button-extras-menu-assetbrowser'),
    ('CREDITS_BUTTON', '220', 'label-credits-title'),
]:
    content = replace_button(content, btn, y, txt)

# EXTRAS BACK button (Key: escape is between X and Y lines)
old_back = (t6 + 'Button@BACK_BUTTON:\n'
            + t7 + 'X: PARENT_WIDTH / 2 - WIDTH / 2\n'
            + t7 + 'Key: escape\n'
            + t7 + 'Y: 260\n'
            + t7 + 'Width: 140\n'
            + t7 + 'Height: 30\n'
            + t7 + 'Text: button-back\n'
            + t7 + 'Font: Bold')
new_back = (t6 + 'Button@BACK_BUTTON:\n'
            + t7 + 'Key: escape\n'
            + t7 + 'Width: 140\n'
            + t7 + 'Height: 30\n'
            + t7 + 'Text: button-back\n'
            + t7 + 'Font: Bold\n'
            + t7 + 'Positioning: Flex')

# Find the EXTRAS one (after CREDITS_BUTTON)
pos = content.find(old_back)
if pos >= 0:
    ctx = content[max(0, pos-300):pos]
    if 'CREDITS_BUTTON' in ctx:
        content = content[:pos] + new_back + content[pos + len(old_back):]
        print('  OK: EXTRAS BACK_BUTTON')
    else:
        print('  SKIP: first BACK not EXTRAS')
else:
    print('  FAIL: EXTRAS BACK_BUTTON')

# === MAP_EDITOR_MENU ===
print('MAP_EDITOR_MENU:')
content = add_flex_props(content, 'MAP_EDITOR_MENU_TITLE', 'MAP_EDITOR_MENU')

for btn, y, txt in [
    ('NEW_MAP_BUTTON', '60', 'button-map-editor-new-map'),
    ('LOAD_MAP_BUTTON', '100', 'button-map-editor-load-map'),
]:
    content = replace_button(content, btn, y, txt)

with open('mods/d2k/chrome/mainmenu.yaml', 'w', newline='\n') as f:
    f.write(content)

print('\nDone!')
