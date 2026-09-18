"""Provider-neutral, request-scoped transport. ASP.NET owns JPEG normalization."""
import base64
import binascii
from typing import Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, field_validator

MAX_IMAGES = 3
MAX_IMAGE_BYTES = 2 * 1024 * 1024
MAX_TOTAL_BYTES = 4 * 1024 * 1024
MAX_BASE64_CHARACTERS = 4 * ((MAX_IMAGE_BYTES + 2) // 3)
MAX_TOTAL_BASE64_CHARACTERS = 4 * ((MAX_TOTAL_BYTES + 2 * MAX_IMAGES) // 3)
MAX_DIMENSION = 2048
VisionStatus = Literal["not_requested", "available", "unsupported", "failed"]


class VisualEvidence(BaseModel):
    """No provider SDK objects, filenames or paths. Never persist or log content."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", frozen=True,
                              hide_input_in_errors=True)
    attachment_id: UUID = Field(alias="attachmentId")
    content_type: Literal["image/jpeg"] = Field(alias="contentType")
    data_base64: str = Field(alias="dataBase64", min_length=4,
                             max_length=MAX_BASE64_CHARACTERS, repr=False)
    width: int = Field(strict=True, ge=1, le=MAX_DIMENSION)
    height: int = Field(strict=True, ge=1, le=MAX_DIMENSION)

    @field_validator("attachment_id")
    @classmethod
    def nonempty_id(cls, value: UUID) -> UUID:
        if value.int == 0:
            raise ValueError("Attachment identity must not be empty.")
        return value

    @field_validator("data_base64")
    @classmethod
    def valid_content(cls, value: str) -> str:
        try:
            decoded = base64.b64decode(value, validate=True)
        except (ValueError, binascii.Error):
            raise ValueError("Invalid Base64 evidence.") from None
        if not 0 < len(decoded) <= MAX_IMAGE_BYTES:
            raise ValueError("Evidence exceeds decoded byte limits.")
        if base64.b64encode(decoded).decode("ascii") != value:
            raise ValueError("Evidence must use canonical Base64.")
        return value

    @property
    def decoded_size(self) -> int:
        # Content has already been strictly validated; do not retain a second byte copy.
        return len(self.data_base64) // 4 * 3 - (len(self.data_base64) - len(self.data_base64.rstrip("=")))


def validate_evidence_collection(items: list[VisualEvidence]) -> list[VisualEvidence]:
    if len(items) > MAX_IMAGES:
        raise ValueError("At most three evidence items are allowed.")
    if len({item.attachment_id for item in items}) != len(items):
        raise ValueError("Evidence identities must be unique.")
    if sum(item.decoded_size for item in items) > MAX_TOTAL_BYTES:
        raise ValueError("Evidence exceeds aggregate decoded byte limit.")
    if sum(len(item.data_base64) for item in items) > MAX_TOTAL_BASE64_CHARACTERS:
        raise ValueError("Evidence exceeds aggregate Base64 limit.")
    return items
