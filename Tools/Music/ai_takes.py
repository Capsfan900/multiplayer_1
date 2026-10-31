# /// script
# requires-python = ">=3.10"
# dependencies = ["numpy", "scipy", "soundfile"]
# ///
"""Generation spec for the Wildstone music takes (ACE-Step 1.5 turbo, instrumental). Not run by the build.

Needs a local ACE-Step 1.5 install and the `ai.py` runner from the user's code-music-production skill next to this
file (it runs ACE-Step in its own venv; see README). Captions and seeds are the record of what was generated on
2026-10-30; regenerating on other hardware/drivers may not be bit-identical, so the chosen takes are archived and
`loop_takes.py` locks their loop lengths.

  uv run ai_takes.py [menu|atrium|split_gallery|terrace_steps] -> ../takes/<name>_s<seed>.wav (48 kHz float)
"""
import json
import sys
from pathlib import Path

import ai

NEG = ("vocals, singing, female vocal, male vocal, vocal chops, rap, MC, choir, spoken word, dubstep, heavy drop, "
       "growling bass, trap, supersaw, hyperpop, K-pop, 8-bit chiptune, rock guitar, distorted guitar, aggressive")
BASE = ("instrumental, no vocals, warm Rhodes electric piano chords, round warm sub bassline, soft rounded kick, "
        "shuffled hi-hats, airy pads, sunlit afternoon, bright clean modern stone architecture, polished production")
JOBS = [
    dict(name="menu", bpm=116, key="D major",
         caption="chill house, mellow deep house, laid-back relaxed groove, dreamy jazzy ninth chords, soft marimba "
                 "plucks, gentle and welcoming, " + BASE),
    dict(name="atrium", bpm=122, key="A minor",
         caption="melodic deep house, steady four-on-the-floor groove, smooth rolling bassline, jazzy minor seventh "
                 "chords, light percussion, focused and confident, " + BASE),
    dict(name="split_gallery", bpm=124, key="C minor",
         caption="chill tech house, hypnotic steady groove, plucky bass, soft arpeggiated synth, filtered chord stabs, "
                 "cool and focused, " + BASE),
    dict(name="terrace_steps", bpm=120, key="E major",
         caption="sunny chill house, uplifting gentle groove, bright piano chords, soft vibraphone melody, bouncy "
                 "bassline, warm and open, " + BASE),
]
SEEDS = [11, 22, 33]
EXTRA = {"terrace_steps": [44, 55, 66]}  # s11's vocal stem showed short vocal-like passages, so three more seeds

if __name__ == "__main__":
    only = sys.argv[1:]
    jobs = [dict(j, lyrics="[Instrumental]", seeds=SEEDS + EXTRA.get(j["name"], []), duration=180, timesignature="4",
                 lm_negative_prompt=NEG) for j in JOBS if not only or j["name"] in only]
    takes = ai.generate(jobs, Path(__file__).parents[1] / "takes")
    print(json.dumps([{k: t.get(k) for k in ("name", "seed", "secs", "path")} for t in takes], indent=1))
