#!/usr/bin/env python3
"""
Calcula los rangos de referencia humanos de cada señal (p10, mediana, p90 de la
puntuación normalizada) desde features.csv, usando solo las respuestas humanas
(label=0) en las que la señal midió de verdad (<señal>_hasdata=1).

El resultado se embebe en IAdetector.Infrastructure y lo usa DetectionExplainer
para decirle a un agente si una señal cae dentro de lo típico en texto humano
de este corpus, y cuánto se sale. Son referencias de HC3 (inglés), no universales.

Uso: python make_reference_ranges.py <features.csv> <HumanReferenceRanges.json>
"""
import csv
import json
import sys

SIGNALS = [
    "burstiness", "type-token-ratio", "lexical-entropy", "compression-ratio",
    "ngram-repetition", "onnx-classifier", "perplexity",
]


def percentile(sorted_values, q):
    if not sorted_values:
        return None
    k = (len(sorted_values) - 1) * q
    lo = int(k)
    hi = min(lo + 1, len(sorted_values) - 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (k - lo)


def main():
    if len(sys.argv) < 3:
        print(f"Uso: {sys.argv[0]} <features.csv> <salida.json>", file=sys.stderr)
        return 1
    ranges = {}
    with open(sys.argv[1], newline="", encoding="utf-8") as f:
        rows = [r for r in csv.DictReader(f) if r["label"] == "0"]
    for name in SIGNALS:
        values = sorted(
            float(r[name]) for r in rows if r.get(f"{name}_hasdata") == "1")
        if len(values) < 30:
            print(f"AVISO: {name} solo tiene {len(values)} muestras humanas con datos; se omite.")
            continue
        ranges[name] = {
            "p10": round(percentile(values, 0.10), 6),
            "median": round(percentile(values, 0.50), 6),
            "p90": round(percentile(values, 0.90), 6),
            "samples": len(values),
        }
        print(f"{name}: n={len(values)} p10={ranges[name]['p10']} "
              f"mediana={ranges[name]['median']} p90={ranges[name]['p90']}")
    doc = {
        "source": "HC3 wiki_csai, respuestas humanas con datos por señal (features.csv, Fase 1.2)",
        "ranges": ranges,
    }
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
    print(f"Escrito: {sys.argv[2]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
