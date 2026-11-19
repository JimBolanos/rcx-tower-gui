# EV3 Print3rbot con Pybricks

Programa de Pybricks para el plotter **Print3rbot** (modelo de EV3, set 31313 + un sensor
de toque extra) y una página para convertir una imagen sencilla en trazos.

| Archivo | Qué es |
|---|---|
| `print3rbot.py` | Programa principal del robot (Pybricks, EV3). |
| `dibujo.py` | El dibujo: lista de trazos en mm. Trae un ejemplo (marco + estrella + círculo). |
| `convertidor.html` | Abre en el navegador: imagen → `dibujo.py`. |

## 1. Instalar Pybricks en el EV3

Dos opciones, el código sirve para ambas:

- **Pybricks v2.0 (estable):** graba la imagen *EV3 MicroPython* en una microSD, usa VS Code
  con la extensión *LEGO MINDSTORMS EV3 MicroPython*, crea un proyecto y pon
  `print3rbot.py` y `dibujo.py` en la misma carpeta.
- **Firmware Pybricks nuevo para EV3:** en code.pybricks.com, sube los dos archivos y
  ejecuta `print3rbot.py`.

## 2. Conexiones (por defecto)

| Puerto | Pieza |
|---|---|
| A | Motor grande: carro, eje X |
| B | Motor grande: avance, eje Y |
| C | Motor mediano: lápiz arriba/abajo |
| 1 | Sensor de toque: fin de carrera X (home) |
| 2 | (opcional) segundo sensor de toque: fin de carrera Y → pon `PORT_TOUCH_Y = Port.S2` |

Si tu montaje usa otros puertos, cámbialos al inicio de `print3rbot.py`.

## 3. Calibrar (una sola vez)

1. Pon `CALIBRAR = True` y ejecuta. El robot dibuja un cuadrado de 50 mm.
2. **Si un eje va al revés**, cambia `DIR_X`/`DIR_Y` a `Direction.COUNTERCLOCKWISE`.
   Si el home se aleja del sensor en vez de acercarse, también es eso.
3. Mide los lados del cuadrado y corrige:
   `DEG_PER_MM_X = DEG_PER_MM_X_actual * 50 / lado_medido_X` (igual para Y).
4. Lápiz: ajusta `PEN_HOME_DIR` (hacia dónde sube) y `PEN_DOWN_ANGLE` (cuánto baja hasta
   tocar el papel). Si tu mecanismo no tiene tope, pon `PEN_HOME_STALL = False` y deja el
   lápiz arriba antes de empezar.
5. Pon `MAX_X_MM`/`MAX_Y_MM` según el recorrido real. Luego `CALIBRAR = False`.

## 4. Dibujar

1. Abre `convertidor.html`, sube la imagen, elige ancho en mm y modo:
   - **Contorno**: dibuja los bordes de las zonas negras (ideal para siluetas y logos).
   - **Relleno**: rellena las zonas negras con rayado.
   - **Ambos**: borde + relleno.
2. Copia el resultado en `dibujo.py`.
3. Ejecuta `print3rbot.py`, pulsa el botón central para empezar. Pulsa el centro durante
   el dibujo para pausar (entre trazos).

Consejos: imágenes de alto contraste (negro sobre blanco) funcionan mejor. Si las líneas
salen curvas o temblorosas, baja `SPEED_DRAW`. Si hay holgura en los engranajes, se nota
al cambiar de dirección: aprieta el montaje o dibuja más lento.
