# bundle_executor/__init__.py
"""Package for executing prompt bundles via Autogen.

The core entry point is `executor.run_bundle(bundle_path)` which:
1. Loads a JSON bundle definition.
2. Instantiates Autogen `AssistantAgent` (Gemma 3 4B via llama.cpp).
3. Executes the ordered prompts.
4. Returns the final assistant response.
"""
