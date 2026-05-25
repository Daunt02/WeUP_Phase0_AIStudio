# bundle_executor/executor.py
"""Executor for JSON prompt bundles using Autogen ChatAgent and a Transformers backend.

Requirements:
- `autogen` package (`pip install autogen`)
- `torch` and `transformers` (`pip install torch transformers`)
- Gemma 3 4B model (either a local checkpoint directory or a Hugging Face repo).

The executor performs:
1. Load a bundle JSON file (validated against `bundles/bundle_schema.json`).
2. Initialise a `UserProxyAgent` and an `AssistantAgent` that uses a Transformers model.
3. Feed the prompts in order, optionally rendering Jinja2 placeholders.
4. Return the final assistant response.
"""

import json
import os
from pathlib import Path
from typing import List, Dict, Any
import jinja2

# Autogen imports – these will be available after installing the package.
try:
    import autogen
except ImportError:
    raise ImportError("autogen package is required. Install with 'pip install autogen'.")

# Transformers imports for the LLM backend.
try:
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer
except ImportError:
    raise ImportError("torch and transformers packages are required. Install with 'pip install torch transformers'.")

# ---------------------------------------------------------------------------
# Helper: Load and validate bundle JSON (basic validation against schema).
# ---------------------------------------------------------------------------
def load_bundle(bundle_path: str) -> Dict[str, Any]:
    """Load a JSON bundle and return its dictionary.

    Minimal validation: required keys exist and prompts list is not empty.
    """
    path = Path(bundle_path).resolve()
    if not path.is_file():
        raise FileNotFoundError(f"Bundle file not found: {bundle_path}")
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    required = {"id", "name", "description", "prompts", "environment"}
    missing = required - data.keys()
    if missing:
        raise ValueError(f"Bundle missing required fields: {missing}")
    if not isinstance(data["prompts"], list) or not data["prompts"]:
        raise ValueError("Bundle must contain a non‑empty 'prompts' list.")
    return data

# ---------------------------------------------------------------------------
# Llama.cpp model wrapper compatible with Autogen's OpenAIChatAssistant interface.
# ---------------------------------------------------------------------------
class LlamaCppChatWrapper:
    """Wrapper for llama.cpp model using llama_cpp_python.

    Provides a `generate(messages, **kwargs)` method compatible with Autogen.
    """

    def __init__(self, model_path: str):
        try:
            from llama_cpp import Llama
        except ImportError as e:
            raise ImportError(
                "llama_cpp_python package is required for GGUF models. Install with 'pip install llama-cpp-python'."
            ) from e
        self.model = Llama(model_path=model_path, n_ctx=2048, n_gpu_layers=-1)
        self._system_prompt = ""

    def generate(self, messages: List[Dict[str, str]], **kwargs) -> Dict[str, str]:
        # Concatenate messages into a single prompt.
        prompt_parts = []
        for m in messages:
            role = m.get("role")
            content = m.get("content", "")
            if role == "system":
                self._system_prompt = content  # store for later if needed
                continue
            prefix = "[USER] " if role == "user" else "[ASSISTANT] "
            prompt_parts.append(f"{prefix}{content}\n")
        prompt = "".join(prompt_parts)
        # llama_cpp expects a raw prompt string.
        output = self.model(prompt, **kwargs)
        # output[0]['choices'][0]['text'] contains the generated text.
        generated_text = output[0]['choices'][0]['text']
        # Strip any leading system prompt that might be echoed.
        if self._system_prompt and generated_text.startswith(self._system_prompt):
            generated_text = generated_text[len(self._system_prompt):]
        return {"content": generated_text.strip()}

class TransformersChatWrapper:
    """Wrapper that presents a `generate(messages, **kwargs)` API compatible with Autogen.

    It uses a Hugging Face `AutoModelForCausalLM` and `AutoTokenizer`.
    """

    def __init__(self, model_path: str):
        # Decide device automatically.
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        # Load tokenizer and model. `model_path` may be a local directory or a HF repo name.
        self.tokenizer = AutoTokenizer.from_pretrained(model_path)
        self.model = AutoModelForCausalLM.from_pretrained(model_path).to(self.device)
        # Ensure the tokenizer has a pad token.
        if self.tokenizer.pad_token is None:
            self.tokenizer.pad_token = self.tokenizer.eos_token

    def generate(self, messages: List[Dict[str, str]], **kwargs) -> Dict[str, str]:
        # Build a single prompt string from the message list.
        prompt_parts = []
        for m in messages:
            role = m.get("role")
            content = m.get("content", "")
            if role == "system":
                prompt_parts.append(f"[SYSTEM] {content}\n")
            elif role == "assistant":
                prompt_parts.append(f"[ASSISTANT] {content}\n")
            else:  # user
                prompt_parts.append(f"[USER] {content}\n")
        prompt = "".join(prompt_parts)
        inputs = self.tokenizer(prompt, return_tensors="pt").to(self.device)
        # Generation parameters – allow overrides via kwargs (e.g., max_new_tokens).
        gen_kwargs = {
            "max_new_tokens": kwargs.get("max_new_tokens", 256),
            "temperature": kwargs.get("temperature", 0.7),
            "do_sample": kwargs.get("do_sample", True),
        }
        output_ids = self.model.generate(**inputs, **gen_kwargs)
        # Decode only the newly generated tokens (skip the prompt part).
        generated_text = self.tokenizer.decode(output_ids[0], skip_special_tokens=True)
        # Remove the original prompt to return only the assistant’s response.
        if generated_text.startswith(prompt):
            generated_text = generated_text[len(prompt):]
        return {"content": generated_text.strip()}

# ---------------------------------------------------------------------------
# Core executor function.
# ---------------------------------------------------------------------------
def run_bundle(bundle_path: str, model_dir: str = None) -> str:
    """Execute the bundle and return the final assistant response.

    Parameters
    ----------
    bundle_path: str
        Path to the JSON bundle file.
    model_dir: str, optional
        Directory containing the Gemma 3 4B GGUF model. If omitted, the function
        will look for the environment variable `GEMMA_MODEL_DIR`.
    """
    bundle = load_bundle(bundle_path)
    env = bundle["environment"]
    model_name = env.get("model")
    if model_name != "gemma-3-4b":
        raise ValueError(f"Unsupported model '{model_name}'. Only 'gemma-3-4b' is supported.")

    # Resolve model file path.
    model_dir = Path(model_dir or os.getenv("GEMMA_MODEL_DIR", ""))
    if not model_dir:
        raise EnvironmentError("GEMMA_MODEL_DIR environment variable not set and no model_dir argument provided.")
    # Resolve possible model identifier
    possible_file = model_dir / "gemma-3-4b.gguf"
    if possible_file.is_file():
        model_path = possible_file
    else:
        # Assume the user provided a HuggingFace model repo name (e.g., "google/gemma-2b-it")
        # We'll pass the string directly to the Transformers wrapper.
        model_path = model_dir  # could be a path or repo name; wrapper will handle it


    # Initialise the appropriate LLM wrapper based on the model path.
    # If a GGUF file is provided, use the llama.cpp wrapper; otherwise fall back to Transformers.
    if isinstance(model_path, Path) and model_path.suffix.lower() == ".gguf":
        llm_wrapper = LlamaCppChatWrapper(str(model_path))
    else:
        # Could be a directory or a HuggingFace repo name.
        llm_wrapper = TransformersChatWrapper(str(model_path))

    # Build Autogen agents.
    # UserProxyAgent forwards messages to the assistant.
    user_proxy = autogen.UserProxyAgent(
        name="UserProxy",
        system_message="You are the user proxy handling input/output for the bundle execution.",
        code_execution_config=False,
    )
    assistant = autogen.AssistantAgent(
        name="GemmaAssistant",
        llm_config={
            "config_list": [
                {
                    "model": "gemma-3-4b",
                    "api_key": "none",  # not used for local model
                    "model_path": str(model_path),
                    "client": llm_wrapper,
                }
            ]
        },
        system_message="You are Gemma 3 4B, responding to prompts from the bundle.",
    )

    # Render prompts with Jinja2 (allows {{var}} placeholders).
    env_vars = {}
    # Users can set variables via OS env or by providing a context dict later.
    # For now we expose nothing.
    jinja_env = jinja2.Environment(undefined=jinja2.StrictUndefined)

    # Feed prompts sequentially.
    for idx, p in enumerate(bundle["prompts"]):
        role = p["role"]
        raw_content = p["content"]
        # Render any placeholders.
        try:
            content = jinja_env.from_string(raw_content).render(**env_vars)
        except jinja2.exceptions.UndefinedError as e:
            raise ValueError(f"Undefined variable in prompt #{idx}: {e}")
        # Dispatch based on role.
        if role == "system":
            assistant.update_system_message(content)
        elif role == "assistant":
            # Assistant speaks first – send a message to itself.
            # We simulate by having the user proxy send it.
            user_proxy.initiate_chat(assistant, message=content)
        elif role == "user":
            # Normal user message.
            user_proxy.initiate_chat(assistant, message=content)
        else:
            raise ValueError(f"Unsupported role '{role}' in prompt #{idx}.")

    # After all prompts, retrieve the last assistant message.
    # Autogen stores chat history in the assistant agent.
    chat_history = assistant.chat_messages
    # Find the last assistant role message.
    final_resp = None
    for msg in reversed(chat_history):
        if msg.get("role") == "assistant":
            final_resp = msg.get("content")
            break
    return final_resp or ""

# ---------------------------------------------------------------------------
# CLI entry point for convenience.
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    import argparse, sys
    parser = argparse.ArgumentParser(description="Run a prompt bundle using Gemma 3 4B via Autogen.")
    parser.add_argument("bundle", help="Path to the JSON bundle file")
    parser.add_argument("--model-dir", help="Directory containing gemma-3-4b.gguf", default=None)
    args = parser.parse_args()
    try:
        result = run_bundle(args.bundle, model_dir=args.model_dir)
        print("=== Bundle Execution Result ===")
        print(result)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
