"""Verify Mocap Adventure's compiled SDK boundary without starting Unity/builds."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]
SDK = 'hd-motion-engine-sdk-0.1.0-preview.2-windows-x64.tgz'
SDK_SHA256 = 'f3fec3e5e55355d936d4eebe54d715a2806845243968d7495ed13659fa3da859'
RETIRED = (
    'Assets/GoldenNeedle/Core/', 'Assets/GoldenNeedle/Debug/',
    'Assets/GoldenNeedle/Plugins/', 'Packages/com.github.homuler.mediapipe/',
    'Tools/OpenVino', 'Tools/MediaPipe', 'Tools/Foundation', 'Tools/AvatarDrive',
    'Assets/Editor/SDKMigration/',
)
GENERATED_MODELS = (
    'Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/hand_landmarker.task',
    'Assets/StreamingAssets/GoldenNeedle/PoseTrackingSpike/Models/pose_landmarker_lite.bytes',
)
PRIVATE_SOURCE_SUFFIXES = {'.cs', '.cpp', '.cc', '.c', '.h', '.hpp', '.py', '.ps1', '.sh', '.asmdef', '.pdb', '.mdb'}


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def verify():
    tracked = [p for p in subprocess.check_output(
        ['git', '-C', str(ROOT), 'ls-files', '-z']).decode('utf-8').split('\0') if p]
    leftovers = [p for p in tracked if p.startswith(RETIRED) or p in GENERATED_MODELS]
    require(not leftovers, 'Retired engine files tracked: ' + ', '.join(leftovers))
    for prefix in RETIRED:
        folder = ROOT / prefix
        if folder.is_dir():
            require(not any(p.is_file() for p in folder.rglob('*')),
                    'Retired engine directory contains local files: ' + prefix)
    for p in tracked:
        if p.endswith('.cs'):
            source = (ROOT / p).read_text(encoding='utf-8-sig')
            require(not re.search(r'\bnamespace\s+(?:GoldenNeedle\.Core\.Motion|Mediapipe)(?:\s|[.;{])', source),
                    'Private implementation namespace declared: ' + p)
        if p.startswith('.github/workflows/'):
            workflow = (ROOT / p).read_text(encoding='utf-8')
            require('engine/pose-tracking-spike' not in workflow and not any(prefix in workflow for prefix in RETIRED),
                    'Obsolete engine CI paths restored: ' + p)

    manifest = json.loads((ROOT/'Packages/manifest.json').read_text(encoding='utf-8'))
    lock = json.loads((ROOT/'Packages/packages-lock.json').read_text(encoding='utf-8'))
    expected = 'file:HDMotionEngine/' + SDK
    require(manifest['dependencies'].get('com.hd.motion-engine') == expected,
            'Product must depend on the reviewed compiled SDK archive')
    require('com.github.homuler.mediapipe' not in manifest['dependencies'],
            'Embedded MediaPipe source dependency restored')
    require(lock['dependencies']['com.hd.motion-engine']['version'] == expected,
            'SDK package lock differs from manifest')
    require('com.github.homuler.mediapipe' not in lock['dependencies'],
            'Embedded MediaPipe source remains in package lock')
    package = ROOT/'Packages/HDMotionEngine'/SDK
    require(hashlib.sha256(package.read_bytes()).hexdigest() == SDK_SHA256,
            'SDK archive changed; review a new version and update verification deliberately')
    inventory = json.loads((ROOT/'Docs/motion-sdk-content-manifest.json').read_text(encoding='utf-8'))
    expected_files = {entry['path']: entry for entry in inventory['files']}
    actual_files = set()
    with tarfile.open(package, 'r:gz') as archive:
        for member in archive.getmembers():
            require(member.isdir() or member.isfile(), 'Unsupported SDK archive member: ' + member.name)
            require(member.name.startswith('package/'), 'Unexpected SDK archive prefix: ' + member.name)
            name = member.name[len('package/'):]
            require('..' not in Path(name).parts, 'Unsafe SDK archive path')
            if not member.isfile():
                continue
            require(name not in actual_files, 'Duplicate SDK file: ' + name)
            require(Path(name).suffix.lower() not in PRIVATE_SOURCE_SUFFIXES,
                    'Implementation source/symbols in SDK: ' + name)
            require(not any(marker in name for marker in ('Unity.Pipeline', 'Microsoft.CodeAnalysis', 'TestRunner', 'HDMotionLab')),
                    'Development tooling in SDK: ' + name)
            entry = expected_files.get(name)
            require(entry is not None, 'Unreviewed SDK file: ' + name)
            data = archive.extractfile(member).read()
            require(len(data) == entry['size'] and hashlib.sha256(data).hexdigest() == entry['sha256'],
                    'SDK file differs from verified inventory: ' + name)
            actual_files.add(name)
    require(actual_files == set(expected_files), 'SDK inventory incomplete')
    for name in GENERATED_MODELS:
        require((ROOT/(name+'.meta')).is_file(), 'Model metadata lost: ' + name)
    require((ROOT/'Assets/GoldenNeedle/Editor/ExcludeDevelopmentToolingFromPlayer.cs').is_file(),
            'Player development-tool exclusion missing')
    print(json.dumps({'status': 'PASS', 'sdk_version': '0.1.0-preview.2',
                      'sdk_files_verified': len(actual_files), 'engine_source_leftovers': 0,
                      'builds_started': 0}))


if __name__ == '__main__':
    verify()
