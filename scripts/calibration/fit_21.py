#!/usr/bin/env python3
"""Recalibración de la Fase 2.1: compara LogisticRegression con pesos unitarios
frente a class_weight='balanced', con GroupKFold(5) por pregunta.

Reutiliza load_csv/evaluate de fit_logistic_regression.py (mismo procedimiento
que la 3.2). Escribe dos candidatos fuera de producción:
  EnsembleWeightsEs_21_candidate.json           (pesos unitarios)
  EnsembleWeightsEs_21_candidate_balanced.json  (class_weight='balanced')
Uso: python3 fit_21.py <features_train_21.csv>
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fit_logistic_regression import SIGNAL_NAMES, evaluate, load_csv  # noqa: E402

from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import GroupKFold, cross_val_predict


def run_variant(x, y, groups, class_weight, tag):
    model = LogisticRegression(max_iter=2000, class_weight=class_weight)
    cv = GroupKFold(n_splits=5)
    prob = cross_val_predict(model, x, y, cv=cv, groups=groups,
                             method="predict_proba")[:, 1]
    m = evaluate(y, prob)
    print(f"[{tag}] GroupKFold accuracy={m['accuracy']:.4f} brier={m['brier']:.4f} "
          f"ece={m['ece']:.4f} auroc={m['auroc']:.4f}")
    model.fit(x, y)
    print(f"[{tag}] accuracy en entrenamiento completo: {model.score(x, y):.4f}")
    weights = {name: float(c) for name, c in zip(SIGNAL_NAMES, model.coef_[0])}
    return {
        "intercept": float(model.intercept_[0]),
        "weights": weights,
        "trainedOn": {"samples": len(y),
                      "cvAccuracy": float(m["accuracy"]),
                      "trainAccuracy": float(model.score(x, y))},
        "evaluation": {"method": "GroupKFold(5) por pregunta, predicciones fuera de fold",
                       "groupedByQuestion": m},
    }


def main():
    features_path = sys.argv[1]
    out_dir = Path(features_path).resolve().parent.parent  # scripts/calibration/
    x, y, groups, hasdata = load_csv(features_path)
    print(f"Muestras: {len(y)} (humanas={y.count(0)}, IA={y.count(1)})")
    print(f"Preguntas distintas: {len(set(groups))}")
    for name in SIGNAL_NAMES:
        print(f"  Cobertura {name}: {hasdata[name]}/{len(y)} con datos")

    unit = run_variant(x, y, groups, None, "pesos unitarios")
    balanced = run_variant(x, y, groups, "balanced", "class_weight=balanced")

    for data, name in ((unit, "EnsembleWeightsEs_21_candidate.json"),
                       (balanced, "EnsembleWeightsEs_21_candidate_balanced.json")):
        path = out_dir / name
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"Escrito: {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
