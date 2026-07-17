#!/usr/bin/env python3
"""
Ajusta una regresión logística sobre las señales extraídas por IAdetector.Calibration
(ver tools/IAdetector.Calibration) y exporta los coeficientes a EnsembleWeights.json,
consumido por CalibratedEnsembleScorer en tiempo de ejecución (C#).

Uso:
    python fit_logistic_regression.py <features.csv> <EnsembleWeights.json de salida>
"""
import csv
import json
import sys

from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import cross_val_score

SIGNAL_NAMES = [
    "burstiness",
    "type-token-ratio",
    "lexical-entropy",
    "compression-ratio",
    "ngram-repetition",
    "onnx-classifier",
]


def load_csv(path):
    labels, features = [], []
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            labels.append(int(row["label"]))
            features.append([float(row[name]) for name in SIGNAL_NAMES])
    return features, labels


def main():
    if len(sys.argv) < 3:
        print(f"Uso: {sys.argv[0]} <features.csv> <EnsembleWeights.json>", file=sys.stderr)
        return 1

    features_path, output_path = sys.argv[1], sys.argv[2]
    x, y = load_csv(features_path)
    print(f"Muestras: {len(y)} (humanas={y.count(0)}, IA={y.count(1)})")

    model = LogisticRegression(max_iter=2000)

    # Validación cruzada (5 folds) solo para reportar qué tan bien generaliza antes de
    # entrenar con todo el dataset para los coeficientes finales.
    scores = cross_val_score(model, x, y, cv=5, scoring="accuracy")
    print(f"Accuracy validación cruzada (5-fold): {scores.mean():.4f} (+/- {scores.std():.4f})")

    model.fit(x, y)
    train_accuracy = model.score(x, y)
    print(f"Accuracy sobre el set de entrenamiento completo: {train_accuracy:.4f}")

    weights = {name: float(coef) for name, coef in zip(SIGNAL_NAMES, model.coef_[0])}
    output = {
        "intercept": float(model.intercept_[0]),
        "weights": weights,
        "trainedOn": {
            "samples": len(y),
            "cvAccuracy": float(scores.mean()),
            "trainAccuracy": float(train_accuracy),
        },
    }

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(output, f, indent=2, ensure_ascii=False)

    print(f"Escrito: {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
