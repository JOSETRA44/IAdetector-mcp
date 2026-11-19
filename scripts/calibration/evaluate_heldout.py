#!/usr/bin/env python3
"""Evalúa un features.csv con unos pesos dados (réplica del ensemble de producción).

Igual que evaluate_spanish.py pero con --weights para poder evaluar candidatos
sin tocar los pesos de producción. Reproduce la lógica de producción: logit con
intercept + coeficientes; si ninguna señal tiene datos, p = 0.5.
Uso: python3 evaluate_heldout.py <pesos.json> <features.csv> [...]
"""
import csv
import json
import math
import sys

SIGNALS = ["burstiness", "type-token-ratio", "lexical-entropy", "compression-ratio",
           "ngram-repetition", "onnx-classifier", "perplexity"]


def probs(path, W, B):
    rows = list(csv.DictReader(open(path)))
    out = []
    for r in rows:
        if not any(r[f"{s}_hasdata"] == "1" for s in SIGNALS):
            p = 0.5
        else:
            logit = B + sum(W[s] * float(r[s]) for s in SIGNALS)
            p = 1 / (1 + math.exp(-logit))
        out.append((int(r["label"]), p, r))
    return out


def main():
    weights_path = sys.argv[1]
    weights_doc = json.load(open(weights_path))
    W, B = weights_doc["weights"], weights_doc["intercept"]
    print(f"Pesos: {weights_path} (intercept={B:.4f})")
    for path in sys.argv[2:]:
        data = probs(path, W, B)
        hum = [p for y, p, _ in data if y == 0]
        ai = [p for y, p, _ in data if y == 1]
        fp = sum(1 for p in hum if p >= 0.5)
        det = sum(1 for p in ai if p >= 0.5)
        print(f"\n== {path}")
        print(f"Humanos n={len(hum)}: prob. IA media={sum(hum)/len(hum):.3f} | "
              f"falsos positivos (≥0.5): {fp}/{len(hum)} = {fp/len(hum):.1%}")
        print(f"IA      n={len(ai)}: prob. IA media={sum(ai)/len(ai):.3f} | "
              f"detectados (≥0.5): {det}/{len(ai)} = {det/len(ai):.1%}")
        for s in SIGNALS:
            hv = [float(r[s]) for y, _, r in data if y == 0 and r[f"{s}_hasdata"] == "1"]
            av = [float(r[s]) for y, _, r in data if y == 1 and r[f"{s}_hasdata"] == "1"]
            if hv and av:
                print(f"  {s}: media humanos={sum(hv)/len(hv):.3f} (n={len(hv)}) | "
                      f"media IA={sum(av)/len(av):.3f} (n={len(av)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
