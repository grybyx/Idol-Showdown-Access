#!/usr/bin/env python3
"""Baut das Release-ZIP und das Quellcode-ZIP.

Umgebungsvariablen:
  ISA_BEPINEX_DIR  entpacktes BepInEx_win_x64_5.4.23.2 (mit winhttp.dll, doorstop_config.ini, BepInEx/core)
  ISA_TOLK_DIR     entpacktes tolk.zip (mit x64/Tolk.dll, x64/nvdaControllerClient64.dll)
  ISA_OUT          Ausgabeordner (Standard: ./dist)
Vorher ./build.sh ausführen (erzeugt build/IdolShowdownAccess.dll und build/ISAccessSapi32.exe).
"""
import os, re, sys, zipfile

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bep = os.environ.get('ISA_BEPINEX_DIR'); tolk = os.environ.get('ISA_TOLK_DIR')
out = os.environ.get('ISA_OUT') or os.path.join(root, 'dist')
if not bep or not tolk:
    sys.exit('ISA_BEPINEX_DIR und ISA_TOLK_DIR setzen.')
version = re.search(r'Version = "([^"]+)"', open(os.path.join(root, 'src', 'Plugin.cs'), encoding='utf-8').read()).group(1)
os.makedirs(out, exist_ok=True)

def text_win(path, bom=True):
    """Text mit Windows-Zeilenenden, optional UTF-8 mit BOM (für Anleitungen und PowerShell 5.1)."""
    t = open(path, encoding='utf-8-sig').read().replace('\r\n', '\n').replace('\n', '\r\n')
    return (b'\xef\xbb\xbf' if bom else b'') + t.encode('utf-8')

def raw(path):
    return open(path, 'rb').read()

plug = 'BepInEx/plugins/IdolShowdownAccess/'
files = {}
files['winhttp.dll'] = raw(os.path.join(bep, 'winhttp.dll'))
files['.doorstop_version'] = raw(os.path.join(bep, '.doorstop_version'))
ini = open(os.path.join(bep, 'doorstop_config.ini'), encoding='utf-8').read().replace('\r\n', '\n')
ini = re.sub(r'(?m)^dll_search_path_override =.*$', 'dll_search_path_override = unstripped_corlib', ini)
files['doorstop_config.ini'] = ini.replace('\n', '\r\n').encode('utf-8')
for f in sorted(os.listdir(os.path.join(bep, 'BepInEx', 'core'))):
    files['BepInEx/core/' + f] = raw(os.path.join(bep, 'BepInEx', 'core', f))
files[plug + 'IdolShowdownAccess.dll'] = raw(os.path.join(root, 'build', 'IdolShowdownAccess.dll'))
files[plug + 'ISAccessSapi32.exe'] = raw(os.path.join(root, 'build', 'ISAccessSapi32.exe'))
files[plug + 'Tolk.dll'] = raw(os.path.join(tolk, 'x64', 'Tolk.dll'))
files[plug + 'nvdaControllerClient64.dll'] = raw(os.path.join(tolk, 'x64', 'nvdaControllerClient64.dll'))
files[plug + 'LICENSE.txt'] = text_win(os.path.join(root, 'LICENSE'), bom=False)
files[plug + 'THIRD_PARTY_NOTICES.txt'] = text_win(os.path.join(root, 'THIRD_PARTY_NOTICES.md'))
for f in sorted(os.listdir(os.path.join(root, 'licenses'))):
    files[plug + 'licenses/' + f] = text_win(os.path.join(root, 'licenses', f), bom=False)
for f in sorted(os.listdir(os.path.join(root, 'scripts'))):
    p = os.path.join(root, 'scripts', f)
    files[f] = text_win(p, bom=f.endswith('.ps1'))
for f in sorted(os.listdir(os.path.join(root, 'docs'))):
    files[f] = text_win(os.path.join(root, 'docs', f))
files['unstripped_corlib/README.txt'] = (
    'This folder is filled by IdolShowdownAccess_Installieren.bat.\r\n'
    'Dieser Ordner wird von IdolShowdownAccess_Installieren.bat befuellt.\r\n').encode('utf-8')

mod_zip = os.path.join(out, 'IdolShowdownAccess_Mod_v%s.zip' % version)
with zipfile.ZipFile(mod_zip, 'w', zipfile.ZIP_DEFLATED) as z:
    for name in sorted(files):
        z.writestr(name, files[name])

# Quellcode-ZIP: alles, was auch ins Repository gehört
src_zip = os.path.join(out, 'IdolShowdownAccess_Quellcode_v%s.zip' % version)
skip_dirs = {'build', 'dist', '.git', 'bin', 'obj'}
with zipfile.ZipFile(src_zip, 'w', zipfile.ZIP_DEFLATED) as z:
    for dp, dns, fns in os.walk(root):
        dns[:] = [d for d in dns if d not in skip_dirs and not d.startswith('__')]
        for f in fns:
            if f == 'env.sh' or f.endswith('.zip') or f.endswith('.pyc'):
                continue
            p = os.path.join(dp, f)
            z.write(p, os.path.relpath(p, root))

for p in (mod_zip, src_zip):
    with zipfile.ZipFile(p) as z:
        print(os.path.basename(p), os.path.getsize(p), 'Bytes,', len(z.namelist()), 'Dateien')
