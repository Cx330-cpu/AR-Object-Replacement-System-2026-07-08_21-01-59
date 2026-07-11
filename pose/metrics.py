from __future__ import annotations

import math

import numpy as np
from scipy.spatial.transform import Rotation


def rotation_angle_error_degrees(a: np.ndarray, b: np.ndarray) -> float:
    delta = Rotation.from_matrix(a[:3, :3]).inv() * Rotation.from_matrix(b[:3, :3])
    return float(math.degrees(delta.magnitude()))


def translation_error_m(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.linalg.norm(a[:3, 3] - b[:3, 3]))
