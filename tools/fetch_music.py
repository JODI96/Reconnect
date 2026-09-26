"""Downloads the room music (CC0, OpenGameArt) into client/Assets/ThirdParty/Music/<theme>.<ext>.

Usage: python tools/fetch_music.py
Each track's page is checked for a CC0 licence before anything is downloaded; the chosen file is the first
.ogg (else .mp3, .flac, .wav) attachment. Writes Music/CREDITS.md with title, page and licence. Idempotent.
"""
import html
import os
import re
import urllib.request

# Room theme id -> OpenGameArt page. Themes: see RoomThemes (backend) / RoomTheme.For (client).
TRACKS = {
    "lobby": "https://opengameart.org/content/elevator-music",
    "cafe": "https://opengameart.org/content/bossa-nova",
    "skylounge": "https://opengameart.org/content/chill-lofi-inspired-loop-edit",
    "rooftop": "https://opengameart.org/content/calm-relax-1-synthwave-421k",
    "pool": "https://opengameart.org/content/aquaria",
    "library": "https://opengameart.org/content/calm-piano-1-vaporware",
    "opera": "https://opengameart.org/content/harpsichord-flurry",
    "atelier": "https://opengameart.org/content/frets",
    "coworking": "https://opengameart.org/content/menu-chill-music",
    "conference": "https://opengameart.org/content/calm-ambient-2-synthwave-15k",
    "default": "https://opengameart.org/content/calm-ambient-1-synthwave-4k",
}
ROOT = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "ThirdParty", "Music")
HEADERS = {"User-Agent": "Reconnect asset fetch (CC0 music)"}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=60) as response:
        return response.read()


def main():
    os.makedirs(ROOT, exist_ok=True)
    credits = ["# Room music", "", "All tracks CC0 1.0 (public domain) from OpenGameArt.org – no attribution required.", ""]
    for theme, page in TRACKS.items():
        text = get(page).decode("utf-8", "replace")
        licence = re.search(r'field-name-field-art-licenses.*?</div>\s*</div>\s*</div>', text, re.S)
        if not licence or "CC0" not in licence.group(0):
            raise SystemExit(f"{page}: no CC0 licence found – not downloading")
        title = html.unescape(re.search(r"<title>(.*?)\s*\|", text).group(1)).strip()
        files = re.findall(r'href="(https://opengameart\.org/sites/default/files/[^"]+\.(?:ogg|mp3|wav|flac))"', text)
        order = {".ogg": 0, ".mp3": 1, ".flac": 2, ".wav": 3}   # smallest first; Unity re-encodes to Vorbis anyway
        files.sort(key=lambda f: order[os.path.splitext(f)[1]])
        if not files:
            raise SystemExit(f"{page}: no audio file found")
        url = files[0]
        target = os.path.join(ROOT, theme + os.path.splitext(url)[1])
        if not os.path.exists(target):
            with open(target, "wb") as f:
                f.write(get(url))
        credits.append(f"- **{theme}**: [{title}]({page}) – `{os.path.basename(url)}` – CC0")
        print(f"{theme}: {title} -> {os.path.basename(target)} ({os.path.getsize(target) // 1024} KB)")
    with open(os.path.join(ROOT, "CREDITS.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(credits) + "\n")


if __name__ == "__main__":
    main()
