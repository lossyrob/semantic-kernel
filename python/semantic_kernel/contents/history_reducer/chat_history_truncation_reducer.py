# Copyright (c) Microsoft. All rights reserved.

import logging
import sys
from typing import Any

from semantic_kernel.utils.experimental_decorator import experimental_class

if sys.version < "3.11":
    from typing_extensions import Self  # pragma: no cover
else:
    from typing import Self  # type: ignore # pragma: no cover

from semantic_kernel.contents.history_reducer.chat_history_reducer import ChatHistoryReducer
from semantic_kernel.contents.history_reducer.chat_history_reducer_utils import (
    extract_range,
    locate_safe_reduction_index,
)

logger = logging.getLogger(__name__)


@experimental_class
class ChatHistoryTruncationReducer(ChatHistoryReducer):
    """A ChatHistory that supports truncation logic.

    Because this class inherits from ChatHistoryReducer (which in turn inherits from ChatHistory),
    it can also be used anywhere a ChatHistory is expected, while adding truncation capability.
    """

    def __init__(self, target_count: int, threshold_count: int | None = None, **kwargs: Any):
        """Initialize the truncation reducer."""
        args: dict[str, Any] = {
            "target_count": target_count,
        }
        if threshold_count is not None:
            args["threshold_count"] = threshold_count
        super().__init__(**args, **kwargs)

    async def reduce(self) -> Self | None:
        """Truncate the chat history to the target message count, avoiding orphaned calls.

        Returns:
            The truncated list of messages if truncation occurred, or None otherwise.
        """
        history = self.messages
        if len(history) <= self.target_count + (self.threshold_count or 0):
            # No need to reduce
            return None

        logger.info("Performing chat history truncation check...")

        truncation_index = locate_safe_reduction_index(history, self.target_count, self.threshold_count)
        if truncation_index is None:
            logger.info(
                f"No truncation index found. Target count: {self.target_count}, Threshold: {self.threshold_count}"
            )
            return None

        logger.info(f"Truncating history to {truncation_index} messages.")
        truncated_list = extract_range(history, start=truncation_index)
        self.messages = truncated_list
        return self

    def __eq__(self, other: object) -> bool:
        """Compare equality based on truncation settings.

        (We don't factor in the actual ChatHistory messages themselves.)

        Returns:
            True if the other object is a ChatHistoryTruncationReducer with the same truncation settings.
        """
        if not isinstance(other, ChatHistoryTruncationReducer):
            return False
        return self.threshold_count == other.threshold_count and self.target_count == other.target_count

    def __hash__(self) -> int:
        """Return a hash code based on truncation settings.

        Returns:
            A hash code based on the truncation settings.
        """
        return hash((self.__class__.__name__, self.threshold_count, self.target_count))