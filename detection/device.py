from __future__ import annotations

import os


def resolve_torch_device(requested_device: str) -> str:
    requested = (requested_device or "auto").strip().lower()
    if requested != "auto":
        return requested

    try:
        import torch
    except ImportError:
        return "cpu"

    if torch.cuda.is_available():
        return "cuda"

    mps_backend = getattr(torch.backends, "mps", None)
    if mps_backend is not None and mps_backend.is_available():
        os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")
        return "mps"

    return "cpu"

