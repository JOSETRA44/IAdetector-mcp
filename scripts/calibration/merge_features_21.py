#!/usr/bin/env python3
"""Fusiona feat_p1/p2/p3.csv de la Fase 2.1 en features_train_21.csv.

Renumerar question_id de forma única (1..371) en el orden de las partes, como
se hizo con features_train.csv en la 3.2. Valida los conteos finales.
Uso: python3 merge_features_21.py <feat_p1.csv> <feat_p2.csv> <feat_p3.csv> <salida.csv>
"""
import csv
import sys

EXPECTED_ROWS = 1032
EXPECTED_HUMAN = 336
EXPECTED_AI = 696


def main():
    in_paths, out_path = sys.argv[1:4], sys.argv[4]
    rows, offset, header = [], 0, None
    for path in in_paths:
        with open(path, newline="", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            if header is None:
                header = reader.fieldnames
            part_rows = list(reader)
            max_q = max(int(r["question_id"]) for r in part_rows)
            for r in part_rows:
                r["question_id"] = str(int(r["question_id"]) + offset)
                rows.append(r)
            offset += max_q
            print(f"{path}: {len(part_rows)} filas, question_id 1..{max_q}")
    assert len(rows) == EXPECTED_ROWS, f"filas={len(rows)}, esperadas {EXPECTED_ROWS}"
    labels = [int(r["label"]) for r in rows]
    assert labels.count(0) == EXPECTED_HUMAN, f"humanos={labels.count(0)}"
    assert labels.count(1) == EXPECTED_AI, f"IA={labels.count(1)}"
    assert len({r["question_id"] for r in rows}) == 371, "grupos distintos != 371"
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=header)
        writer.writeheader()
        writer.writerows(rows)
    print(f"escrito {out_path}: {len(rows)} filas, 371 grupos, "
          f"humanos={labels.count(0)}, IA={labels.count(1)}")


if __name__ == "__main__":
    sys.exit(main())
