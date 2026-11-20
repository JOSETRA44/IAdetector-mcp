#!/usr/bin/env python3
"""Ensamblado del corpus multigenerador de la Fase 2.1 (IAdetector).

Combina los 3 archivos train_es_part{1,2,3}.jsonl (Fase 3.2: humanos + Gemini)
con generated_chatgpt.jsonl y generated_claude.jsonl. Para cada uno de los
232 temas de IA, `chatgpt_answers` pasa a ser [gemini, chatgpt, claude] (la
clave conserva el nombre histórico del formato HC3; la procedencia de cada
elemento se documenta en train_es_21.provenance.txt). Los archivos originales
de la 3.2 NO se modifican.
"""
import json
import re
import sys
from datetime import date
from pathlib import Path

BASE = Path.home() / "workspace/iadetector-mcp/scripts/calibration/data/es"
MIN_W, MAX_W = 195, 285


def wc(text):
    return len(re.findall(r"\S+", text))


def load_jsonl(path):
    rows = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                r = json.loads(line)
                rows[r["question"]] = r["text"]
    return rows


def main():
    chatgpt = load_jsonl(BASE / "generated_chatgpt.jsonl")
    claude = load_jsonl(BASE / "generated_claude.jsonl")
    print(f"chatgpt={len(chatgpt)} claude={len(claude)}")

    total_q, total_h, total_ai = 0, 0, 0
    bad = []
    for part in (1, 2, 3):
        src = BASE / f"train_es_part{part}.jsonl"
        dst = BASE / f"train_es_part{part}_21.jsonl"
        out_lines = []
        for line in src.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            row = json.loads(line)
            total_q += 1
            total_h += len(row.get("human_answers", []))
            if "chatgpt_answers" in row:
                q = row["question"]
                gemini = row["chatgpt_answers"]
                assert len(gemini) == 1, f"{q}: se esperaban 1 texto Gemini, hay {len(gemini)}"
                if q not in chatgpt or q not in claude:
                    bad.append(q)
                    continue
                for src_name, txt in (("chatgpt", chatgpt[q]), ("claude", claude[q])):
                    n = wc(txt)
                    if not (MIN_W <= n <= MAX_W):
                        bad.append(f"{q} [{src_name} {n}w]")
                row["chatgpt_answers"] = [gemini[0], chatgpt[q], claude[q]]
                total_ai += 3
            out_lines.append(json.dumps(row, ensure_ascii=False))
        dst.write_text("\n".join(out_lines) + "\n", encoding="utf-8")
        print(f"escrito {dst.name}: {len(out_lines)} preguntas")

    print(f"preguntas={total_q} humanos={total_h} IA={total_ai} total={total_h + total_ai}")
    if bad:
        print(f"PROBLEMAS ({len(bad)}): {bad[:10]}")
        return 1
    assert total_q == 371 and total_h == 336 and total_ai == 696, "conteos inesperados"
    print("conteos OK: 371 preguntas, 336 humanos, 696 IA (232×3), total 1.032")

    prov = BASE / "train_es_21.provenance.txt"
    prov.write_text(f"""Corpus de calibración en español (Fase 2.1) — train_es_part{{1,2,3}}_21.jsonl
========================================================================
Generado el {date.today().isoformat()}. Grupos (question = tema) mezclados con
semilla 42 y partidos en 3, igual que en la Fase 3.2; features_train_21.csv es
la unión con question_id renumerado de forma única (371 grupos, 1.032 filas).
Los archivos train_es_part{{1,2,3}}.jsonl de la 3.2 quedan intactos.

Humanos (336, sin cambios respecto a la 3.2):
- 244 muestras de la Wikipedia en español, última revisión de cada artículo
  de 2021 o anterior: 123 temas × 2 muestras de 200-300 palabras
  (source: wikipedia_es_rev<=2021).
- 92 muestras de dominio público (Project Gutenberg): Don Quijote #2000 (45),
  La Regenta #17073 (35), Lazarillo de Tormes #320 (12), en subgrupos por obra.

IA (696 = 232 temas × 3 generadores, 200-280 palabras, prosa expositiva):
- Gemini (Antigravity CLI), generado el 2026-11-09/10 en la Fase 3.2, reutilizado
  sin cambios. En cada fila es el elemento [0] de `chatgpt_answers`.
- Claude (Claude Code CLI, modelo Opus, lotes de 6 temas), generado el
  2026-11-13 con scripts/calibration/generate_claude.py. Elemento [2].
- ChatGPT (Codex CLI, modelo gpt-6.1-terra, suscripción ChatGPT Pro, lotes de
  6 temas), generado el 2026-11-19 con scripts/calibration/generate_chatgpt.py.
  Elemento [1].

Nota sobre el nombre de la clave: `chatgpt_answers` es el nombre histórico del
formato HC3 para las respuestas de IA; en la 2.1 contiene [gemini, chatgpt,
claude] en ese orden. La procedencia por generador queda en este archivo y en
generated_chatgpt.jsonl / generated_claude.jsonl (campo `source` por texto).

Held-out principal: el de la Fase 3 (es_eval.jsonl / features_before.csv: 21
humanos de Gutenberg + 18 de IA de Muse Spark, generados el 2026-11-09, familia
Meta, NO presente en este entrenamiento). Línea base: detección 12/18,
falsos positivos 1/21.
""", encoding="utf-8")
    print(f"escrito {prov.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
