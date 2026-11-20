#!/usr/bin/env python3
"""Generación de textos en español con Claude (Fase 2.1, IAdetector).

Lee topics_21.json, genera en lotes de 6 temas vía Claude Code (modelo Opus,
sin herramientas), valida longitud (195-285 palabras) y guarda el progreso de
forma incremental en generated_claude.jsonl para poder reanudar.
"""
import json
import re
import subprocess
import sys
import time
from pathlib import Path

BASE = Path.home() / "workspace/iadetector-mcp/scripts/calibration/data/es"
TOPICS = BASE / "topics_21.json"
OUT = BASE / "generated_claude.jsonl"
CLAUDE = str(Path.home() / ".local/bin/claude")
BATCH = 6
MIN_W, MAX_W = 195, 285

PROMPT = """Escribe textos expositivos en español para un corpus lingüístico.

Para cada uno de los temas siguientes, escribe UN texto de entre 220 y 260 palabras,
en prosa expositiva continua y neutra, de 2 o 3 párrafos. Sin títulos, sin Markdown,
sin listas, sin frases conversacionales («aquí tienes», «en conclusión», «espero que»):
solo el texto, como un artículo enciclopédico breve.

Temas:
{temas}

Responde ÚNICAMENTE con un array JSON válido, sin texto adicional ni bloques de código,
con un objeto por tema y en el mismo orden:
[{{"tema": "<tema exacto>", "texto": "<el texto>"}}]"""


def word_count(text: str) -> int:
    return len(re.findall(r"\S+", text))


def clean(text: str) -> str:
    text = text.strip()
    text = re.sub(r"^#+\s.*$", "", text, flags=re.M)  # por si cuela un título
    return text.strip()


def call_claude(topics):
    prompt = PROMPT.format(temas="\n".join(f"{i+1}. {t}" for i, t in enumerate(topics)))
    res = subprocess.run(
        [CLAUDE, "-p", prompt, "--model", "opus", "--tools", ""],
        capture_output=True, text=True, timeout=900,
    )
    if res.returncode != 0:
        raise RuntimeError(f"claude rc={res.returncode}: {res.stderr[-400:]}")
    raw = res.stdout.strip()
    m = re.search(r"\[.*\]", raw, flags=re.S)
    if not m:
        raise RuntimeError(f"sin JSON en la salida: {raw[:200]}")
    return json.loads(m.group(0))


def main():
    topics = json.loads(TOPICS.read_text(encoding="utf-8"))
    done = {}
    if OUT.exists():
        for line in OUT.read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                done[r["question"]] = r["text"]
    pending = [t for t in topics if t not in done]
    print(f"total={len(topics)} hechos={len(done)} pendientes={len(pending)}", flush=True)

    with OUT.open("a", encoding="utf-8") as fh:
        for i in range(0, len(pending), BATCH):
            batch = pending[i:i + BATCH]
            try:
                items = call_claude(batch)
            except Exception as e:
                print(f"ERROR lote {batch[:1]}: {e}", flush=True)
                items = []
            got = {it["tema"]: clean(it["texto"]) for it in items if "tema" in it and "texto" in it}
            for t in batch:
                txt = got.get(t, "")
                wc = word_count(txt)
                if not (MIN_W <= wc <= MAX_W):
                    # reintento individual con ajuste de longitud
                    try:
                        one = call_claude([t])
                        txt2 = clean(one[0]["texto"]) if one else ""
                        if MIN_W <= word_count(txt2) <= MAX_W:
                            txt, wc = txt2, word_count(txt2)
                    except Exception as e:
                        print(f"  reintento falló {t!r}: {e}", flush=True)
                if MIN_W <= wc <= MAX_W:
                    fh.write(json.dumps({"question": t, "source": "claude", "text": txt},
                                        ensure_ascii=False) + "\n")
                    fh.flush()
                    print(f"OK {wc}w {t!r}", flush=True)
                else:
                    print(f"DESCARTADO ({wc}w) {t!r}", flush=True)
            time.sleep(3)
    print("FIN", flush=True)


if __name__ == "__main__":
    sys.exit(main())
