#!/usr/bin/env python3
"""
Cuenta lo que expone el API, leyendo el contrato publicado.

POR QUE EXISTE. Las cifras del API viven en tres documentos —`arquitectura.md`, `api.md` y
`api-festivales.md`— y se habían medido a mano contra el servidor en marcha. El 17 de septiembre de
2026, al remedirlas, un lector de YAML escrito de paso **ignoró los 157 caminos entrecomillados**
—los que llevan `{id}`— y dio 104 caminos donde hay 261: la cifra estuvo a punto de entrar en la
documentación. Un contador que se escribe una vez y se ejecuta no comete ese error dos veces.

SE LEE EL CONTRATO, NO EL SERVIDOR. `pnmc-api/openapi.yaml` lo mantiene igual al código una prueba
(`ContratoOpenApiTests`), así que no hace falta levantar nada para medir.

    python3 scripts/contar-contrato.py
"""

import collections
import pathlib
import sys

VERBOS = ("get:", "post:", "put:", "patch:", "delete:")


def leer(ruta: pathlib.Path):
    """Devuelve [(camino, verbo)] del contrato."""
    operaciones = []
    dentro = False
    camino = None
    for linea in ruta.read_text().split("\n"):
        if linea.startswith("paths:"):
            dentro = True
            continue
        if dentro and linea and not linea.startswith(" "):
            break
        if not dentro:
            continue
        limpio = linea.strip()
        # EL CAMINO PUEDE IR ENTRECOMILLADO: los que llevan `{id}` lo van siempre.
        es_camino = (linea.startswith("  /") or linea.startswith("  '/")) and not linea.startswith("   ")
        if es_camino and limpio.endswith(":"):
            camino = limpio.rstrip(":").strip("'")
        elif linea.startswith("    ") and not linea.startswith("     ") and limpio in VERBOS and camino:
            operaciones.append((camino, limpio.rstrip(":")))
    return operaciones


def main() -> int:
    ruta = pathlib.Path(__file__).resolve().parent.parent / "pnmc-api" / "openapi.yaml"
    if not ruta.exists():
        print(f"No encuentro {ruta}", file=sys.stderr)
        return 1

    operaciones = leer(ruta)
    caminos = {c for c, _ in operaciones}
    print(f"{len(operaciones)} operaciones en {len(caminos)} caminos\n")

    por_prefijo = collections.Counter("/".join(c.split("/")[:4]) for c, _ in operaciones)
    print("Por prefijo:")
    for prefijo, cuantas in por_prefijo.most_common():
        print(f"  {cuantas:4}  {prefijo}")

    def cuantas_de(palabras):
        return collections.Counter(
            "/".join(c.split("/")[:4])
            for c, _ in operaciones
            if any(p in c.lower() for p in palabras)
        )

    for nombre, palabras in (("Festivales y Ediciones", ("festival", "edicion")), ("Mercados", ("mercado",))):
        reparto = cuantas_de(palabras)
        print(f"\n{nombre}: {sum(reparto.values())}")
        for prefijo, cuantas in reparto.most_common():
            print(f"  {cuantas:4}  {prefijo}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
