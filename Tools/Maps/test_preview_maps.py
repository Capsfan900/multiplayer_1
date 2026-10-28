"""Focused offline regressions; these do not execute Unity or render Blender."""

import contextlib
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import preview_maps as previews


class MapLintTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.arenas = json.loads(previews.SOURCE.read_text(encoding="utf-8"))

    def test_all_exported_defaults_have_clear_supported_zone_footprints(self):
        for arena in self.arenas:
            with self.subTest(map=arena["Id"]):
                self.assertEqual([], previews.lint(arena)[0])

    def test_old_landmarks_block_zone_perimeter_even_with_clear_center(self):
        for arena_id, name, z in (("atrium", "Gallery fin east", 27),
                                   ("terrace-steps", "Cantilever tower", 28)):
            arena = copy.deepcopy(next(a for a in self.arenas if a["Id"] == arena_id))
            next(p for p in arena["Pieces"] if p["Name"] == name)["Position"]["z"] = z
            with self.subTest(map=arena_id):
                self.assertTrue(any("clearance" in issue for issue in previews.lint(arena)[0]))

    def test_capsule_checks_its_segment_and_rounded_corners(self):
        box = previews.Box({"Name": "Thin middle blocker", "Position": {"x": 0, "y": .93, "z": 0},
                            "Size": {"x": 1, "y": .1, "z": 1},
                            "Euler": {"x": 0, "y": 0, "z": 0}, "Tone": 0})
        self.assertTrue(box.blocks_capsule((0, 0, 0)))
        self.assertTrue(box.blocks_capsule((.7, 0, .7)))
        self.assertFalse(box.blocks_capsule((.8, 0, .8)))

    def test_missing_foundation_returns_failure_without_build_or_verify(self):
        arena = copy.deepcopy(self.arenas[0])
        arena["Pieces"] = [p for p in arena["Pieces"] if p["Name"] != "Foundation"]
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "unsupported.json"
            source.write_text(json.dumps([arena]), encoding="utf-8")
            for action in ("lint", "build", "verify"):
                with self.subTest(action=action), patch.object(previews, "SOURCE", source), \
                        patch.object(previews, "build") as build, patch.object(previews, "verify") as verify, \
                        patch("sys.argv", ["preview_maps.py", action]), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1, previews.main())
                    build.assert_not_called()
                    verify.assert_not_called()


if __name__ == "__main__":
    unittest.main()
