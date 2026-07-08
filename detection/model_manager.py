from __future__ import annotations

from pathlib import Path
from urllib.request import urlretrieve


class ModelManager:
    """Manages YOLO model location, cache presence, and optional refresh."""

    def __init__(self, model_path: str, download_url: str = "") -> None:
        self.model_path = Path(model_path)
        self.download_url = download_url

    def exists(self) -> bool:
        return self.model_path.exists()

    def resolve(self, allow_download: bool = False) -> Path:
        if self.exists():
            return self.model_path

        if allow_download:
            return self.download()

        raise FileNotFoundError(
            f"YOLO model not found at {self.model_path}. "
            "Place weights there or enable download with a configured URL."
        )

    def download(self) -> Path:
        if not self.download_url:
            raise ValueError("Model download URL is not configured.")

        self.model_path.parent.mkdir(parents=True, exist_ok=True)
        urlretrieve(self.download_url, self.model_path)
        return self.model_path

    def refresh(self) -> Path:
        return self.download()
