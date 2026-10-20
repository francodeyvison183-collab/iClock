# -*- coding: utf-8 -*-
"""Regenerate docs/en/index.html from docs/index.html (static English mirror).

Run after editing docs/index.html:  python scripts/build_en_page.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "docs" / "index.html"
DST = ROOT / "docs" / "en" / "index.html"
BASE = "https://francodeyvison183-collab.github.io/iClock/"
EN_URL = BASE + "en/"


def extract_en_dict(src: str) -> dict:
    m = re.search(r"\ben:\s*\{", src)
    if not m:
        raise SystemExit("en i18n dict not found")
    start = m.end() - 1
    depth = 0
    i = start
    while i < len(src):
        c = src[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                break
        elif c == '"':
            i += 1
            while i < len(src) and src[i] != '"':
                i += 2 if src[i] == "\\" else 1
        i += 1
    block = src[start + 1 : i]
    out = {}
    for line in block.splitlines():
        km = re.match(r'\s*([A-Za-z_][\w]*):\s*"(.*)",?\s*$', line)
        if km:
            out[km.group(1)] = km.group(2).replace('\\"', '"').replace("\\\\", "\\")
    return out


def replace_attr(src: str, pattern: str, value: str) -> str:
    new, n = re.subn(pattern, lambda m: m.group(1) + value + m.group(2), src, count=1)
    if n == 0:
        raise SystemExit(f"pattern not found: {pattern}")
    return new


def walk_replace(src: str, key: str, value: str) -> tuple[int, str]:
    """Replace inner content of all elements carrying data-i18n="key" (nesting-aware)."""
    open_re = re.compile(r"<([a-zA-Z][\w-]*)((?:[^>]*?)\bdata-i18n=\"" + re.escape(key) + r"\"(?:[^>]*?))>")
    count = 0
    pos = 0
    while True:
        m = open_re.search(src, pos)
        if not m:
            break
        tag = m.group(1)
        content_start = m.end()
        scan = content_start
        depth = 1
        tok = re.compile(r"<(/?)" + re.escape(tag) + r"([^>]*)>")
        close_start = None
        for t in tok.finditer(src, scan):
            if t.group(1) == "/":
                depth -= 1
                if depth == 0:
                    close_start = t.start()
                    break
            elif t.group(2).rstrip().endswith("/"):
                continue
            else:
                depth += 1
        if close_start is None:
            break
        src = src[:content_start] + "\n            " + value + "\n          " + src[close_start:]
        count += 1
        pos = content_start + len(value)
    return count, src


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    src = SRC.read_text(encoding="utf-8")
    en = extract_en_dict(src)

    title = en["page_title"]
    desc = en["page_desc"]

    src = src.replace('<html lang="zh-CN" data-theme="light">', '<html lang="en" data-theme="light">', 1)
    src = replace_attr(src, r"(<title[^>]*>)[^<]*(</title>)", title)
    src = replace_attr(src, r'(<meta name="description"[^>]*?content=")[^"]*(")', desc)
    src = replace_attr(src, r'(<link rel="canonical" href=")[^"]*(")', EN_URL)
    src = replace_attr(src, r'(<meta property="og:url" content=")[^"]*(")', EN_URL)
    src = replace_attr(src, r'(<meta property="og:locale" content=")[^"]*(")', "en_US")
    src = replace_attr(src, r'(<meta property="og:locale:alternate" content=")[^"]*(")', "zh_CN")
    src = replace_attr(src, r'(<meta property="og:title" content=")[^"]*(")', title)
    src = replace_attr(src, r'(<meta property="og:description" content=")[^"]*(")', desc)
    src = replace_attr(src, r'(<meta property="og:image:alt" content=")[^"]*(")', "iClock floating countdown timer over a Windows desktop")
    src = replace_attr(src, r'(<meta name="twitter:title" content=")[^"]*(")', title)
    src = replace_attr(src, r'(<meta name="twitter:description" content=")[^"]*(")', desc)
    src = replace_attr(src, r'(<meta name="twitter:image:alt" content=")[^"]*(")', "iClock floating countdown timer over a Windows desktop")

    # English alt texts
    src = src.replace('alt="iClock 真实桌面运行效果"', 'alt="iClock floating overlay on a real Windows desktop"')
    src = src.replace('alt="iClock 微信赞赏码"', 'alt="iClock WeChat sponsor QR code"')

    # MCP setup prompt block
    prompt_en = (
        "Please connect my local desktop productivity tool iClock to the MCP Server list of the current environment.\n"
        'The GUI and stdio MCP bridge are the same iClock.exe in this unzipped directory; launch it with arguments ["--mcp"]. The bridge connects to the GUI over a local Named Pipe.\n'
        "The server natively provides 6 tools: iclock_ask_human (topmost desktop decision gate), iclock_progress (project task steps and progress onto the floating clock HUD), iclock_timer (set & query countdowns), iclock_notify (milestone desktop notifications with chime), iclock_quote (desktop quote rotation & expansion), and iclock_status (runtime status).\n"
        "After configuration, please call the iclock_status tool directly to verify the connection."
    )
    src, n_prompt = re.subn(
        r'(<pre><code id="codePrompt">).*?(</code></pre>)',
        lambda m: m.group(1) + prompt_en + m.group(2),
        src,
        count=1,
        flags=re.S,
    )
    if n_prompt == 0:
        raise SystemExit("codePrompt block not found")

    # Static defaults that are not wired to data-i18n (most are runtime-relocalized by JS,
    # but crawlers without JS must still see English on the /en/ page)
    static_pairs = [
        ('>🏆 官方推荐 · 极简原生<', '>🏆 Editor’s Pick · Minimal & Native<'),
        ('✘</span> 无</td>', '✘</span> None</td>'),
        ('✘</span> 仅限网页内部</td>', '✘</span> Web-only</td>'),
        ('>🎲 换一句格言</button>', '>🎲 Next Quote</button>'),
        ('>📋 复制格言</button>', '>📋 Copy Quote</button>'),
        ('>智能体接入提示词</button>', '>Agent Setup Prompt</button>'),
        ('iclock --start [分钟]', 'iclock --start [minutes]'),
        ('iclock -n &lt;消息&gt;', 'iclock -n &lt;message&gt;'),
        ('iclock -n "生产构建已完成！"', 'iclock -n "Production build finished!"'),
        ('托盘图标 + 滚轮滚动', 'Tray Icon + Mouse Wheel'),
        ('+ 鼠标左键拖拽', '+ Left-drag'),
        ('+ 滚轮', '+ Mouse Wheel'),
        ('>已成功复制到剪贴板<', '>' + en['toast_copied'] + '<'),
        ('>“至繁归于至简，方能行远。”<', '>“Simplicity is the ultimate sophistication.”<'),
        ('>— 列奥纳多·达·芬奇<', '>— Leonardo da Vinci<'),
        ('>▶ 开始倒计时</button>', '>' + en['sim_btn_start'] + '</button>'),
        ('>↺ 重置</button>', '>' + en['sim_btn_reset'] + '</button>'),
        ('>格式: MM:SS</button>', '>' + en['sim_format_prefix'] + 'MM:SS</button>'),
        ('>按 Ctrl+Shift+T 或下方按钮启停<', '>' + en['sim_status_idle'] + '<'),
    ]
    for zh_s, en_s in static_pairs:
        src = src.replace(zh_s, en_s)

    # JSON-LD: localize FAQ answers/questions (schema stays valid JSON)
    def localize_jsonld(m: re.Match) -> str:
        data = json.loads(m.group(1))
        for node in data.get("@graph", []):
            if node.get("@type") == "FAQPage":
                qs = []
                for idx in range(1, 6):
                    qs.append({
                        "@type": "Question",
                        "name": en[f"faq_q{idx}"],
                        "acceptedAnswer": {"@type": "Answer", "text": en[f"faq_a{idx}"]},
                    })
                node["mainEntity"] = qs
        return '<script type="application/ld+json">\n' + json.dumps(data, ensure_ascii=False, indent=2) + "\n  </script>"

    src, n_jsonld = re.subn(r'<script type="application/ld\+json">(.*?)</script>', localize_jsonld, src, count=1, flags=re.S)
    if n_jsonld == 0:
        raise SystemExit("JSON-LD block not found")

    # Language pills: cross-URL navigation
    src = src.replace(
        '<button class="lang-pill-item active" id="btnLangZh" onclick="switchLanguage(\'zh\')">中文</button>',
        '<a class="lang-pill-item" id="btnLangZh" href="../" hreflang="zh-CN">中文</a>',
        1,
    )
    src = src.replace(
        '<a class="lang-pill-item" id="btnLangEn" href="./en/" hreflang="en">EN</a>',
        '<button class="lang-pill-item active" id="btnLangEn" onclick="switchLanguage(\'en\')">EN</button>',
        1,
    )

    # Rewrite relative asset links for the /en/ subdirectory
    src = re.sub(r'((?:href|src|srcset)=")(?!https?:|mailto:|#|data:|\.)([^"]+)"', r'\g<1>../\g<2>"', src)

    # Static translation of every data-i18n element (crawler-visible English)
    missing = []
    for key, value in en.items():
        _, src = walk_replace(src, key, value)
    for key in set(re.findall(r'data-i18n="([^"]+)"', src)):
        if key not in en:
            missing.append(key)

    DST.parent.mkdir(parents=True, exist_ok=True)
    DST.write_text(src, encoding="utf-8", newline="\n")
    print(f"wrote {DST.relative_to(ROOT)} ({len(src)} bytes)")
    if missing:
        print("WARNING: no EN translation for keys:", ", ".join(sorted(missing)))


if __name__ == "__main__":
    main()
