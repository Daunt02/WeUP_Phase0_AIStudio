import unittest
from unittest.mock import patch, MagicMock
import sys
from pathlib import Path

# Ensure the project root is in sys.path for imports
PROJECT_ROOT = Path(__file__).resolve().parents[2]
sys.path.append(str(PROJECT_ROOT))

from bundle_executor.executor import run_bundle

class TestBundleExecutor(unittest.TestCase):
    @patch('bundle_executor.executor.TransformersChatWrapper')
    def test_example_bundle_execution(self, MockTransformersWrapper):
        # Mock the generate method to return a predictable response
        mock_instance = MagicMock()
        mock_instance.generate.return_value = {"content": "This is a summary of the input file."}
        MockTransformersWrapper.return_value = mock_instance

        # Path to the example bundle
        bundle_path = PROJECT_ROOT / 'bundles' / 'example_bundle.json'
        # Run the bundle (model_dir can be dummy because we mock the wrapper)
        result = run_bundle(str(bundle_path), model_dir=str(PROJECT_ROOT / 'models'))
        self.assertIn('summary', result.lower())
        self.assertEqual(result, "This is a summary of the input file.")

if __name__ == '__main__':
    unittest.main()
