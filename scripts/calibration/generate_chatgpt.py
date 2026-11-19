#!/usr/bin/env python3
"""Generación de textos en español con ChatGPT vía Codex CLI (Fase 2.1, IAdetector).

Lee topics_21.json, genera en lotes de 6 temas vía `codex exec` (modelo por
defecto de la suscripción ChatGPT Pro), valida longitud (195-285 palabras) y
guarda el progreso de forma incremental en generated_chatgpt.jsonl para poder
reanudar. La salida del agente se recoge con -o (último mensaje) para no
tener que parsear el banner de Codex.
"""
import json
import re
import subprocess
import sys
import time
from pathlib import Path

BASE = Path.home() / "workspace/iadetector-mcp/scripts/calibration/data/es"
TOPICS = BASE / "topics_21.json"
OUT = BASE / "generated_chatgpt.jsonl"
LASTMSG = Path.home() / "workspace/generate-chatgpt-21-lastmsg.txt"
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


def extract_json(raw: str):
    raw = raw.strip()
    raw = re.sub(r"^```(?:json)?\s*", "", raw, flags=re.I)
    raw = re.sub(r"\s*```$", "", raw)
    m = re.search(r"\[.*\]", raw, flags=re.S)
    if not m:
        raise RuntimeError(f"sin JSON en la salida: {raw[:200]}")
    return json.loads(m.group(0))


def call_codex(topics):
    prompt = PROMPT.format(temas="\n".join(f"{i+1}. {t}" for i, t in enumerate(topics)))
    if LASTMSG.exists():
        LASTMSG.unlink()
    res = subprocess.run(
        ["codex", "exec", "--ephemeral", "--color", "never",
         "-o", str(LASTMSG), prompt],
        stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=900,
        cwd=str(Path.home() / "workspace/iadetector-mcp"),
    )
    if not LASTMSG.exists():
        raise RuntimeError(f"codex rc={res.returncode} sin -o: {res.stdout[-300:]} {res.stderr[-300:]}")
    raw = LASTMSG.read_text(encoding="utf-8")
    return extract_json(raw)


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
                items = call_codex(batch)
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
                        one = call_codex([t])
                        txt2 = clean(one[0]["texto"]) if one else ""
                        if MIN_W <= word_count(txt2) <= MAX_W:
                            txt, wc = txt2, word_count(txt2)
                    except Exception as e:
                        print(f"  reintento falló {t!r}: {e}", flush=True)
                if MIN_W <= wc <= MAX_W:
                    fh.write(json.dumps({"question": t, "source": "chatgpt", "text": txt},
                                        ensure_ascii=False) + "\n")
                    fh.flush()
                    print(f"OK {wc}w {t!r}", flush=True)
                else:
                    print(f"DESCARTADO ({wc}w) {t!r}", flush=True)
            time.sleep(2)
    print("FIN", flush=True)


if __name__ == "__main__":
    sys.exit(main())
