import copy
import json
import tempfile
import unittest
from pathlib import Path

import ledger

ROW = {
    "id": "fills-solid", "area": "fills", "name": "Solid fill", "source": "core-render",
    "behavior": "b", "codeRefs": ["lib/fills.js:1"], "quirks": "", "portTarget": "p",
    "risk": "low", "parityCheck": "c", "owner": "Engine.Fills", "check": "golden",
    "disposition": "keep", "divergence": "", "status": "open",
}


def data(*rows):
    return {"generated": "2026-10-11", "source": "test", "crossCutting": [], "rows": list(rows)}


def row(**kw):
    r = copy.deepcopy(ROW)
    r.update(kw)
    return r


class CheckTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())

    def write_md(self, d):
        (self.tmp / "parity-ledger.md").write_text(ledger.render_ledger(d), encoding="utf-8")
        (self.tmp / "divergences.md").write_text(ledger.render_divergences(d), encoding="utf-8")

    def test_valid_ledger_has_no_problems(self):
        d = data(row())
        self.write_md(d)
        self.assertEqual(ledger.problems(d, self.tmp), [])

    def test_duplicate_ids_are_flagged(self):
        d = data(row(), row())
        self.write_md(d)
        self.assertTrue(any("duplicate id fills-solid" in p for p in ledger.problems(d, self.tmp)))

    def test_unknown_owner_check_disposition_status_are_flagged(self):
        d = data(row(owner="Engine.Nope", check="vibes", disposition="maybe", status="wip"))
        self.write_md(d)
        found = "\n".join(ledger.problems(d, self.tmp))
        for word in ("owner", "check", "disposition", "status"):
            self.assertIn(word, found)

    def test_fix_without_divergence_sentence_is_flagged(self):
        d = data(row(disposition="fix", divergence=""))
        self.write_md(d)
        self.assertTrue(any("divergence" in p for p in ledger.problems(d, self.tmp)))

    def test_check_flags_stale_markdown(self):
        d = data(row())
        self.write_md(d)
        (self.tmp / "parity-ledger.md").write_text("hand edited\n", encoding="utf-8")
        self.assertTrue(any("parity-ledger.md is stale" in p for p in ledger.problems(d, self.tmp)))

    def test_pipes_in_cells_are_escaped(self):
        md = ledger.render_ledger(data(row(name="a | b")))
        self.assertIn("a \\| b", md)

    def test_mark_done_sets_status_and_rejects_unknown_ids(self):
        d = data(row(), row(id="fills-camo"))
        out = ledger.mark_done(d, ["fills-camo"])
        self.assertEqual([r["status"] for r in out["rows"]], ["open", "done"])
        with self.assertRaises(KeyError):
            ledger.mark_done(d, ["no-such-row"])

    def test_real_ledger_is_valid(self):
        root = Path(__file__).resolve().parents[2]
        d = ledger.load(root / "docs" / "port" / "parity-ledger.json")
        self.assertEqual(ledger.problems(d, root / "docs" / "port"), [])


if __name__ == "__main__":
    unittest.main()
