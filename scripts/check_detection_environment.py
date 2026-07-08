from __future__ import annotations

import platform
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))


def main() -> int:
    print(f"python={sys.executable}")
    print(f"platform={platform.platform()}")
    print(f"machine={platform.machine()}")
    print(f"mac_ver={platform.mac_ver()[0]}")

    try:
        import torch
    except ImportError:
        print("torch=missing")
        print("resolved_device=cpu")
        return 1

    print(f"torch={torch.__version__}")
    print(f"cuda_available={torch.cuda.is_available()}")
    mps_backend = getattr(torch.backends, "mps", None)
    mps_built = bool(mps_backend and mps_backend.is_built())
    mps_available = bool(mps_backend and mps_backend.is_available())
    print(f"mps_built={mps_built}")
    print(f"mps_available={mps_available}")

    if mps_built:
        try:
            tensor = torch.ones(1, device="mps")
            print(f"mps_tensor={tensor}")
        except Exception as exc:
            print(f"mps_error={type(exc).__name__}: {exc}")

    try:
        import ultralytics
        import cv2
        import numpy
    except ImportError as exc:
        print(f"detection_dependency_error={exc}")
        return 1

    print(f"ultralytics={ultralytics.__version__}")
    print(f"cv2={cv2.__version__}")
    print(f"numpy={numpy.__version__}")

    from detection.device import resolve_torch_device

    print(f"resolved_device={resolve_torch_device('auto')}")
    return 0 if mps_available or torch.cuda.is_available() else 2


if __name__ == "__main__":
    raise SystemExit(main())
