#!/usr/bin/env python3
"""Tests for storage push validation fail-closed behavior."""

from __future__ import annotations

import os
import unittest
from unittest.mock import patch

from storage_push_validation import validate_push


class StoragePushValidationTests(unittest.TestCase):
    def test_validate_push_fails_closed_when_api_url_missing(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            with patch("storage_push_validation.API_URL_FILE") as api_url_file:
                api_url_file.is_file.return_value = False
                with self.assertRaises(RuntimeError) as ctx:
                    validate_push("/srv/git/sample.git", [("0" * 40, "a" * 40, "refs/heads/main")])

        self.assertIn("OPENGITBASE_API_URL", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
