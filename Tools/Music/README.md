# Wildstone music

The four chill-house loops in `Assets/Wildstone/Resources/Audio/Music/` are cut from
ACE-Step 1.5 instrumental takes (D032). Provenance, licence, take hashes and
measurements are in [ASSET-PIPELINE](../../docs/ASSET-PIPELINE.md#music).

## Rebuild the shipped loops

From the repository root:

```
uv run Tools/Music/loop_takes.py <takes dir>                  # all four: cut, master, encode, verify (~1 min)
uv run Tools/Music/loop_takes.py <takes dir> Level_atrium     # one track
uv run Tools/Music/loop_takes.py <takes dir> --probe          # every seed's beat grid and loop window, no output
uv run Tools/Music/loop_takes.py --check                      # measure the committed OGGs only (no takes needed)
```

`<takes dir>` holds the chosen takes (archived at `D:\WildstoneArchive\music\takes`).
A build asserts the locked bar and sample counts, the exact loop length, a
click-free seam, -16..-14 LUFS integrated and a true peak of -1 dBTP or lower. It
fails if any of these miss. Two consecutive builds produced byte-identical OGGs.

## Regenerate takes (GPU, not part of the build)

`ai_takes.py` holds the captions, seeds and settings that produced the takes. It
needs a local ACE-Step 1.5 install and the `ai.py` runner from the
code-music-production skill next to it. Ask before using the GPU. Regenerating may
not be bit-identical on other hardware.

## Synthesized fallback (D030, superseded)

`build_music.py` with `engine.py` renders the original code-only loops. They use no
samples and no AI model. `engine.py` is a verbatim copy of the user's
`code-music-production` skill engine (sha256
`887732c6428ac5e40abbd86bba66a7c9e0e3295f340b51ebb098fb22ceb02dc1`).
`uv run Tools/Music/build_music.py` overwrites the shipped OGGs with that set. Its
`--check` measures against the synthesized lengths, so it fails on the current
files.

## Environment used

- Windows 11, `uv` 0.12.7. Inline headers pin Python >= 3.12 (3.12.14 resolved),
  numpy 2.5.4, scipy 1.18.1 and soundfile 0.14.0 (libsndfile 1.2.2).
- `ffmpeg` on PATH (9.0.1 gyan.dev full build via winget). It encodes with libvorbis
  `-q:a 6` using bitexact muxing, and meters with `ebur128=peak=true` (BS.1770
  integrated loudness and 4x-oversampled true peak).

## How the loops stay seamless

- **AI loops:** each cut starts on a kick-grid downbeat and spans whole 8-bar
  phrases. The beat that followed the loop end in the take is crossfaded over the
  loop start, and the limiter runs circularly.
- **Synthesized loops:** every linear stage renders past the loop end, and the tail
  is wrapped onto the start. The sidechain is periodic, and the nonlinear master
  runs on the loop padded with its own ends.

Unity re-encodes on import. `MusicImportTests` checks that the imported clip keeps
the exact sample count, and `Invoke-VisualCheck.ps1 -Music` checks streaming in the
packaged player.
