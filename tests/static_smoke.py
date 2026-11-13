from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
required = [
    'YandexMusicController.csproj', 'App.xaml', 'App.xaml.cs',
    'MainWindow.xaml', 'MainWindow.xaml.cs',
    'Services/MediaSessionService.cs',
    'Services/HotkeyService.cs', 'Services/HistoryService.cs',
    'Services/StartupService.cs', 'Services/TrayService.cs',
    'start.bat', 'start-hidden.vbs', 'build.ps1', 'README.md'
]
for name in required:
    assert (ROOT / name).is_file(), f'Missing required file: {name}'

for name in ['YandexMusicController.csproj', 'App.xaml', 'MainWindow.xaml', 'app.manifest']:
    ET.parse(ROOT / name)

for name in ['MainWindow']:
    xaml = (ROOT / f'{name}.xaml').read_text(encoding='utf-8')
    code = (ROOT / f'{name}.xaml.cs').read_text(encoding='utf-8')
    handlers = set(re.findall(r'\b(?:Click|Checked|Unchecked)="([A-Za-z_][A-Za-z0-9_]*)"', xaml))
    for handler in handlers:
        assert re.search(r'\b' + re.escape(handler) + r'\s*\(', code), f'{name}: missing handler {handler}'
    names = re.findall(r'x:Name="([^"]+)"', xaml)
    assert len(names) == len(set(names)), f'{name}: duplicate x:Name'

hotkeys = (ROOT / 'Services/HotkeyService.cs').read_text(encoding='utf-8')
for token in ['VkLeft', 'VkRight', 'VkSpace', 'RegisterHotKey', 'UnregisterHotKey']:
    assert token in hotkeys, f'Missing hotkey implementation: {token}'

history = (ROOT / 'Services/HistoryService.cs').read_text(encoding='utf-8')
for token in ['TouchTrackAsync', 'AddListeningTimeAsync', 'GetTracksAsync', 'UNIQUE(Title, Artist)']:
    assert token in history, f'Missing history feature: {token}'

media = (ROOT / 'Services/MediaSessionService.cs').read_text(encoding='utf-8')
assert 'Contains("yandex"' in media, 'Media session selection must explicitly target Yandex'
assert 'GetCurrentSession()' not in media, 'Must not fall back to another player/browser'

main = (ROOT / 'MainWindow.xaml').read_text(encoding='utf-8')
assert 'HistoryView' in main and 'PlayerView' in main, 'History must be embedded in main window'

for name in ['start.bat', 'start-hidden.vbs']:
    data = (ROOT / name).read_bytes()
    assert not data.startswith(b'\xef\xbb\xbf'), f'{name} has UTF-8 BOM'

print('PASS: required project files exist')
print('PASS: XAML, project and manifest XML parse')
print('PASS: XAML event handlers and names are consistent')
print('PASS: global hotkey registrations are present')
print('PASS: unique-track history with cumulative time is present')
print('PASS: media selection targets Yandex only')
print('PASS: history is embedded in the main window')
print('PASS: launch scripts are UTF-8 without BOM')
print('NOTE: This is a static smoke test; Windows compilation and live GSMTC integration require Windows/.NET 8.')
