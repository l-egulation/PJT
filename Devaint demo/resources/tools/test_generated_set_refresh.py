"""Re-exporting an older family must preserve newly registered art families."""
import unittest
from index_starting_hub import merge_generated_records


class GeneratedRefreshTests(unittest.TestCase):
    def test_preserves_other_family_and_only_replaces_refreshed_path(self):
        earlier=[{'path':'ui/hub/old.png','sha256':'old'},
                 {'path':'characters/story/helper.png','sha256':'keep'}]
        result=merge_generated_records(earlier,[{'path':'ui/hub/old.png','sha256':'new'}])
        self.assertEqual([{'path':'characters/story/helper.png','sha256':'keep'},
                          {'path':'ui/hub/old.png','sha256':'new'}],result)
        self.assertEqual('old',earlier[0]['sha256'])

    def test_rejects_duplicate_refresh_records(self):
        with self.assertRaisesRegex(ValueError,'Duplicate'):
            merge_generated_records([],[{'path':'same'},{'path':'same'}])


if __name__=='__main__':unittest.main()
