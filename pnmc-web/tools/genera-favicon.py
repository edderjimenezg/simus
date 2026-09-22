"""
Genera el favicon y el icono de iOS desde la marca del PNMC.

POR QUE HAY QUE ENGROSAR LAS CRESTAS, Y NO ES UN CAPRICHO. La marca son cuatro huellas
dactilares de línea fina: medida, la tinta cubre el 15,1 % del lienzo de 2000x2000. Al reducir a
16 px cada píxel promedia unas ciento veinte líneas de origen con el blanco entre ellas, y el
resultado es una mancha gris lavada —se pierden la forma Y el color de marca—. Ensanchando las
crestas antes de reducir, el promedio conserva la tinta y a 16 px se siguen leyendo las cuatro
huellas en morado.

El ensanchado se aplica SOLO en los tamaños donde hace falta: a 48 px el detalle ya sobrevive con
un ensanchado suave, y a 180 px se usa el original sin tocar.

Se ejecuta con:  python3 tools/genera-favicon.py
"""
from pathlib import Path

from PIL import Image, ImageFilter

MORADO = (41, 18, 66)  # --color-morado del tema
# Las rutas se resuelven desde el propio fichero y no desde el directorio de trabajo: así el
# script funciona igual llamado desde `pnmc-web` o desde la raíz del repositorio.
RAIZ = Path(__file__).resolve().parent.parent
ORIGEN = RAIZ / 'public' / 'Icono PNMC Negro.png'
FAVICON = RAIZ / 'public' / 'favicon.ico'
ICONO_IOS = RAIZ / 'public' / 'apple-touch-icon.png'
# Los PNG llevan la medida en el nombre a propósito: ver el porqué en `main()`.
PNG_POR_LADO = {lado: RAIZ / 'public' / f'icono-pnmc-{lado}.png' for lado in (32, 192, 512)}
MARGEN = 0.06  # Un favicon pegado al borde se ve apretado en la pestaña.


def lienzo_cuadrado(imagen: Image.Image) -> Image.Image:
    recorte = imagen.crop(imagen.getbbox())
    lado = max(recorte.size)
    margen = int(lado * MARGEN)
    fondo = Image.new('RGBA', (lado + 2 * margen, lado + 2 * margen), (0, 0, 0, 0))
    fondo.alpha_composite(recorte, ((fondo.width - recorte.width) // 2,
                                    (fondo.height - recorte.height) // 2))
    return fondo


def teñir(alfa: Image.Image) -> Image.Image:
    return Image.merge('RGBA', (Image.new('L', alfa.size, MORADO[0]),
                                Image.new('L', alfa.size, MORADO[1]),
                                Image.new('L', alfa.size, MORADO[2]),
                                alfa))


def engrosar(alfa: Image.Image, radio: int) -> Image.Image:
    return alfa if radio <= 1 else alfa.filter(ImageFilter.MaxFilter(radio))


def escribir_ico(ruta: Path, capas: list[Image.Image]) -> None:
    """
    Empaqueta cada capa a su tamaño, sin que una se derive de otra.

    NO SE USA `Image.save(format='ICO', sizes=...)`: ese camino mete UNA sola imagen y deja que
    Pillow la reduzca para cada tamaño, de modo que el ensanchado medido para 16 px se perdería.
    Comprobado: el fichero que producía llevaba dentro únicamente la capa de 16.

    El formato es un contenedor sencillo —cabecera de 6 bytes, una entrada de 16 por capa y los
    datos al final— y admite PNG dentro de cada entrada desde Windows Vista.
    """
    import struct
    from io import BytesIO

    cuerpos = []
    for capa in capas:
        memoria = BytesIO()
        capa.save(memoria, format='PNG', optimize=True)
        cuerpos.append(memoria.getvalue())

    desplazamiento = 6 + 16 * len(capas)
    with open(ruta, 'wb') as fichero:
        fichero.write(struct.pack('<HHH', 0, 1, len(capas)))
        for capa, cuerpo in zip(capas, cuerpos):
            lado = 0 if capa.width >= 256 else capa.width
            fichero.write(struct.pack('<BBBBHHII', lado, lado, 0, 0, 1, 32, len(cuerpo), desplazamiento))
            desplazamiento += len(cuerpo)
        for cuerpo in cuerpos:
            fichero.write(cuerpo)


def main() -> None:
    base = lienzo_cuadrado(Image.open(ORIGEN).convert('RGBA'))
    alfa = base.split()[3]

    # Cuanto más pequeño el destino, más hay que ensanchar para que la tinta sobreviva.
    capas = [teñir(engrosar(alfa, radio)).resize((lado, lado), Image.LANCZOS)
             for lado, radio in ((16, 15), (32, 15), (48, 9))]
    escribir_ico(FAVICON, capas)

    # UN JUEGO DE PNG ADEMAS DEL .ICO, Y NO ES REDUNDANCIA.
    #
    # Un navegador guarda el icono por su URL y lo conserva con muchísima insistencia: sustituir el
    # contenido de `/favicon.ico` deja a quien ya visitó el sitio viendo el anterior durante días.
    # Estos ficheros son direcciones NUEVAS, así que entran a la primera; y los navegadores
    # modernos prefieren el PNG declarado con su medida antes que el `.ico`, que se queda como
    # respaldo para los antiguos y para la petición implícita a la raíz.
    #
    # 32 para la pestaña, 192 y 512 para cuando el sitio se guarda en la pantalla de inicio.
    for lado, destino in PNG_POR_LADO.items():
        radio = 15 if lado <= 32 else 1
        icono = teñir(engrosar(alfa, radio)).resize((lado, lado), Image.LANCZOS)
        icono.quantize(colors=64, method=Image.Quantize.FASTOCTREE).save(destino, optimize=True)

    # iOS pinta 180 px: ahí el detalle de las crestas se ve entero.
    # Paleta y no color verdadero: son dos tintas sobre transparente, y con `optimize` a secas el
    # fichero pasaba de 17 a 42 KB para el mismo dibujo.
    ios = teñir(alfa).resize((180, 180), Image.LANCZOS)
    ios.quantize(colors=64, method=Image.Quantize.FASTOCTREE).save(ICONO_IOS, optimize=True)

    generados = ', '.join(destino.name for destino in PNG_POR_LADO.values())
    print(f'{FAVICON.name} (16, 32, 48), {generados} y {ICONO_IOS.name} (180) regenerados')


if __name__ == '__main__':
    main()
