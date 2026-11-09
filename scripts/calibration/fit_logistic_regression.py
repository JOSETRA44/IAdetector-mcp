#!/usr/bin/env python3
"""
Ajusta una regresión logística sobre las señales extraídas por IAdetector.Calibration
(ver tools/IAdetector.Calibration) y exporta los coeficientes a EnsembleWeights.json,
consumido por CalibratedEnsembleScorer en tiempo de ejecución (C#).

Uso:
    python fit_logistic_regression.py <features.csv> <EnsembleWeights.json de salida>

Evaluación (Fase 1.2): la validación se hace con GroupKFold por pregunta cuando el CSV
trae question_id, para que las respuestas a una misma pregunta no queden a ambos lados
del split (con filas sueltas, la CV anterior inflaba la accuracy: las respuestas
hermanas se "filtran" entre entrenamiento y validación). Además de la accuracy se
reportan métricas de calibración de verdad (Brier, ECE) y de discriminación (AUROC),
calculadas sobre predicciones fuera de fold. A modo de contraste se reporta también la
accuracy con KFold por filas, que es la cifra comparable con la calibración original.
"""
import csv
import json
import sys

from sklearn.linear_model import LogisticRegression
from sklearn.metrics import brier_score_loss, roc_auc_score
from sklearn.model_selection import GroupKFold, KFold, cross_val_predict

SIGNAL_NAMES = [
    "burstiness",
    "type-token-ratio",
    "lexical-entropy",
    "compression-ratio",
    "ngram-repetition",
    "onnx-classifier",
    "perplexity",
]


def load_csv(path):
    labels, features, groups = [], [], []
    hasdata_counts = {name: 0 for name in SIGNAL_NAMES}
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        has_groups = "question_id" in (reader.fieldnames or [])
        for row in reader:
            labels.append(int(row["label"]))
            features.append([float(row[name]) for name in SIGNAL_NAMES])
            groups.append(row["question_id"] if has_groups else None)
            for name in SIGNAL_NAMES:
                column = f"{name}_hasdata"
                if column in row and row[column] == "1":
                    hasdata_counts[name] += 1
    return features, labels, (groups if has_groups else None), hasdata_counts


def expected_calibration_error(y_true, y_prob, bins=10):
    """ECE con contenedores de ancho uniforme sobre la probabilidad predicha."""
    ece = 0.0
    n = len(y_true)
    for b in range(bins):
        lo, hi = b / bins, (b + 1) / bins
        idx = [i for i, p in enumerate(y_prob) if (lo <= p < hi) or (b == bins - 1 and p == 1.0)]
        if not idx:
            continue
        bin_accuracy = sum(y_true[i] for i in idx) / len(idx)
        bin_confidence = sum(y_prob[i] for i in idx) / len(idx)
        ece += (len(idx) / n) * abs(bin_accuracy - bin_confidence)
    return ece


def evaluate(y_true, y_prob):
    predicted = [1 if p >= 0.5 else 0 for p in y_prob]
    accuracy = sum(1 for p, t in zip(predicted, y_true) if p == t) / len(y_true)
    return {
        "accuracy": accuracy,
        "brier": brier_score_loss(y_true, y_prob),
        "ece": expected_calibration_error(y_true, y_prob),
        "auroc": roc_auc_score(y_true, y_prob),
    }


def main():
    if len(sys.argv) < 3:
        print(f"Uso: {sys.argv[0]} <features.csv> <EnsembleWeights.json>", file=sys.stderr)
        return 1

    features_path, output_path = sys.argv[1], sys.argv[2]
    x, y, groups, hasdata_counts = load_csv(features_path)
    print(f"Muestras: {len(y)} (humanas={y.count(0)}, IA={y.count(1)})")
    if groups is not None:
        print(f"Preguntas distintas: {len(set(groups))}")
    for name in SIGNAL_NAMES:
        print(f"  Cobertura {name}: {hasdata_counts[name]}/{len(y)} con datos")

    model = LogisticRegression(max_iter=2000)

    # Predicciones fuera de fold con splits agrupados por pregunta: la evaluación honesta.
    if groups is not None:
        grouped_cv = GroupKFold(n_splits=5)
        grouped_prob = cross_val_predict(
            model, x, y, cv=grouped_cv, groups=groups, method="predict_proba")[:, 1]
        grouped = evaluate(y, grouped_prob)
        print(f"[Agrupado por pregunta] accuracy={grouped['accuracy']:.4f} "
              f"brier={grouped['brier']:.4f} ece={grouped['ece']:.4f} auroc={grouped['auroc']:.4f}")
    else:
        grouped = None
        print("AVISO: el CSV no trae question_id; no se puede validar agrupando por pregunta.")

    # Contraste: la validación por filas de la calibración original (comparable con el
    # cvAccuracy histórico), para medir cuánta inflación introducía la fuga entre folds.
    rows_cv = KFold(n_splits=5, shuffle=True, random_state=42)
    rows_prob = cross_val_predict(model, x, y, cv=rows_cv, method="predict_proba")[:, 1]
    rows = evaluate(y, rows_prob)
    print(f"[Por filas, como antes] accuracy={rows['accuracy']:.4f} "
          f"brier={rows['brier']:.4f} ece={rows['ece']:.4f} auroc={rows['auroc']:.4f}")

    model.fit(x, y)
    train_accuracy = model.score(x, y)
    print(f"Accuracy sobre el set de entrenamiento completo: {train_accuracy:.4f}")

    evaluation = {
        "method": "GroupKFold(5) por pregunta, predicciones fuera de fold" if grouped else
                  "KFold(5) por filas (sin question_id en el CSV)",
        "groupedByQuestion": grouped,
        "byRows": rows,
    }
    headline = grouped if grouped is not None else rows

    weights = {name: float(coef) for name, coef in zip(SIGNAL_NAMES, model.coef_[0])}
    output = {
        "intercept": float(model.intercept_[0]),
        "weights": weights,
        "trainedOn": {
            "samples": len(y),
            "cvAccuracy": float(headline["accuracy"]),
            "trainAccuracy": float(train_accuracy),
        },
        "evaluation": evaluation,
    }

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(output, f, indent=2, ensure_ascii=False)

    print(f"Escrito: {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
