import csv, json, math, sys

SIGNALS = ["burstiness","type-token-ratio","lexical-entropy","compression-ratio",
           "ngram-repetition","onnx-classifier","perplexity"]
weights_doc = json.load(open("src/IAdetector.Infrastructure/Ensemble/EnsembleWeights.json"))
W = weights_doc["weights"]; B = weights_doc["intercept"]

def probs(path):
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

"""Evalúa un features.csv en español replicando el ensemble de producción
(intercept + coeficientes calibrados, imputación 0.5 ya aplicada en el CSV).
La réplica se valida contra HC3: debe reproducir ≈0.969 de accuracy.
Uso: python evaluate_spanish.py <features_es.csv> [...]
"""
# Validación de la réplica contra HC3: debe reproducir ~0.968 de accuracy
hc3 = probs("scripts/calibration/data/features.csv")
acc = sum(1 for y, p, _ in hc3 if (p >= 0.5) == (y == 1)) / len(hc3)
print(f"Réplica en HC3: accuracy={acc:.4f} (esperado ≈0.968)")

for path in sys.argv[1:]:
    data = probs(path)
    hum = [p for y, p, _ in data if y == 0]
    ai = [p for y, p, _ in data if y == 1]
    fp = sum(1 for p in hum if p >= 0.5)
    fn = sum(1 for p in ai if p < 0.5)
    print(f"\n== {path}")
    print(f"Humanos n={len(hum)}: prob. IA media={sum(hum)/len(hum):.3f} | falsos positivos (≥0.5): {fp}/{len(hum)} = {fp/len(hum):.1%}")
    print(f"IA      n={len(ai)}: prob. IA media={sum(ai)/len(ai):.3f} | no detectados (<0.5): {fn}/{len(ai)} = {fn/len(ai):.1%}")
    for s in SIGNALS:
        hv = [float(r[s]) for y, _, r in data if y == 0 and r[f"{s}_hasdata"] == "1"]
        av = [float(r[s]) for y, _, r in data if y == 1 and r[f"{s}_hasdata"] == "1"]
        if hv and av:
            print(f"  {s}: media humanos={sum(hv)/len(hv):.3f} (n={len(hv)}) | media IA={sum(av)/len(av):.3f} (n={len(av)})")
