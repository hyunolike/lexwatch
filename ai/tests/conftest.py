import json
from pathlib import Path

import pytest

FIX = Path(__file__).resolve().parents[2] / "fixtures" / "nim"


@pytest.fixture
def fixture():
    return lambda name: json.loads((FIX / name).read_text())
