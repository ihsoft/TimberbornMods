#!/usr/bin/env python3
"""Collect public Workshop gallery image URLs with a bounded incremental crawl."""

from __future__ import annotations

import argparse
from datetime import datetime, timedelta, timezone
import gzip
import html
import json
from pathlib import Path
import random
import re
import time
from urllib.error import HTTPError
from urllib.request import Request, urlopen


GALLERY_BLOCK = re.compile(r"var rgScreenshotURLs\s*=\s*\{(?P<body>.*?)\};", re.DOTALL)
GALLERY_URL = re.compile(r"https://images\.steamusercontent\.com/ugc/[^'\"\s]+")


class SteamThrottleError(RuntimeError):
    """Steam asked the crawler to stop making public item-page requests."""

    def __init__(self, status_code: int, retry_after_seconds: int | None):
        detail = f"; Retry-After={retry_after_seconds}" if retry_after_seconds else ""
        super().__init__(f"HTTP {status_code}{detail}")
        self.status_code = status_code
        self.retry_after_seconds = retry_after_seconds


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--previous-results")
    parser.add_argument("--output", required=True)
    parser.add_argument("--max-pages", type=int, default=50)
    parser.add_argument("--max-images-per-map", type=int, default=8)
    parser.add_argument("--delay-seconds", type=float, default=20.0)
    parser.add_argument("--refresh-after-days", type=int, default=90)
    parser.add_argument("--max-consecutive-failures", type=int, default=3)
    parser.add_argument("--max-runtime-seconds", type=int, default=1_200)
    return parser.parse_args()


def open_json_lines(path: Path):
    if path.suffix == ".gz":
        return gzip.open(path, "rt", encoding="utf-8")
    return path.open("r", encoding="utf-8")


def read_json_lines(path: Path | None) -> list[dict]:
    if path is None or not path.is_file():
        return []
    with open_json_lines(path) as stream:
        return [json.loads(line) for line in stream if line.strip()]


def parse_utc(value: str | None) -> datetime | None:
    if not value:
        return None
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def normalize_gallery_url(value: str) -> str:
    base = html.unescape(value).split("?", 1)[0]
    return (
        base
        + "?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true"
    )


def parse_gallery_urls(page_html: str) -> list[str]:
    match = GALLERY_BLOCK.search(page_html)
    if not match:
        return []
    urls = (
        normalize_gallery_url(url_match.group())
        for url_match in GALLERY_URL.finditer(match.group("body"))
    )
    return list(dict.fromkeys(urls))


def fetch_gallery_urls(
    published_file_id: str, delay_seconds: float, deadline: float
) -> list[str]:
    url = f"https://steamcommunity.com/sharedfiles/filedetails/?id={published_file_id}&l=english"
    request = Request(url, headers={"User-Agent": "TimberbornMods-PublicWorkshopIndexer/1.0"})
    last_exception = None
    for attempt in range(3):
        if time.monotonic() >= deadline:
            raise TimeoutError("Gallery crawl time budget exhausted")
        if delay_seconds > 0:
            time.sleep(delay_seconds + random.random())
        try:
            timeout = max(1, min(20, int(deadline - time.monotonic())))
            with urlopen(request, timeout=timeout) as response:
                return parse_gallery_urls(response.read().decode("utf-8", errors="replace"))
        except HTTPError as exception:
            last_exception = exception
            if exception.code in (403, 429):
                retry_after = exception.headers.get("Retry-After")
                retry_after_seconds = (
                    int(retry_after) if retry_after and retry_after.isdigit() else None
                )
                raise SteamThrottleError(
                    exception.code, retry_after_seconds
                ) from exception
            if exception.code not in (500, 502, 503, 504):
                break
        except Exception as exception:
            last_exception = exception
        if attempt < 2:
            time.sleep(min(30, 5 * 2**attempt) + random.random())
    raise RuntimeError(f"Could not read Workshop page after 3 attempts: {last_exception}")


def needs_refresh(item: dict, previous: dict | None, refresh_before: datetime) -> bool:
    if previous is None:
        return True
    if previous.get("collection_state") in {"stale", "deferred"}:
        return True
    if previous.get("source_updated_at_utc") != item.get("updated_at_utc"):
        return True
    checked_at = parse_utc(previous.get("gallery_checked_at_utc"))
    return checked_at is None or checked_at < refresh_before


def candidate_key(item: dict, previous: dict | None) -> tuple:
    minimum = datetime.min.replace(tzinfo=timezone.utc)
    updated_at = parse_utc(item.get("updated_at_utc")) or minimum
    if previous and previous.get("source_updated_at_utc") != item.get("updated_at_utc"):
        return 0, 0
    if previous is None:
        return 1, -updated_at.timestamp()
    checked_at = parse_utc(previous.get("gallery_checked_at_utc")) or minimum
    return 2, checked_at.timestamp()


def throttle_policy(
    exception: SteamThrottleError, throttle_events: int, request_delay: float
) -> tuple[bool, int, float]:
    if exception.status_code == 403 or throttle_events >= 3:
        return True, 0, request_delay
    base_cooldown = exception.retry_after_seconds or 60
    cooldown = base_cooldown * 2 ** (throttle_events - 1)
    next_delay = min(120, max(20, request_delay * 2))
    return False, cooldown, next_delay


def main() -> int:
    args = parse_args()
    if args.max_pages < 1:
        raise ValueError("--max-pages must be positive")
    if args.max_images_per_map < 1 or args.max_images_per_map > 32:
        raise ValueError("--max-images-per-map must be between 1 and 32")
    if args.delay_seconds < 0:
        raise ValueError("--delay-seconds cannot be negative")
    if args.max_consecutive_failures < 1:
        raise ValueError("--max-consecutive-failures must be positive")
    if args.max_runtime_seconds < 1:
        raise ValueError("--max-runtime-seconds must be positive")

    maps = [
        item
        for item in read_json_lines(Path(args.snapshot))
        if item.get("primary_category") == "map"
    ]
    previous_records = read_json_lines(
        Path(args.previous_results) if args.previous_results else None
    )
    previous_by_id = {record["published_file_id"]: record for record in previous_records}
    now = datetime.now(timezone.utc)
    refresh_before = now - timedelta(days=args.refresh_after_days)
    candidates = [
        item
        for item in maps
        if needs_refresh(item, previous_by_id.get(item["published_file_id"]), refresh_before)
    ]
    candidates.sort(
        key=lambda item: candidate_key(
            item, previous_by_id.get(item["published_file_id"])
        )
    )
    selected_ids = {
        item["published_file_id"] for item in candidates[: args.max_pages]
    }
    output_records = []
    fetched = 0
    failed = 0
    consecutive_failures = 0
    throttle_events = 0
    request_delay = args.delay_seconds
    fetching_enabled = True
    deadline = time.monotonic() + args.max_runtime_seconds
    for item in maps:
        published_file_id = item["published_file_id"]
        previous = previous_by_id.get(published_file_id)
        if time.monotonic() >= deadline:
            fetching_enabled = False
        if published_file_id not in selected_ids:
            if previous:
                output_records.append({**previous, "collection_state": "reused"})
            continue
        if not fetching_enabled:
            if previous:
                output_records.append({**previous, "collection_state": "deferred"})
            continue
        try:
            urls = fetch_gallery_urls(published_file_id, request_delay, deadline)
            output_records.append(
                {
                    "published_file_id": published_file_id,
                    "source_updated_at_utc": item.get("updated_at_utc"),
                    "gallery_checked_at_utc": now.isoformat(),
                    "gallery_urls": urls[: args.max_images_per_map],
                    "gallery_images_found": len(urls),
                    "gallery_truncated": len(urls) > args.max_images_per_map,
                    "collection_state": "fetched",
                }
            )
            fetched += 1
            consecutive_failures = 0
        except SteamThrottleError as exception:
            failed += 1
            throttle_events += 1
            if previous:
                output_records.append({**previous, "collection_state": "deferred"})
            stop, cooldown, request_delay = throttle_policy(
                exception, throttle_events, request_delay
            )
            if stop:
                fetching_enabled = False
                print(
                    f"Gallery crawl stopped after Steam throttling on "
                    f"{published_file_id}: {exception}",
                    flush=True,
                )
            else:
                cooldown = min(cooldown, max(0, deadline - time.monotonic()))
                print(
                    f"Steam rate limit on {published_file_id}; cooling down for "
                    f"{cooldown:.0f}s, then continuing with {request_delay:.0f}s delay",
                    flush=True,
                )
                time.sleep(cooldown)
        except Exception as exception:
            failed += 1
            consecutive_failures += 1
            print(f"Gallery {published_file_id} failed: {exception}", flush=True)
            if previous:
                output_records.append({**previous, "collection_state": "stale"})
            if consecutive_failures >= args.max_consecutive_failures:
                fetching_enabled = False
                print(
                    "Gallery crawl stopped after consecutive failures; remaining pages are deferred",
                    flush=True,
                )
        if published_file_id in selected_ids and (fetched + failed) % 25 == 0:
            print(
                f"Gallery progress: {fetched + failed} / {len(selected_ids)} pages; "
                f"fetched {fetched}, failed {failed}",
                flush=True,
            )

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as stream:
        for record in output_records:
            serialized = json.dumps(
                record, ensure_ascii=False, separators=(",", ":")
            )
            stream.write(serialized + "\n")
    print(
        f"Wrote {len(output_records)} gallery records; selected {len(selected_ids)}, "
        f"fetched {fetched}, failed {failed}, remaining {max(0, len(candidates) - fetched - failed)}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
